# GBFR Reloaded Sigil Slots — AI 接手维护手册

> 面向对象：后续接管本项目的 AI agent / 开发者。
> 阅读前提：先读 `README.md`（用户向说明）。本手册是**技术维护**文档。
> 项目位置：本仓库根目录。游戏版本：Granblue Fantasy: Relink Endless Ragnarok **2.0.5**。
> 当前版本：0.2.0。派生自 GBFR Extra Sigil Slots（Hiyajomaho-num9），已大幅精简。

---

## 1. 一句话说明

游戏原生只计算 12 个可见因子槽（内部 trait 循环上限 13）。本 mod 把循环上限扩到
`13 + VirtualSlotCount`，并 Hook 因子读取函数：当游戏询问第 13 号起的虚拟槽时，
按**内置模板表**现场合成一份 GemData 交给游戏。**不写存档、不依赖库存、不改
`GemData.WORN_BY`**；战斗数值是真实的本地效果（在线 = 作弊级，风险自负）。

## 2. 目录结构与文件职责

```
build-release.ps1                    构建+打包脚本（MSBuild native → dotnet managed → zip）
docs/
  gbfr-sigil-hashes.zh-CN.tsv        hash 查询表（S=物品/gem_id，T=词条/trait，仅参考不打包）
  gbfr-sigil-hashes.en.tsv           同上（英文）
  MAINTENANCE.md                     本手册
GBFR.ReloadedSigilSlots/             C# 托管层（Reloaded-II 插件壳）
  Mod.cs                             生命周期、日志（时间戳）、250ms 维持 Tick
  NativeCore.cs                      原生门面：加载/ABI 校验/日志回调/Tick/Shutdown/消息读取
  NativeCore.Interop.cs              P/Invoke 声明（必须与 native_api.h 同步）
  ModConfig.json                     ModId/版本/描述（发布信息）
GBFR.ReloadedSigilSlots.Native/      C++ 原生核心
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
    config_store.cpp                 INI 校验/备份重建、选择加载（恰 3 个 key）
    name_tables.cpp                  兼容表加载（199 条角色限制映射，缺失即 fail-closed）
    template_loadout.cpp             ★★配装表——日常维护唯一要改的文件
```

`★` = 高风险区，除非明确任务需要，不要动。

## 3. 核心数据流

