#include "../native_internal.h"

#include <intrin.h>
#include <sstream>

namespace gbfr::native
{
SafetyHookInline g_get_gem_hook;
SafetyHookMid g_trait_fetch_hook;
SafetyHookMid g_status_owner_tick_hook;
SafetyHookMid g_local_context1_bind_call_hook;
SafetyHookMid g_local_context1_bind_return_hook;

std::atomic<uint32_t> g_last_character_hash{0};
std::atomic<int32_t> g_last_context_mode{-1};
std::atomic_uint64_t g_status_owner_manager_address{0};
std::atomic_uint32_t g_status_owner_thread_id{0};
std::atomic_uint64_t g_status_owner_tick_count{0};
std::atomic_uint32_t g_status_owner_character_count{0};
std::array<std::atomic_uint32_t, 4> g_status_owner_character_hashes{};
std::shared_mutex g_local_context1_binding_mutex;
std::unordered_map<uintptr_t, LocalContext1Binding> g_local_context1_bindings;
std::atomic_uint64_t g_local_context1_binding_generation{0};
std::atomic_uint32_t g_active_getter_calls{0};
std::atomic_uint32_t g_active_mid_calls{0};
thread_local uint64_t g_tls_apply_generation = 0;
thread_local NaturalContributionFrame g_tls_natural_contribution{};
thread_local LocalContext1Binding g_tls_local_context1_binding{};
std::atomic_uint64_t g_natural_bind_attempts{0};
std::atomic_uint64_t g_natural_bind_successes{0};
std::atomic_uint64_t g_natural_bind_status_address{0};
std::atomic_uint32_t g_natural_bind_character_hash{0};
std::atomic_int32_t g_natural_bind_context{-1};
std::atomic_uint32_t g_natural_bind_expected_count{0};
std::atomic_uint32_t g_natural_bind_injected_count{0};
std::atomic_int32_t g_natural_bind_result{NaturalBindNone};
std::atomic_uint32_t g_natural_bind_owner_key{0};
std::atomic_uint64_t g_natural_bind_owner_status_address{0};

namespace
{
void PublishNaturalBindDiagnostic(
   uintptr_t status,
   const StatusIdentity& identity,
   uint32_t expected,
   uint32_t injected,
   NaturalBindResult result) noexcept
{
   g_natural_bind_status_address.store(status, std::memory_order_release);
   g_natural_bind_character_hash.store(identity.character_hash, std::memory_order_release);
   g_natural_bind_context.store(identity.context_mode, std::memory_order_release);
   g_natural_bind_expected_count.store(expected, std::memory_order_release);
   g_natural_bind_injected_count.store(injected, std::memory_order_release);
   g_natural_bind_result.store(result, std::memory_order_release);
}

uint32_t CountSelectedSlots(
   const std::array<uint32_t, kVirtualSlotCapacity>& selection) noexcept
{
   return static_cast<uint32_t>(std::count_if(
      selection.begin(),
      selection.begin() + GetVirtualSlotCount(),
      [](uint32_t slot_id) { return slot_id != 0; }));
}

template <size_t Size>
bool RequireCodePreflight(
   std::string_view stage,
   uintptr_t rva,
   const std::array<uint8_t, Size>& expected)
{
   if (MatchesBytes(g_image_base + rva, expected))
      return true;

   std::ostringstream message;
   message << "Native code preflight failed: stage=" << stage << ", RVA=0x"
           << ToUpperHex(static_cast<uint32_t>(rva)) << ", expected " << Size
           << " bytes; no hook or byte patch was installed.";
   SetRuntimeMessage(message.str(), true);
   return false;
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

   PublishNaturalBindDiagnostic(
      status, identity, expected, 0, NaturalBindInProgress);
   if (identity.context_mode != 1)
   {
      PublishNaturalBindDiagnostic(
         status, identity, expected, 0, NaturalBindContextRejected);
      return;
   }

   g_natural_bind_attempts.fetch_add(1, std::memory_order_acq_rel);
   g_tls_natural_contribution.status = status;
   g_tls_natural_contribution.identity = identity;
   g_tls_natural_contribution.slots = selection;
   g_tls_natural_contribution.expected = expected;
   g_tls_natural_contribution.next_slot = kNativeInternalSlotCount;
   g_tls_natural_contribution.active = true;
   g_natural_bind_owner_key.store(0, std::memory_order_release);
   g_natural_bind_owner_status_address.store(status, std::memory_order_release);
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
      PublishNaturalBindDiagnostic(
         status,
         identity,
         g_tls_natural_contribution.expected,
         g_tls_natural_contribution.injected,
         NaturalBindSequenceRejected);
      g_tls_natural_contribution = {};
      return;
   }

