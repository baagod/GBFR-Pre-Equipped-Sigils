# 实现计划：配装配置化（loadout.json + WPF-UI 工具）

> 状态：**已批准待实施**（2026-09-04 决策）
> 关联：MAINTENANCE §9/§12（配置化=未来方向）、用户请求（自定义词条、槽位增删、每槽 1-2 词条）
> 参考实现：https://github.com/BitterG/GBFR-PE-Patch-Tool （工具形态先例，Go+Web；本项目用 C# 栈）

---

## 1. 目标

玩家**自由预配**虚拟槽位（可增删槽、每槽 1-2 词条+等级、启用/删除），
mod 运行中**热更新**生效。目的：终结"能不能加 xx / X 顶满"类需求——
需求方从作者变为玩家；**不做数值加强/顶配**（保持平衡，拒绝超强路线，见 §12 决策）。

## 2. 架构决策（已确认，勿反复）

| 决策点 | 结论 | 理由 |
|---|---|---|
| UI 形态 | **独立 WPF-UI exe**（C# .NET 8，自包含） | 不做 ImGui overlay（原版模式，几万行）；不做 Reloaded-II 静态配置页（无动态行）；不做 HTML 套壳（原生控件足够） |
| 与 mod 关联 | **共享文件协议**：mod 目录 `loadout.json` + mtime 检测 | 零进程间通信；任一方崩溃无影响；玩家也可手改 |
| 工具入口 | **热键（默认 F8，可自定义）**——仅游戏前台 → `Process.Start(LoadoutTool.exe --mod-dir <dir>)`；键位经 **Reloaded-II 配置页**（静态枚举字段：F6-F11/Insert）设置，防热键冲突 | 玩家视角等同原版 F8；键位设置=配置页静态字段（其适用场景）；`OnConfigurationUpdated` 热改键位 |
| 热更新 | 现有链：tick 检测 → 运行时模板替换 → `ScheduleSelectedStatusRebind`/`RequestHotApply` → Generation 日志 | 全部现有机件复用 |
| 数据流 | `loadout.json`（中文词条名）→ 托管层解析校验 → **ABI v16** `GBFR20_SetCustomLoadout` → native 运行时模板表 | 校验/错误日志放 .NET；native 仅存数据 |
| 槽数 | 运行时化（`GetVirtualSlotCount()` 改读运行时值）；变化时**运行中重 patch 两处循环上限字节**（现有 WriteByte+读回校验） | 架构红利：现有函数已参数化；hot-apply 已证明运行中可改 |
| 回退 | 无配置/配置无效 → **内置默认 8 槽**（温和 fail-closed） | 默认体验 = 现状，零操作玩家无感 |
| 词条字典 | `traits.json`（中文名→T hash，由 hash TSV 生成，工具与 mod 共用） | 玩家永不接触 hash |
| 物品显示名 | **先复用内置物品**（词条实际效果只吃 T hash） | 权威"词条→物品组合表"后补（GBFRDataTools 导出） |
| 每角色开关 | **v2**；T1 先全局模板（所有角色同一配置） | 少一个维度，第一版稳 |

## 3. 数据格式（三端共享，行内单一权威）

### 3.1 `loadout.json`（玩家/工具 → mod）

```json
{
  "slots": [
    { "trait1": "追击", "level1": 15, "trait2": "迅捷能力", "level2": 15, "enabled": true },
    { "trait1": "怒涛", "level1": 15, "trait2": "激昂", "level2": 15, "enabled": true }
  ]
}
```

规则：词条名必须命中 traits.json；level 1-20（默认 15）；槽数 1-24；
`trait2` 可省略（单词条槽，内部用 `0x887AE0B0` 哨兵，不得用 0——§4 踩坑）；
`enabled=false` 槽跳过；无效槽 → 跳过+日志，整体无效 → 保留上次有效配置。

### 3.2 `traits.json`（生成器产物，打包进 mod + 工具）

```json
[ { "nameZh": "追击", "nameEn": "Supplementary DMG", "hash": "57AB5B10" }, ... ]
```

生成脚本：`docs/tool-gen-trait-dict.ps1`（读 `gbfr-sigil-hashes.zh-CN.tsv` T 行 +
`...en.tsv` 同 hash 行合并；控制台输出文件并打印统计）。T 行中文名唯一性由脚本校验。

## 4. T1 任务清单（mod 侧，预期 1.5-2 天）

