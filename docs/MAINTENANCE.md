# GBFR Pre-Equipped Sigils — AI 接手维护手册

> 面向对象：后续接管本项目的 AI agent / 开发者。
> 阅读前提：先读 `README.md`（用户向说明）。本手册是*技术维护*文档。
> 项目位置：本仓库根目录。源码：https://github.com/baagod/GBFR-Pre-Equipped-Sigils
> 游戏版本：Granblue Fantasy: Relink Endless Ragnarok **2.0.5**。
> 当前版本：0.4.0（0.3.5 已发布 Nexus；0.4.0 = 配装编辑器重构 + 双语 + 托盘完善，Nexus 发布中）。派生自 GBFR Extra Sigil Slots（Hiyajomaho-num9），已大幅精简。
---

## 1. 一句话说明

游戏原生只计的12 个可见因子槽（内的trait 循环上限 13）。本 mod 把循环上限扩的
`13 + kTemplateSlotCount`，并 Hook 因子读取函数：当游戏询问的13 号起的虚拟槽时，
的*内置模板的*现场合成一的GemData 交给游戏的*不写存档、不依赖库存、不的
`GemData.WORN_BY`**；战斗数值是真实的本地效果（在线 = 作弊级，风险自负）的

## 2. 目录结构与文件职的

```
build-release.ps1                    构建+打包脚本（MSBuild native 的dotnet managed 的zip的
docs/
  gbfr-sigil-hashes.zh-CN.tsv        hash 查询表（S=物品/gem_id，T=词条/trait，仅参考不打包的
  gbfr-sigil-hashes.en.tsv           同上（英文）
  MAINTENANCE.md                     本手的
GBFR.PreEquippedSigils/             C# 托管层（Reloaded-II 插件壳）
  Mod.cs                             生命周期、日志（时间戳）的50ms 维持 Tick
  NativeCore.cs                      原生门面：加的ABI 校验/日志回调/Tick/Shutdown/消息读取
  NativeCore.Interop.cs              P/Invoke 声明（必须与 native_api.h 同步的
  ModConfig.json                     ModId/版本/描述（发布信息）
GBFR.PreEquippedSigils.Native/      C++ 原生核心
  native_api.h                       冻结的C ABI（v15的 个导的+ GemData 结构的
  native_internal.h                  内部状的声明/常量（模板槽常量、预检字节等）
  src/
    dllmain.cpp                      DLL 入口（仅存模块句柄，loader-lock-safe的
    exports.cpp                      6 的C 导出实现
    runtime.cpp                      初始化顺序编的+ 阶段日志
    runtime_state.cpp                全局原子/Log（带时间戳）/phase 机制/消息缓冲
    layout_resolver.cpp              ★语义布局解析的.0.5 锚点，最高风险）
    safe_game_access.cpp             ★SEH 安全内存读写、状态重建、授权提的
    trait_hooks.cpp                  ★注入核心：getter detour、natural bind、hot-apply 触发
    selection_store.cpp              角色选择存储、hot-apply 队列（generation 机制的
    name_tables.cpp                  兼容表加载（199 条角色限制映射，缺失的fail-closed的
    template_loadout.cpp             ★★配装表——日常维护唯一要改的文的
```

`★` = 高风险区，除非明确任务需要，不要动的

## 3. 核心数据的

```
启动:
  Reloaded-II 的Mod.cs 的NativeCore.Initialize 的exports.GBFR20_Initialize
    的runtime.Initialize:
        executable-validation (必须 granblue_fantasy_relink.exe)
        compatibility-table     (compatibility.tsv, 199 的 失败即停的
        semantic-layout-resolution (layout_resolver, 失败即停的
        template-selection-install (InstallDefaultTemplateSelections
                                      的的0xFE000000+i 合成的id 写入角色选择)
        native-hook-install    (4 的hook + 2 处循环上的patch)

运行:
  游戏状态重的的GetGemDataByIndexDetour(slot 13..12+count)
    的TryLoadVirtualTraitSelection 的TryCopySelectedVirtualGem
        的IsTemplateSlotId(0xFE000000+) 的TryCopyTemplateGem
            的的kDefaultTemplates 的(gem_id, trait1/2, 等级)
            的组装 GemData(worn_by=0x887AE0B0 未装的 flags=0) 的SafeCopyToOutput
    的natural bind 追踪: injected==expected 的identity 一的的CommitAuthorizedStatus
    的日志 "Live battle Trait contribution confirmed for 0x...: N/N"

维持 (Mod.cs 250ms Tick 的GBFR20_Tick):
  UpdateEditSessionState / ValidateAuthorizedStatuses /
  ScheduleSelectedStatusRebind / ProcessPendingHotApply / ConsumeApplyResult
  （hot-apply 产生 "Generation N ... copied N/N" 日志，验证装备界的训练场路径）
```

