# GBFR Pre-Equipped Sigils

派生自 [GBFR Extra Sigil Slots](https://github.com/cajoxorize366-oss/GBFR-Extra-Sigil-Slots) 的《碧蓝幻想：Relink》预配装因子 Mod（ER 2.0.5）：**全角色自动预配因子**，运行时合成注入，不占用本体 12 槽位，**无需库存、无配置、零存档**。

**下载**：[Nexus 页面](https://www.nexusmods.com/granbluefantasyrelink/mods/823)

## 配装模板

| 槽位 | 因子 | 词条 |
|---|---|---|
| 1 | 觉醒＋ | 该角色两个专属词条。如娜露梅：斩姬梦幻 + 斩姬武艺 |
| 2 | 激昂V+ | 激昂 + 角色战气（角色专属） |
| 3 | 豪胆V+ | 豪胆 + 自动复活 |
| 4 | 不动V+ | 不动 + 明镜止水 |
| 5 | 刚健V+ | 刚健 + 药水携带数 |
| 6 | 守护V+ | 守护 + 躲避性能 |
| 7 | 追击V+ | 追击 + 迅捷能力 |
| 8 | 可怕的漆黑钳蟹因子 | 可怕的漆黑钳蟹因子 |

## 文档入口

| 文档 | 对象 | 内容 |
|---|---|---|
| [GBFR.PreEquippedSigils/README.md](GBFR.PreEquippedSigils/README.md) | 用户 | 功能简介、配装表、安装、注意事项 |
| [docs/MAINTENANCE.md](docs/MAINTENANCE.md) | **AI 接手者** | 架构、数据流、雷区、构建部署、操作速查 |
| [docs/gbfr-sigil-hashes.zh-CN.tsv](docs/gbfr-sigil-hashes.zh-CN.tsv) | 维护者 | 完整 hash 查询表（S=物品，T=词条） |

## 仓库结构

```
GBFR.PreEquippedSigils/          C# 托管层（Reloaded-II 壳，打包进 Mod）
GBFR.PreEquippedSigils.Native/   C++ 原生核心（Hook 与模板合成引擎）
docs/                            参考文档与 hash 表（不打包）
dist/                            构建产物（zip，git 忽略）
build-release.ps1                一键构建脚本
```

## 快速上手（维护）

```powershell
# 构建（需 VS2022 Build Tools + .NET 8 SDK）
powershell -ExecutionPolicy Bypass -File .\build-release.ps1

# 部署：游戏退出后，把 dist\GBFR.PreEquippedSigils 复制到 Reloaded-II 的 Mods\
```

改配装、加槽位、加角色的具体步骤见 [docs/MAINTENANCE.md](docs/MAINTENANCE.md) 第 4、10 节。
