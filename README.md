# GBFR Reloaded Sigil Slots

《碧蓝幻想：Relink》自动配装虚拟因子槽位 Mod（ER 2.0.5）：**全部 29 名角色自动获得 5 个虚拟因子槽**，运行时合成因子注入 —— **零库存、零存档、零配置**。派生自 [GBFR Extra Sigil Slots](https://github.com/cajoxorize366-oss/GBFR-Extra-Sigil-Slots)。

## 模板配装

5 个虚拟槽全部词条 15 级，不占用本体 12 槽。

| 槽位 | 因子 | 词条 |
|---|---|---|
| 1 | 角色觉醒＋（每角色专属） | 该角色两个专属词条（如娜露梅：斩姬梦幻 + 斩姬武艺） |
| 2 | 激昂Ⅴ＋ | 激昂 + 角色战气（角色专属） |
| 3 | 豪胆Ⅴ＋ | 豪胆 + 自动复活 |
| 4 | 不动Ⅴ＋ | 不动 + 躲避性能 |
| 5 | 刚健Ⅴ＋ | 刚健 + 药水携带数 |

## 文档入口

| 文档 | 对象 | 内容 |
|---|---|---|
| [GBFR.ReloadedSigilSlots/README.md](GBFR.ReloadedSigilSlots/README.md) | 用户 | 功能简介、配装表、安装、注意事项 |
| [docs/MAINTENANCE.md](docs/MAINTENANCE.md) | **AI 接手者** | 架构、数据流、雷区、构建部署、操作速查 |
| [docs/gbfr-sigil-hashes.zh-CN.tsv](docs/gbfr-sigil-hashes.zh-CN.tsv) | 维护者 | 完整 hash 查询表（S=物品，T=词条） |

## 仓库结构

```
GBFR.ReloadedSigilSlots/         C# 托管层（Reloaded-II 壳，打包进 Mod）
GBFR.ReloadedSigilSlots.Native/  C++ 原生核心（Hook 与模板合成引擎）
docs/                            参考文档与 hash 表（不打包）
dist/                            构建产物（zip，git 忽略）
build-release.ps1                一键构建脚本
```

## 快速上手（维护）

```powershell
# 构建（需 VS2022 Build Tools + .NET 8 SDK）
powershell -ExecutionPolicy Bypass -File .\build-release.ps1

# 部署：游戏退出后，把 dist\GBFR.ReloadedSigilSlots 复制到 Reloaded-II 的 Mods\
```

改配装、加槽位、加角色的具体步骤见 [docs/MAINTENANCE.md](docs/MAINTENANCE.md) 第 4、10 节。
