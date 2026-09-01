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
struct ResolvedGameLayout
{
   uintptr_t trait_apply_loop_limit_immediate_rva = 0;
   uintptr_t trait_apply_getter_return_rva = 0;
   uintptr_t trait_category_loop_limit_immediate_rva = 0;
   uintptr_t trait_fetch_path_rva = 0;
   uintptr_t trait_fetch_call_path_rva = 0;
   uintptr_t trait_category_getter_return_rva = 0;
   uintptr_t get_gem_data_by_index_rva = 0;
   uintptr_t status_rebuild_rva = 0;
   uintptr_t status_notifier_rva = 0;
   uintptr_t status_owner_tick_rva = 0;
   uintptr_t status_owner_character_loop_rva = 0;
   uintptr_t system_data_global_rva = 0;
   uintptr_t status_manager_global_rva = 0;
   uintptr_t ui_manager_global_rva = 0;
   uintptr_t ui_state_source_global_rva = 0;
   uintptr_t main_gem_array_offset = 0;
   uintptr_t ui_selected_character_hash_offset = 0;
   uintptr_t ui_mode_offset = 0;
   uintptr_t ui_state_source_mode_offset = 0;
   uintptr_t status_map_sentinel_offset = 0;
   uintptr_t status_map_buckets_offset = 0;
   uintptr_t status_map_mask_offset = 0;
   uintptr_t status_character_hash_offset = 0;
   uintptr_t status_context_mode_offset = 0;
   uint8_t trait_apply_original_limit = 0;
   uint8_t trait_category_original_limit = 0;
   uint32_t pe_timestamp = 0;
};

inline constexpr int kNativeInternalSlotCount = 13;
inline constexpr int kTemplateSlotCount = 6;
inline constexpr int kVirtualSlotCapacity = 24;
inline constexpr uint32_t kExpectedCompatibilityMappingCount = 199;
inline constexpr uint32_t kUnwornCharacterHash = 0x887AE0B0;
inline constexpr uint32_t kGranCharacterHash = 0x2A26B1B2;
inline constexpr uint32_t kDjeetaCharacterHash = 0xA4ACBA76;

// Template (synthesized) sigil slots use a slot-id range that can never
// collide with real inventory slot ids (0 .. 5099).
inline constexpr uint32_t kTemplateSlotIdBase = 0xFE000000u;

inline constexpr bool IsTemplateSlotId(uint32_t slot_id) noexcept
{
   return slot_id >= kTemplateSlotIdBase;
}

inline constexpr uint32_t MakeTemplateSlotId(int virtual_slot) noexcept
{
   return kTemplateSlotIdBase + static_cast<uint32_t>(virtual_slot);
}

struct TemplateGemSlot
{
   uint32_t gem_id = 0; // real gem hash for the gem-master lookup; 0 = empty slot
   uint32_t trait1 = 0;
   int32_t trait1_level = 0;
   uint32_t trait2 = 0; // 0 = single-trait sigil
   int32_t trait2_level = 0;
   int32_t sigil_level = 0; // displayed sigil level (V+ = 15)
};

struct CharacterTemplate
{
   uint32_t character_hash = 0;
   std::array<TemplateGemSlot, kVirtualSlotCapacity> slots{};
};

inline constexpr bool IsCaptainCharacterHash(uint32_t character_hash) noexcept
{
   return character_hash == kGranCharacterHash || character_hash == kDjeetaCharacterHash;
}

inline constexpr bool IsCharacterCompatible(
   uint32_t required_character_hash,
   uint32_t character_hash) noexcept
{
   return required_character_hash == 0 ||
      required_character_hash == character_hash ||
      (IsCaptainCharacterHash(required_character_hash) &&
       IsCaptainCharacterHash(character_hash));
}

static_assert(IsCharacterCompatible(kGranCharacterHash, kDjeetaCharacterHash));
static_assert(IsCharacterCompatible(kDjeetaCharacterHash, kGranCharacterHash));
static_assert(!IsCharacterCompatible(kGranCharacterHash, 0x18E2F9F9));
static_assert(!IsCharacterCompatible(0x18E2F9F9, kDjeetaCharacterHash));

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

