#include "../native_internal.h"

namespace gbfr::native
{
std::shared_mutex g_selection_mutex;
std::unordered_map<uint32_t, std::array<uint32_t, kVirtualSlotCapacity>> g_character_selections;
std::unordered_map<uint32_t, VirtualOwner> g_virtual_owner_by_slot_id;
std::shared_mutex g_authorization_mutex;
std::unordered_map<uintptr_t, AuthorizedStatus> g_authorized_statuses;
std::atomic<uint32_t> g_last_authorized_character_hash{0};
std::atomic<uint64_t> g_last_authorized_status_address{0};

std::atomic_bool g_pending_refresh{false};
std::atomic<uint32_t> g_pending_character_hash{0};
std::atomic_uint32_t g_pending_injected_count{0};
std::atomic_uint32_t g_next_apply_generation{0};
std::atomic_uint64_t g_queued_apply_request{0};
std::atomic_uint64_t g_apply_retry_not_before_ms{0};
std::atomic_bool g_apply_in_flight{false};
std::mutex g_reconcile_apply_mutex;
std::unordered_set<uint32_t> g_reconcile_apply_hashes;
std::atomic_uint64_t g_active_apply_generation{0};
std::atomic_uint64_t g_claimed_apply_generation{0};
std::atomic_uint32_t g_active_apply_thread_id{0};
std::atomic_uint64_t g_active_apply_status{0};
std::atomic_bool g_native_apply_call_active{false};
std::array<std::atomic_uint32_t, kVirtualSlotCapacity> g_active_apply_slots{};
std::atomic_uint32_t g_active_apply_expected_count{0};
std::atomic_uint64_t g_last_apply_generation{0};
std::atomic_uint32_t g_last_apply_character_hash{0};
std::atomic_uint32_t g_last_apply_expected_count{0};
std::atomic_uint32_t g_last_apply_injected_count{0};
std::atomic_int g_apply_result{0};
std::atomic_int g_last_consumed_apply_result{0};

namespace
{
struct ApplyInFlightGuard
{
   ApplyInFlightGuard()
      : acquired(!g_apply_in_flight.exchange(true, std::memory_order_acq_rel))
   {
   }
   ~ApplyInFlightGuard()
   {
      if (acquired)
         g_apply_in_flight.store(false, std::memory_order_release);
   }
   bool acquired = false;
};

void RequeueHotApply(uint64_t request, uint64_t delay_ms)
{
   if (request == 0)
      return;
   uint64_t expected = 0;
   if (g_queued_apply_request.compare_exchange_strong(
          expected, request, std::memory_order_acq_rel, std::memory_order_acquire))
      g_apply_retry_not_before_ms.store(GetTickCount64() + delay_ms, std::memory_order_release);
}
}

std::array<uint32_t, kVirtualSlotCapacity> GetSelection(uint32_t character_hash)
{
   std::shared_lock lock(g_selection_mutex);
   const auto iterator = g_character_selections.find(character_hash);
   return iterator == g_character_selections.end()
      ? std::array<uint32_t, kVirtualSlotCapacity>{}
      : iterator->second;
}

void ScheduleReconcileApply(uint32_t character_hash)
{
   if (character_hash == 0)
      return;
   std::scoped_lock lock(g_reconcile_apply_mutex);
   g_reconcile_apply_hashes.emplace(character_hash);
}

void PumpReconcileApplyQueue()
{
   if (g_queued_apply_request.load(std::memory_order_acquire) != 0)
      return;
   uint32_t character_hash = 0;
   {
      std::scoped_lock lock(g_reconcile_apply_mutex);
      if (g_reconcile_apply_hashes.empty())
         return;
      const auto iterator = g_reconcile_apply_hashes.begin();
      character_hash = *iterator;
      g_reconcile_apply_hashes.erase(iterator);
   }
   RequestHotApply(character_hash);
}

uint32_t RequestHotApply(uint32_t character_hash)
{
   if (character_hash == 0)
   {
      g_apply_result.store(ApplyResultSavedNoStatus, std::memory_order_release);
      return 0;
   }
   uint32_t generation =
      g_next_apply_generation.fetch_add(1, std::memory_order_acq_rel) + 1;
   if (generation == 0)
      generation = g_next_apply_generation.fetch_add(1, std::memory_order_acq_rel) + 1;
   const uint64_t request =
      (static_cast<uint64_t>(generation) << 32) | static_cast<uint64_t>(character_hash);
   g_apply_retry_not_before_ms.store(0, std::memory_order_release);
   g_queued_apply_request.store(request, std::memory_order_release);
   return generation;
}

void ProcessPendingHotApply()
{
   if (GetTickCount64() < g_apply_retry_not_before_ms.load(std::memory_order_acquire))
      return;
   const uint64_t request = g_queued_apply_request.exchange(0, std::memory_order_acq_rel);
   if (request == 0)
      return;

   ApplyInFlightGuard in_flight;
   if (!in_flight.acquired)
   {
      RequeueHotApply(request, 16);
      return;
   }

   const uint64_t generation = request >> 32;
   const uint32_t character_hash = static_cast<uint32_t>(request);
   g_pending_character_hash.store(character_hash, std::memory_order_release);
   g_last_apply_generation.store(generation, std::memory_order_release);
   g_last_apply_character_hash.store(character_hash, std::memory_order_release);
   g_last_apply_expected_count.store(0, std::memory_order_release);
   g_last_apply_injected_count.store(0, std::memory_order_release);
   if (!g_hooks_ready.load(std::memory_order_acquire) || character_hash == 0)
   {
      g_apply_result.store(ApplyResultSavedNoStatus, std::memory_order_release);
      return;
   }

   const uint32_t current_thread_id = GetCurrentThreadId();
   const int32_t edit_session = g_edit_session_state.load(std::memory_order_acquire);
   AuthorizedStatus context1_authorization{};
   const bool use_context1_status =
      (edit_session == EditSessionFreeTraining ||
       edit_session == EditSessionMissionLocked) &&
      TryGetAuthorizedContext1Status(character_hash, context1_authorization);

   uintptr_t manager = 0;
   uintptr_t status = 0;
   if (use_context1_status)
   {
      status = context1_authorization.status;
   }
   else if (!SafeResolveCharacterStatus(character_hash, manager, status))
   {
      g_apply_result.store(ApplyResultStatusLookupFailed, std::memory_order_release);
      RequeueHotApply(request, 100);
      return;
   }

   StatusIdentity original_identity{};
   if (!SafeReadStatusIdentity(status, original_identity) ||
       original_identity.character_hash != character_hash ||
       original_identity.context_mode < 0 || original_identity.context_mode > 2)
   {
      g_apply_result.store(ApplyResultStatusLookupFailed, std::memory_order_release);
      RequeueHotApply(request, 100);
      return;
   }

   const std::array<uint32_t, kVirtualSlotCapacity> selection = GetSelection(character_hash);
   uint32_t expected = 0;
   const size_t active_slot_count = static_cast<size_t>(GetVirtualSlotCount());
   for (size_t index = 0; index < selection.size(); ++index)
   {
      g_active_apply_slots[index].store(selection[index], std::memory_order_release);
      if (index < active_slot_count && selection[index] != 0)
         ++expected;
   }
   g_active_apply_expected_count.store(expected, std::memory_order_release);
   g_last_apply_expected_count.store(expected, std::memory_order_release);
   g_pending_injected_count.store(0, std::memory_order_release);
   g_claimed_apply_generation.store(0, std::memory_order_release);
   g_active_apply_thread_id.store(current_thread_id, std::memory_order_release);
   g_active_apply_status.store(status, std::memory_order_release);
   g_active_apply_generation.store(generation, std::memory_order_release);
   g_pending_refresh.store(true, std::memory_order_release);
   g_native_apply_call_active.store(true, std::memory_order_release);

   g_tls_apply_generation = generation;
   StatusIdentity restored_identity{};
   const bool rebuild_succeeded = SafeInvokeStatusRebuild(
      status, character_hash, restored_identity, use_context1_status);
   g_tls_apply_generation = 0;
   g_native_apply_call_active.store(false, std::memory_order_release);
   g_active_apply_status.store(0, std::memory_order_release);
   const uint64_t active_after_rebuild =
      g_active_apply_generation.load(std::memory_order_acquire);
   const bool trait_loop_claimed =
      g_claimed_apply_generation.load(std::memory_order_acquire) == generation;
   const uint32_t injected = g_pending_injected_count.load(std::memory_order_acquire);
   g_last_apply_injected_count.store(injected, std::memory_order_release);
   g_pending_refresh.store(false, std::memory_order_release);
   if (active_after_rebuild == generation)
      g_active_apply_generation.store(0, std::memory_order_release);

   if (!rebuild_succeeded)
   {
      EraseAuthorizedStatus(status);
      g_apply_result.store(ApplyResultNativeRebuildFailed, std::memory_order_release);
      return;
   }
   if (!trait_loop_claimed || active_after_rebuild == generation)
   {
      EraseAuthorizedStatus(status);
      g_apply_result.store(ApplyResultNativeTraitLoopMissing, std::memory_order_release);
      return;
   }

   if (injected == expected)
   {
      if (expected == 0)
      {
         std::unique_lock lock(g_authorization_mutex);
         g_authorized_statuses.erase(status);
         if (g_authorized_statuses.empty())
         {
            g_last_authorized_character_hash.store(0, std::memory_order_release);
            g_last_authorized_status_address.store(0, std::memory_order_release);
         }
      }
      else if (restored_identity.character_hash == character_hash &&
               restored_identity.context_mode == original_identity.context_mode)
      {
         CommitAuthorizedStatus(status, restored_identity, generation, selection);
      }
   }
   else
   {
      EraseAuthorizedStatus(status);
   }
   g_apply_retry_not_before_ms.store(0, std::memory_order_release);
   MarkInventoryDirty();

   if (!use_context1_status &&
       !SafeNotifyStatusDirty(manager, character_hash, 0xFFFFFFFFu))
      g_apply_result.store(ApplyResultNotifierFailed, std::memory_order_release);
}

bool SetSelection(uint32_t character_hash, int virtual_slot, uint32_t inventory_slot_id)
{
   if (character_hash == 0 || virtual_slot < 0 ||
       virtual_slot >= GetVirtualSlotCount() ||
       !SafeCanEditCharacter(character_hash))
      return false;

   if (inventory_slot_id != 0)
   {
      const uintptr_t source_address = ResolveGemAddress(inventory_slot_id);
      GemData source{};
      const uint32_t required_character = source_address == 0
         ? 0
         : (SafeReadGem(source_address, source)
               ? GetRequiredCharacterHash(source.gem_id)
               : 0);
      if (source_address == 0 || source.slot_id != inventory_slot_id ||
          source.gem_id == 0 || source.worn_by != kUnwornCharacterHash ||
          (source.flags & kGemInvalidFlag) != 0 ||
          (required_character != 0 && required_character != character_hash))
         return false;
   }

   std::unordered_set<uint32_t> affected_characters;
   {
      std::scoped_lock transition_lock(g_gem_protection_transition_mutex);
      std::unique_lock lock(g_selection_mutex);
      auto& target_slots = g_character_selections[character_hash];
      const uint32_t previous_slot_id = target_slots[static_cast<size_t>(virtual_slot)];
      if (previous_slot_id != 0)
         g_virtual_owner_by_slot_id.erase(previous_slot_id);
      if (inventory_slot_id != 0)
      {
         for (auto& [owner_hash, owner_slots] : g_character_selections)
         {
            for (uint32_t& existing : owner_slots)
            {
               if (existing != inventory_slot_id)
                  continue;
               existing = 0;
               affected_characters.emplace(owner_hash);
            }
         }
      }
      target_slots[static_cast<size_t>(virtual_slot)] = inventory_slot_id;
      affected_characters.emplace(character_hash);
      if (inventory_slot_id != 0)
         g_virtual_owner_by_slot_id[inventory_slot_id] = {character_hash, virtual_slot};
   }
   for (const uint32_t affected_hash : affected_characters)
      SaveCharacterSelection(affected_hash);
   ScheduleGemProtectionReconcile();
   MarkInventoryDirty();

   bool auto_apply = false;
   {
      std::scoped_lock lock(g_settings_mutex);
      auto_apply = g_settings.auto_apply;
   }
   if (auto_apply)
      RequestHotApply(character_hash);
   for (const uint32_t affected_hash : affected_characters)
      if (affected_hash != character_hash)
         ScheduleReconcileApply(affected_hash);
   return true;
}

bool ApplyPresetSelections(
   const GBFR20_PresetCharacterSelection* selections,
   uint32_t selection_count,
   GBFR20_PresetSlotResult* slot_results,
   uint32_t slot_result_capacity,
   uint32_t* slot_result_count)
{
   if (slot_result_count != nullptr)
      *slot_result_count = 0;
   if (selections == nullptr || selection_count == 0 ||
       selection_count > GBFR20_PRESET_CHARACTER_CAPACITY ||
       slot_results == nullptr || slot_result_count == nullptr)
      return false;

   const int active_slot_count = GetVirtualSlotCount();
   const uint32_t required_result_capacity =
      selection_count * static_cast<uint32_t>(active_slot_count);
   if (slot_result_capacity < required_result_capacity)
      return false;

   uint32_t current_character_hash = 0;
   if (!SafeReadUiSelectedCharacterHash(current_character_hash) ||
       !SafeCanEditCharacter(current_character_hash))
      return false;

   struct CandidateSelection
   {
      uint32_t character_hash = 0;
      std::array<uint32_t, kVirtualSlotCapacity> slots{};
   };

   std::vector<CandidateSelection> candidates;
   candidates.reserve(selection_count);
   std::vector<GBFR20_PresetSlotResult> results;
   results.reserve(required_result_capacity);
   std::unordered_set<uint32_t> seen_characters;
   std::unordered_set<uint32_t> claimed_slot_ids;

   for (uint32_t selection_index = 0; selection_index < selection_count; ++selection_index)
   {
      const GBFR20_PresetCharacterSelection& source_selection = selections[selection_index];
      if (source_selection.character_hash == 0 ||
          !seen_characters.emplace(source_selection.character_hash).second)
         return false;

      CandidateSelection candidate{};
      candidate.character_hash = source_selection.character_hash;
      for (int slot_index = 0; slot_index < active_slot_count; ++slot_index)
      {
         const uint32_t requested_slot_id =
            source_selection.slots[static_cast<size_t>(slot_index)];
         GBFR20_PresetSlotResult result{};
         result.character_hash = source_selection.character_hash;
         result.virtual_slot = slot_index;
         result.requested_slot_id = requested_slot_id;
         result.status = GBFR20_PRESET_SLOT_EMPTY;

         if (requested_slot_id == 0)
         {
            results.push_back(result);
            continue;
         }

         const uintptr_t source_address = ResolveGemAddress(requested_slot_id);
         GemData source{};
         if (source_address == 0 || !SafeReadGem(source_address, source) ||
             source.slot_id != requested_slot_id || source.gem_id == 0)
         {
            result.status = GBFR20_PRESET_SLOT_MISSING;
            results.push_back(result);
            continue;
         }
         if ((source.flags & kGemInvalidFlag) != 0)
         {
            result.status = GBFR20_PRESET_SLOT_DISABLED;
            results.push_back(result);
            continue;
         }
         if (source.worn_by != kUnwornCharacterHash)
         {
            result.owner_character_hash = source.worn_by;
            result.status = GBFR20_PRESET_SLOT_EQUIPPED;
            results.push_back(result);
            continue;
         }

         const uint32_t required_character = GetRequiredCharacterHash(source.gem_id);
         if (required_character != 0 &&
             required_character != source_selection.character_hash)
         {
            result.owner_character_hash = required_character;
            result.status = GBFR20_PRESET_SLOT_CHARACTER_RESTRICTED;
            results.push_back(result);
            continue;
         }
         if (!claimed_slot_ids.emplace(requested_slot_id).second)
         {
            result.status = GBFR20_PRESET_SLOT_DUPLICATE;
            results.push_back(result);
            continue;
         }

         candidate.slots[static_cast<size_t>(slot_index)] = requested_slot_id;
         result.status = GBFR20_PRESET_SLOT_APPLIED;
         results.push_back(result);
      }
      candidates.push_back(candidate);
   }

   std::unordered_set<uint32_t> affected_characters;
   {
      std::scoped_lock transition_lock(g_gem_protection_transition_mutex);
      std::unique_lock lock(g_selection_mutex);
      auto next_selections = g_character_selections;

      for (auto& [character_hash, slots] : next_selections)
      {
         for (uint32_t& existing_slot_id : slots)
         {
            if (existing_slot_id != 0 && claimed_slot_ids.contains(existing_slot_id))
               existing_slot_id = 0;
         }
      }
      for (const CandidateSelection& candidate : candidates)
         next_selections[candidate.character_hash] = candidate.slots;

      std::vector<uint32_t> character_hashes;
      character_hashes.reserve(next_selections.size());
      for (const auto& [character_hash, slots] : next_selections)
         character_hashes.push_back(character_hash);
      std::sort(character_hashes.begin(), character_hashes.end());

      std::unordered_set<uint32_t> rebuilt_claims;
      std::unordered_map<uint32_t, VirtualOwner> rebuilt_owners;
      for (const uint32_t character_hash : character_hashes)
      {
         auto& slots = next_selections[character_hash];
         for (int slot_index = 0; slot_index < active_slot_count; ++slot_index)
         {
            uint32_t& slot_id = slots[static_cast<size_t>(slot_index)];
            if (slot_id == 0)
               continue;
            if (!rebuilt_claims.emplace(slot_id).second)
            {
               slot_id = 0;
               continue;
            }
            rebuilt_owners[slot_id] = {character_hash, slot_index};
         }
         for (int slot_index = active_slot_count;
              slot_index < kVirtualSlotCapacity;
              ++slot_index)
         {
            slots[static_cast<size_t>(slot_index)] = 0;
         }
      }

      for (const auto& [character_hash, next_slots] : next_selections)
      {
         const auto previous = g_character_selections.find(character_hash);
         const std::array<uint32_t, kVirtualSlotCapacity> empty{};
         const auto& previous_slots = previous == g_character_selections.end()
            ? empty
            : previous->second;
         if (previous_slots != next_slots)
            affected_characters.emplace(character_hash);
      }

      g_character_selections = std::move(next_selections);
      g_virtual_owner_by_slot_id = std::move(rebuilt_owners);
   }

   for (const uint32_t affected_hash : affected_characters)
      SaveCharacterSelection(affected_hash);
   ScheduleGemProtectionReconcile();
   MarkInventoryDirty();

   bool auto_apply = false;
   {
      std::scoped_lock lock(g_settings_mutex);
      auto_apply = g_settings.auto_apply;
   }
   if (auto_apply && affected_characters.contains(current_character_hash))
      RequestHotApply(current_character_hash);
   for (const uint32_t affected_hash : affected_characters)
      if (affected_hash != current_character_hash)
         ScheduleReconcileApply(affected_hash);

   std::memcpy(
      slot_results,
      results.data(),
      results.size() * sizeof(GBFR20_PresetSlotResult));
   *slot_result_count = static_cast<uint32_t>(results.size());
   return true;
}
}
