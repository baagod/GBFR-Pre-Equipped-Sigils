# GBFR Pre-Equipped Sigils — AI 接手维护手册

> 面向对象：后续接管本项目的 AI agent / 开发者。
> 阅读前提：先读 `README.md`（用户向说明）。本手册是*技术维护*文档。
> 项目位置：本仓库根目录。源码：https://github.com/baagod/GBFR-Pre-Equipped-Sigils
> 游戏版本：Granblue Fantasy: Relink Endless Ragnarok **2.0.5**。
> 当前版本：0.5.0（热键修复 + 日志精简 + 数据表命名统一）。派生自 GBFR Extra Sigil Slots（Hiyajomaho-num9），已大幅精简。
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
  MAINTENANCE.md                     本手册
  tool-extract-exclusives.ps1        提取每角色专属因子（觉醒＋ gem、两个专属词条、战气词条）
  tool-gen-loadout.ps1               生成 kDefaultTemplates[] 数组文本
GBFR.PreEquippedSigils/             C# 托管层（Reloaded-II 插件壳）
  Mod.cs                             生命周期、日志（时间戳）的50ms 维持 Tick
  NativeCore.cs                      原生门面：加的ABI 校验/日志回调/Tick/Shutdown/消息读取
  NativeCore.Interop.cs              P/Invoke 声明（必须与 native_api.h 同步的
  ModConfig.json                     ModId/版本/描述（发布信息）
  sigils.json                        运行时因子表（词条/物品 ID + 双语名；由 extract 管线生成的*见"数据文件生成"§4.x）
  skills.json                        运行时词条字典（hash/zh/en/cap；同上）
  pre-loadout.json                   内置模板配置（mod 目录兜底；用户配置在 %LOCALAPPDATA%\GBFRPreEquippedSigils\loadout.json）
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
    的日志 "Trait contribution confirmed for 0x...: N/N"（会话内首次状态重建报一次；未满 N/M 每次报 incomplete）

维持 (Mod.cs 250ms Tick 的GBFR20_Tick):
  UpdateEditSessionState / ValidateAuthorizedStatuses /
  ScheduleSelectedStatusRebind / ProcessPendingHotApply / ConsumeApplyResult
  （hot-apply 产生 "Generation N ... copied N/N" 日志，验证装备界的训练场路径）