using GemData = GBFR20_GemData;
static_assert(sizeof(GemData) == 0x24);

struct StatusIdentity
{
   uint32_t character_hash = 0;
   int32_t context_mode = -1;
};

struct AuthorizedStatus
{
   uintptr_t status = 0;
   uint32_t character_hash = 0;
   int32_t context_mode = -1;
   uint64_t generation = 0;
   std::array<uint32_t, kVirtualSlotCapacity> slots{};
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
extern std::filesystem::path g_compatibility_path;
extern std::once_flag g_initialize_once;
extern std::atomic_bool g_initialized;
extern std::atomic_bool g_hooks_ready;
extern std::atomic_bool g_layout_ready;
extern ResolvedGameLayout g_game_layout;
extern std::atomic_bool g_shutting_down;
extern std::atomic_bool g_shutdown_complete;
extern std::atomic<GBFR20_LogCallback> g_log_callback;
extern std::mutex g_message_mutex;
extern std::string g_runtime_message;
extern bool g_runtime_message_is_error;

extern SafetyHookInline g_get_gem_hook;
extern SafetyHookMid g_trait_fetch_hook;
extern SafetyHookMid g_status_owner_tick_hook;

extern std::shared_mutex g_selection_mutex;
extern std::unordered_map<uint32_t, std::array<uint32_t, kVirtualSlotCapacity>> g_character_selections;
extern std::unordered_map<uint32_t, uint32_t> g_required_character_by_gem;
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

extern std::atomic<uint32_t> g_last_character_hash;
extern std::atomic<int32_t> g_last_context_mode;
extern std::atomic_uint64_t g_status_owner_manager_address;
extern std::atomic_uint32_t g_status_owner_thread_id;
extern std::atomic_uint64_t g_status_owner_tick_count;
extern std::atomic_uint32_t g_status_owner_character_count;
extern std::array<std::atomic_uint32_t, 4> g_status_owner_character_hashes;
extern std::atomic_bool g_pending_refresh;
extern std::atomic<uint32_t> g_pending_character_hash;
extern std::atomic_uint32_t g_pending_injected_count;
extern std::atomic_uint32_t g_next_apply_generation;
extern std::atomic_uint64_t g_queued_apply_request;
extern std::atomic_uint64_t g_apply_retry_not_before_ms;
extern std::atomic_bool g_apply_in_flight;
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
extern thread_local uint64_t g_tls_apply_generation;
extern thread_local NaturalContributionFrame g_tls_natural_contribution;
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
bool SafeResolveCharacterStatus(uint32_t character_hash, uintptr_t& manager, uintptr_t& status) noexcept;
bool SafeResolveSelectedCharacterStatus(uint32_t character_hash, uintptr_t& manager, uintptr_t& status, StatusIdentity& identity) noexcept;
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

bool LoadCompatibilityTable(const std::filesystem::path& path);
uint32_t GetRequiredCharacterHash(uint32_t gem_hash);

std::array<uint32_t, kVirtualSlotCapacity> GetSelection(uint32_t character_hash);
uint32_t RequestHotApply(uint32_t character_hash);
void ProcessPendingHotApply();

const CharacterTemplate* FindCharacterTemplate(uint32_t character_hash) noexcept;
const TemplateGemSlot* FindTemplateSlot(uint32_t character_hash, int virtual_slot) noexcept;
void InstallDefaultTemplateSelections();
bool TryCopyTemplateGem(uint32_t character_hash, uint32_t selected_slot_id, void* output) noexcept;

void ScheduleSelectedStatusRebind();
bool ResolveGameLayout();
bool RevalidateGameLayout();
void ResetGameLayout() noexcept;
void ShutdownHooks();
bool InstallHooks();
void Initialize();
void EnsureInitialized();
void ConsumeApplyResult();
}