```
启动:
  Reloaded-II → Mod.cs → NativeCore.Initialize → exports.GBFR20_Initialize
    → runtime.Initialize:
        settings-and-selections (INI 校验/重建 → LoadSettingsAndSelections)
        executable-validation (必须 granblue_fantasy_relink.exe)
        compatibility-table     (compatibility.tsv, 199 条, 失败即停止)
        semantic-layout-resolution (layout_resolver, 失败即停止)
        verified-settings-activation (LoadSettingsAndSelections(true)
                                      → InstallDefaultTemplateSelections
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

文件：`GBFR.ReloadedSigilSlots.Native/src/template_loadout.cpp` 的 `kDefaultTemplates[]`。
**v0.3 起覆盖全部 29 名角色**（每角色 5 槽），数据由生成脚本维护，不要手写 hash：

| 工具 | 作用 |
|---|---|
| `docs/tool-extract-exclusives.ps1` | 从 compatibility.tsv + 名字表提取每角色专属因子（觉醒＋ gem、两个专属词条、战气词条） |
| `docs/tool-gen-loadout.ps1` | 内嵌每角色专属数据 → 生成 `kDefaultTemplates[]` 数组文本 |

**改配装的标准流程**：改 `tool-gen-loadout.ps1` 里的数据表（或改通用槽定义）→ 运行脚本输出到临时文件 → 替换 `template_loadout.cpp` 中 `constexpr CharacterTemplate kDefaultTemplates[] = { ... };` 段（自动定位起止替换）。

结构（每槽一个 `TemplateGemSlot`）：

```cpp
TemplateGemSlot{
   0x335DA2A5, // gem_id: 物品 hash（S 行）。游戏按它查 master 表拿显示名；词条效果吃的是下面两个 hash（已实验验证）
   0xE69A4694, // trait1: 主词条 hash（T 行）
   15,         // trait1_level: Ⅴ＋ = 15
   0x95F3FA86, // trait2: 副词条 hash（0 = 单词条）
   15,         // trait2_level
   15,         // sigil_level: 物品显示等级
},
```

**规则**：
- 每角色一个 `CharacterTemplate{ character_hash, slots[24] }`；`slots` 从 0 起**连续**，遇 `gem_id==0` 视为表结束（`InstallDefaultTemplateSelections` 与 `FindTemplateSlot` 依赖此约定）。
- 合成槽 id = `kTemplateSlotIdBase(0xFE000000) + 槽序号`，不会与真实库存槽位冲突；`IsTemplateSlotId` 判定。
- **加槽位必须同步**：INI `VirtualSlotCount` ≥ 槽数（循环上限 = 13 + 该值，默认 5）。
- 角色专属物品（觉醒＋/战气）受 `compatibility.tsv` 限制：`TryCopyTemplateGem` 会用
  `GetRequiredCharacterHash(gem_id)` 校验，专属因子只能装给对应角色（古兰/姬塔互通，姬塔条目使用古兰专属）。
- 词条 hash 查询：`docs/gbfr-sigil-hashes.zh-CN.tsv`（S=物品、T=词条；Ctrl+F 搜名字）。
- 角色 hash（角色名 → hash）：见 `UiLocalization.cs` 的历史版本或 compatibility.tsv 的
  character_key 列；常用：古兰 `2A26B1B2`、姬塔 `A4ACBA76`、娜露梅 `E7053919`、
  芙劳 `646C3168`、菲迪埃尔 `74DD4C79`。

## 5. 构建与部署

环境要求：Windows x64、VS2022 Build Tools（MSVC v143 + Windows SDK）、.NET 8 SDK。

```powershell
powershell -ExecutionPolicy Bypass -File .\build-release.ps1   # 默认 Release/x64/0.2.0
# 产物: dist\GBFR-ReloadedSigilSlots-<version>.zip
```

- 部署：**游戏必须退出**，把 `dist\GBFR.ReloadedSigilSlots` 整个文件夹复制到
  Reloaded-II 的 `Mods\`（覆盖/先删旧目录）。改名后**必须**同步：
  - `runtime.cpp` 的 `g_config_path` / `g_compatibility_path`
  - csproj 的 `<None Include=...>` 复制项
  - 已安装目录的旧文件
- 版本号：同步改 `ModConfig.json` 的 `ModVersion` 与 `build-release.ps1` 默认 `$Version`。

## 6. 验证清单（每次改动后必须）

1. 编译：**0 警告 0 错误**（third_party 的 C4834 已在 vcxproj 单独压制）。
2. 日志 `ReloadedSigilSlots.Reloaded.log`（mod 目录）：
   - `Installed N built-in template loadout selection(s); inventory-independent.`
   - `Native hook installation completed with N virtual slots; ...`
   - 进战斗：`Live battle Trait contribution confirmed for 0xE7053919: N/N ...`
   - 装备界面/训练场：`Generation M for 0xE7053919: equipment/test rebuild copied N/N ...`
3. 训练场实测词条效果（如豪胆濒死不死、自动复活自起）+ 血条下 buff 图标。
4. 重启游戏配置保留；改过 INI 后验证"非法→备份→重建"行为不回归。

## 7. 雷区（fail-closed 与安全边界，禁止削弱）

- `layout_resolver.cpp`：唯一语义锚点、call/RIP 推导、精确字节预检。解析不完整/多重匹配/
  校验不过 → **整套 gameplay hook 不安装**（fail-closed），不降级"找个像的就 Hook"。
- `trait_hooks.cpp`：detour 的 TLS/generation/identity/context/expected/injected 校验顺序；
  natural bind 的授权提交（`CommitAuthorizedStatus`）与 `ValidateAuthorizedStatuses`。
- `safe_game_access.cpp`：所有游戏内存读取必须走 SEH 安全包装与地址范围检查。
- `compatibility.tsv` 缺失或条目数 != 199 → 启动失败（fail-closed）。
- ABI：`native_api.h`（导出签名、packing、`GBFR20_ABI_VERSION=15`）与
  `NativeCore.Interop.cs`、`NativeCore.cs` 的 `AbiVersion` 必须一致；改动需三方同步 + 版本号递增。
- INI 校验语义：**恰好 3 个 key**（ConfigVersion=2 / AutoApply=1 / VirtualSlotCount=1..24）。
  非法文件 → 备份 `.invalid-*.bak` → 原子重建默认。改校验必须同步 `kDefaultConfigText`。
- 第三方 `third_party/`（safetyhook、Zydis）只可升级替换，不可手改。
- 保持上游 3 空格缩进风格（native），托管层 4 空格。

## 8. 保留但易被误判为"死代码"的机制

| 机制 | 位置 | 作用 | 删除后果 |
|---|---|---|---|
| hot-apply（RequestHotApply / ProcessPendingHotApply / ScheduleSelectedStatusRebind） | selection_store / trait_hooks / exports.Tick | 主动重建角色状态，产生 Generation 确认日志，装备界面即时生效 | 失去验证日志；部分场景生效延迟到下次自然重建。**不建议删** |
| EditSession 状态（UpdateEditSessionState / SafeReadUiModes） | safe_game_access | hot-apply 的 context1 分支判据 | 与 hot-apply 绑定 |
| INI 校验/备份重建（~200 行） | config_store | 防手改坏配置丢数据 | 健壮性下降 |

## 9. 已知限制与未来方向

- 配装表编译期内置；"配置化（INI/JSON 外置配装表 + 每角色开关）"是明确的下一步方向，
  实现时注意：校验语义、hash 仍需查表、兼容 fail-closed。
- 当前只配了娜露梅 1 个角色；扩展其他角色 = 模板表加条目 + 查该角色觉醒＋/战气 hash。
- 游戏更新后需回归：`layout_resolver` 锚点可能失效 → 日志出现 layout failed → 等上游
  方案或重新逆向。

## 10. 常用操作速查（给接手 AI 的指令模板）

- **改某角色某槽的词条**：编辑 `template_loadout.cpp` 对应 `TemplateGemSlot` 的
  `trait1/trait2` hash 与等级（hash 查 `docs/gbfr-sigil-hashes.zh-CN.tsv`）→ 编译 → 部署 → 验证。
- **加槽位**：表内追加 `TemplateGemSlot`（保持连续）→ INI `VirtualSlotCount` 调大 →
  编译 → 部署 → 验证。
- **加角色**：查该角色觉醒＋/战气的 S/T hash（compatibility.tsv + 名字表）→ 模板表加
  `CharacterTemplate` 条目 → 编译 → 部署 → 验证。
- **提交**：`git -c user.name="baagod" -c user.email="780810441@qq.com" commit ...`
  （不要改全局 git config）。提交前 `git status` 确认无 bin/obj/dist 混入。
