#pragma once

#include <cstdint>

#if defined(GBFR20_NATIVE_EXPORTS)
#define GBFR20_API extern "C" __declspec(dllexport)
#else
#define GBFR20_API extern "C" __declspec(dllimport)
#endif

#define GBFR20_CALL __cdecl

// Slimmed ABI v17: lifecycle exports + custom loadout injection + per-
// character exclusive overrides. All selector/inventory/preset/input/present/
// state APIs of the derived original were removed.
constexpr uint32_t GBFR20_ABI_VERSION = 17;

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

// Per-character exclusive switch (default: all enabled). Bits in order of the
// struct fields: disable_t1 / disable_t2 / disable_war.
struct GBFR20_ExclusiveOverride
{
   uint32_t character_hash;
   uint8_t disable_t1;
   uint8_t disable_t2;
   uint8_t disable_war;
   uint8_t reserved;
};
#pragma pack(pop)

static_assert(sizeof(GBFR20_GemData) == 0x24);
static_assert(sizeof(GBFR20_TemplateSlot) == 0x18);
static_assert(sizeof(GBFR20_ExclusiveOverride) == 0x08);

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
// Applies per-character exclusive overrides (disable_t1/t2/war bits);
// nullptr/count==0 clears all overrides (everything enabled again). Called
// from the managed upkeep tick; the native side copies the overrides.
GBFR20_API int32_t GBFR20_CALL GBFR20_SetExclusiveOverrides(
   const GBFR20_ExclusiveOverride* overrides,
   uint32_t count);
