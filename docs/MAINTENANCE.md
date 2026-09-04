# GBFR Pre-Equipped Sigils — AI 接手维护手册

> 面向对象：后续接管本项目的 AI agent / 开发者。
> 阅读前提：先读 `README.md`（用户向说明）。本手册是**技术维护**文档。
> 项目位置：本仓库根目录。源码：https://github.com/baagod/GBFR-Pre-Equipped-Sigils
> 游戏版本：Granblue Fantasy: Relink Endless Ragnarok **2.0.5**。
> 当前版本：0.3.6（T1 配置化已本地构建，Nexus 尚未发布；0.3.5 已发布 Nexus）。派生自 GBFR Extra Sigil Slots（Hiyajomaho-num9），已大幅精简。

---

## 1. 一句话说明

游戏原生只计算 12 个可见因子槽（内部 trait 循环上限 13）。本 mod 把循环上限扩到
`13 + kTemplateSlotCount`，并 Hook 因子读取函数：当游戏询问第 13 号起的虚拟槽时，
按**内置模板表**现场合成一份 GemData 交给游戏。**不写存档、不依赖库存、不改
`GemData.WORN_BY`**；战斗数值是真实的本地效果（在线 = 作弊级，风险自负）。

## 2. 目录结构与文件职责

```
build-release.ps1                    构建+打包脚本（MSBuild native → dotnet managed → zip）
docs/
  gbfr-sigil-hashes.zh-CN.tsv        hash 查询表（S=物品/gem_id，T=词条/trait，仅参考不打包）
  gbfr-sigil-hashes.en.tsv           同上（英文）
  MAINTENANCE.md                     本手册
GBFR.PreEquippedSigils/             C# 托管层（Reloaded-II 插件壳）
  Mod.cs                             生命周期、日志（时间戳）、250ms 维持 Tick
  NativeCore.cs                      原生门面：加载/ABI 校验/日志回调/Tick/Shutdown/消息读取
  NativeCore.Interop.cs              P/Invoke 声明（必须与 native_api.h 同步）
  ModConfig.json                     ModId/版本/描述（发布信息）
GBFR.PreEquippedSigils.Native/      C++ 原生核心
  native_api.h                       冻结的 C ABI（v15，6 个导出 + GemData 结构）
  native_internal.h                  内部状态/声明/常量（模板槽常量、预检字节等）
  src/
    dllmain.cpp                      DLL 入口（仅存模块句柄，loader-lock-safe）
    exports.cpp                      6 个 C 导出实现
    runtime.cpp                      初始化顺序编排 + 阶段日志
    runtime_state.cpp                全局原子/Log（带时间戳）/phase 机制/消息缓冲
    layout_resolver.cpp              ★语义布局解析（2.0.5 锚点，最高风险）
    safe_game_access.cpp             ★SEH 安全内存读写、状态重建、授权提交
    trait_hooks.cpp                  ★注入核心：getter detour、natural bind、hot-apply 触发
    selection_store.cpp              角色选择存储、hot-apply 队列（generation 机制）
    name_tables.cpp                  兼容表加载（199 条角色限制映射，缺失即 fail-closed）
    template_loadout.cpp             ★★配装表——日常维护唯一要改的文件
```

`★` = 高风险区，除非明确任务需要，不要动。

## 3. 核心数据流

```
启动:
  Reloaded-II → Mod.cs → NativeCore.Initialize → exports.GBFR20_Initialize
    → runtime.Initialize:
        executable-validation (必须 granblue_fantasy_relink.exe)
        compatibility-table     (compatibility.tsv, 199 条, 失败即停止)
        semantic-layout-resolution (layout_resolver, 失败即停止)
        template-selection-install (InstallDefaultTemplateSelections
                                      ← 把 0xFE000000+i 合成槽 id 写入角色选择)
        native-hook-install    (4 个 hook + 2 处循环上限 patch)

运行:
  游戏状态重建 → GetGemDataByIndexDetour(slot 13..12+count)
    → TryLoadVirtualTraitSelection → TryCopySelectedVirtualGem
        → IsTemplateSlotId(0xFE000000+) → TryCopyTemplateGem
            → 从 kDefaultTemplates 取 (gem_id, trait1/2, 等级)
            → 组装 GemData(worn_by=0x887AE0B0 未装备, flags=0) → SafeCopyToOutput
    → natural bind 追踪: injected==expected 且 identity 一致 → CommitAuthorizedStatus
    → 日志 "Live battle Trait contribution confirmed for 0x...: N/N"

维持 (Mod.cs 250ms Tick → GBFR20_Tick):
  UpdateEditSessionState / ValidateAuthorizedStatuses /
  ScheduleSelectedStatusRebind / ProcessPendingHotApply / ConsumeApplyResult
  （hot-apply 产生 "Generation N ... copied N/N" 日志，验证装备界面/训练场路径）
```

