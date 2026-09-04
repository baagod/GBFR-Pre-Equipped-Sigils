#pragma once

#include <cstdint>

#if defined(GBFR20_NATIVE_EXPORTS)
#define GBFR20_API extern "C" __declspec(dllexport)
#else
#define GBFR20_API extern "C" __declspec(dllimport)
#endif

#define GBFR20_CALL __cdecl

// Slimmed ABI v16: lifecycle exports + custom loadout injection.
// All selector/inventory/preset/input/present/state APIs of the derived
// original were removed.
constexpr uint32_t GBFR20_ABI_VERSION = 16;
constexpr uint32_t GBFR20_VIRTUAL_SLOT_CAPACITY = 24;

using GBFR20_LogCallback = void(GBFR20_CALL*)(const char* message);

#pragma pack(push, 1)
struct GBFR20_GemData
{
   uint32_t trait1;
   int32_t trait1_level;
   uint32_t trait2;
   int32_t trait2_level;
   uint32_t gem_id;
   uint32_t worn_by;
   int32_t sigil_level;
   uint32_t slot_id;
   uint32_t flags;
};

// ABI mirror of the native TemplateGemSlot (same field order, packed 1).
struct GBFR20_TemplateSlot
{
   uint32_t gem_id;
   uint32_t trait1;
   int32_t trait1_level;
   uint32_t trait2;
   int32_t trait2_level;
   int32_t sigil_level;
};
#pragma pack(pop)

static_assert(sizeof(GBFR20_GemData) == 0x24);
static_assert(sizeof(GBFR20_TemplateSlot) == 0x18);

GBFR20_API uint32_t GBFR20_CALL GBFR20_GetAbiVersion();
GBFR20_API void GBFR20_CALL GBFR20_SetLogCallback(GBFR20_LogCallback callback);
GBFR20_API int32_t GBFR20_CALL GBFR20_Initialize();
GBFR20_API void GBFR20_CALL GBFR20_Tick();
GBFR20_API void GBFR20_CALL GBFR20_Shutdown();
GBFR20_API uint32_t GBFR20_CALL GBFR20_CopyRuntimeMessage(
   char* buffer,
   uint32_t buffer_size);
// Applies a custom loadout (nullptr/count==0 restores the built-in template).
// Called from the managed upkeep tick; the native side copies the slots.
GBFR20_API int32_t GBFR20_CALL GBFR20_SetCustomLoadout(
   const GBFR20_TemplateSlot* slots,
   uint32_t count);