```

## 4. 模板配装表（日常维护核心的

文件：`GBFR.PreEquippedSigils.Native/src/template_loadout.cpp` 的`kCharacterExclusives[]` +
`kGeneralSlots[]`（2026-09 由 26×9 全展开表去重：7 个通用槽全角色相同）的
**v0.3 起覆盖全角色**（v0.3.5 起每角色 8 槽），数据由生成脚本维护，不要手的hash的

| 工具 | 作用 |
|---|---|
| `docs/tool-extract-exclusives.ps1` | 的compatibility.tsv + 名字表提取每角色专属因子（觉醒＋ gem、两个专属词条、战气词条） |
| `docs/tool-gen-loadout.ps1` | 内嵌每角色专属数的的生成 `kCharacterExclusives[]`/`kGeneralSlots[]` 数组文本 |
| [Nenkai/relink-modding](https://nenkai.github.io/relink-modding/) + [GBFRDataTools](https://github.com/Nenkai/GBFRDataTools) | 开发期数据核实（官的ID 的/ 解包导出）—的*运行时不依赖**，仅开发工的|

**改配装的标准流程**：改 `tool-gen-loadout.ps1` 里的数据表（或改通用槽定义）的运行脚本输出到临时文的的替换 `template_loadout.cpp` 中从 `constexpr CharacterExclusiveLoadout kCharacterExclusives[] = {` 到 `kGeneralSlots ... };` 的整段（自动定位起止替换）的

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
- 数据源：`kCharacterExclusives[]`（每角色 2 条：slot0 觉醒＋、slot1 战气）+ `kGeneralSlots[]`（slot3-9 七槽通用，全角色相同）；运行时由 `BuildCharacterTemplate` 组装为 `CharacterTemplate{ character_hash, slots[24] }`；`slots` 的0 的*连续**，遇 `gem_id==0` 视为表结束（`InstallDefaultTemplateSelections`/`TryGetRuntimeSlot` 依赖此约定）的
- 合成的id = `kTemplateSlotIdBase(0xFE000000) + 槽序号`，不会与真实库存槽位冲突；`IsTemplateSlotId` 判定的
- **内置模板槽数（出厂预设）**：`native_internal.h` 的`kTemplateSlotCount`（当的9）只决定"无玩家配置时的默认槽的；玩家配置（loadout.json）可任意 2+启用槽（的2），**不受该常量约的*。仅当修改内置默认（模板表）时需同步该常量的
- 角色专属物品（觉醒＋/战气）受 `compatibility.tsv` 限制：`TryCopyTemplateGem` 会用
  `GetRequiredCharacterHash(gem_id)` 校验，专属因子只能装给对应角色（古兰/姬塔互通，姬塔条目使用古兰专属）的
- 词条 hash 查询：`extract/skills.json`（词条 hash/名）与 `extract/loadout.json`（物品 gem/名）；
  或 `sigils_all_full.xlsx` 的 `gem_key`/`skill1_hash` 列（Ctrl+F 搜名字）的
- 角色 hash（角色名 的hash）：的`UiLocalization.cs` 的历史版本或 compatibility.tsv 的
  character_key 列；常用：古的`2A26B1B2`、姬的`A4ACBA76`、娜露梅 `E7053919`的
  芙劳 `646C3168`、菲迪埃的`74DD4C79`的

## 4.1 数据文件生成（mod 运行时表：sigils.json / skills.json）

mod 目录下的 `sigils.json`（279 因子）与 `skills.json`（200 词条）**不是手工维护的**，
由 `extract` 管线一次性导出（源 → 导出 → 运行时子集）：

```
extract/skills.json（词条字典，200 条）──┐
                                          ├── export_runtime_data.py ──> mod 目录
