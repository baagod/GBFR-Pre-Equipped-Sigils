# GBFR Pre-Equipped Sigils — AI 接手维护手册

> 面向对象：后续接管本项目的 AI agent / 开发者。
> 阅读前提：先读 `README.md`（用户向说明）。本手册是*技术维护*文档。
> 项目位置：本仓库根目录。源码：https://github.com/baagod/GBFR-Pre-Equipped-Sigils
> 游戏版本：Granblue Fantasy: Relink Endless Ragnarok **2.0.5**。
> 当前版本：0.5.1（ABI v17；当前状态与历史见 §12）。
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
GBFR.PreEquippedSigils/             C# 托管层（Reloaded-II 插件壳）
  Mod.cs                             生命周期、日志（时间戳）、250ms 维持 Tick
  NativeCore.cs                      原生门面：ABI 校验/日志回调/Tick/Shutdown/消息读取
  NativeCore.Interop.cs              P/Invoke 声明（必须与 native_api.h 同步）
  LoadoutConfig.cs                   解析 loadout.json（通用槽 + exclusive 段）→ ABI
  ModConfig.json                     ModId/版本/描述（发布信息）
  sigils.json / skills.json          运行时因子表/词条字典（extract 管线生成，见 §4.1）
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
    name_tables.cpp                  兼容表加载（199 条角色限制映射，缺失即 fail-closed）
    template_loadout.cpp             ★★专属配装表（表段由生成器产出，勿手改；组装逻辑见 §4）
