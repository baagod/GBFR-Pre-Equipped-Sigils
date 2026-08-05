# GBFR Extra Sigil Slots

Reloaded-II configurable extra-sigil-slot mod for Granblue Fantasy: Relink ER 2.0.2 and 2.0.3.

The repository contains a C++ native hook and a C# Reloaded-II loader, but they are packaged as one Reloaded-II mod.

Reloaded-II 1.30.3 or newer is recommended. The native DLL is loaded by the managed Reloaded-II mod and is not a standalone ASI plugin. To launch through Steam with ASI injection, use Reloaded-II's `Edit Application -> Advanced Tools & Options -> Deploy ASI Loader`, then launch the normal game executable. Re-deploy the ASI Loader after moving or updating Reloaded-II, and do not rename `GBFR.ExtraSigilSlots.Native.dll` to `.asi`.

The compact selector opens with `F8` by default; its hotkey can be changed in Reloaded-II without adding a hotkey editor to the in-game ImGui menu. It supports Simplified Chinese and English (including Chinese IME input), displays the current character by name, and reports the complete valid physical-sigil scan count separately from the filtered picker match count. Version 0.8.2 supports ER 2.0.2 and 2.0.3 through one-shot semantic layout resolution while retaining recoverable Overlay Broker handoff, 1–24 configurable virtual slots, per-character named presets and preset transfer, usage filters, body-slot conflict reporting, and protected virtual-slot sigils. Input release is now coordinated through the native effective-device mask, so closing the overlay while a hotkey or mouse button is still held no longer leaves window messages and polling input in conflicting states.

## Virtual slot count

Enter the desired count in the in-game ImGui menu and save it, then restart the game. The current effective count never changes while the game is running. The default is `8`, the supported range is `1` through `24`, and invalid input is normalized to `1`. Manual `VirtualSlotCount` editing in `GBFR-ExtraSigilSlotsNumConfig.ini` remains supported.

- If the file is missing, the native runtime creates a complete default INI.
- If the complete settings and character-selection data is valid, startup leaves every byte untouched.
- If any required setting or saved slot value is invalid—including `0`, a negative value, non-numeric text, or a value above `24`—the original bytes are first saved beside it as `GBFR-ExtraSigilSlotsNumConfig.ini.invalid-<timestamp>.bak`, then a complete default INI is created with `VirtualSlotCount=8`.
- A UI count change is stored separately until restart. On the next start, the native runtime first makes an exact `.resize-<timestamp>.bak`, atomically rewrites NumConfig, and clears every character's current selection beyond the new limit. Inventory sigils are only detached, never deleted; named presets retain all 24 stored slot definitions.
- Increasing the count creates empty new current slots. It does not silently restore old high-slot assignments; a saved preset may reapply them through the normal ownership and conflict checks.
- The release archive contains neither mutable NumConfig nor its pending count request.

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

The script defaults to `Release`, `x64`, and version `0.8.2`. These defaults can
be overridden explicitly, for example with `-Configuration Debug` or
`-Version 0.8.2-test`.

The installable archive is generated at:

```text
dist\GBFR-Extra-Sigil-Slots-0.8.2.zip
```

Extract the `GBFR.ExtraSigilSlots.Reloaded` folder from the ZIP into Reloaded-II's `Mods` directory. Remove or disable the old `GBFR.ExtraSigilSlots20.Reloaded` mod so Reloaded-II cannot load both identities. Neither `GBFR-ExtraSigilSlotsNumConfig.ini` nor `GBFR-ExtraSigilSlots.presets.json` is included in an archive. Missing NumConfig is created by the native runtime; an existing valid NumConfig is preserved byte-for-byte, while an invalid NumConfig is backed up before a complete default is generated. Named presets are stored in Reloaded-II's persistent mod-config directory and automatically migrate from valid current/legacy JSON files left in an older mod directory. An invalid persistent preset file is preserved as a content-addressed `.invalid-<digest>.bak` before recovery is attempted. Settings are not copied into a missing NumConfig by the managed migrator.

If first launch appears to hang, collect `%APPDATA%\Reloaded-Mod-Loader-II\Logs` and the mod's `ExtraSigilSlots.Reloaded.log`. Startup entries use `phase`, `state`, and `elapsed_ms`; the last `state=begin` line identifies the operation that did not return. One-shot semantic layout resolution, exact local-byte preflight, and hook installation remain synchronous so the game cannot run ahead of the hooks. There is no timer or per-frame signature scan. An ambiguous or incomplete layout fails closed without rewriting saved sigil selections; the independent input transaction remains available so the overlay can report the compatibility error. Only after initialization does a background `executable-sha256` diagnostic read the full EXE; it is marked `diagnostic_only=true` and never enables, rejects, or rolls back hooks. The same log explicitly reports `由 Launcher 注入`, `由 .asi Bootstrapper 加载`, or `source=unknown` after checking the official Reloaded bootstrapper module and its `InitializeASI` export.

## Development

- [Native architecture and refactor plan](docs/native-architecture.md)
- [Smoke-test harnesses](tests/README.md)
