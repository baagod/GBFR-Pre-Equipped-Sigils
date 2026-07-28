#pragma once

#define WIN32_LEAN_AND_MEAN
#define NOMINMAX

#include <windows.h>

#include "native_api.h"
#include "third_party/safetyhook.hpp"

#include <algorithm>
#include <array>
#include <atomic>
#include <cstdint>
#include <cstring>
#include <filesystem>
#include <memory>
#include <mutex>
#include <shared_mutex>
#include <string>
#include <string_view>
#include <unordered_map>
#include <unordered_set>
#include <vector>

namespace gbfr::native
{
inline constexpr uintptr_t kTraitApplyLoopLimitImmediateRva = 0x00A25484;
inline constexpr uintptr_t kTraitApplyGetterReturnRva = 0x00A254A9;
inline constexpr uintptr_t kTraitCategoryLoopLimitImmediateRva = 0x00A26096;
inline constexpr uintptr_t kTraitFetchPathRva = 0x00A260AE;
inline constexpr uintptr_t kTraitFetchCallPathRva = 0x00A260F0;
inline constexpr uintptr_t kTraitCategoryGetterReturnRva = 0x00A260FE;
inline constexpr uintptr_t kGetGemDataByIndexRva = 0x00A2C610;
inline constexpr uintptr_t kStatusRebuildRva = 0x00A23CC0;
inline constexpr uintptr_t kStatusNotifierRva = 0x002D93F0;
inline constexpr uintptr_t kStatusOwnerTickRva = 0x0024B2F0;
inline constexpr uintptr_t kStatusOwnerCharacterLoopRva = 0x0024CA4A;
inline constexpr uintptr_t kLocalContext1BindCallRva = 0x002EA29D;
inline constexpr uintptr_t kLocalContext1BindReturnRva = 0x002EA2A2;
inline constexpr uintptr_t kSystemDataGlobalRva = 0x07C20940;
inline constexpr uintptr_t kStatusManagerGlobalRva = 0x07C24980;
inline constexpr uintptr_t kUiManagerGlobalRva = 0x07C4A140;
inline constexpr uintptr_t kUiStateSourceGlobalRva = 0x07C4A1A8;
inline constexpr int kNativeInternalSlotCount = 13;
inline constexpr int kDefaultVirtualSlotCount = 8;
inline constexpr int kVirtualSlotCapacity = 24;
inline constexpr int kMainGemCapacity = 5100;
inline constexpr int kCurrentSettingsVersion = 2;
inline constexpr uint32_t kExpectedCompatibilityMappingCount = 199;
inline constexpr uintptr_t kMainGemArrayOffset = 0x25D0;
inline constexpr uintptr_t kUiSelectedCharacterHashOffset = 0x5F0;
inline constexpr uintptr_t kUiModeOffset = 0xB14;
inline constexpr uintptr_t kUiStateSourceModeOffset = 0x34;
inline constexpr uintptr_t kStatusMapSentinelOffset = 0xA30;
inline constexpr uintptr_t kStatusMapBucketsOffset = 0xA40;
inline constexpr uintptr_t kStatusMapMaskOffset = 0xA58;
inline constexpr uintptr_t kCharacterRecordMapSentinelOffset = 0xED738;
inline constexpr uintptr_t kCharacterRecordMapBucketsOffset = 0xED748;
inline constexpr uintptr_t kCharacterRecordMapMaskOffset = 0xED760;
inline constexpr uintptr_t kCharacterRecordPrimaryHashOffset = 0x59F4;
inline constexpr uintptr_t kCharacterRecordFallbackHashOffset = 0x59F0;
inline constexpr uintptr_t kStatusCharacterHashOffset = 0x5EA8;
inline constexpr uintptr_t kStatusContextModeOffset = 0x5EAC;
inline constexpr uint32_t kUnwornCharacterHash = 0x887AE0B0;
inline constexpr uint32_t kLocalPlayerSlotKey = 0xDBD9A18D;

inline constexpr std::array<uint8_t, 16> kTraitApplyLoopPreflight = {
   0xFF, 0xC7, 0x83, 0xFF, 0x0D, 0x0F, 0x84, 0xB7,
   0x00, 0x00, 0x00, 0xC5, 0xF8, 0x11, 0x75, 0xF0};
inline constexpr std::array<uint8_t, 12> kTraitApplyGetterReturnPreflight = {
   0x84, 0xC0, 0x74, 0xD3, 0xF6, 0x45, 0x00, 0x10, 0x75, 0xCD, 0x44, 0x8B};
inline constexpr std::array<uint8_t, 13> kTraitCategoryLoopPreflight = {
   0x49, 0xFF, 0xC5, 0x49, 0x83, 0xFD, 0x0D, 0x0F, 0x84, 0xE4, 0x00, 0x00, 0x00};
inline constexpr std::array<uint8_t, 11> kTraitFetchPreflight = {
   0x84, 0xDB, 0x74, 0x3E, 0x49, 0x8B, 0x87, 0x80, 0x5E, 0x00, 0x00};
inline constexpr std::array<uint8_t, 14> kTraitFetchCallPathPreflight = {
   0x4C, 0x89, 0xF9, 0x44, 0x89, 0xEA, 0x4D, 0x89, 0xE0, 0xE8, 0x12, 0x65, 0x00, 0x00};
inline constexpr std::array<uint8_t, 12> kTraitCategoryGetterReturnPreflight = {
   0x84, 0xC0, 0x74, 0x8E, 0xF6, 0x45, 0xD8, 0x10, 0x75, 0x88, 0x8B, 0x55};
inline constexpr std::array<uint8_t, 12> kGetterPreflight = {
   0x55, 0x41, 0x57, 0x41, 0x56, 0x56, 0x57, 0x53, 0x48, 0x83, 0xEC, 0x28};
inline constexpr std::array<uint8_t, 12> kStatusRebuildPreflight = {
   0x55, 0x56, 0x57, 0x48, 0x83, 0xEC, 0x50, 0x48, 0x8D, 0x6C, 0x24, 0x50};
inline constexpr std::array<uint8_t, 12> kStatusNotifierPreflight = {
   0x41, 0x56, 0x56, 0x57, 0x53, 0x48, 0x83, 0xEC, 0x38, 0x44, 0x89, 0xC6};
inline constexpr std::array<uint8_t, 24> kStatusOwnerTickPreflight = {
   0x55, 0x41, 0x57, 0x41, 0x56, 0x41, 0x55, 0x41,
   0x54, 0x56, 0x57, 0x53, 0x48, 0x81, 0xEC, 0x98,
   0x05, 0x00, 0x00, 0x48, 0x8D, 0xAC, 0x24, 0x80};
inline constexpr std::array<uint8_t, 24> kStatusOwnerCharacterLoopPreflight = {
   0x48, 0x8B, 0x73, 0x20, 0x48, 0x8B, 0x7B, 0x28,
   0x48, 0x39, 0xFE, 0x0F, 0x84, 0x76, 0x01, 0x00,
   0x00, 0x4C, 0x8D, 0xB3, 0x30, 0x32, 0x00, 0x00};
inline constexpr std::array<uint8_t, 10> kLocalContext1BindCallPreflight = {
   0xE8, 0xEE, 0x47, 0x74, 0x00, 0x89, 0xD8, 0x89, 0x5D, 0xFC};
inline constexpr std::array<uint8_t, 10> kLocalContext1BindReturnPreflight = {
   0x89, 0xD8, 0x89, 0x5D, 0xFC, 0x48, 0x8B, 0x45, 0xE8, 0x8B};

using GemData = GBFR20_GemData;
static_assert(sizeof(GemData) == 0x24);

struct StatusIdentity
{
   uint32_t character_hash = 0;
   int32_t context_mode = -1;
};

struct InventoryItem
{
   GemData gem{};
   uintptr_t address = 0;
   bool equipped = false;
   uint32_t required_character_hash = 0;
   uint32_t virtual_owner_character_hash = 0;
   int32_t virtual_owner_slot = -1;
   std::string label;
   std::string searchable;
};

struct AuthorizedStatus
{
   uintptr_t status = 0;
   uint32_t character_hash = 0;
   int32_t context_mode = -1;
   uint64_t generation = 0;
   std::array<uint32_t, kVirtualSlotCapacity> slots{};
};

struct VirtualOwner
{
   uint32_t character_hash = 0;
   int32_t virtual_slot = -1;
};

enum NaturalBindResult : int32_t
{
   NaturalBindNone = 0,
   NaturalBindSucceeded = 1,
   NaturalBindInProgress = 2,
   NaturalBindContextRejected = -1,
   NaturalBindOwnerRejected = -2,
   NaturalBindStatusRejected = -3,
   NaturalBindSelectionRejected = -4,
   NaturalBindSequenceRejected = -5,
   NaturalBindFinalValidationRejected = -6,
   NaturalBindCopyRejected = -7,
};

struct NaturalContributionFrame
{
   uintptr_t status = 0;
   StatusIdentity identity{};
   std::array<uint32_t, kVirtualSlotCapacity> slots{};
   uint32_t expected = 0;
   uint32_t injected = 0;
   int next_slot = kNativeInternalSlotCount;
   bool active = false;
};

struct LocalContext1Binding
{
   uintptr_t manager = 0;
   uintptr_t record = 0;
   uintptr_t status = 0;
   uint32_t owner_key = 0;
   uint32_t character_hash = 0;
   uint32_t thread_id = 0;
   uint64_t generation = 0;
   bool active = false;
};

struct IatPatch
{
   void** slot = nullptr;
   void* original = nullptr;
   void* replacement = nullptr;
};

struct UiSettings
{
   int toggle_key = VK_F8;
   bool show_equipped = false;
   bool auto_apply = true;
   std::string language = "zh-CN";
   int virtual_slot_count = kDefaultVirtualSlotCount;
};

enum EditSessionState : int32_t
{
   EditSessionUnknownLocked = 0,
   EditSessionEquipment = 1,
   EditSessionMissionLocked = 2,
   EditSessionFreeTraining = 3,
};

enum ApplyResult : int
{
   ApplyResultNone = 0,
   ApplyResultAppliedDuringNativeRebuild = 2,
   ApplyResultSavedNoStatus = -1,
   ApplyResultVirtualCopyFailed = -2,
   ApplyResultOwnerThreadMismatch = -3,
   ApplyResultStatusLookupFailed = -4,
   ApplyResultNativeRebuildFailed = -5,
   ApplyResultNativeTraitLoopMissing = -6,
   ApplyResultNotifierFailed = -7,
};

struct ActiveCallGuard
{
   explicit ActiveCallGuard(std::atomic_uint32_t& value) : counter(value)
   {
      counter.fetch_add(1, std::memory_order_acq_rel);
   }
   ~ActiveCallGuard()
   {
      counter.fetch_sub(1, std::memory_order_acq_rel);
   }
   std::atomic_uint32_t& counter;
};

template <size_t Size>
bool MatchesBytes(uintptr_t address, const std::array<uint8_t, Size>& expected) noexcept
{
   __try
   {
      return std::memcmp(reinterpret_cast<const void*>(address), expected.data(), Size) == 0;
   }
   __except (EXCEPTION_EXECUTE_HANDLER)
   {
      return false;
   }
}

extern HMODULE g_module;
extern uintptr_t g_image_base;
extern std::filesystem::path g_module_directory;
extern std::filesystem::path g_config_path;
extern std::filesystem::path g_compatibility_path;
extern std::once_flag g_initialize_once;
extern std::atomic_bool g_initialized;
extern std::atomic_bool g_hooks_ready;
extern std::atomic_bool g_shutting_down;
extern std::atomic_bool g_shutdown_complete;
extern std::atomic<GBFR20_LogCallback> g_log_callback;
extern std::mutex g_message_mutex;
extern std::string g_runtime_message;
extern bool g_runtime_message_is_error;

extern SafetyHookInline g_get_gem_hook;
extern SafetyHookMid g_trait_fetch_hook;
extern SafetyHookMid g_status_owner_tick_hook;
extern SafetyHookMid g_local_context1_bind_call_hook;
extern SafetyHookMid g_local_context1_bind_return_hook;

extern std::shared_mutex g_selection_mutex;
extern std::unordered_map<uint32_t, std::array<uint32_t, kVirtualSlotCapacity>> g_character_selections;
extern std::unordered_map<uint32_t, uint32_t> g_required_character_by_gem;
extern std::unordered_map<uint32_t, VirtualOwner> g_virtual_owner_by_slot_id;
extern std::shared_mutex g_authorization_mutex;
extern std::unordered_map<uintptr_t, AuthorizedStatus> g_authorized_statuses;
extern std::atomic<uint32_t> g_last_authorized_character_hash;
extern std::atomic<uint64_t> g_last_authorized_status_address;

extern std::atomic_int32_t g_edit_session_state;
extern std::atomic_uint32_t g_observed_character_hash;
extern std::atomic_uint64_t g_observed_status_address;
extern std::atomic_int32_t g_observed_status_context;
extern std::atomic_uint32_t g_lifecycle_rebind_attempts;
extern std::atomic_uint64_t g_lifecycle_rebind_signature;
extern std::atomic_uint32_t g_lifecycle_signature_attempts;
extern std::atomic_uint64_t g_lifecycle_rebind_not_before_ms;

extern std::mutex g_inventory_index_mutex;
extern uintptr_t g_indexed_system_data;
extern std::unordered_map<uint32_t, uintptr_t> g_inventory_by_slot_id;
extern std::atomic<uint32_t> g_last_character_hash;
extern std::atomic<int32_t> g_last_context_mode;
extern std::atomic_uint64_t g_status_owner_manager_address;
extern std::atomic_uint32_t g_status_owner_thread_id;
extern std::atomic_uint64_t g_status_owner_tick_count;
extern std::atomic_uint32_t g_status_owner_character_count;
extern std::array<std::atomic_uint32_t, 4> g_status_owner_character_hashes;
extern std::shared_mutex g_local_context1_binding_mutex;
extern std::unordered_map<uintptr_t, LocalContext1Binding> g_local_context1_bindings;
extern std::atomic_uint64_t g_local_context1_binding_generation;
extern std::atomic_uint32_t g_overlay_thread_id;
extern std::atomic_uint64_t g_overlay_frame_count;
extern std::atomic_uint64_t g_inventory_revision;
extern std::atomic_bool g_inventory_dirty;

extern std::atomic_bool g_pending_refresh;
extern std::atomic<uint32_t> g_pending_character_hash;
extern std::atomic_uint32_t g_pending_injected_count;
extern std::atomic_uint32_t g_next_apply_generation;
extern std::atomic_uint64_t g_queued_apply_request;
extern std::atomic_uint64_t g_apply_retry_not_before_ms;
extern std::atomic_bool g_apply_in_flight;
extern std::mutex g_reconcile_apply_mutex;
extern std::unordered_set<uint32_t> g_reconcile_apply_hashes;
extern std::atomic_uint64_t g_active_apply_generation;
extern std::atomic_uint64_t g_claimed_apply_generation;
extern std::atomic_uint32_t g_active_apply_thread_id;
extern std::atomic_uint64_t g_active_apply_status;
extern std::atomic_bool g_native_apply_call_active;
extern std::array<std::atomic_uint32_t, kVirtualSlotCapacity> g_active_apply_slots;
extern std::atomic_uint32_t g_active_apply_expected_count;
extern std::atomic_uint64_t g_last_apply_generation;
extern std::atomic_uint32_t g_last_apply_character_hash;
extern std::atomic_uint32_t g_last_apply_expected_count;
extern std::atomic_uint32_t g_last_apply_injected_count;
extern std::atomic_int g_apply_result;
extern std::atomic_int g_last_consumed_apply_result;
extern std::atomic_uint32_t g_active_getter_calls;
extern std::atomic_uint32_t g_active_mid_calls;
extern std::atomic_uint32_t g_active_input_calls;
extern thread_local uint64_t g_tls_apply_generation;
extern thread_local NaturalContributionFrame g_tls_natural_contribution;
extern thread_local LocalContext1Binding g_tls_local_context1_binding;
extern std::atomic_uint64_t g_natural_bind_attempts;
extern std::atomic_uint64_t g_natural_bind_successes;
extern std::atomic_uint64_t g_natural_bind_status_address;
extern std::atomic_uint32_t g_natural_bind_character_hash;
extern std::atomic_int32_t g_natural_bind_context;
extern std::atomic_uint32_t g_natural_bind_expected_count;
extern std::atomic_uint32_t g_natural_bind_injected_count;
extern std::atomic_int32_t g_natural_bind_result;
extern std::atomic_uint32_t g_natural_bind_owner_key;
extern std::atomic_uint64_t g_natural_bind_owner_status_address;

extern std::atomic_bool g_input_capture_requested;
extern std::atomic_bool g_input_capture_effective;
extern std::atomic_uint32_t g_input_capture_requested_devices;
extern std::atomic_uint32_t g_input_capture_effective_devices;
extern std::atomic_uint32_t g_input_neutral_frames;
extern std::atomic_bool g_input_hooks_enabled;
extern std::atomic_bool g_input_iat_hooks_ready;
extern std::atomic_bool g_direct_input_hook_ready;
extern std::mutex g_input_hook_mutex;
extern std::vector<IatPatch> g_iat_patches;
extern POINT g_frozen_cursor_position;
using GetAsyncKeyStateFn = SHORT(WINAPI*)(int);
using GetKeyStateFn = SHORT(WINAPI*)(int);
using GetKeyboardStateFn = BOOL(WINAPI*)(PBYTE);
using GetCursorPosFn = BOOL(WINAPI*)(LPPOINT);
using SetCursorPosFn = BOOL(WINAPI*)(int, int);
using ClipCursorFn = BOOL(WINAPI*)(const RECT*);
using DirectInput8CreateFn = HRESULT(WINAPI*)(
   HINSTANCE,
   DWORD,
   REFIID,
   LPVOID*,
   void*);
extern GetAsyncKeyStateFn g_original_get_async_key_state;
extern GetKeyStateFn g_original_get_key_state;
extern GetKeyboardStateFn g_original_get_keyboard_state;
extern GetCursorPosFn g_original_get_cursor_pos;
extern SetCursorPosFn g_original_set_cursor_pos;
extern ClipCursorFn g_original_clip_cursor;
extern DirectInput8CreateFn g_original_direct_input8_create;

extern UiSettings g_settings;
extern std::mutex g_settings_mutex;
extern std::atomic_int32_t g_virtual_slot_count;
extern std::shared_mutex g_name_table_mutex;
extern std::unordered_map<uint32_t, std::string> g_sigil_names;
extern std::unordered_map<uint32_t, std::string> g_trait_names;
extern bool g_names_are_english;
extern std::mutex g_inventory_snapshot_mutex;
extern std::vector<InventoryItem> g_inventory_snapshot;

int GetVirtualSlotCount() noexcept;
int GetExpandedInternalSlotCount() noexcept;
void Log(const std::string& message);
uint64_t BeginStartupPhase(std::string_view phase);
void CompleteStartupPhase(std::string_view phase, uint64_t started_at_ms, bool succeeded);
void SetRuntimeMessage(std::string message, bool is_error);
std::string GetRuntimeMessage(bool& is_error);
std::string WideToUtf8(std::wstring_view text);
std::string ToUpperHex(uint32_t value);
std::string ToLowerAscii(std::string value);

bool SafeReadPointer(uintptr_t address, uintptr_t& value) noexcept;
bool SafeReadUiSelectedCharacterHash(uint32_t& character_hash) noexcept;
bool SafeReadInt32(uintptr_t address, int32_t& value) noexcept;
void SafeReadUiModes(int32_t& ui_mode, int32_t& source_mode) noexcept;
void UpdateEditSessionState() noexcept;
bool SafeReadGem(uintptr_t address, GemData& value) noexcept;
bool SafeReadStatusIdentity(uintptr_t status, StatusIdentity& identity) noexcept;
uint32_t SafeReadOwnerCharacterHashes(uintptr_t manager, std::array<uint32_t, 4>& hashes) noexcept;
bool SafeResolveStatusByMapKey(uintptr_t manager, uint32_t map_key, uintptr_t& status) noexcept;
bool SafeResolveCharacterRecordByOwnerKey(uintptr_t manager, uint32_t owner_key, uintptr_t& record) noexcept;
bool SafeReadCharacterRecordHash(uintptr_t record, uint32_t& character_hash) noexcept;
bool ValidateLocalContext1Binding(const LocalContext1Binding& binding, uintptr_t expected_status, const StatusIdentity* expected_identity = nullptr) noexcept;
bool TryGetLocalContext1Binding(uintptr_t status, uint32_t character_hash, LocalContext1Binding& binding) noexcept;
bool TryGetLocalContext1BindingByCharacter(uint32_t character_hash, LocalContext1Binding& binding) noexcept;
bool SafeResolveCharacterStatus(uint32_t character_hash, uintptr_t& manager, uintptr_t& status) noexcept;
bool SafeResolveSelectedCharacterStatus(uint32_t character_hash, uintptr_t& manager, uintptr_t& status, StatusIdentity& identity) noexcept;
bool SafeCanEditCharacter(uint32_t character_hash) noexcept;
void MarkInventoryDirty() noexcept;
void CommitAuthorizedStatus(uintptr_t status, const StatusIdentity& identity, uint64_t generation, const std::array<uint32_t, kVirtualSlotCapacity>& slots);
bool TryGetAuthorizedSelection(uintptr_t status, const StatusIdentity& identity, std::array<uint32_t, kVirtualSlotCapacity>& slots);
bool HasMatchingAuthorizedSelection(uintptr_t status, const StatusIdentity& identity, const std::array<uint32_t, kVirtualSlotCapacity>& slots);
bool TryGetAuthorizedContext1Status(uint32_t character_hash, AuthorizedStatus& authorization);
void EraseAuthorizedStatus(uintptr_t status);
void ValidateAuthorizedStatuses();
bool SafeCopyToOutput(const GemData& source, void* destination) noexcept;
bool SafeInvokeStatusRebuild(uintptr_t status, uint32_t character_hash, StatusIdentity& restored_identity, bool preserve_context) noexcept;
bool SafeNotifyStatusDirty(uintptr_t manager, uint32_t character_hash, uint32_t dirty_mask) noexcept;
bool ReadByte(uintptr_t address, uint8_t& value) noexcept;
bool WriteByte(uintptr_t address, uint8_t value);

void RestoreInputIatHooks();
void RestoreDirectInputInstanceHooks() noexcept;
void ResetDirectInputInstanceHooks() noexcept;
bool InstallInputIatHooks();
void UpdateInputCaptureBarrier();

void LoadSettingsAndSelections();
void SaveUiSettings();
void SaveCharacterSelection(uint32_t character_hash);
bool ReloadNameTable(std::string_view language);
bool LoadCompatibilityTable(const std::filesystem::path& path);
uint32_t GetRequiredCharacterHash(uint32_t gem_hash);
std::string LookupName(const std::unordered_map<uint32_t, std::string>& table, uint32_t hash, const char* prefix);

std::array<uint32_t, kVirtualSlotCapacity> GetSelection(uint32_t character_hash);
void ScheduleReconcileApply(uint32_t character_hash);
void PumpReconcileApplyQueue();
uint32_t RequestHotApply(uint32_t character_hash);
void ProcessPendingHotApply();
bool SetSelection(uint32_t character_hash, int virtual_slot, uint32_t inventory_slot_id);
bool ApplyPresetSelections(const GBFR20_PresetCharacterSelection* selections, uint32_t selection_count, GBFR20_PresetSlotResult* slot_results, uint32_t slot_result_capacity, uint32_t* slot_result_count);
uintptr_t ResolveGemAddress(uint32_t slot_id);
bool RefreshInventorySnapshot();

void ScheduleSelectedStatusRebind();
void ShutdownHooks();
bool InstallHooks();
void Initialize();
void EnsureInitialized();
void ConsumeApplyResult();
}
