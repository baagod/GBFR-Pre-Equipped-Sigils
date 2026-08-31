# GBFR Reloaded Sigil Slots

Auto-loadout virtual sigil slots for Granblue Fantasy: Relink Endless Ragnarok (ER 2.0.2–2.0.5).

A standalone Reloaded-II mod derived from [GBFR Extra Sigil Slots](https://github.com/cajoxorize366-oss/GBFR-Extra-Sigil-Slots)
(Hiyajomaho-num9) with the selector UI, Overlay Broker, input capture, and preset machinery
removed, and an inventory-independent **template loadout engine** added.

## What it does

The game natively computes traits from 12 visible sigil slots (internal loop limit 13).
This mod patches the trait loop limits and injects **synthesized sigils** into the local
status calculation: each configured character automatically receives the built-in template
loadout without occupying body slots and **without requiring any sigil to exist in the
inventory**.

- No save-data writes. No `GemData.WORN_BY` changes.
- Works offline; in online play the extra traits are real local combat effects
  (cheat-level), use at your own risk.
- Battle application is verified by the native trait-contribution tracking
  ("Live battle Trait contribution confirmed …").

## Current template loadout (v0.1)

| Character | Virtual slot | Sigil (gem master) | Traits (level) |
|---|---|---|---|
| Narmaya (娜露梅) | 1 | Guts V+ (`0x335DA2A5`) | Guts 15 (`0xE69A4694`) + Autorevive 15 (`0x95F3FA86`) |

## Install

1. Install [Reloaded-II](https://github.com/Reloaded-II/Reloaded-II) (1.30.3+).
2. Extract the mod folder into Reloaded-II's `Mods` directory.
3. Enable the mod; launch the game (Launcher or Deploy-ASI from Steam).

## Verify

- Log file `ReloadedSigilSlots.Reloaded.log` in the mod directory should show:
  - `Installed 1 built-in template loadout selection(s)`
  - `Live battle Trait contribution confirmed for 0xE7053919: 1/1 virtual sigils reached the context-1 status`
- In training mode: Guts triggers at low HP (survive lethal hit), Autorevive triggers on KO.
- **Buff icons for the injected traits appear under the HP bar**, confirming the game's
  buff/display chain accepts the synthesized sigils. (Observed in the v0.1 prototype test.)

## Status (v0.1 prototype, validated)

The inventory-independent synthesis approach is confirmed working: the game consumes the
traits from the synthesized GemData directly (no gem-master re-lookup of traits), and the
HP-bar buff display recognizes the injected sigils. Next: extend to the full 5-slot loadout
(character awakening+, stout heart + character war spirit, guts + autorevive, steadfast +
improved dodging, perseverance + potion hoarder) with per-character exclusive sigils taken
from the compatibility table.

## Build

Requirements: Windows x64, Visual Studio 2022 Build Tools (MSVC v143 + Windows SDK),
.NET 8 SDK.

```powershell
powershell -ExecutionPolicy Bypass -File .\build-release.ps1
```

Output: `dist\GBFR-ReloadedSigilSlots-<version>.zip`.

## Compatibility

Game versions ER 2.0.2–2.0.5 are resolved by the inherited one-shot semantic layout
resolver; an ambiguous or unsupported layout fails closed without touching saves.

## License note

This project is derived from GBFR Extra Sigil Slots, which is published without a
LICENSE file (all rights reserved). Redistribution of a modified build requires the
original author's permission.
