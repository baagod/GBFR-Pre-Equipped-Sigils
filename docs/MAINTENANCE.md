# GBFR Pre-Equipped Sigils — AI 接手维护手册

> 面向对象：后续接管本项目的 AI agent / 开发者。
> 阅读前提：先读 `README.md`（用户向说明）。本手册是*技术维护*文档。
> 项目位置：本仓库根目录。源码：https://github.com/baagod/GBFR-Pre-Equipped-Sigils
> 游戏版本：Granblue Fantasy: Relink Endless Ragnarok **2.0.5**。
> 当前版本：0.5.7（ABI v17；当前状态与机制沿革见 §12）。
---

## 1. 一句话说明

游戏原生只计 12 个可见因子槽（内的 trait 循环上限 13）。本 mod 把循环上限扩到
`13 + 虚拟槽数`（= 3 内置专属 + 玩家通用槽），并 Hook 因子读取函数：当游戏询问 13 号起的虚拟槽时，
把*内置模板*现场合成一份 GemData 交给游戏：不写存档、不依赖库存、不占用库存
（`GemData.WORN_BY` 表示未装备）。战斗数值是真实的本地效果（在线 = 作弊级，风险自负）。

## 2. 目录结构与文件职责

```
build-release.ps1                    构建+打包脚本（MSBuild native、dotnet managed、Wails 工具、zip）
docs/
  MAINTENANCE.md                     本手册
  tool-gen-loadout.ps1               生成 kCharacterExclusives[] 表与 character-exclusives.json（§4）
gen/                                因子数据生成管线（说明书：gen/数据表说明.md，命令链在其 §5）
  filter1.js filter-groups.js filter-drop.js   级联筛选（CSV → xlsx 部件）
  build-*.js add-*.js replace-skills.js rename-headers.js make-xlsx.js
  make-sigils-json.js               最终部件 → sigils.json（运行时因子表）
  style-xlsx.js                     最终 xlsx 行底色 + 标题行样式
  xlsx-lib.js parse-msg.js          公共库 / .msg 文本解析
  extracted/ GBFRDataTools/ sqlite/ 游戏数据与工具（gitignored）
GBFR.PreEquippedSigils/             C# 托管层（Reloaded-II 插件壳）
  Mod.cs                             生命周期、日志（时间戳）、250ms 维持 Tick
  NativeCore.cs                      原生门面：ABI 校验/日志回调/Tick/Shutdown/消息读取
  NativeCore.Interop.cs              P/Invoke 声明（必须与 native_api.h 同步）
  LoadoutConfig.cs                   解析 loadout.json（通用槽 + exclusive 段）→ ABI
  Hotkey.cs                          系统热键：RegisterHotKey 优先、250ms 轮询兜底、热启动工具
  HotkeyConfig.cs                    热键配置页（Reloaded-II 启动器）
  ModConfig.json                     ModId/版本/描述（发布信息）
  sigils.json                     运行时因子表（合并单表；含词条 cap 与专属行 character 字段，见 §4.1）
  character-exclusives.json          每角色专属因子表（生成器产物；工具"专属因子"页数据源）
GBFR.PreEquippedSigils.Native/      C++ 原生核心
  native_api.h                       冻结的 C ABI（v17，8 个导出 + GemData 结构）
  native_internal.h                  内部状态声明/常量（模板槽常量、预检字节等）
  src/
    dllmain.cpp                      DLL 入口（仅存模块句柄，loader-lock-safe）
    exports.cpp                      8 个 C 导出实现
    runtime.cpp                      初始化顺序编排 + 阶段日志
    runtime_state.cpp                全局原子/Log（带时间戳）/phase 机制/消息缓冲
    layout_resolver.cpp              ★语义布局解析（2.0.5 锚点，最高风险）
    safe_game_access.cpp             ★SEH 安全内存读写、状态重建、授权提交
    trait_hooks.cpp                  ★注入核心：getter detour、natural bind、hot-apply 触发
    selection_store.cpp              角色选择存储、hot-apply 队列（generation 机制）
    name_tables.cpp                  兼容表加载（sigils.json 专属行 character 字段，缺失即 fail-closed）
    template_loadout.cpp             ★★专属配装表（表段由生成器产出，勿手改；组装逻辑见 §4）
Loadout/                            Wails v3 配装编辑器（Go 服务 + React 前端，打包进 Mod）
  main.go                            窗口/托盘/单实例/假隐藏与 0x8010 激活命令（§12 机制沿革）
  loadoutservice.go                  数据读写（sigils/exclusives/loadout；原子写 + 结构校验）
  frontend/src/                      编辑器 UI（App/SlotEditor/TraitPicker/ExclusivePanel）
```

