# GBFR Reloaded Sigil Slots

《碧蓝幻想：Relink》自动配装虚拟因子槽位 Mod（ER 2.0.5）。全部 29 名角色自动获得 5 个虚拟槽，运行时合成因子，零库存、零存档、零配置。

派生自 [GBFR Extra Sigil Slots](https://github.com/cajoxorize366-oss/GBFR-Extra-Sigil-Slots)，已删除选择器 UI、Overlay Broker、输入捕获、预设与库存依赖，新增与库存无关的模板配装引擎。



## 文档入口

| 文档 | 对象 | 内容 |
|---|---|---|
| [GBFR.ReloadedSigilSlots/README.md](GBFR.ReloadedSigilSlots/README.md) | 用户 | 功能简介、配装表、安装、注意事项 |
| [docs/MAINTENANCE.md](docs/MAINTENANCE.md) | **AI 接手者** | 架构、数据流、雷区、构建部署、操作速查 |
| [docs/gbfr-sigil-hashes.zh-CN.tsv](docs/gbfr-sigil-hashes.zh-CN.tsv) | 维护者 | 完整 hash 查询表（S=物品，T=词条） |



## 仓库结构

```
GBFR.ReloadedSigilSlots/         C# 托管层（Reloaded-II 壳，打包进 mod）
GBFR.ReloadedSigilSlots.Native/  C++ 原生核心（Hook 与模板合成引擎）
docs/                            参考文档与 hash 表（不打包）
dist/                            构建产物（zip，git 忽略）
build-release.ps1                一键构建脚本
```



## 快速上手（维护）

```powershell
# 构建（需 VS2022 Build Tools + .NET 8 SDK）
powershell -ExecutionPolicy Bypass -File .\build-release.ps1

# 部署：游戏退出后，把 dist\GBFR.ReloadedSigilSlots 复制到 Reloaded-II 的 Mods/
```

改配装、加槽位、加角色的具体步骤见 [docs/MAINTENANCE.md](docs/MAINTENANCE.md) 第 4、10 节。
