#define DIRECTINPUT_VERSION 0x0800

#include "../native_internal.h"

#include <dinput.h>

namespace gbfr::native
{
SafetyHookInline g_direct_input_create_device_hook;
SafetyHookInline g_direct_input_get_state_hook;
SafetyHookInline g_direct_input_get_data_hook;
SafetyHookInline g_direct_input_get_state_hook_secondary;
SafetyHookInline g_direct_input_get_data_hook_secondary;
std::atomic_bool g_input_capture_requested{false};
std::atomic_bool g_input_capture_effective{false};
std::atomic_uint32_t g_input_capture_requested_devices{0};
std::atomic_uint32_t g_input_capture_effective_devices{0};
std::atomic_uint32_t g_input_neutral_frames{0};
std::atomic_bool g_input_hooks_enabled{true};
std::atomic_bool g_input_iat_hooks_ready{false};
std::atomic_bool g_direct_input_hook_ready{false};
std::atomic_uintptr_t g_direct_input_mouse_device{0};
std::atomic_uintptr_t g_direct_input_keyboard_device{0};
std::mutex g_input_hook_mutex;
std::vector<IatPatch> g_iat_patches;
POINT g_frozen_cursor_position{};
GetAsyncKeyStateFn g_original_get_async_key_state = nullptr;
GetKeyStateFn g_original_get_key_state = nullptr;
GetKeyboardStateFn g_original_get_keyboard_state = nullptr;
GetCursorPosFn g_original_get_cursor_pos = nullptr;
SetCursorPosFn g_original_set_cursor_pos = nullptr;
ClipCursorFn g_original_clip_cursor = nullptr;
DirectInput8CreateFn g_original_direct_input8_create = nullptr;
std::atomic_uint32_t g_active_input_calls{0};

namespace
{
bool IsInputDeviceCaptured(uint32_t device) noexcept
{
   return (g_input_capture_effective_devices.load(std::memory_order_acquire) & device) != 0;
}

bool ShouldDiscardDirectInput(uintptr_t device_address) noexcept
{
   if (device_address == 0)
      return false;
   if (device_address == g_direct_input_keyboard_device.load(std::memory_order_acquire))
      return IsInputDeviceCaptured(GBFR20_INPUT_CAPTURE_KEYBOARD);
   if (device_address == g_direct_input_mouse_device.load(std::memory_order_acquire))
      return IsInputDeviceCaptured(GBFR20_INPUT_CAPTURE_MOUSE);
   return false;
}

constexpr GUID kDirectInputSystemMouse = {
   0x6F1D2B60,
   0xD5A0,
   0x11CF,
   {0xBF, 0xC7, 0x44, 0x45, 0x53, 0x54, 0x00, 0x00}};
constexpr GUID kDirectInputSystemKeyboard = {
   0x6F1D2B61,
   0xD5A0,
   0x11CF,
   {0xBF, 0xC7, 0x44, 0x45, 0x53, 0x54, 0x00, 0x00}};
constexpr GUID kDirectInput8AInterface = {
   0xBF798030,
   0x483A,
   0x4DA2,
   {0xAA, 0x99, 0x5D, 0x64, 0xED, 0x36, 0x97, 0x00}};
constexpr GUID kDirectInput8WInterface = {
   0xBF798031,
   0x483A,
   0x4DA2,
   {0xAA, 0x99, 0x5D, 0x64, 0xED, 0x36, 0x97, 0x00}};

constexpr size_t kCreateDeviceVtableIndex = 3;
constexpr size_t kGetDeviceStateVtableIndex = 9;
constexpr size_t kGetDeviceDataVtableIndex = 10;

uintptr_t g_direct_input_get_state_target = 0;
uintptr_t g_direct_input_get_data_target = 0;
uintptr_t g_direct_input_get_state_target_secondary = 0;
uintptr_t g_direct_input_get_data_target_secondary = 0;

class ScopedStartupPhase
{
public:
   explicit ScopedStartupPhase(std::string_view phase) noexcept
      : phase_(phase)
   {
      try
      {
         started_at_ms_ = BeginStartupPhase(phase_);
         active_ = true;
      }
      catch (...)
      {
         // A phase marker must never unwind through a DirectInput callback.
      }
   }