`★` = 高风险区，除非明确任务需要，不要动。

## 3. 核心数据流

```
启动:
  Reloaded-II：Mod.cs → NativeCore.Initialize → exports.GBFR20_Initialize
    → runtime.Initialize:
        executable-validation (必须 granblue_fantasy_relink.exe)
        character-restrictions (sigils.json 专属行 character 字段，失败即停)
        semantic-layout-resolution (layout_resolver, 失败即停)
        template-selection-install (InstallDefaultTemplateSelections: 以 0xFE000000+i 合成 id 写入角色选择)
        native-hook-install    (2 个 hook + 2 处循环上限 patch)

运行:
  游戏状态重建：GetGemDataByIndexDetour（slot 13 起共 count 个）
    → TryLoadVirtualTraitSelection → TryCopySelectedVirtualGem
        → IsTemplateSlotId(0xFE000000+) → TryCopyTemplateGem
            → kCharacterExclusives（按 exclusive 状态组装三个专属槽，见 §4）
            → 组装 GemData（worn_by=0x887AE0B0 未装备，flags=0）→ SafeCopyToOutput
    → natural bind 追踪：injected==expected 且 identity 一致 → CommitAuthorizedStatus
    → 日志 "Trait contribution confirmed for 0x...: N/N"（会话内首次状态重建报一次；未满 N/M 每次报 incomplete）

维持（Mod.cs 250ms Tick → GBFR20_Tick）:
  UpdateEditSessionState / ValidateAuthorizedStatuses /
  ScheduleSelectedStatusRebind / ProcessPendingHotApply / ConsumeApplyResult
  （hot-apply 产生 "Generation N ... copied N/N" 日志，验证装备界面/训练场路径）
```

## 4. 模板配装表（日常维护核心）

内置模板 = 每角色专属 3 槽（slot0/T1、slot1/T2、slot2/战气，每槽一个独立专属因子，**无"觉醒＋"合并**）；
专属可经 loadout.json 的 `exclusive` 段逐项开关；通用槽无内置默认（来自玩家配置）。
**数据由生成脚本维护，不要手改 hash。**

