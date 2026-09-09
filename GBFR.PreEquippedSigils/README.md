# GBFR Pre-Equipped Sigils

为全角色预配扩展因子，不占用本体 12 槽位，无需库存、不写存档。
**[Mod](https://github.com/baagod/GBFR-Pre-Equipped-Sigils) 的本质是把这些“必带槽”从玩家的预算里抽走，本体 12 槽位留给玩家自由发挥，每个角色都能多出几成配装自由。**

> **AI 辅助开发声明**：本 Mod 代码由 AI 助手在人类指导下编写；需求设计、配装内容、游戏内验证与文档由人类主导。
## 配装

**其他玩家看不到扩展因子，在线游玩时风险自负。**

本 Mod 为每角色预配 **3 个独立专属因子槽**（随附工具可逐项开关）：

1. 专属因子 T1（该角色第一专属词条。如娜露梅：斩姬梦幻）
2. 专属因子 T2（该角色第二专属词条。如娜露梅：斩姬武艺）
3. 战气因子（激昂 + 角色战气，角色专属）

本体 12 槽位留给玩家自由发挥：通用槽由随附工具编辑（无内置默认）。

## 安装

1. 安装 [Reloaded-II](https://github.com/Reloaded-Project/Reloaded-II)，2. 把 zip 解压到 Reloaded-II 的 Mods 目录，3. 启用 Mod 后启动游戏。
## 致谢（Credit）
- 派生自 [GBFR Extra Sigil Slots](https://www.nexusmods.com/granbluefantasyrelink/mods/657)（作者：Hiyajomaho-num9），**经作者许可发布**。
- 数据核实参考社区工具链：[Nenkai/relink-modding](https://nenkai.github.io/relink-modding/)（官方 ID 表）与 [GBFRDataTools](https://github.com/Nenkai/GBFRDataTools)（解包/导出）。
---

## Build (for review)

Source: https://github.com/baagod/GBFR-Pre-Equipped-Sigils

Requirements: Windows x64, Visual Studio 2022 Build Tools (MSVC v143 + Windows SDK), .NET 8 SDK, Go, Node.js.

```powershell
powershell -ExecutionPolicy Bypass -File .\build-release.ps1
# outputs dist\GBFR-Pre-Equipped-Sigils-<version>.zip
```

The release package contains:
- `GBFR.PreEquippedSigils.dll` — C# (Reloaded-II mod hook, built by build-release.ps1),
- `GBFR.PreEquippedSigils.Native.dll` — C++ (game hook, same script),
- `Loadout.exe` — Wails v3 (Go) GUI tool (its frontend is also built by the script; no external assets are downloaded at build time).
