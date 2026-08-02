#include "../native_internal.h"

using namespace gbfr::native;

uint32_t GBFR20_CALL GBFR20_GetAbiVersion()
{
   return GBFR20_ABI_VERSION;
}

void GBFR20_CALL GBFR20_SetLogCallback(GBFR20_LogCallback callback)
{
   g_log_callback.store(callback, std::memory_order_release);
}

int32_t GBFR20_CALL GBFR20_Initialize()
{
   if (g_shutting_down.load(std::memory_order_acquire))
      return 0;
   EnsureInitialized();
   return g_hooks_ready.load(std::memory_order_acquire) ? 1 : 0;
}

void GBFR20_CALL GBFR20_Tick()
{
   if (g_shutting_down.load(std::memory_order_acquire))
      return;
   g_overlay_thread_id.store(GetCurrentThreadId(), std::memory_order_release);
   g_overlay_frame_count.fetch_add(1, std::memory_order_acq_rel);
   EnsureInitialized();
   UpdateInputCaptureBarrier();
   if (!g_hooks_ready.load(std::memory_order_acquire) ||
       !g_layout_ready.load(std::memory_order_acquire))
      return;
   UpdateEditSessionState();
   ValidateAuthorizedStatuses();
   ScheduleSelectedStatusRebind();
   PumpReconcileApplyQueue();
   ProcessPendingHotApply();
   ConsumeApplyResult();
}

void GBFR20_CALL GBFR20_Shutdown()
{
   if (g_shutdown_complete.exchange(true, std::memory_order_acq_rel))
      return;
   ShutdownHooks();
}