| 工具 | 作用 |
|---|---|
| `docs/tool-gen-sigils-required.js`（已删除） | **停用并移除**（2026-09 数据字段与 sigils.xlsx 对齐后失效：旧版抛 "no sort line"；"专属行必为模板 3 gem"的合体版剔除规则实测会把新增普通专属因子误删 3 行，误跑会改坏 sigils.json）。需要重新规范化时请从 sigils.xlsx 重建，勿再寻找该脚本 |
| `docs/tool-gen-loadout.ps1` | 内嵌每角色专属数据（Hash/T1/T2/War），从 sigils.json 推导变体 hash 与 player 码；**直接写回** `template_loadout.cpp` 的 `kCharacterExclusives[]` 段，并生成 `character-exclusives.json`（内容不变则不重写，幂等）|
| [Nenkai/relink-modding](https://nenkai.github.io/relink-modding/) + [GBFRDataTools](https://github.com/Nenkai/GBFRDataTools) | 开发期数据核实（官方 ID 表 / 解包导出），运行时不依赖 |

**改配装的标准流程**：改 `tool-gen-loadout.ps1` 的 `$chars` 数据表 → 运行脚本（自动替换 `template_loadout.cpp`
里 `constexpr CharacterExclusiveLoadout kCharacterExclusives[] = {` 到 `};` 的整段，并把 `character-exclusives.json`
同步更新；两者幂等）→ 编译 → 部署 → 验证（§6）。

行结构（`*_gem` = 物品 hash 由脚本推导，trait = 词条 hash）：

```cpp
{ 0x079DF0CC,             // character_hash
  0x9F08F697, 0x151E4674, // t1Gem, t1
  0xD48ABDDA, 0xA374FDF0, // t2Gem, t2
  0xBC53CE24, 0xD76F4D24, // warGem, war
},
```

每槽由 `MakeSingleTraitSlot` 组装为单词条 `TemplateGemSlot`：

```cpp
TemplateGemSlot{
   gem_id,        // 物品 hash（S 行）：游戏按它查 master 表拿显示名；词条效果吃下面两个 hash
   trait1,        // 主词条 hash（T 行）
   15,            // trait1_level：Ⅴ＋ = 15（漆黑钳蟹 = 20）
   0x887AE0B0,    // trait2：**单词条必须用 0x887AE0B0（"不选择"哨兵），不能用 0**
   0,             // trait2_level
   15,            // sigil_level：物品显示等级（漆黑钳蟹 = 20）
}
```

> ⚠️ **坑（2026-09-02，ER 2.0.5）**：单词条因子把 `trait2` 写成 `0` 会在游戏"全部因子列表"
> 中**多渲染一个空的 Lv1 条目**；正确写法即上面的哨兵值 `0x887AE0B0`。覆盖所有单词条槽位（战气/激昂/钳蟹）。

**规则**：
- 每角色固定槽位：slot0=T1、slot1=T2、slot2=战气；禁用的槽留空（**槽位不连续**——
  `InstallDefaultTemplateSelections` 跳过空槽继续、`TryGetRuntimeSlot` 对空槽返回 false）。
- 运行时由 `BuildCharacterTemplate` 按 exclusive 状态组装为 `CharacterTemplate{ character_hash, slots[24] }`；
  合成 id = `kTemplateSlotIdBase(0xFE000000) + 槽序号`（不与真实库存冲突，`IsTemplateSlotId` 判定）。
- **内置默认（无配置）**：专属 3 槽全开，通用槽全空；总虚拟槽 = 3 + 通用槽数（≤12）。
- 角色专属物品受 `sigils.json` 专属行的 `character` 字段限制：`TryCopyTemplateGem` 用
  `GetRequiredCharacterHash(gem_id)` 校验，只能装给对应角色（古兰/姬塔互通，姬塔条目使用古兰专属）。
- 词条 hash 查询：`sigils.json`（词条 hash/名/上限）或 `gen\extracted\sigils-full.xlsx`（Ctrl+F 搜名字）。
- 角色 hash：`sigils.json` 专属行的 `character` 字段；常用：古兰 `2A26B1B2`、姬塔 `A4ACBA76`、
  娜露梅 `E7053919`、芙劳 `646C3168`、菲迪埃 `74DD4C79`。

## 4.1 数据文件生成（mod 运行时表：sigils.json）

mod 目录下的 `sigils.json`（**合并单表**）**不是手工维护的**，
由 `gen\` 下的管线导出（脚本入库、数据源不入库；步骤见 `gen\数据表说明.md`，构建会校验一致性）。
字段名与 sigils.xlsx 表头一致：
`{ key, hash, name, zh, skill1, sec, category, player, special, cap, lot, character }`：

- 物品行（`hash != skill1`）：`skill1` 主词条 hash、`sec` 固定第二词条 hash（无副 = ""，如永恒钳蟹因子 = D3B8C21F）、
  `cap` 主词条属性、`lot` 池版合法副列表、`name`/`zh` 物品名（原样）；
  `player != ""` 为角色专属因子，`special` 为特殊行（钳蟹系等）。
- 非物品技能行（`hash == skill1`）：不作主、不作副，仅出现在词条字典（角色可持有该技能）。
- 词条字典（副下拉）= 按 `skill1` 去重派生（**取首行**，即"以词条命名的物品行"：zh/name = 词条名；前端另按
  `player == ""` 过滤掉专属词条，专属词条只经"专属因子"页管理）；主下拉 =
  `player == ""`（**含钳蟹系/相扑斗力等特殊行**；非物品技能行不在物品集内，天然不作主；专属因子 `player != ""` 不作主）。

**派生规则**：
- 主因子按 `name`（英文名）**分组**（同名变体一行）；下拉只显示唯一名字；仅专属因子（`player != ""`）不作通用主
  （由"专属因子"页管理）；钳蟹系/相扑斗力等 `special` 行**可作为通用主因子**（该行 `onlyone=1`，不参与组合，见下方“组合规则”）。
  例外：`_74` 专属行的 name/zh 保留官方"＋"后缀，不影响分组（专属行不进主下拉）。
- **组合规则**（2.0.5 实测：游戏**合成结果 = 两输入因子词条的任意组合**；一切组合均允许，“非法”仅为 UI 提示样式，
不禁止选择/保存/实装）。**主副双向判定**：
  1. 无法参与组合：`onlyone`、`hash=skill1`。
  2. `mix=1` 只能组合其 `lot` 因子或固定副，若不匹配则无法组合。
  3. 其余普通因子均能互相组合。
- UI：非法副词条灰显（`opacity-45`）、选中非法时 trigger 红框；**仅提示，不禁止**——选择、自动保存、C# 解析与原生注入
  均不拦截（Go 侧仍做结构/等级范围校验，C# 做最终 cap 兜底）；非法组合的已选副值**不会被清空**。
  方向键（↑/↓）不会从 trigger 打开下拉列表（Base UI 默认行为已在捕获层禁用），留给字段/数字输入导航。
  Esc：焦点在下拉/对话框内时只关闭它们（判断在**捕获阶段** keydown 做——Base UI 在 React 处理键时即卸载弹层，
  冒泡阶段再查会拿到已脱离 DOM 的目标而误判）；其余情况按习惯隐藏窗口。
- 装配 hash：pool 族（lot != []）各名字组只保留池版行 → 保存时按副因子选池版/固定版 hash（副命中池 lot → 池版；
  命中某变体 sec → 该固定版；其余 → 池版，仅样式不阻断）；副词条随配置写入
  （mod 合成形态，与 2.0.5 合成规则一致；无池版组 = plain/专属组原样）。工具界面就地重载预设，不重启进程。

**字段名约定**：`sigils.json` 字段名与 sigils.xlsx 表头一致（key/hash/skill1/lot/…，见 gen\数据表说明.md §2；额外 `sec`/`special`
为工具扩展字段）；loadout.json 协议中物品 ID 仍叫 `gem`、词条 ID 叫 `hash`（mod 读取，不能改）。

**与模板表的关系**：§4 的 `kCharacterExclusives[]` 是**内置专属默认**（直接内嵌 C++，不走 JSON）；
`sigils.json` 只是**玩家配置**（`loadout.json`）解析用的 ID→名称/上限映射，两者独立。

## 5. 构建与部署

环境要求：Windows x64、VS2022 Build Tools（MSVC v143 + Windows SDK）、.NET 8 SDK。

```powershell
powershell -ExecutionPolicy Bypass -File .\build-release.ps1   # 默认 Release/x64/<version>
# 产物: dist\GBFR-Pre-Equipped-Sigils-<version>.zip；脚本结束会自动启动工具（Loadout.exe）
```

- 一键部署（构建后）：`.\deploy.ps1` —— 自动停止运行的 Loadout.exe、覆盖 Mods 目标目录（默认
  `C:\Users\baago\Desktop\Reloaded-II\Mods\GBFR.PreEquippedSigils`，可用 `-Target` 覆盖）、完成后自动重新打开工具；游戏在运行会直接报错。
- 部署：**游戏必须退出**，把 `dist\GBFR.PreEquippedSigils` 整个文件夹复制到 Reloaded-II 的 `Mods\`
  （覆盖前先删旧目录；工具进程运行时文件被占用，先停 Loadout.exe）。
  本机示例：`C:\Users\baago\Desktop\Reloaded-II\Mods\GBFR.PreEquippedSigils`。

### 发布（版本号同步）
1. 同改 `ModConfig.json` 的 `ModVersion` 与 `build-release.ps1` 默认 `$Version`；
2. 全文档旧版本号残留扫描：MAINTENANCE 头部、README×2；发布描述素材从 git log 提炼；
3. 重建（自动产出 zip）→ 部署 → 验证（§6）；Nexus 发布则同步描述。

## 6. 验证清单（每次改动后必须做）

1. 编译：**0 警告 0 错误**（third_party 的 C4834 已在 vcxproj 单独压制）。
2. 日志 `GBFR.PreEquippedSigils.Reloaded.log`（mod 目录）：
   - `Installed N built-in template loadout selection(s). Exclusive slots 1-3 (T1/T2/war), general slots 4-M; inventory-independent.`（当前 29 角色无配置 = **87**；有配置时 N = 29 × (3+通用槽数)，通用槽全角色共享，槽位布局：专属 1-3、通用 4 起）
   - `Native hooks installed: N virtual slots.`
   - 启动/换人/进战斗（context-1 状态重建）：`Trait contribution confirmed for 0xE7053919: N/N ...`（首次；二次出现应为 `incomplete: N/M`）
   - 装备界面/训练场：`Generation M for 0xE7053919: equipment/test rebuild copied N/N ...`
3. 训练场实测词条效果（如豪胆濒死不死、自动复活自起）+ 血条下 buff 图标。
4. 重启游戏配置保留。

## 7. 雷区（fail-closed 与安全边界，禁止削弱）

- `layout_resolver.cpp`：唯一语义锚点、call/RIP 推导、精确字节预检。解析不完整/多重匹配/
  校验不过 则**整套 gameplay hook 不安装**（fail-closed），不降级为"找个像的就 Hook"。
- `trait_hooks.cpp`：detour 的 TLS/generation/identity/context/expected/injected 校验顺序、
  natural bind 的授权提交（`CommitAuthorizedStatus`）与 `ValidateAuthorizedStatuses`。
- `safe_game_access.cpp`：所有游戏内存读取必须走 SEH 安全包装与地址范围检查。
  *SafeInvokeStatusRebuild 已复核（2026-09）：调用前校验 status.character_hash == 目标角色；
  写入仅 context_mode 销 0（单字段对齐原子写 + 同步 + SEH，无撕裂读风险）；勿再引入 8 字节原子写。*
- 角色限制改判据：`sigils.json` 专属行 `character` 字段缺失或条目数 != 87 则启动失败（fail-closed）。
  数据由 sigils.xlsx 管线生成（87 = 28 角色 × 3 专属 gem（古兰/姬塔共享合并）＋ 3 条 `_74` 进阶：涯之七星＋/涯之二王＋/无态＋；
  游戏原版专属物品共 199 条，其余 115 条为模板外的专属物品/觉醒合体版，配装路径不可达，不再校验）。
- ABI：`native_api.h`（导出签名、packing、`GBFR20_ABI_VERSION=17`）与 `NativeCore.Interop.cs`、
  `NativeCore.cs` 的 `AbiVersion` 必须一致；改动需三方同步 + 版本号递增。
- **可选配置**：无 `loadout.json` = 内置专属全开、通用全空；有 = 3 专属（exclusive 段开关，
  键 = PL 码/角色名/角色 hash，内层 = 词条 hash→bool，兼容旧 `{t1,t2,war}`）+ 通用槽
  （`LoadoutConfig` 解析校验、mtime 250ms 热应用）。
- 第三方 `third_party/`（safetyhook、Zydis）只可升级替换，不可手改。
- 保持上游 3 空格缩进风格（native），托管用 4 空格。

## 8. 保留但易被误判为"死代码"的机制

| 机制 | 位置 | 作用 | 删除后果 |
|---|---|---|---|
| hot-apply（RequestHotApply / ProcessPendingHotApply / ScheduleSelectedStatusRebind） | selection_store / trait_hooks / exports.Tick | 主动重建角色状态，产生 Generation 确认日志，装备界面即时生效 | 失去验证日志；部分场景生效延迟到下次自然重建。**不建议删** |
| EditSession 状态（UpdateEditSessionState / SafeReadUiModes） | safe_game_access | hot-apply 的 context1 分支判据 | hot-apply 与状态重建绑定 |

## 9. 已知限制与未来方向

- 配置化已完成（loadout.json + Wails v3 工具 `Loadout/`：托盘/单实例/自动保存/热键 F1；ABI v17）。
  后续方向：物品权威组合表。
- 当前已覆盖全角色；扩展新角色 = 生成器数据表加条目 + 查该角色专属因子 hash。
- 游戏更新后需回归：`layout_resolver` 锚点可能失效；日志出现 layout failed 时等更新方案或重新逆向。

## 10. 常用操作速查（给接手 AI 的指令模板）

- **改专属数据（词条/因子/等级）**：改 `tool-gen-loadout.ps1` 的 `$chars` 表 → 运行 → 替换 C++ 表段
  + `character-exclusives.json` → 编译 → 部署 → 验证。（脚本为**无 BOM UTF-8**：必须用 pwsh 7 运行，
  Windows PowerShell 5.1 会按 GBK 解析导致中文乱码）
- **改通用槽/前端规则**：工具与托管逻辑（无内置通用默认；副因子规则见 §4.1）。
- **加角色**：生成器数据表加行（查该角色专属因子 hash：`sigils.json` 专属行 + 名字表）→ 同上。
- **升版本**：§5 发布（版本号同步、全文档残留扫描、Nexus 描述同步）。
- **提交**：`git -c user.name="baagod" -c user.email="780810441@qq.com" commit ...`
  （不要改全局 git config）。提交前 `git status` 确认无 bin/obj/dist 混入。
- **推送**：`git -c credential.helper="!gh auth git-credential" push origin main`
  （仓库已配置本地代理 127.0.0.1:7890；若提示 403，检查 gh token 的 Contents: Read and write 权限）。

## 12. 背景与交接（2026-09-09 更新）

### 当前状态
- **版本**：v0.5.7（ABI v17）。入口配装：每角色专属 3 独立槽（T1/T2/战气，默认全开）+ 玩家通用槽
  （固定 12 行编辑器，无内置通用默认）。
- **唯一性**：GBFR 唯一"零库存预配装 + 运行时合成 + 不碰存档"的 mod；差异化 = "预配装/全角色/零折腾"。

### 机制沿革（已废弃机制 → 替代；勿按旧描述"修复"）

- WebView2 恢复白闪的旧修法——托盘隐藏淡入（0.4.0）与激活尺寸 nudge（0.5.3）——已由假隐藏取代：
  X/工具内热键/Esc 不再真隐藏窗口（alpha=0 + `EnableWindow(FALSE)` + `WS_EX_TOOLWINDOW` 并清掉 Wails
  强制的 `WS_EX_APPWINDOW`）；托盘/游戏热键/二次启动统一走 0x8010 `revealTool`；窗口尺寸仅在创建时
  设置一次（`Loadout/main.go`）。**假隐藏时把焦点交还"召唤前的前台窗口"（热键路径下=游戏）**：
  禁用窗口会丢焦点，而 mod 只在游戏为前台时才响应 F1，不交还焦点会导致"隐藏后按 F1 再也唤不出工具"。
  两个实现要点：① `user32` 的导出名是 `SetForegroundWindow`（**没有 W 后缀**，写成 `SetForegroundWindowW`
  会在调用时 panic，且托盘路径的 `recover` 会把它吞掉）；② 交还必须回到 UI 线程
  （`fakeHide` 只 `PostMessage` `WM_APP+0x11`，真正的隐藏/交还在 `hideNow`）——在 Wails 服务调用栈上
  直接调用会与 UI 线程互相等待。**"取消焦点"本身没用**：实测禁用或隐藏前台窗口后
  `GetForegroundWindow()` 仍返回那个已隐藏的窗口，必须显式 `SetForegroundWindow`。
  交还目标优先用召唤时记住的窗口；工具是直接打开（没经过 0x8010）时退回 Z-order 下一个
  可见/可用/有标题的窗口（`nextForegroundWindow`）。假隐藏还要加 `WS_EX_TRANSPARENT`：
  否则那个看不见的窗口仍参与命中测试、继续当"鼠标指针归属窗口"，系统就会画我们线程的箭头——
  游戏本来已隐藏光标，切出再切回时箭头却留在屏幕上（游戏把鼠标停在 (0,0) 并隐藏，任何失焦都会
  让系统在 (0,0) 画出默认箭头；alt+tab 同样能复现，与工具无关）。
  隐藏后还会补一次左键点击（`mouse_event`），触发游戏自己的"光标出现后首次点击只隐藏光标、不吃游戏输入"
  逻辑——否则箭头会一直留在 (0,0) 直到玩家点一下。时序：交还焦点后等 **10ms** → 按下 → 保持 **10ms** → 抬起。
  **别再往下调**：游戏运行时会把系统计时器分辨率提到 1ms，1ms 的间隔会落进输入采样的一帧之内，实测
  "时显时不显"；0ms 则完全不生效。**注入有硬条件**：只有"工具是被游戏内 F1 召唤出来的"
  `returnFocusTo != 0`、且交还成功、且 `isGameWindow(target)`（`QueryFullProcessImageNameW` 确认目标窗口属于
  `granblue_fantasy_relink.exe`）才注入；托盘/直接打开一律不注入，避免往任意前台程序点一下。
  工具另带可选诊断日志：exe 目录存在 `tool-debug.on` 时写 `tool-debug.log`。
- status-owner 遥测 mid-hook（5 个只写原子量）0.5.2 已删：无消费者。
- 古兰/姬塔共享 PL0000：工具面板合并一行，mod 侧按 PL 键扇出到两个角色（两者专属因子完全相同）。
- 逐版变更明细见 git log（提交说明与本节内容一致，不再双份维护）。

### 已验证 / 原则
- 主控 + AI 角色都吃注入（明镜止水的守护/HP吸收/追击/迅捷）——卸主槽因子测试确认。
- 槽位/版本/数据改动后需同步：MAINTENANCE 头部、README×2、ModConfig、build-release.ps1。

## 13. 跨语言协议常量表（改动需同步，勿漂移）

| 常量 | 值 | 位置 |
|---|---|---|
| 通用槽上限 MaxSlots | 12（三方均只计启用槽） | C# LoadoutConfig.cs / Go loadoutservice.go / TS App.tsx |
| 默认等级 DefaultLevel | 15 | C# LoadoutConfig.cs / TS App.tsx |
| 未穿戴哨兵 UnwornCharacterHash | 0x887AE0B0 | C# LoadoutConfig.cs / C++ native_internal.h（单词条 trait2 必须用它，不能用 0） |
| 模板槽 ID 基址 | 0xFE000000 | C++ native_internal.h |
| 热键默认 / 工具隐藏键 | F1 (0x70) | C# HotkeyConfig.cs / Go loadoutservice.go / TS App.tsx |
| 工具窗口标题 | GBFR Pre-Equipped Sigils | C# Hotkey.cs / Go main.go |
| 内部显示消息 WM_APP+0x10 | 0x8010 | C# Hotkey.cs / Go main.go |
| 单实例互斥体名 | Local\GBFRPreEquippedSigilsTool | Go main.go |
| 工具热键发布文件 | tool-hotkey.txt（exe 同目录） | C# Hotkey.cs / Go loadoutservice.go |
| 用户配置路径 | %LocalAppData%\GBFRPreEquippedSigils\loadout.json | C# LoadoutConfig.cs / Go loadoutservice.go |
| 原生 ABI 版本 | 17 | native_api.h / C# NativeCore.cs AbiVersion |
| 原生结构尺寸 | TemplateSlot 0x18 / ExclusiveOverride 0x08 | native_api.h static_assert / C# native 侧 runtime 校验 |
| 等级范围校验 | 前端 1..cap（空槽显示 0）；Go 结构校验 0..200；C# 最终 0..cap | TS SlotEditor.tsx / Go loadoutservice.go / C# LoadoutConfig.cs |
| sigils.json character 行数 | 87（发布前由 build-release.ps1 与 native_internal.h 对拍） | native_internal.h / build-release.ps1 |
