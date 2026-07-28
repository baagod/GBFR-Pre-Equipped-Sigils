# GBFR Extra Sigil Slots

Reloaded-II configurable extra-sigil-slot mod for Granblue Fantasy: Relink ER 2.0.2.

The repository contains a C++ native hook and a C# Reloaded-II loader, but they are packaged as one Reloaded-II mod.

Reloaded-II 1.30.3 or newer is recommended. The native DLL is loaded by the managed Reloaded-II mod and is not a standalone ASI plugin. To launch through Steam with ASI injection, use Reloaded-II's `Edit Application -> Advanced Tools & Options -> Deploy ASI Loader`, then launch the normal game executable. Re-deploy the ASI Loader after moving or updating Reloaded-II, and do not rename `GBFR.ExtraSigilSlots.Native.dll` to `.asi`.

The compact selector opens with `F8` by default; its hotkey can be changed in Reloaded-II without adding a hotkey editor to the in-game ImGui menu. It supports Simplified Chinese and English (including Chinese IME input), displays the current character by name, and reports the complete valid physical-sigil scan count separately from the filtered picker match count. Version 0.7.6 provides 1–24 configurable virtual slots, named multi-character presets, usage filters, body-slot conflict reporting, and confirmed ownership transfers.

## Virtual slot count

Set `VirtualSlotCount` in `GBFR-ExtraSigilSlotsNumConfig.ini`, then restart the game. The default is `8` and the supported range is `1` through `24`.

- A missing, empty, non-numeric, signed, negative, or otherwise malformed value is rewritten to `8`.
- `0` is rewritten to `1`.
- A value above `24` is rewritten to `24`.
- Reducing the count clears and rewrites selections beyond the new active range so hidden slots cannot keep inventory sigils reserved.

## Build and package

Requirements:

- Windows x64
- Visual Studio 2022 Build Tools with MSVC v143 and a Windows SDK
- .NET 8 SDK
- PowerShell 5.1 or newer

Run from the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File .\build-release.ps1
```

The script defaults to `Release`, `x64`, and version `0.7.6`. These defaults can
be overridden explicitly, for example with `-Configuration Debug` or
`-Version 0.7.6-test`.

The installable archive is generated at:

```text
dist\GBFR-Extra-Sigil-Slots-0.7.6.zip
```

Extract the `GBFR.ExtraSigilSlots.Reloaded` folder from the ZIP into Reloaded-II's `Mods` directory. Remove or disable the old `GBFR.ExtraSigilSlots20.Reloaded` mod so Reloaded-II cannot load both identities. On first start, the new mod copies settings, character selections, and named presets from the old sibling directory when the new destination still contains only packaged defaults. Legacy files are preserved as backups. The package includes `GBFR-ExtraSigilSlotsNumConfig.ini`; the runtime-created `GBFR-ExtraSigilSlots.presets.json` remains excluded so updates cannot overwrite named presets.

If first launch appears to hang, collect `%APPDATA%\Reloaded-Mod-Loader-II\Logs` and the mod's `ExtraSigilSlots.Reloaded.log`. Startup entries use `phase`, `state`, and `elapsed_ms`; the last `state=begin` line identifies the operation that did not return. Required byte/RVA preflight and hook installation remain synchronous so the game cannot run ahead of the hooks. Only after they return does a background `executable-sha256` diagnostic read the full EXE; it is marked `diagnostic_only=true` and never enables, rejects, or rolls back hooks. The same log explicitly reports `由 Launcher 注入`, `由 .asi Bootstrapper 加载`, or `source=unknown` after checking the official Reloaded bootstrapper module and its `InitializeASI` export.

## Development

- [Native architecture and refactor plan](docs/native-architecture.md)
- [Smoke-test harnesses](tests/README.md)