int32_t GBFR20_CALL GBFR20_GetState(GBFR20_RuntimeState* state, uint32_t state_size)
{
   if (state == nullptr || state_size < sizeof(GBFR20_RuntimeState))
      return 0;

   GBFR20_RuntimeState snapshot{};
   snapshot.abi_version = GBFR20_ABI_VERSION;
   snapshot.struct_size = sizeof(snapshot);
   snapshot.initialized = g_initialized.load(std::memory_order_acquire) ? 1 : 0;
   snapshot.hooks_ready = g_hooks_ready.load(std::memory_order_acquire) ? 1 : 0;
   snapshot.shutting_down = g_shutting_down.load(std::memory_order_acquire) ? 1 : 0;
   {
      std::scoped_lock lock(g_message_mutex);
      snapshot.runtime_message_is_error = g_runtime_message_is_error ? 1 : 0;
   }
   uint32_t ui_character_hash = 0;
   SafeReadUiSelectedCharacterHash(ui_character_hash);
   snapshot.ui_selected_character_hash = ui_character_hash;
   snapshot.last_rebuilt_character_hash =
      g_last_character_hash.load(std::memory_order_acquire);
   snapshot.effective_character_hash = ui_character_hash != 0 ? ui_character_hash : 0;
   snapshot.last_context_mode = g_last_context_mode.load(std::memory_order_acquire);
   snapshot.owner_thread_id = g_status_owner_thread_id.load(std::memory_order_acquire);
   snapshot.overlay_thread_id = g_overlay_thread_id.load(std::memory_order_acquire);
   snapshot.owner_tick_count = g_status_owner_tick_count.load(std::memory_order_acquire);
   snapshot.overlay_frame_count = g_overlay_frame_count.load(std::memory_order_acquire);
   snapshot.owner_character_count = std::min<uint32_t>(
      g_status_owner_character_count.load(std::memory_order_acquire),
      GBFR20_OWNER_CHARACTER_CAPACITY);
   for (uint32_t index = 0; index < GBFR20_OWNER_CHARACTER_CAPACITY; ++index)
      snapshot.owner_character_hashes[index] =
         g_status_owner_character_hashes[index].load(std::memory_order_acquire);
   snapshot.last_apply_generation = g_last_apply_generation.load(std::memory_order_acquire);
   snapshot.last_apply_character_hash =
      g_last_apply_character_hash.load(std::memory_order_acquire);
   snapshot.last_apply_expected_count =
      g_last_apply_expected_count.load(std::memory_order_acquire);
   snapshot.last_apply_injected_count =
      g_last_apply_injected_count.load(std::memory_order_acquire);
   snapshot.last_apply_result =
      g_last_consumed_apply_result.load(std::memory_order_acquire);
   {
      std::scoped_lock lock(g_settings_mutex);
      snapshot.auto_apply = g_settings.auto_apply ? 1 : 0;
      snapshot.show_equipped = g_settings.show_equipped ? 1 : 0;
      snapshot.toggle_key = g_settings.toggle_key;
      snapshot.language = g_settings.language == "en" ? 1 : 0;
   }
   {
      std::shared_lock lock(g_authorization_mutex);
      snapshot.authorized_status_count = g_authorized_statuses.size() > UINT32_MAX
         ? UINT32_MAX
         : static_cast<uint32_t>(g_authorized_statuses.size());
   }
   snapshot.authorized_character_hash =
      g_last_authorized_character_hash.load(std::memory_order_acquire);
   snapshot.authorized_status_address =
      g_last_authorized_status_address.load(std::memory_order_acquire);
   snapshot.inventory_revision = g_inventory_revision.load(std::memory_order_acquire);
   snapshot.inventory_dirty = g_inventory_dirty.load(std::memory_order_acquire) ? 1 : 0;
   snapshot.edit_allowed = SafeCanEditCharacter(snapshot.effective_character_hash) ? 1 : 0;
   SafeReadUiModes(snapshot.ui_mode, snapshot.source_mode);
   snapshot.edit_session_state = g_edit_session_state.load(std::memory_order_acquire);
   snapshot.observed_character_hash =
      g_observed_character_hash.load(std::memory_order_acquire);
   snapshot.observed_status_address =
      g_observed_status_address.load(std::memory_order_acquire);
   snapshot.observed_status_context =
      g_observed_status_context.load(std::memory_order_acquire);
   snapshot.lifecycle_rebind_attempts =
      g_lifecycle_rebind_attempts.load(std::memory_order_acquire);
   snapshot.input_capture_requested =
      g_input_capture_requested.load(std::memory_order_acquire) ? 1 : 0;
   snapshot.input_capture_effective =
      g_input_capture_effective.load(std::memory_order_acquire) ? 1 : 0;
   snapshot.input_iat_hooks_ready =
      g_input_iat_hooks_ready.load(std::memory_order_acquire) ? 1 : 0;
   snapshot.direct_input_hook_ready =
      g_direct_input_hook_ready.load(std::memory_order_acquire) ? 1 : 0;
   snapshot.natural_bind_attempts =
      g_natural_bind_attempts.load(std::memory_order_acquire);
   snapshot.natural_bind_successes =
      g_natural_bind_successes.load(std::memory_order_acquire);
   snapshot.natural_bind_status_address =
      g_natural_bind_status_address.load(std::memory_order_acquire);
   snapshot.natural_bind_character_hash =
      g_natural_bind_character_hash.load(std::memory_order_acquire);
   snapshot.natural_bind_context =
      g_natural_bind_context.load(std::memory_order_acquire);
   snapshot.natural_bind_expected_count =
      g_natural_bind_expected_count.load(std::memory_order_acquire);
   snapshot.natural_bind_injected_count =
      g_natural_bind_injected_count.load(std::memory_order_acquire);
   snapshot.natural_bind_result =
      g_natural_bind_result.load(std::memory_order_acquire);
   snapshot.owner_manager_address =
      g_status_owner_manager_address.load(std::memory_order_acquire);
   snapshot.natural_bind_owner_key =
      g_natural_bind_owner_key.load(std::memory_order_acquire);
   snapshot.natural_bind_owner_status_address =
      g_natural_bind_owner_status_address.load(std::memory_order_acquire);
   snapshot.virtual_slot_count = static_cast<uint32_t>(GetVirtualSlotCount());
   snapshot.virtual_slot_capacity = kVirtualSlotCapacity;
   std::memcpy(state, &snapshot, sizeof(snapshot));
   return 1;
}

uint32_t GBFR20_CALL GBFR20_CopyRuntimeMessage(char* buffer, uint32_t buffer_size)
{
   std::string message;
   {
      std::scoped_lock lock(g_message_mutex);
      message = g_runtime_message;
   }
   const size_t required_size = message.size() + 1;
   if (buffer != nullptr && buffer_size != 0)
   {
      const size_t copy_size = std::min<size_t>(message.size(), buffer_size - 1);
      std::memcpy(buffer, message.data(), copy_size);
      buffer[copy_size] = '\0';
   }
   return required_size > UINT32_MAX ? UINT32_MAX : static_cast<uint32_t>(required_size);
}

