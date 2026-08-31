#include "../native_internal.h"

#include <cctype>
#include <cstdio>
#include <iomanip>
#include <sstream>

namespace gbfr::native
{
HMODULE g_module = nullptr;
uintptr_t g_image_base = 0;
std::filesystem::path g_module_directory;
std::filesystem::path g_compatibility_path;

std::once_flag g_initialize_once;
std::atomic_bool g_initialized{false};
std::atomic_bool g_hooks_ready{false};
std::atomic_bool g_layout_ready{false};
ResolvedGameLayout g_game_layout{};
std::atomic_bool g_shutting_down{false};
std::atomic_bool g_shutdown_complete{false};
std::atomic<GBFR20_LogCallback> g_log_callback{nullptr};
std::mutex g_message_mutex;
std::string g_runtime_message = "Waiting for initialization.";
bool g_runtime_message_is_error = false;

std::atomic_int32_t g_edit_session_state{EditSessionUnknownLocked};
std::atomic_uint32_t g_observed_character_hash{0};
std::atomic_uint64_t g_observed_status_address{0};
std::atomic_int32_t g_observed_status_context{-1};
std::atomic_uint32_t g_lifecycle_rebind_attempts{0};
std::atomic_uint64_t g_lifecycle_rebind_signature{0};
std::atomic_uint32_t g_lifecycle_signature_attempts{0};
std::atomic_uint64_t g_lifecycle_rebind_not_before_ms{0};

int GetVirtualSlotCount() noexcept
{
   return kTemplateSlotCount;
}

int GetExpandedInternalSlotCount() noexcept
{
   return kNativeInternalSlotCount + GetVirtualSlotCount();
}

void Log(const std::string& message)
{
   SYSTEMTIME time{};
   GetLocalTime(&time);
   char timestamp[40]{};
   sprintf_s(
      timestamp,
      "%02u:%02u:%02u.%03u",
      time.wHour,
      time.wMinute,
      time.wSecond,
      time.wMilliseconds);
   const std::string line =
      std::string("[") + timestamp + "] [GBFR ReloadedSigilSlots Native] " + message + "\n";
   OutputDebugStringA(line.c_str());
   if (const GBFR20_LogCallback callback = g_log_callback.load(std::memory_order_acquire);
       callback != nullptr)
   {
      callback(message.c_str());
   }
}

uint64_t BeginStartupPhase(std::string_view)
{
   // Phases log once on completion (with elapsed time); failures are still
   // reported explicitly by CompleteStartupPhase, so a stuck startup is
   // identifiable by the last completed phase.
   return GetTickCount64();
}

void CompleteStartupPhase(
   std::string_view phase,
   uint64_t started_at_ms,
   bool succeeded)
{
   const uint64_t elapsed_ms = GetTickCount64() - started_at_ms;
   Log(
      "Startup phase=" + std::string(phase) + " state=" +
      (succeeded ? "complete" : "failed") + " elapsed_ms=" +
      std::to_string(elapsed_ms) + ".");
}

void SetRuntimeMessage(std::string message, bool is_error)
{
   {
      std::scoped_lock lock(g_message_mutex);
      g_runtime_message = std::move(message);
      g_runtime_message_is_error = is_error;
   }
   Log(g_runtime_message);
}

std::string GetRuntimeMessage(bool& is_error)
{
   std::scoped_lock lock(g_message_mutex);
   is_error = g_runtime_message_is_error;
   return g_runtime_message;
}

std::string WideToUtf8(std::wstring_view text)
{
   if (text.empty())
      return {};
   const int size = WideCharToMultiByte(
      CP_UTF8, 0, text.data(), static_cast<int>(text.size()), nullptr, 0, nullptr, nullptr);
   if (size <= 0)
      return {};
   std::string result(static_cast<size_t>(size), '\0');
   WideCharToMultiByte(
      CP_UTF8, 0, text.data(), static_cast<int>(text.size()), result.data(), size, nullptr, nullptr);
   return result;
}

std::string ToUpperHex(uint32_t value)
{
   std::ostringstream stream;
   stream << std::uppercase << std::hex << std::setw(8) << std::setfill('0') << value;
   return stream.str();
}

std::string ToLowerAscii(std::string value)
{
   std::transform(value.begin(), value.end(), value.begin(), [](unsigned char character) {
      return static_cast<char>(std::tolower(character));
   });
   return value;
}
}