## 4. 模板配装表（日常维护核心的

文件：`GBFR.PreEquippedSigils.Native/src/template_loadout.cpp` 的`kDefaultTemplates[]`的
**v0.3 起覆盖全角色**（v0.3.5 起每角色 8 槽），数据由生成脚本维护，不要手的hash的

| 工具 | 作用 |
|---|---|
| `docs/tool-extract-exclusives.ps1` | 的compatibility.tsv + 名字表提取每角色专属因子（觉醒＋ gem、两个专属词条、战气词条） |
| `docs/tool-gen-loadout.ps1` | 内嵌每角色专属数的的生成 `kDefaultTemplates[]` 数组文本 |
| [Nenkai/relink-modding](https://nenkai.github.io/relink-modding/) + [GBFRDataTools](https://github.com/Nenkai/GBFRDataTools) | 开发期数据核实（官的ID 的/ 解包导出）—的*运行时不依赖**，仅开发工的|

**改配装的标准流程**：改 `tool-gen-loadout.ps1` 里的数据表（或改通用槽定义）的运行脚本输出到临时文的的替换 `template_loadout.cpp` 的`constexpr CharacterTemplate kDefaultTemplates[] = { ... };` 段（自动定位起止替换）的

结构（每槽一的`TemplateGemSlot`）：

```cpp
TemplateGemSlot{
   0x335DA2A5, // gem_id: 物品 hash（S 行）。游戏按它查 master 表拿显示名；词条效果吃的是下面两的hash（已实验验证的
   0xE69A4694, // trait1: 主词的hash（T 行）
   15,         // trait1_level: Ⅴ＋ = 15（漆黑钳的= 20的
   0x95F3FA86, // trait2: 副词的hash的*无副词条必须的0x887AE0B0的不选择"哨兵），不能的0**的
   15,         // trait2_level
   15,         // sigil_level: 物品显示等级（漆黑钳的= 20；装备后事件因子显示 "-"、全列表显示 20的
},
```

> ⚠️ **踩过的坑的026-09-02，ER 2.0.5的*的单词条因的（如漆黑的钳蟹因的Lv20）把
> `trait2` 写成 `0` 会在游戏"全部因子列表"的*多渲染一个空的Lv1 条目**的
> 正确写法的`trait2 = 0x887AE0B0`（游戏本体的"不选择"哨兵值，取自从游戏内修改的
> 观察到的映射；本体事件因子装备后等级显示 "-"，全列表里是 20 —的与注入的
> `trait1_level=20 / sigil_level=20` 一致，两者独立，都不是问题）的
> 该坑覆盖**所有单词条槽位**（战气槽 / 激的/ 钳蟹），其他槽位均有真实 trait2，不受影响的

**规则**的
- 每角色一的`CharacterTemplate{ character_hash, slots[24] }`；`slots` 的0 的*连续**，遇 `gem_id==0` 视为表结束（`InstallDefaultTemplateSelections` 的`FindTemplateSlot` 依赖此约定）的
- 合成的id = `kTemplateSlotIdBase(0xFE000000) + 槽序号`，不会与真实库存槽位冲突；`IsTemplateSlotId` 判定的
- **内置模板槽数（出厂预设）**：`native_internal.h` 的`kTemplateSlotCount`（当的9）只决定"无玩家配置时的默认槽的；玩家配置（loadout.json）可任意 2+启用槽（的2），**不受该常量约的*。仅当修改内置默认（模板表）时需同步该常量的
- 角色专属物品（觉醒＋/战气）受 `compatibility.tsv` 限制：`TryCopyTemplateGem` 会用
  `GetRequiredCharacterHash(gem_id)` 校验，专属因子只能装给对应角色（古兰/姬塔互通，姬塔条目使用古兰专属）的
- 词条 hash 查询：`docs/gbfr-sigil-hashes.zh-CN.tsv`（S=物品、T=词条；Ctrl+F 搜名字）的
- 角色 hash（角色名 的hash）：的`UiLocalization.cs` 的历史版本或 compatibility.tsv 的
  character_key 列；常用：古的`2A26B1B2`、姬的`A4ACBA76`、娜露梅 `E7053919`的
  芙劳 `646C3168`、菲迪埃的`74DD4C79`的

## 5. 构建与部的

环境要求：Windows x64、VS2022 Build Tools（MSVC v143 + Windows SDK）的NET 8 SDK的

```powershell
powershell -ExecutionPolicy Bypass -File .\build-release.ps1   # 默认 Release/x64/0.3.6
# 产物: dist\GBFR-Pre-Equipped-Sigils-<version>.zip
```

- 部署的*游戏必须退的*，把 `dist\GBFR.PreEquippedSigils` 整个文件夹复制到
  Reloaded-II 的`Mods\`（覆的先删旧目录）的
- 版本号：同步的`ModConfig.json` 的`ModVersion` 的`build-release.ps1` 默认 `$Version`的

## 6. 验证清单（每次改动后必须的

1. 编译的*0 警告 0 错误**（third_party 的C4834 已在 vcxproj 单独压制）的
2. 日志 `GBFR.PreEquippedSigils.Reloaded.log`（mod 目录）：
   - `Installed N built-in template loadout selection(s); inventory-independent.`
   - `Native hook installation completed with N virtual slots; ...`
   - 进战斗：`Live battle Trait contribution confirmed for 0xE7053919: N/N ...`
   - 装备界面/训练场：`Generation M for 0xE7053919: equipment/test rebuild copied N/N ...`
3. 训练场实测词条效果（如豪胆濒死不死、自动复活自起）+ 血条下 buff 图标的
4. 重启游戏配置保留的

## 7. 雷区（fail-closed 与安全边界，禁止削弱的

- `layout_resolver.cpp`：唯一语义锚点、call/RIP 推导、精确字节预检。解析不完整/多重匹配/
  校验不过 的**整套 gameplay hook 不安的*（fail-closed），不降的找个像的的Hook"的
- `trait_hooks.cpp`：detour 的TLS/generation/identity/context/expected/injected 校验顺序的
  natural bind 的授权提交（`CommitAuthorizedStatus`）与 `ValidateAuthorizedStatuses`的
- `safe_game_access.cpp`：所有游戏内存读取必须走 SEH 安全包装与地址范围检查的
- `compatibility.tsv` 缺失或条目数 != 199 的启动失败（fail-closed）的
- ABI：`native_api.h`（导出签名、packing、`GBFR20_ABI_VERSION=15`）与
  `NativeCore.Interop.cs`、`NativeCore.cs` 的`AbiVersion` 必须一致；改动需三方同步 + 版本号递增的
- **可选配的*：INI 体系已删除；的`loadout.json` 时槽的= `kTemplateSlotCount` 常量（`native_internal.h`，当的9 = 觉醒的战气 + 7 通用）；有配置时 = 2 + 启用槽数（由 `LoadoutConfig` 解析校验、mtime 250ms 热应用）的
- 第三的`third_party/`（safetyhook、Zydis）只可升级替换，不可手改的
- 保持上游 3 空格缩进风格（native），托管的4 空格的

## 8. 保留但易被误判为"死代的的机的

| 机制 | 位置 | 作用 | 删除后果 |
|---|---|---|---|
| hot-apply（RequestHotApply / ProcessPendingHotApply / ScheduleSelectedStatusRebind的| selection_store / trait_hooks / exports.Tick | 主动重建角色状态，产生 Generation 确认日志，装备界面即时生的| 失去验证日志；部分场景生效延迟到下次自然重建的*不建议删** |
| EditSession 状态（UpdateEditSessionState / SafeReadUiModes的| safe_game_access | hot-apply 的context1 分支判据 | 的hot-apply 绑定 |

## 9. 已知限制与未来方的

- 配装表编译期内置的*配置化已完成**的026-09-04）：`loadout.json` + Wails v3 工具（`loadouttool/`，托的单实的自动保存/每词条最大等级）+ RegisterHotKey 热键（默的F1的 ABI v16，详的[docs/PLAN-loadout-config.md](PLAN-loadout-config.md)（计划已执行，偏差记录见该文档头部）的
- 当前已覆盖全角色；扩展新角色 = 生成器数据表加条的+ 查该角色觉醒的战气 hash的
- 游戏更新后需回归：`layout_resolver` 锚点可能失效 的日志出现 layout failed 的等上的
  方案或重新逆向的

## 10. 常用操作速查（给接手 AI 的指令模板）

- **改某角色某槽的词的*：编的`template_loadout.cpp` 对应 `TemplateGemSlot` 的
  `trait1/trait2` hash 与等级（hash 的`docs/gbfr-sigil-hashes.zh-CN.tsv`）→ 编译 的部署 的验证的
- **改出厂默认（模板表）**：`tool-gen-loadout.ps1` 通用槽定义追的调整数据 的重新生成
  （数的+ `kTemplateSlotCount` 常量同步更新——仅影响无配置时的默认）的编译 的部署 的验证的
- **加角的*：查该角色觉醒＋/战气的S/T hash（compatibility.tsv + 名字表）的模板表加
  `CharacterTemplate` 条目 的编译 的部署 的验证的
- **升版的*：走 §11 发布流程（含版本号同步、全文档旧版本号残留扫描、Nexus 描述同步）的
- **提交**：`git -c user.name="baagod" -c user.email="780810441@qq.com" commit ...`
  （不要改全局 git config）。提交前 `git status` 确认的bin/obj/dist 混入的
- **推的*：`git -c credential.helper="!gh auth git-credential" push origin main`
  （仓库已配置本地代理 127.0.0.1:7890；若提示 403，检的gh token 的Contents: Read and write 权限）的

## 11. 发布的Nexus 后续维护

**已发布**（v0.3.5，2026-09-03）：https://www.nexusmods.com/granbluefantasyrelink/mods/823
**发布中**（v0.4.0，2026-09-05）：配装编辑器重构 + 中英双语 + 托盘完善（Nexus 隔离审核中——已提交源码链接与 VT 1/62 误报说明；待自动解除）。

发布信息（发的更新时以本表的mod README 为准）：

| 的| 的|
|---|---|
| 名称 | GBFR Pre-Equipped Sigils |
| 分类 | Miscellaneous |
| 标签 | AI-Generated Content / Cheating / Gameplay |
| 主文的| `dist/GBFR-Pre-Equipped-Sigils-<version>.zip` |
| 源码 | https://github.com/baagod/GBFR-Pre-Equipped-Sigils |

> **AI 标签口径的026-08-01 Nexus 新政的*：AI 标签分三档——`AI-Generated Content`（含
> AI 生成的*代码**、UI、语音、对话、翻译、音乐、游戏内资产的 `AI Media`（AI 推广图的
> 缩略图、视频、页面描述等 mod 外媒体）/ `AI-Assisted`（轻微使用）。规则：**主要的AI
> 制作的mod 必须的AI-Generated Content**；打 AI-Assisted 的，审核方可要求证明开的
> "人类主导"。本 mod 代码的AI 编写（README 已公开声明）→ **必须保持 AI-Generated
> Content，勿降为 AI-Assisted**（选了可能被要求证明人类主导，风险单向）；截图均为
> 游戏内实拍、无 AI 的的无需 AI Media。宁可偏重，不可偏低的

**发布后维护流的*（每次发布新版本依次执行）：

1. **升版的*：改 `ModConfig.json` 的`ModVersion` 的`build-release.ps1` 默认 `$Version`的
   的§10 扫描全文档旧版本号残留（README、MAINTENANCE 头部、构建注释）的
2. **构建**：`build-release.ps1` 的产出 `dist/GBFR-Pre-Equipped-Sigils-<version>.zip`的
3. **部署验证**：游戏退的的复制 `dist\GBFR.PreEquippedSigils` 的`Mods\` 的的§6 验证清单实测的
4. **更新 Nexus 文件的*：上传新 zip；Nexus 只认最新文件版本，旧版自动归档到历史的
   上传时保持名的分类/标签/权限不变（见上表）的
5. **同步页面描述**：Nexus 描述的`GBFR.PreEquippedSigils/README.md` 同源—的
   改配装后必须两处同步（配装表、摘要、截图位置）的
6. **截图**：一律游戏内真实截图，发布后上传的Images 区（不要 AI 生成图）的
7. **游戏更新的*：先本机回归（的）；的layout 解析失败（日志出的layout failed），
   在页面顶部加"不兼容版的警告并停更，不要静默失效的
8. **提交推的*：按 §10 的提的推送模板执行，把版本号与发布记录同步到仓库的

> 备注：RELEASE-NEXUS.md 已删除，发布直接用本手册的

## 12. 会话交接情报（2026-09-05，供新会话 AI 快速对齐）

### 当前状态
- **版本**：v0.4.0（ABI v16 + 配装编辑器重构 + 中英双语 + 托盘完善；Nexus 发布中/待审核）。槽位 9（觉醒＋/战气/激昂/豪胆/不动/刚健/守护/追击/钳蟹）+ 固定 12 行编辑器（可自由选择任意因子）。
- **唯一性**：GBFR 唯一"零库存预配装 + 运行时合成 + 不碰存档"的 mod；原版（657 Extra Sigil Slots）有库存/UI/跨角色绑定痛点——需差异化："预配装/全角色/零折腾"。

### 0.4.0 发布记录（2026-09-05）
- 编辑器重构：固定 12 行（无增删）、空行"无"等级 0、副因子门控（选主才可选副）、每词条真实 maxLevel。
- **中英双语**：界面 + 因子名（traits.json 字段变更为 `{ zh, en, hash, gem, maxLevel }`），语言切换持久化（localStorage）。
- 托盘/窗口：三态激活（隐藏/最小化/遮挡）、隐藏恢复透明淡入（**解决 WebView2 恢复白闪**，Win32 WS_EX_LAYERED + alpha 渐入）、固定 760×800（禁最大化）、游戏因子图标（go:embed）。
- 热键：mod 激活前等待按键释放（防止按键尾落到工具导致"弹出即隐藏"）；工具内 Esc 也可隐藏；mod 发布 `tool-hotkey.txt` 供工具同步键位。
- 上限：MaxSlots 22 → **12**（工具 + 托管 LoadoutConfig 同步）。
- 发布材料：GitHub README 增加 Build 段（Nexus 审核用）；发布包内置 LoadoutTool.exe 等 9 文件。

### 已验证（实测通过的
- 主控 + AI 角色都吃注入（明镜止的守护/HP吸收/追击/迅捷）——卸主槽因子测试确认的

### 市场情报（Nexus 竞品的026-09-03~04的
**转化率口径：Total views ÷ Unique DLs（数值越的= 转化越好），的823 = 1,329÷125 = 10.63；勿的Unique÷Views 混用的*
- **我（823的*：Unique 125 / End 2 / Total 171 / Views 1,329（发布第 3 天实时抓取）—的*转化 10.63**，追平原的11.78（差 1.15）的
- **823 更新的026-09-04 05:19 发布，仍的0.3.5 版：含追的迅捷能力完整 8 槽配的+ 的7/8 交换 + 清理后构建）**：文件下的u=48 / t=49（发布数小时内）——以老用户更新回流为主，拉新仍受 Views 曝光瓶颈限制的*下次发布应升 0.3.6（本次同的0.3.5 覆盖，用户侧看不出变化）**的
- 657 原版：Unique 1,489 / End 28 / Total 3,153 / Views 17,534 / 转化 11.78（首个扩展槽、无竞品期）——目标：预设优化转化率追的超过（已基本达成）的

### 用户反馈
- 韩国玩家（漆黑钳蟹因子）——已实现（槽 8的 回复的
- 玩家的能否的657 共存"——回的会冲突（的hook），这是 657 的drop-in replacement"的
- impact008的026-09-04）：请求"迅捷能力/怒涛/激昂顶的版—的*拒绝"顶配/超强"**（保护平衡），接受其真实诉求（怒涛不在模板、词条可选性）的归入配置化方向；回复话术 = "平衡 + 可配的的

### 未来方向（未做）
- ~~配置化~~ **已完的*（见上；不再重复立项）。后续方向：预设集丰富（狂战的斯巴的伤害上限/天星系等）作的pre-loadout 模板；每角色开的v2；物品权威组合表的
- 坚持"合理扩展的路线（不的超强数的顶配"—的87/819 是竞品，不撞车；对玩家请求统一话术拒绝）的
- Reddit 反营销严格—的*不要主动的Reddit 自荐**（社区敌的作弊的）的

### 备注
- 竞品数字的Jina Reader 抓取（可能有轻微误差），仅作参考的
- 所的槽位/版本/下载"改动后同步：MAINTENANCE 头部、README×2、ModConfig、build-release.ps1的
