#include "../native_internal.h"

#include <intrin.h>
#include <sstream>

namespace gbfr::native
{
SafetyHookInline g_get_gem_hook;
SafetyHookMid g_trait_fetch_hook;
SafetyHookMid g_status_owner_tick_hook;

std::atomic_uint64_t g_status_owner_manager_address{0};
std::atomic_uint32_t g_status_owner_thread_id{0};
std::atomic_uint64_t g_status_owner_tick_count{0};
std::atomic_uint32_t g_status_owner_character_count{0};
std::array<std::atomic_uint32_t, 4> g_status_owner_character_hashes{};
std::atomic_uint32_t g_active_getter_calls{0};
std::atomic_uint32_t g_active_mid_calls{0};
thread_local uint64_t g_tls_apply_generation = 0;
thread_local NaturalContributionFrame g_tls_natural_contribution{};

namespace
{
uint32_t CountSelectedSlots(
   const std::array<uint32_t, kVirtualSlotCapacity>& selection) noexcept
{
   return static_cast<uint32_t>(std::count_if(
      selection.begin(),
      selection.begin() + GetVirtualSlotCount(),
      [](uint32_t slot_id) { return slot_id != 0; }));
}

void BeginNaturalContributionTracking(
   uintptr_t status,
   const StatusIdentity& identity,
   const std::array<uint32_t, kVirtualSlotCapacity>& selection) noexcept
{
   g_tls_natural_contribution = {};
   const uint32_t expected = CountSelectedSlots(selection);
   if (expected == 0)
      return;

   if (identity.context_mode != 1)
      return;

   g_tls_natural_contribution.status = status;
   g_tls_natural_contribution.identity = identity;
   g_tls_natural_contribution.slots = selection;
   g_tls_natural_contribution.expected = expected;
   g_tls_natural_contribution.next_slot = kNativeInternalSlotCount;
   g_tls_natural_contribution.active = true;
}

void TrackNaturalContributionResult(
   uintptr_t status,
   const StatusIdentity& identity,
   int slot_index,
   uint32_t selected_slot_id,
   bool copied) noexcept
{
   if (!g_tls_natural_contribution.active)
      return;
   if (g_tls_natural_contribution.status != status ||
       g_tls_natural_contribution.identity.character_hash != identity.character_hash ||
       g_tls_natural_contribution.identity.context_mode != identity.context_mode ||
       g_tls_natural_contribution.next_slot != slot_index)
   {
      g_tls_natural_contribution = {};
      return;
   }

   if (selected_slot_id != 0)
   {
      if (!copied)
      {
         g_tls_natural_contribution = {};
         return;
      }
      ++g_tls_natural_contribution.injected;
   }
   ++g_tls_natural_contribution.next_slot;

   if (slot_index != GetExpandedInternalSlotCount() - 1)
      return;

   const uint32_t expected = g_tls_natural_contribution.expected;
   const uint32_t injected = g_tls_natural_contribution.injected;
   StatusIdentity final_identity{};
   const bool final_valid = injected == expected && expected != 0 &&
      SafeReadStatusIdentity(status, final_identity) &&
      final_identity.character_hash == identity.character_hash &&
      final_identity.context_mode == identity.context_mode;
   if (final_valid)
   {
      uint64_t generation =
         g_next_apply_generation.fetch_add(1, std::memory_order_acq_rel) + 1;
      if (generation == 0)
         generation = g_next_apply_generation.fetch_add(1, std::memory_order_acq_rel) + 1;
      CommitAuthorizedStatus(
         status,
         identity,
         generation,
         g_tls_natural_contribution.slots);
      SetRuntimeMessage(
         "Live battle Trait contribution confirmed for 0x" +
            ToUpperHex(identity.character_hash) + ": " +
            std::to_string(injected) + "/" + std::to_string(expected) +
            " virtual sigils reached the context-1 status.",
         false);
   }
   g_tls_natural_contribution = {};
}

bool TryLoadVirtualTraitSelection(
   uintptr_t status,
   const StatusIdentity& identity,
   bool from_trait_data_loop,
   uint64_t& active_generation,
   bool& tracks_pending_apply,
   std::array<uint32_t, kVirtualSlotCapacity>& selection) noexcept
{
   try
   {
      active_generation = g_active_apply_generation.load(std::memory_order_acquire);
      tracks_pending_apply =
         from_trait_data_loop && active_generation != 0 &&
         g_tls_apply_generation == active_generation &&
         g_pending_refresh.load(std::memory_order_acquire) &&
         g_native_apply_call_active.load(std::memory_order_acquire) &&
         g_active_apply_thread_id.load(std::memory_order_acquire) == GetCurrentThreadId() &&
         g_pending_character_hash.load(std::memory_order_acquire) == identity.character_hash &&
         g_active_apply_status.load(std::memory_order_acquire) == status;
      if (tracks_pending_apply)
      {
         for (size_t index = 0; index < selection.size(); ++index)
            selection[index] = g_active_apply_slots[index].load(std::memory_order_acquire);
         return true;
      }

      if (TryGetAuthorizedSelection(status, identity, selection))
         return true;
      if (!from_trait_data_loop)
         return false;

      selection = GetSelection(identity.character_hash);
      return CountSelectedSlots(selection) != 0;
   }
   catch (...)
   {
      active_generation = 0;
      tracks_pending_apply = false;
      selection = {};
      return false;
   }
}

bool TryCopySelectedVirtualGem(
   const StatusIdentity& identity,
   const std::array<uint32_t, kVirtualSlotCapacity>& selection,
   int virtual_index,
   void* output,
   uint32_t& selected_slot_id) noexcept
{
   selected_slot_id = 0;
   if (virtual_index < 0 || virtual_index >= GetVirtualSlotCount() || output == nullptr)
      return false;

   selected_slot_id = selection[static_cast<size_t>(virtual_index)];
   if (selected_slot_id == 0)
      return false;

   // Template slots synthesize a GemData from the built-in loadout table
   // instead of referencing a physical inventory copy. There is no
   // inventory-backed virtual slot path in this mod.
   if (!IsTemplateSlotId(selected_slot_id))
      return false;
   return TryCopyTemplateGem(identity.character_hash, selected_slot_id, output);
}

uint8_t GetGemDataByIndexDetour(void* status, int slot_index, void* output)
{
   ActiveCallGuard active_call(g_active_getter_calls);
   const uintptr_t return_address = reinterpret_cast<uintptr_t>(_ReturnAddress());
   const bool from_trait_apply_loop =
      return_address ==
      g_image_base + g_game_layout.trait_apply_getter_return_rva;
   const bool from_trait_category_loop =
      return_address ==
      g_image_base + g_game_layout.trait_category_getter_return_rva;
   const bool from_trait_data_loop =
      from_trait_apply_loop || from_trait_category_loop;
   StatusIdentity identity{};
   const bool valid_identity =
      SafeReadStatusIdentity(reinterpret_cast<uintptr_t>(status), identity) &&
      identity.context_mode >= 0 && identity.context_mode <= 5;

   const int expanded_slot_count = GetExpandedInternalSlotCount();
   if (slot_index < kNativeInternalSlotCount || slot_index >= expanded_slot_count)
   {
      const uint8_t result = g_get_gem_hook.call<uint8_t>(status, slot_index, output);
      return result;
   }
   if (g_shutting_down.load(std::memory_order_acquire) || !valid_identity ||
       identity.context_mode > 2 || output == nullptr)
      return 0;

   uint64_t active_generation = 0;
   bool tracks_pending_apply = false;
   std::array<uint32_t, kVirtualSlotCapacity> selection{};
   if (!TryLoadVirtualTraitSelection(
          reinterpret_cast<uintptr_t>(status),
          identity,
          from_trait_data_loop,
          active_generation,
          tracks_pending_apply,
          selection))
      return 0;
   const int virtual_index = slot_index - kNativeInternalSlotCount;
   uint32_t selected_slot_id = 0;
   if (tracks_pending_apply && from_trait_apply_loop &&
       slot_index == kNativeInternalSlotCount)
   {
      g_pending_injected_count.store(0, std::memory_order_release);
      g_claimed_apply_generation.store(active_generation, std::memory_order_release);
   }
   const bool generation_claimed =
      tracks_pending_apply && from_trait_apply_loop &&
      g_claimed_apply_generation.load(std::memory_order_acquire) == active_generation;

   if (from_trait_apply_loop && slot_index == kNativeInternalSlotCount &&
       identity.context_mode == 1)
      BeginNaturalContributionTracking(
         reinterpret_cast<uintptr_t>(status), identity, selection);

   const bool copied = TryCopySelectedVirtualGem(
      identity, selection, virtual_index, output, selected_slot_id);

   if (generation_claimed)
   {
      if (copied)
         g_pending_injected_count.fetch_add(1, std::memory_order_acq_rel);
      if (slot_index == expanded_slot_count - 1)
      {
         const uint32_t expected =
            g_active_apply_expected_count.load(std::memory_order_acquire);
         const uint32_t injected =
            g_pending_injected_count.load(std::memory_order_acquire);
         uint64_t expected_generation = active_generation;
         if (g_active_apply_generation.compare_exchange_strong(
                expected_generation,
                0,
                std::memory_order_acq_rel,
                std::memory_order_acquire))
         {
            g_pending_refresh.store(false, std::memory_order_release);
            g_apply_result.store(
               injected == expected ? ApplyResultAppliedDuringNativeRebuild
                                    : ApplyResultVirtualCopyFailed,
               std::memory_order_release);
         }
      }
   }

   if (from_trait_apply_loop)
      TrackNaturalContributionResult(
         reinterpret_cast<uintptr_t>(status),
         identity,
         slot_index,
         selected_slot_id,
         copied);

   return copied ? 1 : 0;
}

void OnTraitFetch(safetyhook::Context& context)
{
   ActiveCallGuard active_call(g_active_mid_calls);
   if (context.r13 < static_cast<uintptr_t>(kNativeInternalSlotCount) ||
       context.r13 >= static_cast<uintptr_t>(GetExpandedInternalSlotCount()))
      return;

   bool copied = false;
   const uintptr_t status = context.r15;
   StatusIdentity identity{};
   if (!g_shutting_down.load(std::memory_order_acquire) && context.r12 != 0 &&
       SafeReadStatusIdentity(status, identity) &&
       identity.context_mode >= 0 && identity.context_mode <= 2)
   {
      uint64_t active_generation = 0;
      bool tracks_pending_apply = false;
      std::array<uint32_t, kVirtualSlotCapacity> selection{};
      if (TryLoadVirtualTraitSelection(
             status,
             identity,
             true,
             active_generation,
             tracks_pending_apply,
             selection))
      {
         uint32_t selected_slot_id = 0;
         copied = TryCopySelectedVirtualGem(
            identity,
            selection,
            static_cast<int>(context.r13) - kNativeInternalSlotCount,
            reinterpret_cast<void*>(context.r12),
            selected_slot_id);
      }
   }

   // Resume after the native getter call so the game still performs its own
   // invalid-flag check, gem-master lookup, category count, cap, and effect math.
   context.rax = copied ? 1 : 0;
   context.rip = g_image_base + g_game_layout.trait_category_getter_return_rva;
}

void OnStatusOwnerCharacterLoop(safetyhook::Context& context)
{
   ActiveCallGuard active_call(g_active_mid_calls);
   if (g_shutting_down.load(std::memory_order_acquire))
      return;

   g_status_owner_manager_address.store(context.rbx, std::memory_order_release);
   g_status_owner_thread_id.store(GetCurrentThreadId(), std::memory_order_release);
   g_status_owner_tick_count.fetch_add(1, std::memory_order_acq_rel);

   std::array<uint32_t, 4> hashes{};
   const uint32_t count = SafeReadOwnerCharacterHashes(context.rbx, hashes);
   for (uint32_t index = 0; index < count; ++index)
      g_status_owner_character_hashes[index].store(hashes[index], std::memory_order_release);

   for (uint32_t index = count; index < g_status_owner_character_hashes.size(); ++index)
      g_status_owner_character_hashes[index].store(0, std::memory_order_release);
   g_status_owner_character_count.store(count, std::memory_order_release);
}

uint64_t BuildLifecycleSignature(
   uint32_t character_hash,
   uintptr_t status,
   int32_t context_mode,
   const std::array<uint32_t, kVirtualSlotCapacity>& slots)
{
   uint64_t signature = static_cast<uint64_t>(status) ^
      (static_cast<uint64_t>(character_hash) << 32) ^
      static_cast<uint32_t>(context_mode);
   const size_t active_slot_count = static_cast<size_t>(GetVirtualSlotCount());
   for (size_t index = 0; index < active_slot_count; ++index)
      signature = (signature ^ slots[index]) * 0x9E3779B185EBCA87ull;
   return signature == 0 ? 1 : signature;
}
}

void ScheduleSelectedStatusRebind()
{
   uint32_t character_hash = 0;
   if (!SafeReadUiSelectedCharacterHash(character_hash))
   {
      g_observed_character_hash.store(0, std::memory_order_release);
      g_observed_status_address.store(0, std::memory_order_release);
      g_observed_status_context.store(-1, std::memory_order_release);
      return;
   }

   uintptr_t manager = 0;
   uintptr_t status = 0;
   StatusIdentity identity{};
   if (!SafeResolveSelectedCharacterStatus(character_hash, manager, status, identity))
   {
      g_observed_character_hash.store(character_hash, std::memory_order_release);
      g_observed_status_address.store(0, std::memory_order_release);
      g_observed_status_context.store(-1, std::memory_order_release);
      return;
   }

   const uint32_t previous_character =
      g_observed_character_hash.exchange(character_hash, std::memory_order_acq_rel);
   const uint64_t previous_status =
      g_observed_status_address.exchange(status, std::memory_order_acq_rel);
   const int32_t previous_context =
      g_observed_status_context.exchange(identity.context_mode, std::memory_order_acq_rel);
   const bool identity_changed = previous_character != character_hash ||
      previous_status != status || previous_context != identity.context_mode;

   const auto selection = GetSelection(character_hash);
   if (std::none_of(selection.begin(), selection.end(), [](uint32_t slot_id) {
          return slot_id != 0;
       }))
      return;
   if (HasMatchingAuthorizedSelection(status, identity, selection))
   {
      g_lifecycle_signature_attempts.store(0, std::memory_order_release);
      return;
   }

   if (!identity_changed)
      return;

   const uint64_t signature =
      BuildLifecycleSignature(character_hash, status, identity.context_mode, selection);
   const uint64_t previous_signature =
      g_lifecycle_rebind_signature.exchange(signature, std::memory_order_acq_rel);
   if (previous_signature != signature)
   {
      g_lifecycle_signature_attempts.store(0, std::memory_order_release);
      g_lifecycle_rebind_not_before_ms.store(0, std::memory_order_release);
   }
   if (g_lifecycle_signature_attempts.load(std::memory_order_acquire) >= 3 ||
       g_queued_apply_request.load(std::memory_order_acquire) != 0 ||
       g_apply_in_flight.load(std::memory_order_acquire) ||
       GetTickCount64() < g_lifecycle_rebind_not_before_ms.load(std::memory_order_acquire))
      return;

   RequestHotApply(character_hash);
   g_lifecycle_signature_attempts.fetch_add(1, std::memory_order_acq_rel);
   g_lifecycle_rebind_not_before_ms.store(
      GetTickCount64() + 1000, std::memory_order_release);
}

namespace
{
void DisableGameplayHooksAndRestore() noexcept
{
   g_hooks_ready.store(false, std::memory_order_release);
   if (g_status_owner_tick_hook)
      (void)g_status_owner_tick_hook.disable();
   if (g_trait_fetch_hook)
      (void)g_trait_fetch_hook.disable();
   if (g_get_gem_hook)
      (void)g_get_gem_hook.disable();

   while (g_active_getter_calls.load(std::memory_order_acquire) != 0 ||
          g_active_mid_calls.load(std::memory_order_acquire) != 0)
      SwitchToThread();

   if (g_image_base != 0 && g_layout_ready.load(std::memory_order_acquire))
   {
      const uint8_t expanded_slot_count =
         static_cast<uint8_t>(GetExpandedInternalSlotCount());
      uint8_t current = 0;
      if (ReadByte(
             g_image_base + g_game_layout.trait_apply_loop_limit_immediate_rva,
             current) &&
          current == expanded_slot_count)
         (void)WriteByte(
            g_image_base + g_game_layout.trait_apply_loop_limit_immediate_rva,
            g_game_layout.trait_apply_original_limit);
      if (ReadByte(
             g_image_base + g_game_layout.trait_category_loop_limit_immediate_rva,
             current) &&
          current == expanded_slot_count)
         (void)WriteByte(
            g_image_base + g_game_layout.trait_category_loop_limit_immediate_rva,
            g_game_layout.trait_category_original_limit);
   }

   g_status_owner_tick_hook.reset();
   g_trait_fetch_hook.reset();
   g_get_gem_hook.reset();
   {
      std::unique_lock lock(g_authorization_mutex);
      g_authorized_statuses.clear();
   }
   ResetGameLayout();
}
}

void ShutdownHooks()
{
   g_shutting_down.store(true, std::memory_order_release);
   g_hooks_ready.store(false, std::memory_order_release);
   g_native_apply_call_active.store(false, std::memory_order_release);
   g_active_apply_status.store(0, std::memory_order_release);
   g_pending_refresh.store(false, std::memory_order_release);
   g_queued_apply_request.store(0, std::memory_order_release);
   g_apply_retry_not_before_ms.store(0, std::memory_order_release);
   g_active_apply_generation.store(0, std::memory_order_release);
   g_observed_character_hash.store(0, std::memory_order_release);
   g_observed_status_address.store(0, std::memory_order_release);
   g_observed_status_context.store(-1, std::memory_order_release);
   g_status_owner_manager_address.store(0, std::memory_order_release);

   DisableGameplayHooksAndRestore();
}

bool InstallHooks()
{
   const uint64_t preflight_started = BeginStartupPhase("required-byte-rva-preflight");
   const bool preflight_ready = RevalidateGameLayout();
   CompleteStartupPhase(
      "required-byte-rva-preflight", preflight_started, preflight_ready);
   if (!preflight_ready)
   {
      ResetGameLayout();
      SetRuntimeMessage(
         "Resolved game layout changed before hook installation; no gameplay hook or byte patch was installed.",
         true);
      return false;
   }

   const uint64_t gem_hook_started = BeginStartupPhase("gem-data-getter-hook");
   g_get_gem_hook = safetyhook::create_inline(
      reinterpret_cast<void*>(
         g_image_base + g_game_layout.get_gem_data_by_index_rva),
      reinterpret_cast<void*>(&GetGemDataByIndexDetour));
   CompleteStartupPhase(
      "gem-data-getter-hook", gem_hook_started, static_cast<bool>(g_get_gem_hook));
   if (!g_get_gem_hook)
   {
      DisableGameplayHooksAndRestore();
      SetRuntimeMessage("Failed to install the GemData getter hook.", true);
      return false;
   }

   const uint64_t trait_hook_started = BeginStartupPhase("trait-fetch-hook");
   g_trait_fetch_hook = safetyhook::create_mid(
      reinterpret_cast<void*>(
         g_image_base + g_game_layout.trait_fetch_path_rva),
      &OnTraitFetch);
   CompleteStartupPhase(
      "trait-fetch-hook", trait_hook_started, static_cast<bool>(g_trait_fetch_hook));
   if (!g_trait_fetch_hook)
   {
      DisableGameplayHooksAndRestore();
      SetRuntimeMessage("Failed to install the trait fetch-path hook.", true);
      return false;
   }

   const uint64_t owner_hook_started = BeginStartupPhase("status-owner-hook");
   g_status_owner_tick_hook = safetyhook::create_mid(
      reinterpret_cast<void*>(
         g_image_base + g_game_layout.status_owner_character_loop_rva),
      &OnStatusOwnerCharacterLoop);
   CompleteStartupPhase(
      "status-owner-hook", owner_hook_started, static_cast<bool>(g_status_owner_tick_hook));
   if (!g_status_owner_tick_hook)
   {
      DisableGameplayHooksAndRestore();
      SetRuntimeMessage("Failed to install the status owner-thread trace hook.", true);
      return false;
   }

   const uint8_t expanded_slot_count = static_cast<uint8_t>(GetExpandedInternalSlotCount());
   const uint64_t loop_patch_started = BeginStartupPhase("trait-loop-limit-patches");
   const bool loop_patches_ready =
      WriteByte(
         g_image_base +
            g_game_layout.trait_apply_loop_limit_immediate_rva,
         expanded_slot_count) &&
      WriteByte(
         g_image_base +
            g_game_layout.trait_category_loop_limit_immediate_rva,
         expanded_slot_count);
   CompleteStartupPhase(
      "trait-loop-limit-patches", loop_patch_started, loop_patches_ready);
   if (!loop_patches_ready)
   {
      DisableGameplayHooksAndRestore();
      SetRuntimeMessage(
         "Failed to patch both native trait loop limits; changes were rolled back.", true);
      return false;
   }

   g_hooks_ready.store(true, std::memory_order_release);
   SetRuntimeMessage(
      "Native hook installation completed with " +
         std::to_string(GetVirtualSlotCount()) +
         " virtual slots; compatibility was resolved synchronously from unique semantic anchors and revalidated before every hook and byte patch.",
      false);
   return true;
}
}