## 4. 模板配装表（日常维护核心）

文件：`GBFR.PreEquippedSigils.Native/src/template_loadout.cpp` 的 `kDefaultTemplates[]`。
**v0.3 起覆盖全角色**（v0.3.5 起每角色 8 槽），数据由生成脚本维护，不要手写 hash：

| 工具 | 作用 |
|---|---|
| `docs/tool-extract-exclusives.ps1` | 从 compatibility.tsv + 名字表提取每角色专属因子（觉醒＋ gem、两个专属词条、战气词条） |
| `docs/tool-gen-loadout.ps1` | 内嵌每角色专属数据 → 生成 `kDefaultTemplates[]` 数组文本 |
| [Nenkai/relink-modding](https://nenkai.github.io/relink-modding/) + [GBFRDataTools](https://github.com/Nenkai/GBFRDataTools) | 开发期数据核实（官方 ID 表 / 解包导出）——**运行时不依赖**，仅开发工具 |

**改配装的标准流程**：改 `tool-gen-loadout.ps1` 里的数据表（或改通用槽定义）→ 运行脚本输出到临时文件 → 替换 `template_loadout.cpp` 中 `constexpr CharacterTemplate kDefaultTemplates[] = { ... };` 段（自动定位起止替换）。

结构（每槽一个 `TemplateGemSlot`）：

```cpp
TemplateGemSlot{
   0x335DA2A5, // gem_id: 物品 hash（S 行）。游戏按它查 master 表拿显示名；词条效果吃的是下面两个 hash（已实验验证）
   0xE69A4694, // trait1: 主词条 hash（T 行）
   15,         // trait1_level: Ⅴ＋ = 15（漆黑钳蟹 = 20）
   0x95F3FA86, // trait2: 副词条 hash。**无副词条必须用 0x887AE0B0（"不选择"哨兵），不能用 0**！
   15,         // trait2_level
   15,         // sigil_level: 物品显示等级（漆黑钳蟹 = 20；装备后事件因子显示 "-"、全列表显示 20）
},
```

> ⚠️ **踩过的坑（2026-09-02，ER 2.0.5）**："单词条因子"（如漆黑的钳蟹因子 Lv20）把
> `trait2` 写成 `0` 会在游戏"全部因子列表"里**多渲染一个空的 Lv1 条目**。
> 正确写法是 `trait2 = 0x887AE0B0`（游戏本体的"不选择"哨兵值，取自从游戏内修改器
> 观察到的映射；本体事件因子装备后等级显示 "-"，全列表里是 20 —— 与注入的
> `trait1_level=20 / sigil_level=20` 一致，两者独立，都不是问题）。
> 该坑只在**首个单词条因子**（槽 8）出现，其他槽位均有真实 trait2，不受影响。

**规则**：
- 每角色一个 `CharacterTemplate{ character_hash, slots[24] }`；`slots` 从 0 起**连续**，遇 `gem_id==0` 视为表结束（`InstallDefaultTemplateSelections` 与 `FindTemplateSlot` 依赖此约定）。
- 合成槽 id = `kTemplateSlotIdBase(0xFE000000) + 槽序号`，不会与真实库存槽位冲突；`IsTemplateSlotId` 判定。
- **加槽位必须同步**：`native_internal.h` 的 `kTemplateSlotCount` 常量 = 槽数（循环上限 = 13 + 该值，当前 8），由 `tool-gen-loadout.ps1` 生成时同步输出。
- 角色专属物品（觉醒＋/战气）受 `compatibility.tsv` 限制：`TryCopyTemplateGem` 会用
  `GetRequiredCharacterHash(gem_id)` 校验，专属因子只能装给对应角色（古兰/姬塔互通，姬塔条目使用古兰专属）。
- 词条 hash 查询：`docs/gbfr-sigil-hashes.zh-CN.tsv`（S=物品、T=词条；Ctrl+F 搜名字）。
- 角色 hash（角色名 → hash）：见 `UiLocalization.cs` 的历史版本或 compatibility.tsv 的
  character_key 列；常用：古兰 `2A26B1B2`、姬塔 `A4ACBA76`、娜露梅 `E7053919`、
  芙劳 `646C3168`、菲迪埃尔 `74DD4C79`。

## 5. 构建与部署

环境要求：Windows x64、VS2022 Build Tools（MSVC v143 + Windows SDK）、.NET 8 SDK。

```powershell
powershell -ExecutionPolicy Bypass -File .\build-release.ps1   # 默认 Release/x64/0.3.6
# 产物: dist\GBFR-Pre-Equipped-Sigils-<version>.zip
```

- 部署：**游戏必须退出**，把 `dist\GBFR.PreEquippedSigils` 整个文件夹复制到
  Reloaded-II 的 `Mods\`（覆盖/先删旧目录）。
- 版本号：同步改 `ModConfig.json` 的 `ModVersion` 与 `build-release.ps1` 默认 `$Version`。

## 6. 验证清单（每次改动后必须）

1. 编译：**0 警告 0 错误**（third_party 的 C4834 已在 vcxproj 单独压制）。
2. 日志 `GBFR.PreEquippedSigils.Reloaded.log`（mod 目录）：
   - `Installed N built-in template loadout selection(s); inventory-independent.`
   - `Native hook installation completed with N virtual slots; ...`
   - 进战斗：`Live battle Trait contribution confirmed for 0xE7053919: N/N ...`
   - 装备界面/训练场：`Generation M for 0xE7053919: equipment/test rebuild copied N/N ...`
3. 训练场实测词条效果（如豪胆濒死不死、自动复活自起）+ 血条下 buff 图标。
4. 重启游戏配置保留。

## 7. 雷区（fail-closed 与安全边界，禁止削弱）

- `layout_resolver.cpp`：唯一语义锚点、call/RIP 推导、精确字节预检。解析不完整/多重匹配/
  校验不过 → **整套 gameplay hook 不安装**（fail-closed），不降级"找个像的就 Hook"。
- `trait_hooks.cpp`：detour 的 TLS/generation/identity/context/expected/injected 校验顺序；
  natural bind 的授权提交（`CommitAuthorizedStatus`）与 `ValidateAuthorizedStatuses`。
- `safe_game_access.cpp`：所有游戏内存读取必须走 SEH 安全包装与地址范围检查。
- `compatibility.tsv` 缺失或条目数 != 199 → 启动失败（fail-closed）。
- ABI：`native_api.h`（导出签名、packing、`GBFR20_ABI_VERSION=15`）与
  `NativeCore.Interop.cs`、`NativeCore.cs` 的 `AbiVersion` 必须一致；改动需三方同步 + 版本号递增。
- **无配置文件**：INI 体系已删除；槽数 = `kTemplateSlotCount` 常量（`native_internal.h`，当前 8）。
- 第三方 `third_party/`（safetyhook、Zydis）只可升级替换，不可手改。
- 保持上游 3 空格缩进风格（native），托管层 4 空格。

## 8. 保留但易被误判为"死代码"的机制

| 机制 | 位置 | 作用 | 删除后果 |
|---|---|---|---|
| hot-apply（RequestHotApply / ProcessPendingHotApply / ScheduleSelectedStatusRebind） | selection_store / trait_hooks / exports.Tick | 主动重建角色状态，产生 Generation 确认日志，装备界面即时生效 | 失去验证日志；部分场景生效延迟到下次自然重建。**不建议删** |
| EditSession 状态（UpdateEditSessionState / SafeReadUiModes） | safe_game_access | hot-apply 的 context1 分支判据 | 与 hot-apply 绑定 |

## 9. 已知限制与未来方向

- 配装表编译期内置；**配置化已立项**：见 [docs/PLAN-loadout-config.md](PLAN-loadout-config.md)
  （loadout.json + WPF-UI 工具 + F8 热键 + ABI v16，已批准待实施；本节不再维护旧方案）。
- 当前已覆盖全角色；扩展新角色 = 生成器数据表加条目 + 查该角色觉醒＋/战气 hash。
- 游戏更新后需回归：`layout_resolver` 锚点可能失效 → 日志出现 layout failed → 等上游
  方案或重新逆向。

## 10. 常用操作速查（给接手 AI 的指令模板）

- **改某角色某槽的词条**：编辑 `template_loadout.cpp` 对应 `TemplateGemSlot` 的
  `trait1/trait2` hash 与等级（hash 查 `docs/gbfr-sigil-hashes.zh-CN.tsv`）→ 编译 → 部署 → 验证。
- **加槽位**：`tool-gen-loadout.ps1` 通用槽定义追加 `TemplateGemSlot` 数据 → 重新生成
  （数组 + `kTemplateSlotCount` 常量同步更新）→ 编译 → 部署 → 验证。
- **加角色**：查该角色觉醒＋/战气的 S/T hash（compatibility.tsv + 名字表）→ 模板表加
  `CharacterTemplate` 条目 → 编译 → 部署 → 验证。
- **升版本**：走 §11 发布流程（含版本号同步、全文档旧版本号残留扫描、Nexus 描述同步）。
- **提交**：`git -c user.name="baagod" -c user.email="780810441@qq.com" commit ...`
  （不要改全局 git config）。提交前 `git status` 确认无 bin/obj/dist 混入。
- **推送**：`git -c credential.helper="!gh auth git-credential" push origin main`
  （仓库已配置本地代理 127.0.0.1:7890；若提示 403，检查 gh token 的 Contents: Read and write 权限）。

## 11. 发布与 Nexus 后续维护

**已发布**（v0.3.5，2026-09-03）：https://www.nexusmods.com/granbluefantasyrelink/mods/823

发布信息（发布/更新时以本表与 mod README 为准）：

| 项 | 值 |
|---|---|
| 名称 | GBFR Pre-Equipped Sigils |
| 分类 | Miscellaneous |
| 标签 | AI-Generated Content / Cheating / Gameplay |
| 主文件 | `dist/GBFR-Pre-Equipped-Sigils-<version>.zip` |
| 源码 | https://github.com/baagod/GBFR-Pre-Equipped-Sigils |

> **AI 标签口径（2026-08-01 Nexus 新政）**：AI 标签分三档——`AI-Generated Content`（含
> AI 生成的**代码**、UI、语音、对话、翻译、音乐、游戏内资产）/ `AI Media`（AI 推广图、
> 缩略图、视频、页面描述等 mod 外媒体）/ `AI-Assisted`（轻微使用）。规则：**主要靠 AI
> 制作的 mod 必须打 AI-Generated Content**；打 AI-Assisted 的，审核方可要求证明开发
> "人类主导"。本 mod 代码由 AI 编写（README 已公开声明）→ **必须保持 AI-Generated
> Content，勿降为 AI-Assisted**（选了可能被要求证明人类主导，风险单向）；截图均为
> 游戏内实拍、无 AI 图 → 无需 AI Media。宁可偏重，不可偏低。

**发布后维护流程**（每次发布新版本依次执行）：

1. **升版本**：改 `ModConfig.json` 的 `ModVersion` 与 `build-release.ps1` 默认 `$Version`，
   按 §10 扫描全文档旧版本号残留（README、MAINTENANCE 头部、构建注释）。
2. **构建**：`build-release.ps1` → 产出 `dist/GBFR-Pre-Equipped-Sigils-<version>.zip`。
3. **部署验证**：游戏退出 → 复制 `dist\GBFR.PreEquippedSigils` 到 `Mods\` → 按 §6 验证清单实测。
4. **更新 Nexus 文件页**：上传新 zip；Nexus 只认最新文件版本，旧版自动归档到历史。
   上传时保持名称/分类/标签/权限不变（见上表）。
5. **同步页面描述**：Nexus 描述与 `GBFR.PreEquippedSigils/README.md` 同源——
   改配装后必须两处同步（配装表、摘要、截图位置）。
6. **截图**：一律游戏内真实截图，发布后上传到 Images 区（不要 AI 生成图）。
7. **游戏更新后**：先本机回归（§6）；若 layout 解析失败（日志出现 layout failed），
   在页面顶部加"不兼容版本"警告并停更，不要静默失效。
8. **提交推送**：按 §10 的提交/推送模板执行，把版本号与发布记录同步到仓库。

> 备注：RELEASE-NEXUS.md 已删除，发布直接用本手册。

## 12. 会话交接情报（2026-09-03，供新会话 AI 快速对齐）

### 当前状态
- **版本**：v0.3.6（本地构建：ABI v16 + 配置化 T1，实测通过后待发布 Nexus；Nexus 现行 0.3.5）。槽位 8（觉醒＋/激昂+战气/豪胆+自动复活/不动+明镜止水/刚健+药水/守护+躲避性能/追击+迅捷能力/漆黑的钳蟹因子 Lv20）。
- **唯一性**：GBFR 唯一"零库存预配装 + 运行时合成 + 不碰存档"的 mod；原版（657 Extra Sigil Slots）有库存/UI/跨角色绑定痛点——需差异化："预配装/全角色/零折腾"。

### 已验证（实测通过）
- 主控 + AI 角色都吃注入（明镜止水/守护/HP吸收/追击/迅捷）——卸主槽因子测试确认。

### 市场情报（Nexus 竞品，2026-09-03~04）
**转化率口径：Total views ÷ Unique DLs（数值越小 = 转化越好），如 823 = 1,329÷125 = 10.63；勿与 Unique÷Views 混用。**
- **我（823）**：Unique 125 / End 2 / Total 171 / Views 1,329（发布第 3 天实时抓取）——**转化 10.63**，追平原版 11.78（差 1.15）。
- **823 更新（2026-09-04 05:19 发布，仍为 0.3.5 版：含追击+迅捷能力完整 8 槽配置 + 槽 7/8 交换 + 清理后构建）**：文件下载 u=48 / t=49（发布数小时内）——以老用户更新回流为主，拉新仍受 Views 曝光瓶颈限制；**下次发布应升 0.3.6（本次同名 0.3.5 覆盖，用户侧看不出变化）**。
- 657 原版：Unique 1,489 / End 28 / Total 3,153 / Views 17,534 / 转化 11.78（首个扩展槽、无竞品期）——目标：预设优化转化率追上/超过（已基本达成）。

### 用户反馈
- 韩国玩家（漆黑钳蟹因子）——已实现（槽 8）+ 回复。
- 玩家问"能否与 657 共存"——回复"会冲突（同 hook），这是 657 的 drop-in replacement"。
- impact008（2026-09-04）：请求"迅捷能力/怒涛/激昂顶满"版——**拒绝"顶配/超强"**（保护平衡），接受其真实诉求（怒涛不在模板、词条可选性）→ 归入配置化方向；回复话术 = "平衡 + 可配置"。

### 未来方向（未做）
- **配置化（2026-09-04 立项，待实施）**：实现计划见 **docs/PLAN-loadout-config.md**——
  loadout.json（中文词条名）+ WPF-UI 工具（F8 热键拉起）+ ABI v16 + 运行时模板 + 热更新；
  **不做预设/顶配**（保持平衡）；每角色开关 v2；物品权威组合表后补。
- 0.3.6 如先做模板丰富（狂战士/斯巴达/伤害上限/天星系等通用输出），作为配置化第一套预设集——**别加"属性克制转换/War Elemental+"（极品掉落=作弊争议）**。
- 坚持"合理扩展槽"路线（不做"超强数值/顶配"——687/819 是竞品，不撞车；对玩家请求统一话术拒绝）。
- Reddit 反营销严格——**不要主动在 Reddit 自荐**（社区敌意"作弊者"）。

### 备注
- 竞品数字由 Jina Reader 抓取（可能有轻微误差），仅作参考。
- 所有"槽位/版本/下载"改动后同步：MAINTENANCE 头部、README×2、ModConfig、build-release.ps1。