   if (selected_slot_id != 0)
   {
      if (!copied)
      {
         PublishNaturalBindDiagnostic(
            status,
            identity,
            g_tls_natural_contribution.expected,
            g_tls_natural_contribution.injected,
            NaturalBindCopyRejected);
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
   PublishNaturalBindDiagnostic(
      status,
      identity,
      expected,
      injected,
      final_valid ? NaturalBindSucceeded : NaturalBindFinalValidationRejected);
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
      g_natural_bind_successes.fetch_add(1, std::memory_order_acq_rel);
      SetRuntimeMessage(
         "Live battle Trait contribution confirmed for 0x" +
            ToUpperHex(identity.character_hash) + ": " +
            std::to_string(injected) + "/" + std::to_string(expected) +
            " virtual sigils reached the context-1 status.",
         false);
   }
   g_tls_natural_contribution = {};
}

uint8_t GetGemDataByIndexDetour(void* status, int slot_index, void* output)
{
   ActiveCallGuard active_call(g_active_getter_calls);
   const uintptr_t return_address = reinterpret_cast<uintptr_t>(_ReturnAddress());
   const bool from_trait_apply_loop =
      return_address == g_image_base + kTraitApplyGetterReturnRva;
   const bool from_trait_category_loop =
      return_address == g_image_base + kTraitCategoryGetterReturnRva;
   const bool from_trait_data_loop =
      from_trait_apply_loop || from_trait_category_loop;
   StatusIdentity identity{};
   const bool valid_identity =
      SafeReadStatusIdentity(reinterpret_cast<uintptr_t>(status), identity) &&
      identity.context_mode >= 0 && identity.context_mode <= 5;
   if (valid_identity)
   {
      g_last_character_hash.store(identity.character_hash, std::memory_order_release);
      g_last_context_mode.store(identity.context_mode, std::memory_order_release);
   }

   const int expanded_slot_count = GetExpandedInternalSlotCount();
   if (slot_index < kNativeInternalSlotCount || slot_index >= expanded_slot_count)
   {
      const uint8_t result = g_get_gem_hook.call<uint8_t>(status, slot_index, output);
      if (from_trait_apply_loop && slot_index == 0)
         MarkInventoryDirty();
      return result;
   }
   if (g_shutting_down.load(std::memory_order_acquire) || !valid_identity ||
       identity.context_mode > 2 || output == nullptr)
      return 0;

   const uint64_t active_generation =
      g_active_apply_generation.load(std::memory_order_acquire);
   const bool tracks_pending_apply =
      from_trait_data_loop && active_generation != 0 &&
      g_tls_apply_generation == active_generation &&
      g_pending_refresh.load(std::memory_order_acquire) &&
      g_native_apply_call_active.load(std::memory_order_acquire) &&
      g_active_apply_thread_id.load(std::memory_order_acquire) == GetCurrentThreadId() &&
      g_pending_character_hash.load(std::memory_order_acquire) == identity.character_hash &&
      g_active_apply_status.load(std::memory_order_acquire) ==
         reinterpret_cast<uintptr_t>(status);
   std::array<uint32_t, kVirtualSlotCapacity> selection{};
   if (tracks_pending_apply)
   {
      for (size_t index = 0; index < selection.size(); ++index)
         selection[index] = g_active_apply_slots[index].load(std::memory_order_acquire);
   }
   else if (!TryGetAuthorizedSelection(
              reinterpret_cast<uintptr_t>(status), identity, selection))
   {
      if (!from_trait_data_loop)
         return 0;
      selection = GetSelection(identity.character_hash);
      if (CountSelectedSlots(selection) == 0)
         return 0;
   }
   const int virtual_index = slot_index - kNativeInternalSlotCount;
   const uint32_t selected_slot_id = selection[static_cast<size_t>(virtual_index)];
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

   bool copied = false;
   if (selected_slot_id != 0)
   {
      const uintptr_t source_address = ResolveGemAddress(selected_slot_id);
      GemData source{};
      copied = source_address != 0 && SafeReadGem(source_address, source) &&
         source.slot_id == selected_slot_id &&
         source.worn_by == kUnwornCharacterHash &&
         (source.flags & 0x10) == 0 &&
         (GetRequiredCharacterHash(source.gem_id) == 0 ||
          GetRequiredCharacterHash(source.gem_id) == identity.character_hash) &&
         SafeCopyToOutput(source, output);
   }

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
   if (context.r13 >= static_cast<uintptr_t>(kNativeInternalSlotCount) &&
       context.r13 < static_cast<uintptr_t>(GetExpandedInternalSlotCount()))
      context.rip = g_image_base + kTraitFetchCallPathRva;
}

void OnLocalContext1BindCall(safetyhook::Context& context)
{
   ActiveCallGuard active_call(g_active_mid_calls);
   g_tls_local_context1_binding = {};
   if (g_shutting_down.load(std::memory_order_acquire))
      return;

   const uint32_t owner_key = static_cast<uint32_t>(context.rbx);
   if (owner_key != kLocalPlayerSlotKey)
      return;

   LocalContext1Binding binding{};
   binding.manager = context.rdi;
   binding.record = context.rdx;
   binding.status = context.rcx;
   binding.owner_key = owner_key;
   binding.thread_id = GetCurrentThreadId();
   binding.generation =
      g_local_context1_binding_generation.fetch_add(1, std::memory_order_acq_rel) + 1;
   if (binding.generation == 0)
      binding.generation =
         g_local_context1_binding_generation.fetch_add(1, std::memory_order_acq_rel) + 1;
   binding.active = true;

   uintptr_t resolved_record = 0;
   uintptr_t resolved_status = 0;
   if (!SafeReadCharacterRecordHash(binding.record, binding.character_hash) ||
       !SafeResolveCharacterRecordByOwnerKey(
          binding.manager, binding.owner_key, resolved_record) ||
       resolved_record != binding.record ||
       !SafeResolveStatusByMapKey(
          binding.manager, binding.owner_key, resolved_status) ||
       resolved_status != binding.status)
   {
      g_status_owner_manager_address.store(binding.manager, std::memory_order_release);
      g_natural_bind_owner_key.store(0, std::memory_order_release);
      g_natural_bind_owner_status_address.store(resolved_status, std::memory_order_release);
      StatusIdentity rejected_identity{binding.character_hash, 1};
      PublishNaturalBindDiagnostic(
         binding.status,
         rejected_identity,
         CountSelectedSlots(GetSelection(binding.character_hash)),
         0,
         NaturalBindStatusRejected);
      return;
   }

   {
      LocalContext1Binding persisted = binding;
      persisted.active = false;
      std::unique_lock lock(g_local_context1_binding_mutex);
      g_local_context1_bindings.clear();
      g_local_context1_bindings.emplace(binding.status, persisted);
   }
   {
      std::unique_lock lock(g_authorization_mutex);
      for (auto iterator = g_authorized_statuses.begin();
           iterator != g_authorized_statuses.end();)
      {
         if (iterator->second.context_mode == 1 && iterator->first != binding.status)
            iterator = g_authorized_statuses.erase(iterator);
         else
            ++iterator;
      }
   }

   g_tls_local_context1_binding = binding;
   g_status_owner_manager_address.store(binding.manager, std::memory_order_release);
   g_status_owner_thread_id.store(binding.thread_id, std::memory_order_release);
   g_natural_bind_owner_key.store(binding.owner_key, std::memory_order_release);
   g_natural_bind_owner_status_address.store(binding.status, std::memory_order_release);
}

void OnLocalContext1BindReturn(safetyhook::Context&)
{
   ActiveCallGuard active_call(g_active_mid_calls);
   g_tls_local_context1_binding.active = false;
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

   bool auto_apply = false;
   {
      std::scoped_lock lock(g_settings_mutex);
      auto_apply = g_settings.auto_apply;
   }
   if (!auto_apply && !identity_changed)
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
   g_lifecycle_rebind_attempts.fetch_add(1, std::memory_order_acq_rel);
   g_lifecycle_rebind_not_before_ms.store(
      GetTickCount64() + 1000, std::memory_order_release);
}

void ShutdownHooks()
{
   g_shutting_down.store(true, std::memory_order_release);
   g_hooks_ready.store(false, std::memory_order_release);
   g_input_capture_requested.store(false, std::memory_order_release);
   g_input_capture_effective.store(false, std::memory_order_release);
   g_input_capture_requested_devices.store(0, std::memory_order_release);
   g_input_capture_effective_devices.store(0, std::memory_order_release);
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
   g_natural_bind_owner_key.store(0, std::memory_order_release);
   g_natural_bind_owner_status_address.store(0, std::memory_order_release);
   g_tls_local_context1_binding = {};

   RestoreInputIatHooks();
   RestoreDirectInputInstanceHooks();

   if (g_local_context1_bind_return_hook)
      (void)g_local_context1_bind_return_hook.disable();
   if (g_local_context1_bind_call_hook)
      (void)g_local_context1_bind_call_hook.disable();
   if (g_status_owner_tick_hook)
      (void)g_status_owner_tick_hook.disable();
   if (g_trait_fetch_hook)
      (void)g_trait_fetch_hook.disable();
   if (g_get_gem_hook)
      (void)g_get_gem_hook.disable();

   while (g_active_getter_calls.load(std::memory_order_acquire) != 0 ||
          g_active_mid_calls.load(std::memory_order_acquire) != 0 ||
          g_active_input_calls.load(std::memory_order_acquire) != 0)
      SwitchToThread();

   if (g_image_base != 0)
   {
      uint8_t apply_loop_limit = 0;
      if (ReadByte(g_image_base + kTraitApplyLoopLimitImmediateRva, apply_loop_limit) &&
          apply_loop_limit == static_cast<uint8_t>(GetExpandedInternalSlotCount()))
         WriteByte(g_image_base + kTraitApplyLoopLimitImmediateRva, kNativeInternalSlotCount);
      uint8_t loop_limit = 0;
      if (ReadByte(g_image_base + kTraitCategoryLoopLimitImmediateRva, loop_limit) &&
          loop_limit == static_cast<uint8_t>(GetExpandedInternalSlotCount()))
         WriteByte(g_image_base + kTraitCategoryLoopLimitImmediateRva, kNativeInternalSlotCount);
   }
   g_status_owner_tick_hook.reset();
   g_local_context1_bind_return_hook.reset();
   g_local_context1_bind_call_hook.reset();
   g_trait_fetch_hook.reset();
   g_get_gem_hook.reset();
   ResetDirectInputInstanceHooks();
   {
      std::unique_lock lock(g_authorization_mutex);
      g_authorized_statuses.clear();
   }
   {
      std::unique_lock lock(g_local_context1_binding_mutex);
      g_local_context1_bindings.clear();
   }
}

bool InstallHooks()
{
   const uint64_t preflight_started = BeginStartupPhase("required-byte-rva-preflight");
   const bool preflight_ready = RequireCodePreflight(
          "trait-apply-loop-limit",
          kTraitApplyLoopLimitImmediateRva - 4,
          kTraitApplyLoopPreflight) &&
       RequireCodePreflight(
          "trait-apply-getter-return",
          kTraitApplyGetterReturnRva,
          kTraitApplyGetterReturnPreflight) &&
       RequireCodePreflight(
          "trait-category-loop-limit",
          kTraitCategoryLoopLimitImmediateRva - 6,
          kTraitCategoryLoopPreflight) &&
       RequireCodePreflight(
          "trait-fetch-path",
          kTraitFetchPathRva,
          kTraitFetchPreflight) &&
       RequireCodePreflight(
          "trait-fetch-call-path",
          kTraitFetchCallPathRva,
          kTraitFetchCallPathPreflight) &&
       RequireCodePreflight(
          "trait-category-getter-return",
          kTraitCategoryGetterReturnRva,
          kTraitCategoryGetterReturnPreflight) &&
       RequireCodePreflight(
          "gem-data-getter",
          kGetGemDataByIndexRva,
          kGetterPreflight) &&
       RequireCodePreflight(
          "status-rebuild",
          kStatusRebuildRva,
          kStatusRebuildPreflight) &&
       RequireCodePreflight(
          "status-notifier",
          kStatusNotifierRva,
          kStatusNotifierPreflight) &&
       RequireCodePreflight(
          "status-owner-tick",
          kStatusOwnerTickRva,
          kStatusOwnerTickPreflight) &&
       RequireCodePreflight(
          "status-owner-character-loop",
          kStatusOwnerCharacterLoopRva,
          kStatusOwnerCharacterLoopPreflight);
   CompleteStartupPhase(
      "required-byte-rva-preflight", preflight_started, preflight_ready);
   if (!preflight_ready)
   {
      return false;
   }

   const uint64_t gem_hook_started = BeginStartupPhase("gem-data-getter-hook");
   g_get_gem_hook = safetyhook::create_inline(
      reinterpret_cast<void*>(g_image_base + kGetGemDataByIndexRva),
      reinterpret_cast<void*>(&GetGemDataByIndexDetour));
   CompleteStartupPhase(
      "gem-data-getter-hook", gem_hook_started, static_cast<bool>(g_get_gem_hook));
   if (!g_get_gem_hook)
   {
      SetRuntimeMessage("Failed to install the GemData getter hook.", true);
      return false;
   }

   const uint64_t trait_hook_started = BeginStartupPhase("trait-fetch-hook");
   g_trait_fetch_hook = safetyhook::create_mid(
      reinterpret_cast<void*>(g_image_base + kTraitFetchPathRva), &OnTraitFetch);
   CompleteStartupPhase(
      "trait-fetch-hook", trait_hook_started, static_cast<bool>(g_trait_fetch_hook));
   if (!g_trait_fetch_hook)
   {
      g_get_gem_hook.reset();
      SetRuntimeMessage("Failed to install the trait fetch-path hook.", true);
      return false;
   }

   const uint64_t owner_hook_started = BeginStartupPhase("status-owner-hook");
   g_status_owner_tick_hook = safetyhook::create_mid(
      reinterpret_cast<void*>(g_image_base + kStatusOwnerCharacterLoopRva),
      &OnStatusOwnerCharacterLoop);
   CompleteStartupPhase(
      "status-owner-hook", owner_hook_started, static_cast<bool>(g_status_owner_tick_hook));
   if (!g_status_owner_tick_hook)
   {
      ShutdownHooks();
      SetRuntimeMessage("Failed to install the status owner-thread trace hook.", true);
      return false;
   }

   const uint8_t expanded_slot_count = static_cast<uint8_t>(GetExpandedInternalSlotCount());
   const uint64_t loop_patch_started = BeginStartupPhase("trait-loop-limit-patches");
   const bool loop_patches_ready =
      WriteByte(g_image_base + kTraitApplyLoopLimitImmediateRva, expanded_slot_count) &&
      WriteByte(g_image_base + kTraitCategoryLoopLimitImmediateRva, expanded_slot_count);
   CompleteStartupPhase(
      "trait-loop-limit-patches", loop_patch_started, loop_patches_ready);
   if (!loop_patches_ready)
   {
      ShutdownHooks();
      SetRuntimeMessage(
         "Failed to patch both native trait loop limits; changes were rolled back.", true);
      return false;
   }

   const uint64_t input_hooks_started = BeginStartupPhase("input-iat-hooks");
   const bool input_hooks_enabled =
      g_input_hooks_enabled.load(std::memory_order_acquire);
   const bool input_hooks_ready = input_hooks_enabled
      ? InstallInputIatHooks()
      : true;
   if (!input_hooks_enabled)
   {
      Log(
         "Game-local USER32 and DirectInput8 hooks were intentionally skipped; "
         "the shared Overlay Hub owns keyboard and mouse interception.");
   }
   CompleteStartupPhase("input-iat-hooks", input_hooks_started, input_hooks_ready);

   g_hooks_ready.store(true, std::memory_order_release);
   SetRuntimeMessage(
      "Native hook installation completed with " +
         std::to_string(GetVirtualSlotCount()) +
         " virtual slots; compatibility was verified synchronously by every required byte/RVA preflight. Full-executable SHA-256 is deferred and diagnostic-only.",
      false);
   return true;
}
}