```

`★` = 高风险区，除非明确任务需要，不要动。

## 3. 核心数据流

```
启动:
  Reloaded-II：Mod.cs → NativeCore.Initialize → exports.GBFR20_Initialize
    → runtime.Initialize:
        executable-validation (必须 granblue_fantasy_relink.exe)
        compatibility-table     (compatibility.tsv, 199 条，失败即停)
        semantic-layout-resolution (layout_resolver, 失败即停)
        template-selection-install (InstallDefaultTemplateSelections: 以 0xFE000000+i 合成 id 写入角色选择)
        native-hook-install    (3 个 hook + 2 处循环上限 patch)

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
| `docs/tool-gen-loadout.ps1` | 内嵌每角色专属数据（Hash/T1/T2/War/Awake），从 sigils.json 推导变体 gem、从 compatibility.tsv 解析 PL 码，生成 `kCharacterExclusives[]` 与 `character-exclusives.json` |
| [Nenkai/relink-modding](https://nenkai.github.io/relink-modding/) + [GBFRDataTools](https://github.com/Nenkai/GBFRDataTools) | 开发期数据核实（官方 ID 表 / 解包导出），运行时不依赖 |

**改配装的标准流程**：改 `tool-gen-loadout.ps1` 数据表 → 运行脚本 → 把输出替换进 `template_loadout.cpp`
（从 `constexpr CharacterExclusiveLoadout kCharacterExclusives[] = {` 到 `};` 的整段，自动定位起止）→
`character-exclusives.json` 同步更新 → 编译 → 部署 → 验证（§6）。

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
- 角色专属物品受 `compatibility.tsv` 限制：`TryCopyTemplateGem` 用 `GetRequiredCharacterHash(gem_id)` 校验，
  只能装给对应角色（古兰/姬塔互通，姬塔条目使用古兰专属）。
- 词条 hash 查询：`extract/skills.json`（词条 hash/名）或 `sigils_all_full.xlsx` 的
  `gem_key`/`skill1_hash` 列（Ctrl+F 搜名字）。
- 角色 hash：compatibility.tsv 的 character_key 列；常用：古兰 `2A26B1B2`、姬塔 `A4ACBA76`、
  娜露梅 `E7053919`、芙劳 `646C3168`、菲迪埃 `74DD4C79`。

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
2. 跑 `extract/export_runtime_data.py` → 写 mod 目录 `sigils.json` + `skills.json`；
3. 校验：`extract/verify_parser.py`（模拟 C# 解析：主词条存在/等级不超 cap/哨兵规则）；
4. 数据版本从 `extract/loadout.json` 行数核对（当前 279）。

**副因子合法性规则**（前端 `App.tsx legalByMain` 与 `extract/verify_parser.py` 镜像）：

- 主因子按 `name`（英文名）**分组**（同名变体一行）；下拉只显示唯一名字。
- **自由组** = 组内存在变体满足 `sec='' 且 pool=0 且 special=False`（战气/霸体/慧眼等 86 组）：
  副因子 = **全部词条 − 独占词条**。
- **正常组**（其余 130 组）：副因子 = `组内池版.pool ∪ 组内固定变体.sec ∪ 自由词条（86，非独占）`。
- **独占词条** = 仅出现在 `special`（single/觉醒＋）行的词条 = 3 个：`082033CB` 钳蟹的共鸣、
  `89C66ACB` 相扑斗力、`D3B8C21F` 终极钳蟹因子。
- **自由词条** = 所有自由变体的主词条（86 个），任何主因子可组合（非独占）。
- UI：非法副词条灰显（`opacity-45`）、选中非法时 trigger 红框；**保存不再拦截**（模组侧 C# 校验兜底）。
- 装配 gem 解析：副因子命中某固定变体 `sec` → 用该变体 gem；否则（池内/无副/自由词条）→
  池版变体 gem（无池则首个变体）。

**重置为预设**（工具右上角"重置"按钮，AlertDialog + 取消默认聚焦）：写入空配置
`{ lang, slots: [] }`（**lang 为工具侧设置，不参与重置**；模组因 slots 为空回退内置专属模板），
界面就地重载预设，不重启进程。

**字段名约定**：物品 ID 全链叫 `gem`；词条 ID 全链叫 `hash`；`key`（GEEN_/SKILL_ 内部名）
只在 extract 源表保留，不进运行时。

**与模板表的关系**：§4 的 `kCharacterExclusives[]` 是**内置专属默认**（直接内嵌 C++，不走 JSON）；
`sigils.json`/`skills.json` 只是**玩家配置**（`loadout.json`）解析用的 ID→名称/上限映射，两者独立。

## 5. 构建与部署

环境要求：Windows x64、VS2022 Build Tools（MSVC v143 + Windows SDK）、.NET 8 SDK。

```powershell
powershell -ExecutionPolicy Bypass -File .\build-release.ps1   # 默认 Release/x64/<version>
# 产物: dist\GBFR-Pre-Equipped-Sigils-<version>.zip；脚本结束会自动启动工具（Loadout.exe）
```

- 部署：**游戏必须退出**，把 `dist\GBFR.PreEquippedSigils` 整个文件夹复制到 Reloaded-II 的 `Mods\`
  （覆盖前先删旧目录；工具进程运行时文件被占用，先停 Loadout.exe）。

### 发布（版本号同步）
1. 同改 `ModConfig.json` 的 `ModVersion` 与 `build-release.ps1` 默认 `$Version`；
2. 全文档旧版本号残留扫描：MAINTENANCE 头部/§12、README×2；
3. 重建（自动产出 zip）→ 部署 → 验证（§6）；Nexus 发布则同步描述。

## 6. 验证清单（每次改动后必须做）

1. 编译：**0 警告 0 错误**（third_party 的 C4834 已在 vcxproj 单独压制）。
2. 日志 `GBFR.PreEquippedSigils.Reloaded.log`（mod 目录）：
   - `Installed N built-in template loadout selection(s). Exclusive slots 1-3 (T1/T2/war), general slot i = slot 3+i; inventory-independent.`（当前 29 角色 × 3 专属 = **87**；槽位布局：专属 1-3，通用第 i 个 = 槽 3+i）
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
- `compatibility.tsv` 缺失或条目数 != 199 则启动失败（fail-closed）。
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
  后续方向：预设集丰富（狂战/斯巴达的伤害上限/天星系等）作玩家侧模板；物品权威组合表。
- 当前已覆盖全角色；扩展新角色 = 生成器数据表加条目 + 查该角色专属因子 hash。
- 游戏更新后需回归：`layout_resolver` 锚点可能失效；日志出现 layout failed 时等更新方案或重新逆向。

## 10. 常用操作速查（给接手 AI 的指令模板）

- **改专属数据（词条/因子/等级）**：改 `tool-gen-loadout.ps1` 的 `$chars` 表 → 运行 → 替换 C++ 表段
  + `character-exclusives.json` → 编译 → 部署 → 验证。
- **改通用槽/前端规则**：工具与托管逻辑（无内置通用默认；副因子规则见 §4.1）。
- **加角色**：生成器数据表加行（查该角色专属因子 hash：compatibility.tsv + 名字表）→ 同上。
- **升版本**：§5 发布（版本号同步、全文档残留扫描、Nexus 描述同步）。
- **提交**：`git -c user.name="baagod" -c user.email="780810441@qq.com" commit ...`
  （不要改全局 git config）。提交前 `git status` 确认无 bin/obj/dist 混入。
- **推送**：`git -c credential.helper="!gh auth git-credential" push origin main`
  （仓库已配置本地代理 127.0.0.1:7890；若提示 403，检查 gh token 的 Contents: Read and write 权限）。

## 12. 背景与交接（2026-09-07 更新）

### 当前状态
- **版本**：v0.5.1（ABI v17）。入口配装：每角色专属 3 独立槽（T1/T2/战气，默认全开）+ 玩家通用槽
  （固定 12 行编辑器，无内置通用默认）。
- **唯一性**：GBFR 唯一"零库存预配装 + 运行时合成 + 不碰存档"的 mod；差异化 = "预配装/全角色/零折腾"。

### 0.5.1 发布记录（2026-09-07）
- **专属因子 3 独立槽重构**（native/C#/工具/数据表，ABI 不变）：原生表 = `{hash, t1Gem, t1, t2Gem, t2, warGem, war}`；
  `kBuiltinExclusiveSlotCount` 2→3（启动 Installed 87）；禁用的槽留空（槽位不连续）。
- **loadout.json `exclusive` 段** = `{ PL码/角色名/zh/角色hash: { 词条hash: bool } }`
  （兼容旧 `{t1,t2,war}`；C# 加载 character-exclusives.json 解析）。
- **工具**：重置 = 写空配置 `{lang, slots:[]}`（lang 不参与重置，默认 zh，唯一来源 loadout.json；
  旧的 ResetLoadout 服务方法已删）；专属因子页只显示角色名（zh/en），PL 代号不显示；通用表头 44px；
  等级输入门控（无因子时禁用）；旧格式（角色 hash 键 + t1/t2/war）首次加载自动迁移为 PL 键；
  **古兰/姬塔共享 PL0000**（面板合并为一行，mod 侧按 PL 键扇出到两个角色——两者专属因子完全相同）。
- **生成器** `tool-gen-loadout.ps1` 重写为干净单遍（重跑零 diff，已验证可复现）；
  `character-exclusives.json` 移除无消费者的 `awakening` 字段。
- **已验证**：游戏内测试通过（专属因子逐项开关、3 槽显示、日志 Installed 87）。

### 历史发布记录（要点）
- **0.4.0（2026-09-05）**：编辑器固定 12 行（无增删）、中英双语、托盘三态激活 + 隐藏淡入
  （解决 WebView2 恢复白闪）、热键 F1、MaxSlots 12、发布包 9 文件。
- **0.5.0（2026-09-06）**：sigils.json 变体模型（name 分组/sec/pool/special）；副因子合法性规则
  （自由组/正常组/独占 3 词条）；重置为预设；mod 运行时表改用 extract 管线（traits.json→skills.json、
  maxLevel→cap、物品键 hash→gem）。

### 已验证 / 原则
- 主控 + AI 角色都吃注入（明镜止水的守护/HP吸收/追击/迅捷）——卸主槽因子测试确认。
- 市场：823 转化率已追平原版 657；竞品 87/819 不撞车；坚持"合理扩展"（拒绝顶配，话术 = "平衡 + 可配置"）；
  数字由 Jina Reader 抓取（可能有误差），仅参考。
- Reddit 反营销严格——**不要主动在 Reddit 自荐**（社区敌视作弊）。
- 槽位/版本/数据改动后需同步：MAINTENANCE 头部、README×2、ModConfig、build-release.ps1。
