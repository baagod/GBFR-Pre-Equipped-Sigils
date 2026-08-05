#include "../native_internal.h"

namespace gbfr::native
{
SafetyHookInline g_set_gem_protection_hook;
std::atomic_uint32_t g_active_protection_calls{0};
std::mutex g_gem_protection_mutex;
std::mutex g_gem_protection_transition_mutex;
std::unordered_map<uint32_t, uint64_t> g_mod_owned_protections;
std::atomic_bool g_gem_protection_reconcile_pending{true};

namespace
{
bool TryReadSystemData(uintptr_t& system_data) noexcept
{
   system_data = 0;
   return g_layout_ready.load(std::memory_order_acquire) &&
      g_hooks_ready.load(std::memory_order_acquire) && g_image_base != 0 &&
      SafeReadPointer(
         g_image_base + g_game_layout.system_data_global_rva, system_data) &&
      system_data != 0;
}

uint64_t GemProtectionFingerprint(const GemData& gem) noexcept
{
   // slot_id is the map key. Levels, worn_by and flags are deliberately
   // excluded because the player can legitimately change them. The stable item
   // and trait hashes reject a reused inventory slot without breaking upgrades.
   uint64_t fingerprint = 14695981039346656037ull;
   const auto mix = [&fingerprint](uint32_t value) {
      for (size_t index = 0; index < sizeof(value); ++index)
      {
         fingerprint ^= static_cast<uint8_t>(value >> (index * 8));
         fingerprint *= 1099511628211ull;
      }
   };
   mix(gem.gem_id);
   mix(gem.trait1);
   mix(gem.trait2);
   return fingerprint == 0 ? 1 : fingerprint;
}

bool TrySetGemProtection(
   uintptr_t system_data,
   uint32_t slot_id,
   bool protected_value,
   bool& changed) noexcept
{
   changed = false;
   if (system_data == 0 || slot_id == 0 || !g_set_gem_protection_hook)
      return false;

   const uintptr_t address = ResolveGemAddress(slot_id);
   GemData before{};
   if (address == 0 || !SafeReadGem(address, before) ||
       before.slot_id != slot_id || before.gem_id == 0 ||
       (before.flags & kGemInvalidFlag) != 0)
      return false;

   const bool already_protected =
      (before.flags & kGemProtectedFlag) != 0;
   if (already_protected == protected_value)
      return true;

   __try
   {
      // This is the game's own MenuGeenListProtect setter. Besides changing
      // bit 0 it invokes the native save-dirty notifier, so all sell,
      // trade-voucher and decomposition paths observe the same state.
      // Enter through the patched target instead of bypassing the detour. For
      // an unlock this gives SetGemProtectionDetour one final, atomic-in-effect
      // ownership check if the selection changed after our snapshot.
      using SetProtectionFn = void (*)(uintptr_t, uint32_t, bool);
      const auto setter = reinterpret_cast<SetProtectionFn>(
         g_image_base + g_game_layout.set_gem_protection_rva);
      setter(system_data, slot_id, protected_value);
   }
   __except (EXCEPTION_EXECUTE_HANDLER)
   {
      return false;
   }

   GemData after{};
   if (!SafeReadGem(address, after) || after.slot_id != slot_id ||
       after.gem_id != before.gem_id ||
       ((after.flags & kGemProtectedFlag) != 0) != protected_value)
      return false;
   changed = true;
   return true;
}
}

void ScheduleGemProtectionReconcile() noexcept
{
   g_gem_protection_reconcile_pending.store(true, std::memory_order_release);
}

void SetGemProtectionDetour(
   uintptr_t system_data,
   uint32_t slot_id,
   bool protected_value)
{
   ActiveCallGuard active_call(g_active_protection_calls);
   if (!g_shutting_down.load(std::memory_order_acquire) &&
       g_hooks_ready.load(std::memory_order_acquire) && !protected_value)
   {
      // Selection commits take the same transition mutex. We only hold the
      // selection mutex for the lookup, then release it before entering game
      // code so native callbacks cannot recursively acquire our shared_mutex.
      std::scoped_lock transition_lock(g_gem_protection_transition_mutex);
      {
         std::shared_lock selection_lock(g_selection_mutex);
         if (g_virtual_owner_by_slot_id.contains(slot_id))
         {
            ScheduleGemProtectionReconcile();
            return;
         }
      }
      g_set_gem_protection_hook.call<void>(
         system_data, slot_id, protected_value);
      return;
   }
   g_set_gem_protection_hook.call<void>(
      system_data, slot_id, protected_value);
}

void ReconcileGemProtection()
{
   if (!g_gem_protection_reconcile_pending.exchange(
          false, std::memory_order_acq_rel))
      return;
   if (g_shutting_down.load(std::memory_order_acquire))
      return;

   uintptr_t system_data = 0;
   if (!TryReadSystemData(system_data))
   {
      ScheduleGemProtectionReconcile();
      return;
   }

   std::unordered_map<uint32_t, VirtualOwner> selected;
   {
      std::shared_lock lock(g_selection_mutex);
      selected = g_virtual_owner_by_slot_id;
   }
   std::unordered_map<uint32_t, uint64_t> owned;
   {
      std::scoped_lock lock(g_gem_protection_mutex);
      owned = g_mod_owned_protections;
   }

   bool retry = false;
   bool inventory_changed = false;

   for (auto iterator = owned.begin(); iterator != owned.end();)
   {
      const uint32_t slot_id = iterator->first;
      const uint64_t expected_fingerprint = iterator->second;

      const uintptr_t address = ResolveGemAddress(slot_id);
      GemData gem{};
      if (address == 0 || !SafeReadGem(address, gem) ||
          gem.slot_id != slot_id || gem.gem_id == 0 ||
          (gem.flags & kGemInvalidFlag) != 0)
      {
         // The physical item no longer exists. Forgetting the ownership record
         // is safe because there is no live protection bit left to restore.
         iterator = owned.erase(iterator);
         continue;
      }
      if (GemProtectionFingerprint(gem) != expected_fingerprint)
      {
         // The inventory ID now belongs to another physical sigil. Never clear
         // a protection bit whose ownership we can no longer prove.
         iterator = owned.erase(iterator);
         continue;
      }
      if (selected.contains(slot_id))
      {
         ++iterator;
         continue;
      }

      bool changed = false;
      if (!TrySetGemProtection(system_data, slot_id, false, changed))
      {
         retry = true;
         ++iterator;
         continue;
      }
      inventory_changed = inventory_changed || changed;
      iterator = owned.erase(iterator);
   }

   for (const auto& [slot_id, owner] : selected)
   {
      const uintptr_t address = ResolveGemAddress(slot_id);
      GemData gem{};
      if (address == 0 || !SafeReadGem(address, gem) ||
          gem.slot_id != slot_id || gem.gem_id == 0 ||
          gem.worn_by != kUnwornCharacterHash ||
          (gem.flags & kGemInvalidFlag) != 0 ||
          (GetRequiredCharacterHash(gem.gem_id) != 0 &&
           GetRequiredCharacterHash(gem.gem_id) != owner.character_hash))
      {
         // Inventory reconciliation owns removal of stale selections. Do not
         // protect a record which no longer satisfies the extra-slot contract.
         MarkInventoryDirty();
         continue;
      }

      if ((gem.flags & kGemProtectedFlag) != 0)
      {
         const auto managed = owned.find(slot_id);
         if (managed != owned.end() &&
             managed->second != GemProtectionFingerprint(gem))
            owned.erase(managed);
         continue; // Preserve a lock which the player already owned.
      }

      bool changed = false;
      if (!TrySetGemProtection(system_data, slot_id, true, changed))
      {
         retry = true;
         continue;
      }
      if (changed)
      {
         owned.insert_or_assign(slot_id, GemProtectionFingerprint(gem));
         inventory_changed = true;
      }
   }

   bool ownership_changed = false;
   {
      std::scoped_lock lock(g_gem_protection_mutex);
      if (g_mod_owned_protections != owned)
      {
         g_mod_owned_protections = std::move(owned);
         ownership_changed = true;
      }
   }
   if (ownership_changed)
      SaveManagedProtectionSlots();
   if (inventory_changed)
      MarkInventoryDirty();
   if (retry)
      ScheduleGemProtectionReconcile();
}
}