   ~ScopedStartupPhase() noexcept
   {
      if (!active_)
         return;
      try
      {
         CompleteStartupPhase(phase_, started_at_ms_, succeeded_);
      }
      catch (...)
      {
         // Diagnostics must never unwind through a DirectInput callback.
      }
   }

   void MarkSucceeded() noexcept
   {
      succeeded_ = true;
   }

private:
   std::string_view phase_;
   uint64_t started_at_ms_ = 0;
   bool active_ = false;
   bool succeeded_ = false;
};

std::atomic_bool g_logged_direct_input_passthrough{false};

bool IsExecutableAddress(uintptr_t address) noexcept;
void RegisterDirectInputDevice(const GUID* device_guid, void* device);
void TryInstallDirectInputFactoryHook(void* factory);
HRESULT __fastcall DirectInputCreateDeviceDetour(
   void* factory,
   const GUID* device_guid,
   void** output_device,
   void* outer);
HRESULT __fastcall DirectInputGetDeviceStateDetour(
   void* device,
   DWORD data_size,
   void* data);
HRESULT __fastcall DirectInputGetDeviceDataDetour(
   void* device,
   DWORD object_data_size,
   void* object_data,
   DWORD* object_count,
   DWORD flags);
HRESULT __fastcall DirectInputGetDeviceStateDetourSecondary(
   void* device,
   DWORD data_size,
   void* data);
HRESULT __fastcall DirectInputGetDeviceDataDetourSecondary(
   void* device,
   DWORD object_data_size,
   void* object_data,
   DWORD* object_count,
   DWORD flags);

bool PatchMainModuleImport(
   const char* module_name,
   const char* function_name,
   void* replacement,
   void*& original)
{
   original = nullptr;
   if (g_image_base == 0 || module_name == nullptr || function_name == nullptr ||
       replacement == nullptr)
      return false;

   __try
   {
      const auto* dos = reinterpret_cast<const IMAGE_DOS_HEADER*>(g_image_base);
      if (dos->e_magic != IMAGE_DOS_SIGNATURE)
         return false;
      const auto* nt = reinterpret_cast<const IMAGE_NT_HEADERS64*>(
         g_image_base + static_cast<uintptr_t>(dos->e_lfanew));
      if (nt->Signature != IMAGE_NT_SIGNATURE)
         return false;
      const IMAGE_DATA_DIRECTORY& directory =
         nt->OptionalHeader.DataDirectory[IMAGE_DIRECTORY_ENTRY_IMPORT];
      if (directory.VirtualAddress == 0 || directory.Size < sizeof(IMAGE_IMPORT_DESCRIPTOR))
         return false;

      auto* descriptor = reinterpret_cast<IMAGE_IMPORT_DESCRIPTOR*>(
         g_image_base + directory.VirtualAddress);
      for (; descriptor->Name != 0; ++descriptor)
      {
         const char* imported_module =
            reinterpret_cast<const char*>(g_image_base + descriptor->Name);
         if (_stricmp(imported_module, module_name) != 0)
            continue;

         auto* first_thunk = reinterpret_cast<IMAGE_THUNK_DATA64*>(
            g_image_base + descriptor->FirstThunk);
         auto* name_thunk = descriptor->OriginalFirstThunk != 0
            ? reinterpret_cast<IMAGE_THUNK_DATA64*>(
                 g_image_base + descriptor->OriginalFirstThunk)
            : first_thunk;
         for (; name_thunk->u1.AddressOfData != 0; ++name_thunk, ++first_thunk)
         {
            if (IMAGE_SNAP_BY_ORDINAL64(name_thunk->u1.Ordinal))
               continue;
            const auto* import = reinterpret_cast<const IMAGE_IMPORT_BY_NAME*>(
               g_image_base + name_thunk->u1.AddressOfData);
            if (std::strcmp(reinterpret_cast<const char*>(import->Name), function_name) != 0)
               continue;

            auto** slot = reinterpret_cast<void**>(&first_thunk->u1.Function);
            DWORD old_protection = 0;
            if (!VirtualProtect(slot, sizeof(void*), PAGE_READWRITE, &old_protection))
               return false;
            original = InterlockedExchangePointer(
               reinterpret_cast<void* volatile*>(slot), replacement);
            DWORD ignored = 0;
            const bool restored =
               VirtualProtect(slot, sizeof(void*), old_protection, &ignored) != FALSE;
            if (!restored || original == nullptr)
               return false;
            g_iat_patches.push_back({slot, original, replacement});
            return true;
         }
      }
   }
   __except (EXCEPTION_EXECUTE_HANDLER)
   {
      original = nullptr;
      return false;
   }
   return false;
}

SHORT WINAPI GetAsyncKeyStateDetour(int virtual_key)
{
   ActiveCallGuard guard(g_active_input_calls);
   if (IsInputDeviceCaptured(GBFR20_INPUT_CAPTURE_KEYBOARD))
      return 0;
   return g_original_get_async_key_state != nullptr
      ? g_original_get_async_key_state(virtual_key)
      : 0;
}

SHORT WINAPI GetKeyStateDetour(int virtual_key)
{
   ActiveCallGuard guard(g_active_input_calls);
   if (IsInputDeviceCaptured(GBFR20_INPUT_CAPTURE_KEYBOARD))
      return 0;
   return g_original_get_key_state != nullptr ? g_original_get_key_state(virtual_key) : 0;
}

BOOL WINAPI GetKeyboardStateDetour(PBYTE key_state)
{
   ActiveCallGuard guard(g_active_input_calls);
   const BOOL result = g_original_get_keyboard_state != nullptr
      ? g_original_get_keyboard_state(key_state)
      : FALSE;
   if (result != FALSE && key_state != nullptr &&
       IsInputDeviceCaptured(GBFR20_INPUT_CAPTURE_KEYBOARD))
      std::memset(key_state, 0, 256);
   return result;
}

BOOL WINAPI GetCursorPosDetour(LPPOINT point)
{
   ActiveCallGuard guard(g_active_input_calls);
   if (IsInputDeviceCaptured(GBFR20_INPUT_CAPTURE_MOUSE))
   {
      if (point != nullptr)
         *point = g_frozen_cursor_position;
      return TRUE;
   }
   return g_original_get_cursor_pos != nullptr ? g_original_get_cursor_pos(point) : FALSE;
}

BOOL WINAPI SetCursorPosDetour(int x, int y)
{
   ActiveCallGuard guard(g_active_input_calls);
   if (IsInputDeviceCaptured(GBFR20_INPUT_CAPTURE_MOUSE))
      return TRUE;
   return g_original_set_cursor_pos != nullptr ? g_original_set_cursor_pos(x, y) : FALSE;
}

BOOL WINAPI ClipCursorDetour(const RECT* rectangle)
{
   ActiveCallGuard guard(g_active_input_calls);
   if (IsInputDeviceCaptured(GBFR20_INPUT_CAPTURE_MOUSE))
   {
      if (g_original_clip_cursor != nullptr)
         (void)g_original_clip_cursor(nullptr);
      return TRUE;
   }
   return g_original_clip_cursor != nullptr ? g_original_clip_cursor(rectangle) : FALSE;
}

bool IsExecutableAddress(uintptr_t address) noexcept
{
   MEMORY_BASIC_INFORMATION information{};
   if (address == 0 || VirtualQuery(
          reinterpret_cast<const void*>(address), &information, sizeof(information)) == 0)
      return false;
   const DWORD protection = information.Protect & 0xFF;
   return information.State == MEM_COMMIT &&
      (protection == PAGE_EXECUTE || protection == PAGE_EXECUTE_READ ||
       protection == PAGE_EXECUTE_READWRITE || protection == PAGE_EXECUTE_WRITECOPY);
}

HRESULT InvokeDirectInput8CreateSafely(
   HINSTANCE instance,
   DWORD version,
   REFIID interface_id,
   LPVOID* output,
   void* outer) noexcept
{
   if (g_original_direct_input8_create == nullptr)
      return DIERR_GENERIC;
   __try
   {
      return g_original_direct_input8_create(instance, version, interface_id, output, outer);
   }
   __except (EXCEPTION_EXECUTE_HANDLER)
   {
      return DIERR_GENERIC;
   }
}

HRESULT InvokeDirectInputCreateDeviceSafely(
   void* factory,
   const GUID* device_guid,
   void** output_device,
   void* outer) noexcept
{
   if (!g_direct_input_create_device_hook)
      return DIERR_GENERIC;
   __try
   {
      return g_direct_input_create_device_hook.call<HRESULT>(
         factory, device_guid, output_device, outer);
   }
   __except (EXCEPTION_EXECUTE_HANDLER)
   {
      return DIERR_GENERIC;
   }
}

HRESULT WINAPI DirectInput8CreateDetour(
   HINSTANCE instance,
   DWORD version,
   REFIID interface_id,
   LPVOID* output,
   void* outer)
{
   ActiveCallGuard guard(g_active_input_calls);
   HRESULT result = DIERR_GENERIC;
   try
   {
      result = InvokeDirectInput8CreateSafely(
         instance, version, interface_id, output, outer);
   }
   catch (...)
   {
      return DIERR_GENERIC;
   }
   const bool is_direct_input_8 = IsEqualGUID(interface_id, kDirectInput8AInterface) ||
      IsEqualGUID(interface_id, kDirectInput8WInterface);
   if (SUCCEEDED(result) && is_direct_input_8 && output != nullptr && *output != nullptr)
   {
      try
      {
         TryInstallDirectInputFactoryHook(*output);
      }
      catch (...)
      {
         // Preserve the original DirectInput result if optional wrapping fails.
      }
   }
   return result;
}

HRESULT __fastcall DirectInputCreateDeviceDetour(
   void* factory,
   const GUID* device_guid,
   void** output_device,
   void* outer)
{
   ActiveCallGuard guard(g_active_input_calls);
   HRESULT result = DIERR_GENERIC;
   try
   {
      result = InvokeDirectInputCreateDeviceSafely(
         factory, device_guid, output_device, outer);
   }
   catch (...)
   {
      return DIERR_GENERIC;
   }
   if (SUCCEEDED(result) && output_device != nullptr && *output_device != nullptr)
   {
      try
      {
         RegisterDirectInputDevice(device_guid, *output_device);
      }
      catch (...)
      {
         // Device creation succeeded; optional keyboard/mouse wrapping did not.
      }
   }
   return result;
}

HRESULT __fastcall DirectInputGetDeviceStateDetour(
   void* device,
   DWORD data_size,
   void* data)
{
   ActiveCallGuard guard(g_active_input_calls);
   const HRESULT result = g_direct_input_get_state_hook.call<HRESULT>(
      device, data_size, data);
   const uintptr_t device_address = reinterpret_cast<uintptr_t>(device);
   const bool discard_input = ShouldDiscardDirectInput(device_address);
   if (SUCCEEDED(result) && data != nullptr && data_size != 0 && discard_input)
      std::memset(data, 0, data_size);
   return result;
}

HRESULT __fastcall DirectInputGetDeviceStateDetourSecondary(
   void* device,
   DWORD data_size,
   void* data)
{
   ActiveCallGuard guard(g_active_input_calls);
   const HRESULT result = g_direct_input_get_state_hook_secondary.call<HRESULT>(
      device, data_size, data);
   const uintptr_t device_address = reinterpret_cast<uintptr_t>(device);
   const bool discard_input = ShouldDiscardDirectInput(device_address);
   if (SUCCEEDED(result) && data != nullptr && data_size != 0 && discard_input)
      std::memset(data, 0, data_size);
   return result;
}

HRESULT __fastcall DirectInputGetDeviceDataDetour(
   void* device,
   DWORD object_data_size,
   void* object_data,
   DWORD* object_count,
   DWORD flags)
{
   ActiveCallGuard guard(g_active_input_calls);
   const uintptr_t device_address = reinterpret_cast<uintptr_t>(device);
   const bool discard_buffered_input = ShouldDiscardDirectInput(device_address);
   const DWORD forwarded_flags = discard_buffered_input && object_data != nullptr &&
         object_count != nullptr
      ? flags & ~DIGDD_PEEK
      : flags;
   const HRESULT result = g_direct_input_get_data_hook.call<HRESULT>(
      device, object_data_size, object_data, object_count, forwarded_flags);
   if (SUCCEEDED(result) && object_count != nullptr && discard_buffered_input)
   {
      *object_count = 0;
      return DI_OK;
   }
   return result;
}

HRESULT __fastcall DirectInputGetDeviceDataDetourSecondary(
   void* device,
   DWORD object_data_size,
   void* object_data,
   DWORD* object_count,
   DWORD flags)
{
   ActiveCallGuard guard(g_active_input_calls);
   const uintptr_t device_address = reinterpret_cast<uintptr_t>(device);
   const bool discard_buffered_input = ShouldDiscardDirectInput(device_address);
   const DWORD forwarded_flags = discard_buffered_input && object_data != nullptr &&
         object_count != nullptr
      ? flags & ~DIGDD_PEEK
      : flags;
   const HRESULT result = g_direct_input_get_data_hook_secondary.call<HRESULT>(
      device, object_data_size, object_data, object_count, forwarded_flags);
   if (SUCCEEDED(result) && object_count != nullptr && discard_buffered_input)
   {
      *object_count = 0;
      return DI_OK;
   }
   return result;
}

void TryInstallDirectInputDeviceHooks(void* device, std::string_view phase_name)
{
   if (device == nullptr || g_shutting_down.load(std::memory_order_acquire))
      return;

   ScopedStartupPhase phase(phase_name);
   std::scoped_lock lock(g_input_hook_mutex);
   uintptr_t vtable = 0;
   uintptr_t get_state = 0;
   uintptr_t get_data = 0;
   if (!SafeReadPointer(reinterpret_cast<uintptr_t>(device), vtable) || vtable == 0 ||
       !SafeReadPointer(vtable + sizeof(uintptr_t) * kGetDeviceStateVtableIndex, get_state) ||
       !SafeReadPointer(vtable + sizeof(uintptr_t) * kGetDeviceDataVtableIndex, get_data) ||
       !IsExecutableAddress(get_state) || !IsExecutableAddress(get_data))
      return;

   bool state_ready = false;
   if (get_state == g_direct_input_get_state_target && g_direct_input_get_state_hook)
      state_ready = true;
   else if (get_state == g_direct_input_get_state_target_secondary &&
            g_direct_input_get_state_hook_secondary)
      state_ready = true;
   else if (g_direct_input_get_state_target == 0)
   {
      g_direct_input_get_state_hook = safetyhook::create_inline(
         reinterpret_cast<void*>(get_state),
         reinterpret_cast<void*>(&DirectInputGetDeviceStateDetour));
      if (g_direct_input_get_state_hook)
      {
         g_direct_input_get_state_target = get_state;
         state_ready = true;
      }
   }
   else if (g_direct_input_get_state_target_secondary == 0)
   {
      g_direct_input_get_state_hook_secondary = safetyhook::create_inline(
         reinterpret_cast<void*>(get_state),
         reinterpret_cast<void*>(&DirectInputGetDeviceStateDetourSecondary));
      if (g_direct_input_get_state_hook_secondary)
      {
         g_direct_input_get_state_target_secondary = get_state;
         state_ready = true;
      }
   }

   bool data_ready = false;
   if (get_data == g_direct_input_get_data_target && g_direct_input_get_data_hook)
      data_ready = true;
   else if (get_data == g_direct_input_get_data_target_secondary &&
            g_direct_input_get_data_hook_secondary)
      data_ready = true;
   else if (g_direct_input_get_data_target == 0)
   {
      g_direct_input_get_data_hook = safetyhook::create_inline(
         reinterpret_cast<void*>(get_data),
         reinterpret_cast<void*>(&DirectInputGetDeviceDataDetour));
      if (g_direct_input_get_data_hook)
      {
         g_direct_input_get_data_target = get_data;
         data_ready = true;
      }
   }
   else if (g_direct_input_get_data_target_secondary == 0)
   {
      g_direct_input_get_data_hook_secondary = safetyhook::create_inline(
         reinterpret_cast<void*>(get_data),
         reinterpret_cast<void*>(&DirectInputGetDeviceDataDetourSecondary));
      if (g_direct_input_get_data_hook_secondary)
      {
         g_direct_input_get_data_target_secondary = get_data;
         data_ready = true;
      }
   }

   if (!state_ready || !data_ready)
   {
      Log("DirectInput keyboard/mouse device used an unsupported third method target; that device was left untouched.");
      return;
   }

   phase.MarkSucceeded();
   g_direct_input_hook_ready.store(true, std::memory_order_release);
   Log("DirectInput keyboard/mouse method gates registered without replacing a COM object vtable; controller devices remain untouched.");
}

void RegisterDirectInputDevice(const GUID* device_guid, void* device)
{
   if (device_guid == nullptr || device == nullptr ||
       g_shutting_down.load(std::memory_order_acquire))
      return;

   if (IsEqualGUID(*device_guid, kDirectInputSystemKeyboard))
   {
      g_direct_input_keyboard_device.store(
         reinterpret_cast<uintptr_t>(device), std::memory_order_release);
      TryInstallDirectInputDeviceHooks(device, "directinput-keyboard-method-hooks");
      return;
   }
   if (IsEqualGUID(*device_guid, kDirectInputSystemMouse))
   {
      g_direct_input_mouse_device.store(
         reinterpret_cast<uintptr_t>(device), std::memory_order_release);
      TryInstallDirectInputDeviceHooks(device, "directinput-mouse-method-hooks");
      return;
   }

   if (!g_logged_direct_input_passthrough.exchange(true, std::memory_order_acq_rel))
      Log("A DirectInput non-keyboard/mouse device was returned untouched for controller pass-through.");
}

void TryInstallDirectInputFactoryHook(void* factory)
{
   if (factory == nullptr || g_shutting_down.load(std::memory_order_acquire))
      return;

   ScopedStartupPhase phase("directinput-create-device-inline-hook");

   bool installed = false;
   {
      std::scoped_lock lock(g_input_hook_mutex);
      if (g_direct_input_create_device_hook)
      {
         phase.MarkSucceeded();
         return;
      }

      uintptr_t vtable_address = 0;
      if (!SafeReadPointer(reinterpret_cast<uintptr_t>(factory), vtable_address) ||
          vtable_address == 0)
         return;
      uintptr_t create_device = 0;
      if (!SafeReadPointer(
             vtable_address + sizeof(void*) * kCreateDeviceVtableIndex, create_device) ||
          !IsExecutableAddress(create_device))
         return;

      g_direct_input_create_device_hook = safetyhook::create_inline(
         reinterpret_cast<void*>(create_device),
         reinterpret_cast<void*>(&DirectInputCreateDeviceDetour));
      installed = static_cast<bool>(g_direct_input_create_device_hook);
   }

   if (installed)
   {
      phase.MarkSucceeded();
      Log("DirectInput8 CreateDevice chain gate installed without replacing the factory vtable; only system keyboard/mouse method targets will be registered.");
   }
}

bool IsPhysicalInputNeutral(uint32_t devices)
{
   if (g_original_get_async_key_state == nullptr)
      return true;

   if ((devices & GBFR20_INPUT_CAPTURE_MOUSE) != 0)
   {
      constexpr int mouse_buttons[] = {
         VK_LBUTTON, VK_RBUTTON, VK_MBUTTON, VK_XBUTTON1, VK_XBUTTON2};
      for (const int button : mouse_buttons)
         if ((g_original_get_async_key_state(button) & 0x8000) != 0)
            return false;
   }
   if ((devices & GBFR20_INPUT_CAPTURE_KEYBOARD) != 0)
   {
      for (int key = VK_BACK; key <= 0xFE; ++key)
         if ((g_original_get_async_key_state(key) & 0x8000) != 0)
            return false;
   }
   return true;
}

void RestoreInputIatHooksLocked() noexcept
{
   for (auto iterator = g_iat_patches.rbegin(); iterator != g_iat_patches.rend(); ++iterator)
   {
      if (iterator->slot == nullptr || iterator->original == nullptr)
         continue;
      DWORD old_protection = 0;
      if (!VirtualProtect(iterator->slot, sizeof(void*), PAGE_READWRITE, &old_protection))
         continue;
      if (*iterator->slot == iterator->replacement)
         InterlockedExchangePointer(
            reinterpret_cast<void* volatile*>(iterator->slot), iterator->original);
      DWORD ignored = 0;
      (void)VirtualProtect(iterator->slot, sizeof(void*), old_protection, &ignored);
   }
   g_iat_patches.clear();
   g_input_iat_hooks_ready.store(false, std::memory_order_release);
}
}

void RestoreInputIatHooks()
{
   std::scoped_lock lock(g_input_hook_mutex);
   RestoreInputIatHooksLocked();
}

void RestoreDirectInputInstanceHooks() noexcept
{
   std::scoped_lock lock(g_input_hook_mutex);
   if (g_direct_input_create_device_hook)
      (void)g_direct_input_create_device_hook.disable();
   if (g_direct_input_get_state_hook)
      (void)g_direct_input_get_state_hook.disable();
   if (g_direct_input_get_data_hook)
      (void)g_direct_input_get_data_hook.disable();
   if (g_direct_input_get_state_hook_secondary)
      (void)g_direct_input_get_state_hook_secondary.disable();
   if (g_direct_input_get_data_hook_secondary)
      (void)g_direct_input_get_data_hook_secondary.disable();
}

void ResetDirectInputInstanceHooks() noexcept
{
   std::scoped_lock lock(g_input_hook_mutex);
   g_direct_input_create_device_hook.reset();
   g_direct_input_get_state_hook.reset();
   g_direct_input_get_data_hook.reset();
   g_direct_input_get_state_hook_secondary.reset();
   g_direct_input_get_data_hook_secondary.reset();
   g_direct_input_get_state_target = 0;
   g_direct_input_get_data_target = 0;
   g_direct_input_get_state_target_secondary = 0;
   g_direct_input_get_data_target_secondary = 0;
   g_direct_input_mouse_device.store(0, std::memory_order_release);
   g_direct_input_keyboard_device.store(0, std::memory_order_release);
   g_direct_input_hook_ready.store(false, std::memory_order_release);
}

bool InstallInputIatHooks()
{
   bool succeeded = false;
   {
      std::scoped_lock lock(g_input_hook_mutex);
      if (g_input_iat_hooks_ready.load(std::memory_order_acquire))
         return true;

      const uint64_t user32_started = BeginStartupPhase("user32-input-iat-hooks");
      void* original = nullptr;
      bool user32_succeeded = PatchMainModuleImport(
         "USER32.dll", "GetAsyncKeyState", reinterpret_cast<void*>(&GetAsyncKeyStateDetour), original);
      g_original_get_async_key_state = reinterpret_cast<GetAsyncKeyStateFn>(original);

      original = nullptr;
      user32_succeeded = PatchMainModuleImport(
         "USER32.dll", "GetKeyState", reinterpret_cast<void*>(&GetKeyStateDetour), original) && user32_succeeded;
      g_original_get_key_state = reinterpret_cast<GetKeyStateFn>(original);

      original = nullptr;
      user32_succeeded = PatchMainModuleImport(
         "USER32.dll", "GetKeyboardState", reinterpret_cast<void*>(&GetKeyboardStateDetour), original) && user32_succeeded;
      g_original_get_keyboard_state = reinterpret_cast<GetKeyboardStateFn>(original);

      original = nullptr;
      user32_succeeded = PatchMainModuleImport(
         "USER32.dll", "GetCursorPos", reinterpret_cast<void*>(&GetCursorPosDetour), original) && user32_succeeded;
      g_original_get_cursor_pos = reinterpret_cast<GetCursorPosFn>(original);

      original = nullptr;
      user32_succeeded = PatchMainModuleImport(
         "USER32.dll", "SetCursorPos", reinterpret_cast<void*>(&SetCursorPosDetour), original) && user32_succeeded;
      g_original_set_cursor_pos = reinterpret_cast<SetCursorPosFn>(original);

      original = nullptr;
      user32_succeeded = PatchMainModuleImport(
         "USER32.dll", "ClipCursor", reinterpret_cast<void*>(&ClipCursorDetour), original) && user32_succeeded;
      g_original_clip_cursor = reinterpret_cast<ClipCursorFn>(original);
      CompleteStartupPhase("user32-input-iat-hooks", user32_started, user32_succeeded);

      const uint64_t direct_input_started = BeginStartupPhase("directinput-iat-hook");
      original = nullptr;
      const bool direct_input_succeeded = PatchMainModuleImport(
         "DINPUT8.dll", "DirectInput8Create", reinterpret_cast<void*>(&DirectInput8CreateDetour), original);
      g_original_direct_input8_create = reinterpret_cast<DirectInput8CreateFn>(original);
      CompleteStartupPhase(
         "directinput-iat-hook", direct_input_started, direct_input_succeeded);

      succeeded = user32_succeeded && direct_input_succeeded;
      if (succeeded)
         g_input_iat_hooks_ready.store(true, std::memory_order_release);
      else
         RestoreInputIatHooksLocked();
   }

   if (!succeeded)
   {
      RestoreDirectInputInstanceHooks();
      while (g_active_input_calls.load(std::memory_order_acquire) != 0)
         SwitchToThread();
      ResetDirectInputInstanceHooks();
   }
   Log(succeeded
      ? "Game-local USER32 and DirectInput8 keyboard/mouse gates installed; controller input is passed through."
      : "One or more game-local USER32/DirectInput8 keyboard and mouse gates failed to install; the partial input transaction was rolled back.");
   return succeeded;
}

void UpdateInputCaptureBarrier()
{
   const uint32_t requested =
      g_input_capture_requested_devices.load(std::memory_order_acquire);
   uint32_t effective =
      g_input_capture_effective_devices.load(std::memory_order_acquire);
   const uint32_t added = requested & ~effective;
   if (added != 0)
   {
      effective |= added;
      g_input_capture_effective_devices.store(effective, std::memory_order_release);
      g_input_capture_effective.store(true, std::memory_order_release);
   }

   const uint32_t pending_release = effective & ~requested;
   if (pending_release == 0)
   {
      g_input_neutral_frames.store(0, std::memory_order_release);
      g_input_capture_effective.store(effective != 0, std::memory_order_release);
      return;
   }
   if (!IsPhysicalInputNeutral(pending_release))
   {
      g_input_neutral_frames.store(0, std::memory_order_release);
      return;
   }
   const uint32_t neutral_frames =
      g_input_neutral_frames.fetch_add(1, std::memory_order_acq_rel) + 1;
   if (neutral_frames >= 2)
   {
      g_input_capture_effective_devices.store(requested, std::memory_order_release);
      g_input_capture_effective.store(requested != 0, std::memory_order_release);
      g_input_neutral_frames.store(0, std::memory_order_release);
   }
}
}