extract/loadout.json（因子物品表，279 条）─┘                             sigils.json + skills.json
```

| 文件 | 字段 | 说明 |
|---|---|---|
| `skills.json` | `{ hash, zh, en, cap }` | 词条 ID（hash）+ 双语名 + 等级上限；源 = extract/skills.json（含 key/desc/player，导出时仅留运行时字段） |
| `sigils.json` | `{ key, gem, name, zh, skill, sec, pool, category, player, special }` | 物品变体行（不合并，279 行）：`name`（英文名）为分组键；`sec`=固定副词条（固定变体）、`pool`=随机池候选（池版变体）；`category` 替代原 rarity；源 = extract/loadout.json |

**重建/更新流程**（只动源数据，不手改 mod 产物）：
1. 改 `extract` 侧源表（`skills.json` / `loadout.json`，生成方式见 `extract/GENERATING.md` §1/§7）；
2. 跑 `extract/export_runtime_data.py`（读 `extract/loadout.json` + `extract/skills.json` → 写 mod 目录
   `GBFR.PreEquippedSigils/sigils.json` + `skills.json`）；
3. 校验：`extract/verify_parser.py`（模拟 C# 解析，检查主词条存在/等级不超 cap/哨兵规则）；
4. 数据版本从 `extract/loadout.json` 行数核对（当前 279）。

**副因子合法性规则**（2026 会话定稿，前端 `App.tsx legalByMain` 与 `extract/verify_parser.py` 镜像）：

- 主因子按 `name`（英文名）**分组**（同名变体一行）；下拉只显示唯一名字。
- **自由组** = 组内存在变体满足 `sec='' 且 pool=0 且 special=False`（战气/霸体/慧眼等 86 组）：
  副因子 = **全部词条 − 独占词条**。
- **正常组**（其余 130 组）：副因子 = `组内池版.pool ∪ 组内固定变体.sec ∪ 自由词条（86，非独占）`。
- **独占词条** = 仅出现在 `special`（single/觉醒＋）行的词条 = 3 个：`082033CB` 钳蟹的共鸣、
  `89C66ACB` 相扑斗力、`D3B8C21F` 终极钳蟹因子。
- **自由词条** = 所有自由变体的主词条（86 个），任何主因子可组合（非独占）。
- UI：非法副词条在列表中灰显（`opacity-45`）、选中非法时 trigger 红框；**保存不再拦截**
  （模组侧 C# 校验仍兜底）。
- 装配 gem 解析：副因子命中某固定变体 `sec` → 用该变体 gem；否则（池内/无副/自由词条）→
  池版变体 gem（无池则首个变体）。

**重置为预设**（工具底部按钮，AlertDialog + 取消默认聚焦）：删除用户
`%LOCALAPPDATA%\GBFRPreEquippedSigils\loadout.json`（模组回退内置模板），界面就地重载预设，
不重启进程。

**与模板表的关系**：`template_loadout.cpp` 的 `kDefaultTemplates[]`（§4）是**内置默认 9 槽**的
模板（gem_id/trait1/trait2 直接内嵌 C++）；`sigils.json`/`skills.json` 是**玩家配置**
（`loadout.json`）解析用的 ID→名称/上限映射。两者独立：玩家配置走 sigils.json/skills.json，
无玩家配置时用内置模板（不走 JSON）。

> 字段名约定（2026 会话定）：物品 ID 全链叫 `gem`；词条 ID 全链叫 `hash`；
> `key`（GEEN_/SKILL_ 内部名）只在 extract 源表保留，不进运行时。

## 5. 构建与部的

环境要求：Windows x64、VS2022 Build Tools（MSVC v143 + Windows SDK）的NET 8 SDK的

```powershell
powershell -ExecutionPolicy Bypass -File .\build-release.ps1   # 默认 Release/x64/<version>（以脚本默认参数为准）的
# 产物: dist\GBFR-Pre-Equipped-Sigils-<version>.zip
```

- 部署的*游戏必须退的*，把 `dist\GBFR.PreEquippedSigils` 整个文件夹复制到
  Reloaded-II 的`Mods\`（覆的先删旧目录）的
- 版本号：同步的`ModConfig.json` 的`ModVersion` 的`build-release.ps1` 默认 `$Version`的

## 6. 验证清单（每次改动后必须的

1. 编译的*0 警告 0 错误**（third_party 的C4834 已在 vcxproj 单独压制）的
2. 日志 `GBFR.PreEquippedSigils.Reloaded.log`（mod 目录）：
   - `Installed N built-in template loadout selection(s); inventory-independent.`
   - `Native hooks installed: N virtual slots.`
   - 启动/换人/进战斗（context-1 状态重建）：`Trait contribution confirmed for 0xE7053919: N/N ...`（首次；二次出现应为 `incomplete: N/M`）
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

- 配装表编译期内置的*配置化已完成**的026-09-04）：`loadout.json` + Wails v3 工具（`Loadout/`，托的单实的自动保存/每词条最大等级）+ RegisterHotKey 热键（默的F1的 ABI v16，详的[docs/PLAN-loadout-config.md](PLAN-loadout-config.md)（计划已执行，偏差记录见该文档头部）的
- 当前已覆盖全角色；扩展新角色 = 生成器数据表加条的+ 查该角色觉醒的战气 hash的
- 游戏更新后需回归：`layout_resolver` 锚点可能失效 的日志出现 layout failed 的等上的
  方案或重新逆向的

## 10. 常用操作速查（给接手 AI 的指令模板）

- **改某角色某槽的词的*：编的`template_loadout.cpp` 对应 `TemplateGemSlot` 的
  `trait1/trait2` hash 与等级（hash 的`extract/skills.json`，Ctrl+F 搜名字）→ 编译 的部署 的验证的
- **改出厂默认（模板表）**：`tool-gen-loadout.ps1` 通用槽定义追的调整数据 的重新生成
  （数的+ `kTemplateSlotCount` 常量同步更新——仅影响无配置时的默认）的编译 的部署 的验证的
- **加角的*：查该角色觉醒＋/战气的S/T hash（compatibility.tsv + 名字表）的模板表加
  `CharacterTemplate` 条目 的编译 的部署 的验证的
- **升版的*：走 §11 发布流程（含版本号同步、全文档旧版本号残留扫描、Nexus 描述同步）的
- **提交**：`git -c user.name="baagod" -c user.email="780810441@qq.com" commit ...`
  （不要改全局 git config）。提交前 `git status` 确认的bin/obj/dist 混入的
- **推的*：`git -c credential.helper="!gh auth git-credential" push origin main`
  （仓库已配置本地代理 127.0.0.1:7890；若提示 403，检的gh token 的Contents: Read and write 权限）的

## 12. 会话交接情报（2026-09-05，供新会话 AI 快速对齐）

### 当前状态
- **版本**：v0.5.0（ABI v16 + 热键修复 + 日志精简 + 数据表命名统一）。槽位 9（觉醒＋/战气/激昂/豪胆/不动/刚健/守护/追击/钳蟹）+ 固定 12 行编辑器（可自由选择任意因子）。
- **唯一性**：GBFR 唯一"零库存预配装 + 运行时合成 + 不碰存档"的 mod；原版（657 Extra Sigil Slots）有库存/UI/跨角色绑定痛点——需差异化："预配装/全角色/零折腾"。

### 0.4.0 发布记录（2026-09-05）
- 编辑器重构：固定 12 行（无增删）、空行"无"等级 0、副因子门控（选主才可选副）、每词条真实 maxLevel。
- **中英双语**：界面 + 因子名（traits.json 字段变更为 `{ zh, en, hash, gem, maxLevel }`），语言切换持久化（localStorage）。
- 托盘/窗口：三态激活（隐藏/最小化/遮挡）、隐藏恢复透明淡入（**解决 WebView2 恢复白闪**，Win32 WS_EX_LAYERED + alpha 渐入）、固定 760×800（禁最大化）、游戏因子图标（go:embed）。
- 热键：mod 激活前等待按键释放（防止按键尾落到工具导致"弹出即隐藏"）；工具内 Esc 也可隐藏；mod 发布 `tool-hotkey.txt` 供工具同步键位。
- 上限：MaxSlots 22 → **12**（工具 + 托管 LoadoutConfig 同步）。
- 发布材料：GitHub README 增加 Build 段（Nexus 审核用）；发布包内置 Loadout.exe 等 9 文件。

### 0.5.0 发布记录（2026-09-06）
- **sigils.json 变体模型**：因子物品表改 `{key, gem, name, zh, skill, sec, pool, category, player, special}`；
  `name`（英文名）为分组键（同名变体一行）；`sec`=固定副词条（固定变体）、`pool`=随机池候选（池版变体）；
  `rarity` 移除、`category` 加入。
- **副因子合法性规则**（名字组级）：自由组（战气/霸体/慧眼等）→ 全词条−独占；正常组 → 池∪固定∪自由词条；
  独占 3 词条（钳蟹共鸣/相扑斗力/终极钳蟹因子）；非法列表灰显 + trigger 红框；保存不再拦截。
- **重置为预设**（底部按钮）：AlertDialog 确认（取消默认聚焦），删除用户配置、就地重载预设。
- **独占因子不取随机池**：觉醒＋保留固定专属词条（sec），战气/专属词条无副。
- **修复**：工具热键呼出（exe 更名 Loadout.exe 对齐）、启动首行版本号、日志精简（战斗确认首报/失败才报）、
  配置加载竞态（主因子显示"无"）、gen 输出 `player` 统一字符串。
- 数据：mod 运行时表改用自有提取（`extract` 管线）；`traits.json`→`skills.json` 更名、`maxLevel`→`cap`、
  物品键 `hash`→`gem`（上游与运行时表一致）。
- 文档：`extract/GENERATING.md`（生成手册）、MAINTENANCE §4.1（运行时表 + 合法性规则）。

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
