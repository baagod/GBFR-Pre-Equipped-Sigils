# Nexus Mods 发布手册（GBFR Reloaded Sigil Slots）

> 面向对象：发布本 mod 到 Nexus Mods 的操作者 / AI。
> 状态：**未发布**。发布前必须完成"前置许可"步骤（见第 1 节）。

---

## 1. 前置：许可问题（法律门槛，必须先做）

本 mod 派生自 [GBFR Extra Sigil Slots](https://www.nexusmods.com/granbluefantasyrelink/mods/657)
（Nexus）/ [cajoxorize366-oss/GBFR-Extra-Sigil-Slots](https://github.com/cajoxorize366-oss/GBFR-Extra-Sigil-Slots)（GitHub，作者 Hiyajomaho-num9）。
**原仓库无 LICENSE 文件（默认保留所有权利）**，因此发布修改版前必须获得原作者许可。

### 1.1 联系作者（二选一）

- **GitHub Issue**：https://github.com/cajoxorize366-oss/GBFR-Extra-Sigil-Slots/issues 新建 issue；
- **Nexus 私信**：https://www.nexusmods.com/granbluefantasyrelink/mods/657 页面 Message 按钮。

### 1.2 私信/Issue 模板（中英对照，任选其一）

**中文：**

> 你好！我基于你的 GBFR Extra Sigil Slots（0.8.6）做了一个精简版派生 mod（GBFR Reloaded Sigil Slots）：删除了 F8 选择器 UI、Overlay Broker、输入捕获和预设机制，改为内置模板配装表自动注入（当前娜露梅 5 槽），不依赖库存、不写存档。代码已从 ~9000 行精简到 ~3900 行，仅保留核心 Hook 与语义布局解析，并已在本机 2.0.5 验证正常。
>
> 想请问：是否可以允许我将其发布到 Nexus Mods？发布时我会明确注明派生来源和你的署名（Credit）。你的仓库文档里写明欢迎后续维护者接手，希望得到你的许可，谢谢！

**English：**

> Hello! I've built a slimmed-down derivative of your GBFR Extra Sigil Slots (0.8.6): removed the F8 selector UI, Overlay Broker, input capture and preset machinery, replaced them with a built-in template loadout engine (currently Narmaya 5 slots, auto-injected at runtime, no inventory dependency, no save writes). Code trimmed from ~9000 to ~3900 lines, keeping only the core hooks and the semantic layout resolver; verified working on ER 2.0.5 locally.
>
> May I publish it on Nexus Mods? I will clearly credit you and the original mod as the source. Your handoff docs welcome future maintainers, so I hope this is okay. Thanks!

**拿到许可后**：保存对话截图/链接备查（Nexus 页面底部 Credit 区需要写明）。

### 1.3 若作者未回复/拒绝

- 未回复：等待 2–4 周后可考虑发布，但页面必须注明"作者未明确授权，如有异议请联系我下架"（有风险，建议继续等）；
- 拒绝：不得发布，仅自用。

---

## 2. 发布信息表

| 字段 | 值 |
|---|---|
| Mod Name | `GBFR Reloaded Sigil Slots (Auto Loadout)` |
| Summary（摘要） | `Auto-applies a fixed template loadout (Narmaya 5 sigil slots) via runtime-injected synthesized sigils. No inventory, no save edits, no UI.` |
| Category | `Gameplay Effects` 或 `Cheats`（如实标注为增强/作弊类） |
| Tags | `Reloaded-II` `Sigils` `Auto Loadout` `QoL` `Gameplay` `Cheat` |
| Version | 0.2.0 |
| Game Version | Granblue Fantasy: Relink (ER 2.0.5) |
| 依赖 | [Reloaded-II](https://github.com/Reloaded-II/Reloaded-II) 1.30.3+（Mod Dependencies 里写明） |
| 主文件 | `dist/GBFR-ReloadedSigilSlots-0.2.0.zip`（zip 内含 `GBFR.ReloadedSigilSlots` 文件夹） |
| 截图 | 至少 2–4 张：① 血条下 5 槽 buff 图标 ② 训练场/战斗截图 ③ 娜露梅状态面板 ④ 日志文件截图（验证行） |

---

## 3. 描述模板（Nexus Description，Markdown）

```markdown
# GBFR Reloaded Sigil Slots (Auto Loadout)

Auto-applies a fixed template loadout to your characters through **runtime-synthesized sigils** — no inventory items, no save editing, no UI, no F8.

[截图 1][截图 2]

## What it does

- Injects **synthesized sigils** directly into the trait calculation of the local status system (same mechanism the game uses for its 12 body slots, extended by 5 virtual slots).
- **No inventory requirement**: you don't need to own, farm or craft any sigil for this to work.
- **No save-data writes**, no `GemData.WORN_BY` changes, nothing permanent.
- Battle application is verified by the native contribution tracker (see the log: `Live battle Trait contribution confirmed ...`).

## Current loadout (v0.2) — Narmaya

| Slot | Sigil | Traits (Lv15) |
|---|---|---|
| 1 | 斩姬之觉醒＋ | 斩姬梦幻 + 斩姬武艺 |
| 2 | 激昂Ⅴ＋ | 激昂 + 斩姬的战气 |
| 3 | 豪胆Ⅴ＋ | 豪胆 + 自动复活 |
| 4 | 不动Ⅴ＋ | 不动 + 躲避性能 |
| 5 | 坚持Ⅴ＋ | 坚持 + 药水携带数 |

> Other characters planned; the loadout table is compiled-in for now.

## Install

1. Install [Reloaded-II](https://github.com/Reloaded-II/Reloaded-II) (1.30.3+).
2. Extract the zip into Reloaded-II's `Mods` folder (the archive contains the `GBFR.ReloadedSigilSlots` folder).
3. Enable the mod and launch the game.

## Verify

- Log file: `ReloadedSigilSlots.Reloaded.log` in the mod folder should show `Installed 5 built-in template loadout selection(s)` and `Live battle Trait contribution confirmed for 0xE7053919: 5/5 ...`.
- Buff icons for the injected traits appear under the HP bar.

## Compatibility & notes

- ER 2.0.5 only (fail-closed on unsupported versions — hooks are not installed, saves untouched).
- **Online play: the extra traits are real local combat effects (cheat-level). Use at your own risk; other players won't see the slots, but the stats apply.**
- Does not conflict with body-slot loadouts; does not touch your save or inventory.

## Credits

- **Derived from [GBFR Extra Sigil Slots](https://www.nexusmods.com/granbluefantasyrelink/mods/657) by Hiyajomaho-num9** — original hook architecture, layout resolver and compatibility tables. Released with permission.
- [Source](https://github.com/cajoxorize366-oss/GBFR-Extra-Sigil-Slots)

## Permissions

- 按第 4 节填写后，此处会自动显示 Nexus 的权限徽章。
```

---

## 4. 权限声明（Declaration）建议

发布表单的权限部分按以下推荐填写（可在页面 Permissions 页随时修改）：

| 权限项 | 推荐值 | 理由 |
|---|---|---|
| 允许他人上传本 mod 到其他网站 | **是（要求注明来源）** | 开放、与原作风格一致 |
| 允许他人修改并上传修改版 | **是（要求注明来源）** | 延续原作"欢迎接手者"的态度；你本人也是派生者 |
| 允许他人在自己的 mod 中使用本 mod 的资产（代码/数据） | **是（要求注明来源）** | 同上 |
| 要求 Credit | **是** | 强制保留派生来源链 |
| 存档（Archiving） | 允许 | 社区惯例 |
| 捐赠积分（Donation Points） | 按个人意愿（可不开） | — |

> 说明：Nexus 发布即代表你声明"你拥有发布该内容的权利"。**必须先完成第 1 节许可步骤再上传。**

---

## 5. 发布操作清单

1. 完成第 1 节许可并获得回复；
2. 准备截图（游戏内 + 日志）；
3. 登录 Nexus → Upload → 填写第 2 节信息表 + 第 3 节描述 + 第 4 节权限；
4. 上传 `GBFR-ReloadedSigilSlots-0.2.0.zip`；
5. 发布后检查：页面渲染正常、Credit 区显示原版链接、权限徽章正确；
6. 在 README 与 ModConfig 的 ProjectUrl 补上 Nexus 页面地址。

## 6. 后续更新流程（交给 AI）

- 改配装/代码 → `build-release.ps1` → 更新 `ModVersion`（如 0.2.1）→ Nexus 文件页添加新版本 zip，更新版本号与 changelog；
- 描述里有配装表，**每次改配装记得同步描述**；
- 游戏更新后先回归（MAINTENANCE.md 第 6 节），不兼容时在页面顶部加警告，不要静默失效。
