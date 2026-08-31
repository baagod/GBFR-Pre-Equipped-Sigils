#include "../native_internal.h"

namespace
{
using DxgiPresentFn = int32_t(__stdcall*)(void*, uint32_t, uint32_t);

constexpr int32_t kEPointer = static_cast<int32_t>(0x80004003u);
constexpr int32_t kEFail = static_cast<int32_t>(0x80004005u);
constexpr uint32_t kMaxSupportedJumpCount = 32;

enum class ResolveStatus : uint32_t
{
   Ok = 0,
   InvalidArgument = 1,
   Unreadable = 2,
   NonExecutable = 3,
   Cycle = 4,
   DepthExceeded = 5,
   UnsupportedJump = 6,
};

enum class JumpDecodeResult
{
   NotJump,
   Resolved,
   Invalid,
   Unsupported,
};

bool IsReadableProtection(DWORD protection) noexcept
{
   if ((protection & (PAGE_GUARD | PAGE_NOACCESS)) != 0)
      return false;
   switch (protection & 0xFFu)
   {
   case PAGE_READONLY:
   case PAGE_READWRITE:
   case PAGE_WRITECOPY:
   case PAGE_EXECUTE:
   case PAGE_EXECUTE_READ:
   case PAGE_EXECUTE_READWRITE:
   case PAGE_EXECUTE_WRITECOPY:
      return true;
   default:
      return false;
   }
}

bool IsExecutableProtection(DWORD protection) noexcept
{
   if ((protection & (PAGE_GUARD | PAGE_NOACCESS)) != 0)
      return false;
   switch (protection & 0xFFu)
   {
   case PAGE_EXECUTE:
   case PAGE_EXECUTE_READ:
   case PAGE_EXECUTE_READWRITE:
   case PAGE_EXECUTE_WRITECOPY:
      return true;
   default:
      return false;
   }
}

bool IsReadableRange(uintptr_t address, size_t size) noexcept
{
   if (address == 0 || size == 0 || address > UINTPTR_MAX - (size - 1))
      return false;

   const uintptr_t last = address + size - 1;
   uintptr_t cursor = address;
   while (cursor <= last)
   {
      MEMORY_BASIC_INFORMATION information{};
      if (VirtualQuery(
             reinterpret_cast<const void*>(cursor),
             &information,
             sizeof(information)) == 0 ||
          information.State != MEM_COMMIT ||
          !IsReadableProtection(information.Protect))
      {
         return false;
      }

      const uintptr_t region_begin = reinterpret_cast<uintptr_t>(
         information.BaseAddress);
      if (information.RegionSize == 0 ||
          region_begin > UINTPTR_MAX - information.RegionSize)
      {
         return false;
      }
      const uintptr_t region_end = region_begin + information.RegionSize;
      if (cursor < region_begin || cursor >= region_end)
         return false;
      if (last < region_end)
         return true;
      cursor = region_end;
   }
   return true;
}

bool IsExecutableAddress(uintptr_t address) noexcept
{
   MEMORY_BASIC_INFORMATION information{};
   return address != 0 &&
      VirtualQuery(
         reinterpret_cast<const void*>(address),
         &information,
         sizeof(information)) != 0 &&
      information.State == MEM_COMMIT &&
      IsExecutableProtection(information.Protect);
}

bool TryReadMemory(uintptr_t address, void* destination, size_t size) noexcept
{
   if (destination == nullptr || !IsReadableRange(address, size))
      return false;
   __try
   {
      std::memcpy(destination, reinterpret_cast<const void*>(address), size);
      return true;
   }
   __except (EXCEPTION_EXECUTE_HANDLER)
   {
      return false;
   }
}

template <typename T>
bool TryReadValue(uintptr_t address, T* destination) noexcept
{
   return TryReadMemory(address, destination, sizeof(T));
}

bool TryAddressAtOffset(
   uintptr_t address,
   size_t offset,
   uintptr_t* result_out) noexcept
{
   if (result_out == nullptr || address > UINTPTR_MAX - offset)
      return false;
   *result_out = address + offset;
   return true;
}

template <typename T>
bool TryReadValueAtOffset(
   uintptr_t address,
   size_t offset,
   T* destination) noexcept
{
   uintptr_t source = 0;
   return TryAddressAtOffset(address, offset, &source) &&
      TryReadValue(source, destination);
}

bool TryReadMemoryAtOffset(
   uintptr_t address,
   size_t offset,
   void* destination,
   size_t size) noexcept
{
   uintptr_t source = 0;
   return TryAddressAtOffset(address, offset, &source) &&
      TryReadMemory(source, destination, size);
}

bool TryAddRelative(
   uintptr_t instruction_end,
   int64_t displacement,
   uintptr_t* target_out) noexcept
{
   if (target_out == nullptr)
      return false;
   if (displacement >= 0)
   {
      const auto offset = static_cast<uintptr_t>(displacement);
      if (instruction_end > UINTPTR_MAX - offset)
         return false;
      *target_out = instruction_end + offset;
      return true;
   }

   const auto magnitude = static_cast<uintptr_t>(
      static_cast<uint64_t>(-(displacement + 1)) + 1);
   if (instruction_end < magnitude)
      return false;
   *target_out = instruction_end - magnitude;
   return true;
}

JumpDecodeResult DecodeEntryJump(
   uintptr_t address,
   uintptr_t* target_out) noexcept
{
   std::array<uint8_t, 2> prefix{};
   if (target_out == nullptr ||
       !TryReadMemory(address, prefix.data(), prefix.size()))
   {
      return JumpDecodeResult::Invalid;
   }

   if (prefix[0] == 0xE9)
   {
      int32_t displacement = 0;
      uintptr_t instruction_end = 0;
      if (!TryReadValueAtOffset(address, 1, &displacement) ||
          !TryAddressAtOffset(address, 5, &instruction_end) ||
          !TryAddRelative(instruction_end, displacement, target_out))
      {
         return JumpDecodeResult::Invalid;
      }
      return JumpDecodeResult::Resolved;
   }
   if (prefix[0] == 0xEB)
   {
      int8_t displacement = 0;
      uintptr_t instruction_end = 0;
      if (!TryReadValueAtOffset(address, 1, &displacement) ||
          !TryAddressAtOffset(address, 2, &instruction_end) ||
          !TryAddRelative(instruction_end, displacement, target_out))
      {
         return JumpDecodeResult::Invalid;
      }
      return JumpDecodeResult::Resolved;
   }

   uintptr_t pointer_slot = 0;
   if (prefix[0] == 0xFF && prefix[1] == 0x25)
   {
      int32_t displacement = 0;
      uintptr_t instruction_end = 0;
      if (!TryReadValueAtOffset(address, 2, &displacement) ||
          !TryAddressAtOffset(address, 6, &instruction_end) ||
          !TryAddRelative(instruction_end, displacement, &pointer_slot) ||
          !TryReadValue(pointer_slot, target_out))
      {
         return JumpDecodeResult::Invalid;
      }
      return JumpDecodeResult::Resolved;
   }
   if (prefix[0] == 0xFF && (prefix[1] & 0x38u) == 0x20u)
      return JumpDecodeResult::Unsupported;

   if ((prefix[0] & 0xF0u) == 0x40u && prefix[1] == 0xFF)
   {
      uint8_t mod_rm = 0;
      if (!TryReadValueAtOffset(address, 2, &mod_rm))
         return JumpDecodeResult::Invalid;
      const bool is_jump = (prefix[0] & 0x04u) == 0 &&
         (mod_rm & 0x38u) == 0x20u;
      if (is_jump && mod_rm == 0x25)
      {
         int32_t displacement = 0;
         uintptr_t instruction_end = 0;
         if (!TryReadValueAtOffset(address, 3, &displacement) ||
             !TryAddressAtOffset(address, 7, &instruction_end) ||
             !TryAddRelative(instruction_end, displacement, &pointer_slot) ||
             !TryReadValue(pointer_slot, target_out))
         {
            return JumpDecodeResult::Invalid;
         }
         return JumpDecodeResult::Resolved;
      }
      if (is_jump)
         return JumpDecodeResult::Unsupported;
   }

   if ((prefix[0] == 0x48 || prefix[0] == 0x49) &&
       prefix[1] >= 0xB8 && prefix[1] <= 0xBF)
   {
      uintptr_t immediate_target = 0;
      if (!TryReadValueAtOffset(address, 2, &immediate_target))
         return JumpDecodeResult::Invalid;

      const uint8_t register_index = static_cast<uint8_t>(prefix[1] - 0xB8);
      if (prefix[0] == 0x48)
      {
         std::array<uint8_t, 2> suffix{};
         if (!TryReadMemoryAtOffset(
                address, 10, suffix.data(), suffix.size()))
            return JumpDecodeResult::Invalid;
         if (suffix[0] == 0xFF &&
             suffix[1] == static_cast<uint8_t>(0xE0 + register_index))
         {
            *target_out = immediate_target;
            return JumpDecodeResult::Resolved;
         }
      }
      else
      {
         std::array<uint8_t, 3> suffix{};
         if (!TryReadMemoryAtOffset(
                address, 10, suffix.data(), suffix.size()))
            return JumpDecodeResult::Invalid;
         if (suffix[0] == 0x41 && suffix[1] == 0xFF &&
             suffix[2] == static_cast<uint8_t>(0xE0 + register_index))
         {
            *target_out = immediate_target;
            return JumpDecodeResult::Resolved;
         }
      }
   }

   return JumpDecodeResult::NotJump;
}

void SetResolveOutputs(
   uint32_t jump_count,
   ResolveStatus status,
   uint32_t* jump_count_out,
   uint32_t* status_out) noexcept
{
   if (jump_count_out != nullptr)
      *jump_count_out = jump_count;
   if (status_out != nullptr)
      *status_out = static_cast<uint32_t>(status);
}

int CaptureExceptionCode(uint32_t code, uint32_t* destination) noexcept
{
   if (code != EXCEPTION_ACCESS_VIOLATION)
      return EXCEPTION_CONTINUE_SEARCH;
   if (destination != nullptr)
      *destination = code;
   return EXCEPTION_EXECUTE_HANDLER;
}
}

