# GBFR Pre-Equipped Sigils

派生自 [GBFR Extra Sigil Slots](https://github.com/cajoxorize366-oss/GBFR-Extra-Sigil-Slots) 的《碧蓝幻想：Relink》预配装因子 Mod（ER 2.0.5）：**全角色自动获得 7 个虚拟因子槽**，运行时合成因子注入，**零库存、零存档、零配置**。

**下载**：[Nexus 页面](https://www.nexusmods.com/granbluefantasyrelink/mods/823)

## 模板配装

7 个虚拟槽（词条 15 级 + 漆黑钳蟹 Lv20），不占用本体 12 槽。

| 槽位 | 因子 | 词条 |
|---|---|---|
| 1 | 角色觉醒＋（每角色专属） | 该角色两个专属词条（如娜露梅：斩姬梦幻 + 斩姬武艺） |
| 2 | 激昂Ⅴ＋ | 激昂 + 角色战气（角色专属） |
| 3 | 豪胆Ⅴ＋ | 豪胆 + 自动复活 |
| 4 | 不动Ⅴ＋ | 不动 + 明镜止水 |
| 5 | 刚健Ⅴ＋ | 刚健 + 药水携带数 |
| 6 | 守护Ⅴ＋ | 守护 + 躲避性能 |
| 7 | 可怕的漆黑钳蟹因子 (Lv20) | 活动限定：伤害上限+5% / 防御+2% / 追击 / 伤害+2% |

## 文档入口

| 文档 | 对象 | 内容 |
|---|---|---|
| [GBFR.PreEquippedSigils/README.md](GBFR.PreEquippedSigils/README.md) | 用户 | 功能简介、配装表、安装、注意事项 |
| [docs/MAINTENANCE.md](docs/MAINTENANCE.md) | **AI 接手者** | 架构、数据流、雷区、构建部署、操作速查 |
| [docs/gbfr-sigil-hashes.zh-CN.tsv](docs/gbfr-sigil-hashes.zh-CN.tsv) | 维护者 | 完整 hash 查询表（S=物品，T=词条） |

## 仓库结构

```
GBFR.PreEquippedSigils/         C# 托管层（Reloaded-II 壳，打包进 Mod）
GBFR.PreEquippedSigils.Native/  C++ 原生核心（Hook 与模板合成引擎）
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
