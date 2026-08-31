# GBFR Reloaded Sigil Slots

《碧蓝幻想：Relink》自动配装虚拟因子槽位 mod（Endless Ragnarok 2.0.5）。

独立 Reloaded-II mod，派生自 [GBFR Extra Sigil Slots](https://github.com/cajoxorize366-oss/GBFR-Extra-Sigil-Slots)（Hiyajomaho-num9）。删除了原版的选择器界面、Overlay Broker、输入捕获、预设机制与库存依赖，新增了**与库存无关的模板配装引擎**。

## 功能

游戏原生只计算 12 个可见因子槽（内部循环上限 13）。本 mod 修改两处循环上限，并把**合成因子**注入本地状态计算：配置的角色自动获得内置模板配装，不占用本体槽位，**不需要库存里存在任何因子**。

- 不写存档、不改 `GemData.WORN_BY`；
- 离线使用无碍；在线时额外词条是真实的本地战斗效果（作弊级），风险自负；
- 战斗生效由原生 trait 注入追踪验证（日志中 "Live battle Trait contribution confirmed …"）。

## 模板配装（v0.2）

| 角色 | 槽位 | 物品（master 表） | 词条（15 级） |
|---|---|---|---|
| 娜露梅 | 1 | 斩姬之觉醒＋ | 斩姬梦幻 + 斩姬武艺 |
| 娜露梅 | 2 | 激昂Ⅴ＋ | 激昂 + 斩姬的战气 |
| 娜露梅 | 3 | 豪胆Ⅴ＋ | 豪胆 + 自动复活 |
| 娜露梅 | 4 | 不动Ⅴ＋ | 不动 + 躲避性能 |
| 娜露梅 | 5 | 坚持Ⅴ＋ | 坚持 + 药水携带数 |

## 安装

1. 安装 [Reloaded-II](https://github.com/Reloaded-II/Reloaded-II)（1.30.3 以上）；
2. 把 mod 文件夹解压到 Reloaded-II 的 `Mods` 目录；
3. 启用 mod 后启动游戏（Launcher 或 Steam + Deploy ASI 均可）。

## 验证

- mod 目录下的日志 `ReloadedSigilSlots.Reloaded.log` 应出现：
  - `Installed 5 built-in template loadout selection(s)`
  - `Live battle Trait contribution confirmed for 0xE7053919: 5/5 virtual sigils reached the context-1 status`
- 训练场实测：豪胆触发（濒死不死）、自动复活触发（倒地自起）；
- 血条下方会出现注入词条的 buff 图标。

## 配置

配置文件 `GBFR-ReloadedSigilSlotsConfig.ini`（首次启动时在 mod 目录自动生成）：

```ini
[Settings]
ConfigVersion=2
AutoApply=1
VirtualSlotCount=5
```

目前只有 `VirtualSlotCount`（1–24）有实际意义。配置文件非法时会被备份为 `.invalid-*.bak` 后重建默认值，不会直接覆盖。

## 构建

环境要求：Windows x64、Visual Studio 2022 Build Tools（MSVC v143 + Windows SDK）、.NET 8 SDK。

```powershell
powershell -ExecutionPolicy Bypass -File .\build-release.ps1
```

产物：`dist\GBFR-ReloadedSigilSlots-<版本>.zip`。

## 兼容性

一次性语义布局解析器面向 ER 2.0.5；布局不明确或不受支持时会安全失败（fail-closed），不碰存档。

## 许可证说明

本 mod 派生自 GBFR Extra Sigil Slots（原仓库无 LICENSE 文件，保留所有权利）。再分发修改版需要原作者许可。
