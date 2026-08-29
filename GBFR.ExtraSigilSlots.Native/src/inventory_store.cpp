#include "../native_internal.h"

#include <limits>
#include <sstream>

namespace gbfr::native
{
std::mutex g_inventory_index_mutex;
uintptr_t g_indexed_system_data = 0;
std::unordered_map<uint32_t, uintptr_t> g_inventory_by_slot_id;
std::atomic_uint64_t g_inventory_revision{0};
std::atomic_bool g_inventory_dirty{true};
std::mutex g_inventory_snapshot_mutex;
std::vector<InventoryItem> g_inventory_snapshot;

namespace
{
bool TryGetInventoryBase(uintptr_t system_data, uintptr_t& inventory_base) noexcept
{
   inventory_base = 0;
   if (!g_layout_ready.load(std::memory_order_acquire) || system_data == 0 ||
       g_game_layout.main_gem_array_offset >
          std::numeric_limits<uintptr_t>::max() - system_data)
      return false;

   const uintptr_t candidate = system_data + g_game_layout.main_gem_array_offset;
   constexpr uintptr_t kInventorySpan =
      static_cast<uintptr_t>(kMainGemCapacity - 1) * sizeof(GemData);
   if (candidate > std::numeric_limits<uintptr_t>::max() - kInventorySpan)
      return false;
   inventory_base = candidate;
   return true;
}

bool RebuildInventoryIndexLocked(uintptr_t system_data)
{
   g_inventory_by_slot_id.clear();
   uintptr_t inventory_base = 0;
   if (!TryGetInventoryBase(system_data, inventory_base))
   {
      g_indexed_system_data = 0;
      return false;
   }
   for (int index = 0; index < kMainGemCapacity; ++index)
   {
      const uintptr_t address = inventory_base +
         static_cast<uintptr_t>(index) * sizeof(GemData);
      GemData gem{};
      if (!SafeReadGem(address, gem))
      {
         g_inventory_by_slot_id.clear();
         g_indexed_system_data = 0;
         return false;
      }
      if (gem.slot_id != 0)
         g_inventory_by_slot_id[gem.slot_id] = address;
   }
   g_indexed_system_data = system_data;
   return true;
}
}

uintptr_t ResolveGemAddress(uint32_t slot_id)
{
   if (!g_layout_ready.load(std::memory_order_acquire) ||
       !g_hooks_ready.load(std::memory_order_acquire) ||
       slot_id == 0 || g_image_base == 0)
      return 0;
   uintptr_t system_data = 0;
   if (!SafeReadPointer(g_image_base + g_game_layout.system_data_global_rva, system_data) || system_data == 0)
      return 0;

   std::scoped_lock lock(g_inventory_index_mutex);
   if (g_indexed_system_data != system_data || g_inventory_by_slot_id.empty())
      RebuildInventoryIndexLocked(system_data);

   auto iterator = g_inventory_by_slot_id.find(slot_id);
   if (iterator == g_inventory_by_slot_id.end())
   {
      RebuildInventoryIndexLocked(system_data);
      iterator = g_inventory_by_slot_id.find(slot_id);
      return iterator == g_inventory_by_slot_id.end() ? 0 : iterator->second;
   }

   GemData current{};
   if (SafeReadGem(iterator->second, current) && current.slot_id == slot_id)
      return iterator->second;

   RebuildInventoryIndexLocked(system_data);
   iterator = g_inventory_by_slot_id.find(slot_id);
   return iterator == g_inventory_by_slot_id.end() ? 0 : iterator->second;
}

bool RefreshInventorySnapshot()
{
   uintptr_t system_data = 0;
   if (!g_layout_ready.load(std::memory_order_acquire) ||
       !g_hooks_ready.load(std::memory_order_acquire) ||
       !SafeReadPointer(g_image_base + g_game_layout.system_data_global_rva, system_data) ||
       system_data == 0)
      return false;

   struct RawInventoryRecord
   {
      GemData gem{};
      uintptr_t address = 0;
   };

   std::vector<RawInventoryRecord> records;
   records.reserve(kMainGemCapacity);
   std::unordered_map<uint32_t, uintptr_t> new_index;
   std::unordered_map<uint32_t, GemData> gems_by_slot_id;
   uintptr_t inventory_base = 0;
   if (!TryGetInventoryBase(system_data, inventory_base))
      return false;
   for (int index = 0; index < kMainGemCapacity; ++index)
   {
      const uintptr_t address = inventory_base +
         static_cast<uintptr_t>(index) * sizeof(GemData);
      GemData gem{};
      if (!SafeReadGem(address, gem))
         return false;
      if (gem.slot_id == 0)
         continue;

      records.push_back({gem, address});
      new_index[gem.slot_id] = address;
      gems_by_slot_id[gem.slot_id] = gem;
   }

   std::unordered_set<uint32_t> affected_characters;
   std::unordered_map<uint32_t, VirtualOwner> virtual_owners;
   {
      std::scoped_lock transition_lock(g_gem_protection_transition_mutex);
      std::unique_lock lock(g_selection_mutex);
      for (auto& [character_hash, slots] : g_character_selections)
      {
         for (uint32_t& selected_slot_id : slots)
         {
            if (selected_slot_id == 0)
               continue;
            const auto gem_iterator = gems_by_slot_id.find(selected_slot_id);
            const bool valid = gem_iterator != gems_by_slot_id.end() &&
               gem_iterator->second.gem_id != 0 &&
               gem_iterator->second.worn_by == kUnwornCharacterHash &&
               (gem_iterator->second.flags & kGemInvalidFlag) == 0 &&
               IsCharacterCompatible(
                  GetRequiredCharacterHash(gem_iterator->second.gem_id),
                  character_hash);
            if (valid)
               continue;
            g_virtual_owner_by_slot_id.erase(selected_slot_id);
            selected_slot_id = 0;
            affected_characters.emplace(character_hash);
         }
      }
      virtual_owners = g_virtual_owner_by_slot_id;
   }
   for (const uint32_t character_hash : affected_characters)
   {
      SaveCharacterSelection(character_hash);
      ScheduleReconcileApply(character_hash);
   }
   // Protect newly selected sigils and release previously selected sigils
   // before publishing this snapshot, so the picker immediately shows the
   // same native locked state the game menus observe.
   ReconcileGemProtection();

   std::vector<InventoryItem> snapshot;
   snapshot.reserve(records.size());
   std::shared_lock name_lock(g_name_table_mutex);
   const bool english = g_names_are_english;
   for (const RawInventoryRecord& record : records)
   {
      // The protection reconcile can change gem flags above. Refresh the
      // record from game memory before publishing the picker snapshot.
      GemData gem = record.gem;
      GemData fresh{};
      if (SafeReadGem(record.address, fresh) && fresh.slot_id == gem.slot_id)
         gem = fresh;
      if (gem.gem_id == 0 || (gem.flags & kGemInvalidFlag) != 0)
         continue;

      InventoryItem item{};
      item.gem = gem;
      item.address = record.address;
      item.equipped = gem.worn_by != kUnwornCharacterHash;
      item.protected_locked = (gem.flags & kGemProtectedFlag) != 0;
      item.required_character_hash = GetRequiredCharacterHash(gem.gem_id);
      if (const auto owner = virtual_owners.find(gem.slot_id); owner != virtual_owners.end())
      {
         item.virtual_owner_character_hash = owner->second.character_hash;
         item.virtual_owner_slot = owner->second.virtual_slot;
      }

      const std::string sigil = LookupName(g_sigil_names, gem.gem_id, "Sigil#");
      const std::string trait1 = LookupName(g_trait_names, gem.trait1, "Trait#");
      const std::string trait2 = LookupName(g_trait_names, gem.trait2, "Trait#");
      std::ostringstream label;
      label << sigil << "  |  " << trait1 << " Lv" << gem.trait1_level;
      if (gem.trait2 != 0)
         label << " + " << trait2 << " Lv" << gem.trait2_level;
      label << "  [#" << gem.slot_id << ']';
      if (item.equipped)
         label << (english ? "  (equipped)" : "  (已装备)");
      else if (item.virtual_owner_character_hash != 0)
         label << (english ? "  (used in an extra slot)" : "  (已用于额外槽)");
      item.label = label.str();
      item.searchable = ToLowerAscii(item.label);
      snapshot.push_back(std::move(item));
   }
   name_lock.unlock();

   std::sort(snapshot.begin(), snapshot.end(), [](const InventoryItem& left, const InventoryItem& right) {
      if (left.equipped != right.equipped)
         return !left.equipped;
      if (left.gem.sigil_level != right.gem.sigil_level)
         return left.gem.sigil_level > right.gem.sigil_level;
      return left.label < right.label;
   });

   {
      std::scoped_lock lock(g_inventory_index_mutex);
      g_inventory_by_slot_id = std::move(new_index);
      g_indexed_system_data = system_data;
   }
   {
      std::scoped_lock lock(g_inventory_snapshot_mutex);
      g_inventory_snapshot = std::move(snapshot);
   }
   g_inventory_dirty.store(false, std::memory_order_release);
   g_inventory_revision.fetch_add(1, std::memory_order_acq_rel);
   return true;
}
}