int32_t GBFR20_CALL GBFR20_RefreshInventory()
{
   EnsureInitialized();
   return RefreshInventorySnapshot() ? 1 : 0;
}

uint32_t GBFR20_CALL GBFR20_GetInventoryCount()
{
   std::scoped_lock lock(g_inventory_snapshot_mutex);
   return g_inventory_snapshot.size() > UINT32_MAX
      ? UINT32_MAX
      : static_cast<uint32_t>(g_inventory_snapshot.size());
}

int32_t GBFR20_CALL GBFR20_CopyInventoryItem(
   uint32_t index,
   GBFR20_InventoryItem* item,
   uint32_t item_size,
   char* label_buffer,
   uint32_t label_buffer_size)
{
   if (item == nullptr || item_size < sizeof(GBFR20_InventoryItem))
      return 0;
   std::scoped_lock lock(g_inventory_snapshot_mutex);
   if (index >= g_inventory_snapshot.size())
      return 0;

   const InventoryItem& source = g_inventory_snapshot[index];
   GBFR20_InventoryItem result{};
   result.gem = source.gem;
   result.equipped = source.equipped ? 1u : 0u;
   result.required_character_hash = source.required_character_hash;
   result.virtual_owner_character_hash = source.virtual_owner_character_hash;
   result.virtual_owner_slot = source.virtual_owner_slot;
   std::memcpy(item, &result, sizeof(result));
   if (label_buffer != nullptr && label_buffer_size != 0)
   {
      const size_t copy_size = std::min<size_t>(source.label.size(), label_buffer_size - 1);
      std::memcpy(label_buffer, source.label.data(), copy_size);
      label_buffer[copy_size] = '\0';
   }
   return 1;
}

int32_t GBFR20_CALL GBFR20_GetSelection(
   uint32_t character_hash,
   uint32_t* slots,
   uint32_t slot_count)
{
   if (character_hash == 0 || slots == nullptr || slot_count < kVirtualSlotCapacity)
      return 0;
   const auto selection = GetSelection(character_hash);
   std::memcpy(slots, selection.data(), sizeof(selection));
   return 1;
}

int32_t GBFR20_CALL GBFR20_SetSelection(
   uint32_t character_hash,
   int32_t virtual_slot,
   uint32_t inventory_slot_id)
{
   return SetSelection(character_hash, virtual_slot, inventory_slot_id) ? 1 : 0;
}

int32_t GBFR20_CALL GBFR20_ApplyPreset(
   const GBFR20_PresetCharacterSelection* selections,
   uint32_t selection_count,
   GBFR20_PresetSlotResult* slot_results,
   uint32_t slot_result_capacity,
   uint32_t* slot_result_count)
{
   return ApplyPresetSelections(
      selections,
      selection_count,
      slot_results,
      slot_result_capacity,
      slot_result_count)
      ? 1
      : 0;
}

uint32_t GBFR20_CALL GBFR20_RequestApply(uint32_t character_hash)
{
   return SafeCanEditCharacter(character_hash) ? RequestHotApply(character_hash) : 0;
}

int32_t GBFR20_CALL GBFR20_SetAutoApply(int32_t enabled)
{
   {
      std::scoped_lock lock(g_settings_mutex);
      g_settings.auto_apply = enabled != 0;
   }
   SaveUiSettings();
   return 1;
}

int32_t GBFR20_CALL GBFR20_SetShowEquipped(int32_t enabled)
{
   {
      std::scoped_lock lock(g_settings_mutex);
      g_settings.show_equipped = enabled != 0;
   }
   SaveUiSettings();
   return 1;
}

int32_t GBFR20_CALL GBFR20_SetToggleKey(int32_t virtual_key)
{
   if (virtual_key < 1 || virtual_key > 255)
      return 0;
   {
      std::scoped_lock lock(g_settings_mutex);
      if (g_settings.toggle_key == virtual_key)
         return 1;
      g_settings.toggle_key = virtual_key;
   }
   SaveUiSettings();
   return 1;
}

