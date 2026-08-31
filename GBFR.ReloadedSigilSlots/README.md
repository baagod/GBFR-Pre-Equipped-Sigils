# GBFR Reloaded Sigil Slots

Auto-loadout virtual sigil slots for Granblue Fantasy: Relink Endless Ragnarok (2.0.5).

A standalone Reloaded-II mod derived from
[GBFR Extra Sigil Slots](https://github.com/cajoxorize366-oss/GBFR-Extra-Sigil-Slots)
(Hiyajomaho-num9) with the selector UI, Overlay Broker, input capture, preset
machinery and inventory dependence removed, and an inventory-independent
**template loadout engine** added.

## What it does

The game natively computes traits from 12 visible sigil slots (internal loop limit 13).
This mod patches the trait loop limits and injects **synthesized sigils** into the local
status calculation: configured characters automatically receive the built-in template
loadout without occupying body slots and **without requiring any sigil to exist in the
inventory**.

- No save-data writes. No `GemData.WORN_BY` changes.
- Works offline; in online play the extra traits are real local combat effects
  (cheat-level), use at your own risk.
- Battle application is verified by the native trait-contribution tracking
  ("Live battle Trait contribution confirmed …").

## Template loadout (v0.2)

| Character | Slot | Sigil (gem master) | Traits (level 15) |
|---|---|---|---|
| Narmaya (娜露梅) | 1 | 斩姬之觉醒＋ | 斩姬梦幻 + 斩姬武艺 |
| Narmaya | 2 | 激昂Ⅴ＋ | 激昂 + 斩姬的战气 |
| Narmaya | 3 | 豪胆Ⅴ＋ | 豪胆 + 自动复活 |
| Narmaya | 4 | 不动Ⅴ＋ | 不动 + 躲避性能 |
| Narmaya | 5 | 坚持Ⅴ＋ | 坚持 + 药水携带数 |

## Install

1. Install [Reloaded-II](https://github.com/Reloaded-II/Reloaded-II) (1.30.3+).
2. Extract the mod folder into Reloaded-II's `Mods` directory.
3. Enable the mod; launch the game (Launcher or Deploy-ASI from Steam).

## Verify

- Log file `ReloadedSigilSlots.Reloaded.log` in the mod directory should show:
  - `Installed 5 built-in template loadout selection(s)`
  - `Live battle Trait contribution confirmed for 0xE7053919: 5/5 virtual sigils reached the context-1 status`
- In training mode: Guts triggers at low HP (survive lethal hit), Autorevive triggers on KO.
- Buff icons for the injected traits appear under the HP bar.

## Config

`GBFR-ReloadedSigilSlotsConfig.ini` (created on first start, in the mod directory):

```ini
[Settings]
ConfigVersion=2
AutoApply=1
VirtualSlotCount=5
```

Only `VirtualSlotCount` (1–24) is meaningful today. An invalid file is backed up
(`.invalid-*.bak`) and replaced with the default.

## Build

Requirements: Windows x64, Visual Studio 2022 Build Tools (MSVC v143 + Windows SDK),
.NET 8 SDK.

```powershell
powershell -ExecutionPolicy Bypass -File .\build-release.ps1
```

Output: `dist\GBFR-ReloadedSigilSlots-<version>.zip`.

## Compatibility

The one-shot semantic layout resolver targets ER 2.0.5; an ambiguous or unsupported
layout fails closed without touching saves.

## License note

Derived from GBFR Extra Sigil Slots, which is published without a LICENSE file
(all rights reserved). Redistribution of a modified build requires the original
author's permission.