| # | 文件 | 内容 |
|---|---|---|
| T1.1 | `docs/tool-gen-trait-dict.ps1`（新） | 生成 traits.json（见 3.2）；加入 build-release 前检查 |
| T1.2 | `GBFR.PreEquippedSigils/LoadoutConfig.cs`（新） | 加载 traits.json 字典 + loadout.json；校验（词条名/等级/槽数/哨兵）；产出 native 结构数组；错误消息走现有 Log |
| T1.3 | `GBFR.PreEquippedSigils/Mod.cs` | Tick 前接入 mtime 检测（对比缓存，变化才解析）；失败保留上次生效配置；启动时读一次 |
| T1.4 | `GBFR.PreEquippedSigils/NativeCore.Interop.cs` | P/Invoke `GBFR20_SetCustomLoadout(...)`；`AbiVersion = 16` |
| T1.5 | native `native_api.h` | ABI v16：新增 `GBFR20_TemplateSlot`（trait1/lvl1/trait2/lvl2/gem_id/sigil_level，pack(1)）与导出声明 |
| T1.6 | native `native_internal.h` | 运行时模板表声明（`std::array<TemplateGemSlot,kVirtualSlotCapacity>` + 计数 + 原子版本号） |
| T1.7 | native `template_loadout.cpp` | 运行时表实现；`kDefaultTemplates` 保留为内置回退；`FindTemplateSlot`/`TryCopyTemplateGem`/`InstallDefaultTemplateSelections` 读运行时表（表空=内置） |
| T1.8 | native `runtime_state.cpp` | `GetVirtualSlotCount()` 改为运行时值（默认 `kTemplateSlotCount`） |
| T1.9 | native `exports.cpp`（+runtime.cpp?） | `GBFR20_SetCustomLoadout` 实现：拷贝数据 → 若槽数变化**重 patch 两处循环上限字节**（WriteByte+读回） → 触发 `ScheduleSelectedStatusRebind` |
| T1.10 | `build-release.ps1` | 打包 traits.json + 示例 loadout.json（示例放 `_example` 名，不覆盖用户文件） |

**T1 验收**：构建 0w0e；手写 loadout.json（示例）→ 游戏内：日志出现
`Generation M ... copied N/N`；装备界面/训练场换词条生效；删除配置回退默认；
错词条名日志定位；**不重启** 改动即在（mtime 热检测）。

## 5. T2 任务清单（工具 + 热键，预期 1.5-2 天）

| # | 文件 | 内容 |
|---|---|---|
| T2.1 | `LoadoutTool/`（新子工程） | `net8.0-windows` + UseWPF + NuGet `WPF-UI`（Dark 主题）；`MainWindow : FluentWindow`（无边框圆角标题栏白送） |
| T2.2 | UI 实现 | `LoadoutSlot`（Enabled/Trait1/Level1/Trait2/Level2）→ `ObservableCollection<LoadoutSlot>` → `ItemsControl + DataTemplate`：词条下拉（traits.json 中文名）+等级框 \| 词条下拉+等级框 \| ✓ −；"添加槽位"按钮；保存按钮 |
| T2.3 | 保存逻辑 | 命令行 `--mod-dir`（mod 拉起时传入）→ 写 `loadout.json`（同 3.1 格式）；未带参 → 提示并兜底默认路径 |
| T2.4 | `GBFR.PreEquippedSigils/Hotkey.cs`（新） | P/Invoke `SetWindowsHookEx(WH_KEYBOARD_LL)`；仅当前台进程是游戏（GetForegroundWindow→进程名）且按**配置键位**（默认 F8）按下沿 → `Process.Start(LoadoutTool.exe --mod-dir <modDir>)` |
| T2.4b | `GBFR.PreEquippedSigils/Config.cs`（新） | Reloaded-II 标准配置：`Config`（enum Hotkey，默认 F8）+ `ConfiguratorMixin`；Mod.cs 读取并转发给 Hotkey.cs；`OnConfigurationUpdated` 热更新键位 |
| T2.5 | `build-release.ps1` | 工具 self-contained 单 exe → 打入 mod zip（mod 目录内 exe，Reloaded-II 无冲突） |

**T2 验收**：工具 UI 完整可用；游戏内 F8 弹出（仅游戏前台）；改后保存→T1 热更新生效；
工具关闭不影响游戏；词条字典与 mod 一致。

## 6. 风险对照

- ABI v15→v16：**模组整体同步发布**，无兼容期问题（文档 §7 三方同步流程现成）。
- 运行时表迁移：消费点仅 3 处（已列 T1.7），内置表保留 = 回退即原行为。
- 运行中重 patch 循环上限：仅在 SetCustomLoadout 且槽数变化时执行；hot-apply 已证明运行中改安全；patch 前后有读回校验。
- 词条名歧义：T 行唯一性由生成器校验；同名冲突出现 → 生成器失败并提示，不静默。
- 等级 1-20、哨兵（0x887AE0B0）规则：校验在托管层，native 侧仍**保持现有防御**（无效数据不注入）。

## 7. 明确不做（本期）

- 每角色开关（v2）；数值加强/顶配（永不）；ImGui overlay（文件级 mod 之后再说）；
- 权威"词条→物品组合表"（T2 后补）；GUI 报错弹窗（先日志）。

---

实施顺序：T1 → T2；各阶段独立提交；提交信息前缀 `feat(loadout-config):`。