uint64_t GBFR20_CALL GBFR20_ResolveHookChainTarget(
   uint64_t function_address,
   uint32_t max_jump_count,
   uint32_t* jump_count_out,
   uint32_t* status_out)
{
   SetResolveOutputs(0, ResolveStatus::Ok, jump_count_out, status_out);
   if (function_address == 0 || max_jump_count == 0 ||
       max_jump_count > kMaxSupportedJumpCount)
   {
      SetResolveOutputs(
         0, ResolveStatus::InvalidArgument, jump_count_out, status_out);
      return 0;
   }

   uintptr_t current = static_cast<uintptr_t>(function_address);
   std::array<uintptr_t, kMaxSupportedJumpCount + 1> visited{};
   uint32_t visited_count = 0;
   uint32_t jump_count = 0;
   for (;;)
   {
      if (!IsExecutableAddress(current))
      {
         SetResolveOutputs(
            jump_count, ResolveStatus::NonExecutable, jump_count_out, status_out);
         return 0;
      }
      if (std::find(
             visited.begin(),
             visited.begin() + visited_count,
             current) != visited.begin() + visited_count)
      {
         SetResolveOutputs(
            jump_count, ResolveStatus::Cycle, jump_count_out, status_out);
         return 0;
      }
      visited[visited_count++] = current;

      uintptr_t next = 0;
      const JumpDecodeResult decode_result = DecodeEntryJump(current, &next);
      if (decode_result == JumpDecodeResult::Invalid)
      {
         SetResolveOutputs(
            jump_count, ResolveStatus::Unreadable, jump_count_out, status_out);
         return 0;
      }
      if (decode_result == JumpDecodeResult::Unsupported)
      {
         SetResolveOutputs(
            jump_count,
            ResolveStatus::UnsupportedJump,
            jump_count_out,
            status_out);
         return 0;
      }
      if (decode_result == JumpDecodeResult::NotJump)
      {
         SetResolveOutputs(
            jump_count, ResolveStatus::Ok, jump_count_out, status_out);
         return static_cast<uint64_t>(current);
      }
      if (jump_count >= max_jump_count)
      {
         SetResolveOutputs(
            jump_count,
            ResolveStatus::DepthExceeded,
            jump_count_out,
            status_out);
         return 0;
      }

      current = next;
      ++jump_count;
   }
}

int32_t GBFR20_CALL GBFR20_InvokeOriginalPresent(
   uint64_t original_function_address,
   void* swap_chain,
   uint32_t sync_interval,
   uint32_t present_flags,
   uint32_t* exception_code_out)
{
   if (exception_code_out != nullptr)
      *exception_code_out = 0;
   if (original_function_address == 0 || swap_chain == nullptr)
      return kEPointer;

   const auto present = reinterpret_cast<DxgiPresentFn>(
      static_cast<uintptr_t>(original_function_address));
   __try
   {
      return present(swap_chain, sync_interval, present_flags);
   }
   __except (CaptureExceptionCode(
      static_cast<uint32_t>(GetExceptionCode()),
      exception_code_out))
   {
      return kEFail;
   }
}