int32_t GBFR20_CALL GBFR20_SetLanguage(int32_t language)
{
   if (language != 0 && language != 1)
      return 0;
   const std::string language_code = language == 1 ? "en" : "zh-CN";
   {
      std::scoped_lock lock(g_settings_mutex);
      if (g_settings.language == language_code)
         return 1;
   }
   if (!ReloadNameTable(language_code))
      return 0;
   {
      std::scoped_lock lock(g_settings_mutex);
      g_settings.language = language_code;
   }
   SaveUiSettings();
   MarkInventoryDirty();
   return 1;
}

int32_t GBFR20_CALL GBFR20_RequestVirtualSlotCount(int32_t slot_count)
{
   EnsureInitialized();
   return RequestPendingVirtualSlotCount(slot_count);
}

int32_t GBFR20_CALL GBFR20_GetPendingVirtualSlotCount()
{
   EnsureInitialized();
   return GetPendingVirtualSlotCount();
}

int32_t GBFR20_CALL GBFR20_SetInputHooksEnabled(int32_t enabled)
{
   const bool requested_mode = enabled != 0;
   if (g_initialized.load(std::memory_order_acquire) ||
       g_hooks_ready.load(std::memory_order_acquire) ||
       g_input_iat_hooks_ready.load(std::memory_order_acquire) ||
       g_direct_input_hook_ready.load(std::memory_order_acquire))
   {
      return g_input_hooks_enabled.load(std::memory_order_acquire) == requested_mode
         ? 1
         : 0;
   }
   g_input_hooks_enabled.store(requested_mode, std::memory_order_release);
   return 1;
}

int32_t GBFR20_CALL GBFR20_SetInputCapture(int32_t requested)
{
   if (requested < 0)
   {
      g_input_capture_requested.store(false, std::memory_order_release);
      g_input_capture_effective.store(false, std::memory_order_release);
      g_input_capture_requested_devices.store(0, std::memory_order_release);
      g_input_capture_effective_devices.store(0, std::memory_order_release);
      g_input_neutral_frames.store(0, std::memory_order_release);
      return 1;
   }
   return GBFR20_SetInputCaptureDevices(requested != 0
      ? GBFR20_INPUT_CAPTURE_KEYBOARD | GBFR20_INPUT_CAPTURE_MOUSE
      : 0);
}

int32_t GBFR20_CALL GBFR20_SetInputCaptureDevices(uint32_t requested_devices)
{
   uint32_t normalized = requested_devices &
      (GBFR20_INPUT_CAPTURE_KEYBOARD | GBFR20_INPUT_CAPTURE_MOUSE);
   if ((requested_devices & GBFR20_INPUT_CAPTURE_TEXT) != 0)
      normalized |= GBFR20_INPUT_CAPTURE_KEYBOARD;

   const uint32_t previous_effective =
      g_input_capture_effective_devices.load(std::memory_order_acquire);
   if ((normalized & GBFR20_INPUT_CAPTURE_MOUSE) != 0 &&
       (previous_effective & GBFR20_INPUT_CAPTURE_MOUSE) == 0)
   {
      if (g_original_get_cursor_pos != nullptr)
         (void)g_original_get_cursor_pos(&g_frozen_cursor_position);
      else
         (void)GetCursorPos(&g_frozen_cursor_position);
   }

   g_input_capture_requested_devices.store(normalized, std::memory_order_release);
   g_input_capture_requested.store(normalized != 0, std::memory_order_release);
   const uint32_t effective = previous_effective | normalized;
   g_input_capture_effective_devices.store(effective, std::memory_order_release);
   g_input_capture_effective.store(effective != 0, std::memory_order_release);
   g_input_neutral_frames.store(0, std::memory_order_release);
   return 1;
}

int32_t GBFR20_CALL GBFR20_GetInputCaptureActive()
{
   return g_input_capture_effective.load(std::memory_order_acquire) ? 1 : 0;
}

int32_t GBFR20_CALL GBFR20_IsInventoryDirty()
{
   return g_inventory_dirty.load(std::memory_order_acquire) ? 1 : 0;
}

int32_t GBFR20_CALL GBFR20_CanEditCharacter(uint32_t character_hash)
{
   return SafeCanEditCharacter(character_hash) ? 1 : 0;
}
