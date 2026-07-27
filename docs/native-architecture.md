# Native architecture and refactor plan

This document records the native runtime boundaries that must remain stable
while the original single-file implementation is split into maintainable
modules. The stable pre-refactor baseline is commit `1351349`, preserved on the
`2026/7/18-backup` branch.

## Runtime boundary

The shipped mod has two runtime components:

- `GBFR.ExtraSigilSlots.Reloaded.dll` owns Reloaded-II lifecycle, ImGui,
  localization, presets, and keyboard/mouse capture policy.
- `GBFR.ExtraSigilSlots.Native.dll` owns executable validation, game-memory
  access, SafetyHook detours, inventory snapshots, per-character selections,
  and same-thread status rebuilds.

The packed declarations in `GBFR.ExtraSigilSlots.Native/native_api.h` are the
only supported boundary between them. Refactoring must not silently change
export names, calling conventions, structure packing, field order, structure
sizes, result values, or ABI version.

## Required dependency direction

```text
C exports / DllMain
        |
        v
RuntimeCoordinator -----> InputCapture
        |                      |
        v                      v
SelectionStore          HookManager
InventoryStore                 |
        |                      v
        +-------------> SafeGameAccess
        |
        v
ApplyCoordinator -----> TraitInjection / thread-local frames
```

Lower layers must not call managed code or render UI. Hooks may delegate to
stores and coordinators, but stores must not install or disable hooks.

## Non-negotiable invariants

1. Only a verified Granblue Fantasy: Relink ER 2.0.2 code layout may install
   game hooks. The executable name remains mandatory. A known executable hash
   identifies the tested build, while an unknown hash is diagnostic only and
   must pass every required RVA/byte preflight before any hook or byte patch is
   installed. A failed preflight rejects the entire transaction.
2. Every game-memory read remains guarded. SEH-safe reads, pointer/range checks,
   and executable-address checks are safety boundaries, not optional cleanup.
3. Hook installation and shutdown remain transactional. A partial install must
   roll back; shutdown must stop new work, wait for active callbacks, restore
   patches, and only then release hook objects and state.
4. Natural trait injection remains on the owning game thread. Its thread-local
   contribution frames, generation checks, character identity checks, context
   checks, and result accounting must move together.
5. The mod never writes save data and never changes `GemData.WORN_BY`.
6. Physical inventory identity is the native slot id. A physical sigil can have
   at most one virtual owner, and native body-slot ownership always wins.
7. Native body slots remain 1-12. Configurable virtual slots remain 1-24 with a
   default of 8; shrinking the configured count clears hidden reservations.
8. Input capture suppresses keyboard and mouse only. DirectInput devices are
   classified when `CreateDevice` receives `GUID_SysKeyboard` or
   `GUID_SysMouse`; XInput, DirectInput gamepad input, Raw HID, and unknown
   future raw-input device types pass through.
9. C ABI functions remain failure-safe for null pointers, short buffers,
   shutdown races, and unsupported executable versions.
10. Dynamic unloading is unsupported (`Mod.CanUnload` is false). Managed
    teardown calls `GBFR20_Shutdown` before process exit; `DllMain` remains
    loader-lock safe and does not attempt complex hook teardown.
11. Required byte/RVA preflight and every game hook install remain synchronous.
    Full-file EXE SHA-256 is a managed, post-hook diagnostic only and cannot
    participate in the compatibility decision or patch rollback.
12. Reloaded-II launch-source diagnostics classify only the official
    `Reloaded.Mod.Loader.Bootstrapper.dll` or deployed `.asi` module with its
    `InitializeASI` export; ambiguous or similarly named modules remain unknown.

## Implemented native source layout

```text
GBFR.ExtraSigilSlots.Native/
  native_api.h
  native_internal.h
  src/
    config_store.cpp
    dllmain.cpp
    exports.cpp
    input_capture.cpp
    inventory_store.cpp
    name_tables.cpp
    runtime.cpp
    runtime_state.cpp
    safe_game_access.cpp
    selection_store.cpp
    trait_hooks.cpp
  third_party/
```

Every listed `.cpp` is a real translation unit. `native_api.h` is the frozen
public C ABI; `native_internal.h` is the private cross-module contract. No
temporary `.inl` fragments or unity-build includes are used.

## State ownership

| Module | Owns | Must not own |
| --- | --- | --- |
| `runtime_state.cpp` / `runtime.cpp` | initialization state, shutdown state, module paths, messages, tick coordination | detour bodies |
| `safe_game_access.cpp` | guarded reads, pointer/range validation, status identity and authorization validation | UI rendering |
| `input_capture.cpp` | USER32/DInput8 IAT patches, event-driven DirectInput device classification, keyboard/mouse method-target inline gates without COM-vptr replacement, capture barrier, cursor freeze state | overlay policy |
| `config_store.cpp` | INI normalization, settings and persisted character selections | game-memory reads |
| `name_tables.cpp` | localized name tables and immutable compatibility mapping | inventory mutation |
| `selection_store.cpp` | per-character selections, reverse ownership and apply-generation transactions | hook installation |
| `inventory_store.cpp` | physical-slot index, validated inventory snapshot and revision | persistent presets |
| `trait_hooks.cpp` | TLS contribution frame, detour callbacks, preflight, install, rollback and quiescent shutdown | managed state |
| `exports.cpp` / `dllmain.cpp` | packed C ABI wrappers and loader-lock-safe module entry | business policy |

Repeated safe-read wrappers, preflight tables, and patch rollback records should
be centralized. Identity checks, active-call counters, atomics, mutexes, SEH
guards, and rollback logic must not be removed merely to reduce line count.

## LocalContext1 quarantine

`kLocalContext1BindCallRva`, `kLocalContext1BindReturnRva`, their preflight
patterns, callbacks, hook handles, map, and TLS state were all introduced in the
initial repository commit. `InstallHooks` has never installed either hook, so
the callback is unreachable and the binding map cannot be populated.

The subsystem must remain explicitly quarantined during mechanical extraction:

- do not activate it just because constants and callbacks exist;
- do not use its empty map as evidence that context-1 ownership is valid;
- do not delete it until the target instructions and register contract have
  been verified against the supported executable;
- after verification, either install it with full preflight and rollback or
  remove the entire dormant path in one dedicated, reviewable commit.

## Completed refactor

The single 3,851-line `main.cpp` was removed and replaced by the translation
units above. The managed frontend now separates lifecycle from Win32/IME input,
separates preset management from inventory conflict dialogs, and separates the
NativeCore facade from its P/Invoke declarations. The unused
`NaturalTraitBindFrame`/`BeginNaturalTraitBind` chain was removed; its active
natural-contribution path was retained unchanged. LocalContext1 remains
quarantined as described above.

All future changes must:

- compile Debug and Release x64 where practical;
- keep the existing default release package layout;
- pass the repository smoke harnesses;
- contain no unrelated behavior change.

## Build and verification

From the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File .\build-release.ps1
powershell -ExecutionPolicy Bypass -File .\tests\run-smoke-tests.ps1
```

The Visual Studio solution is an editor/build convenience. The native
`.vcxproj`, managed `.csproj`, and PowerShell scripts remain the source of truth;
GNU Make and a second independently maintained build graph are intentionally not
introduced. CMake may be reconsidered only after the native source split is
complete and its project files can be generated rather than duplicated.
