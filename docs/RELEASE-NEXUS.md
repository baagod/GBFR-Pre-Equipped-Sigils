# Nexus Mods 发布手册（GBFR Pre-Equipped Sigils）

> 面向对象：发布本 mod 到 Nexus Mods 的操作者 / AI。
> 状态：**已发布**（2026-09-01）。页面：https://www.nexusmods.com/granbluefantasyrelink/mods/823
> 许可：已获原作者（Hiyajomaho-num9）同意发布。截图已由作者在发布后补传。

---

## 1. 前置：许可（已获授权）

本 mod 派生自 [GBFR Extra Sigil Slots](https://www.nexusmods.com/granbluefantasyrelink/mods/657)
（Nexus）/ [cajoxorize366-oss/GBFR-Extra-Sigil-Slots](https://github.com/cajoxorize366-oss/GBFR-Extra-Sigil-Slots)（GitHub，作者 Hiyajomaho-num9）。
**发布许可已由原作者同意**，发布页 Credit 区保留署名即可，无需再申请。

---

## 2. 发布信息表

| 字段 | 值 |
|---|---|
| 名称（Mod Name） | GBFR Pre-Equipped Sigils |
| 摘要（Summary） | 全角色预配装 5 个虚拟因子槽位及对应因子，不依赖库存、不写存档。 |
| 分类（Category） | Miscellaneous |
| 标签（Tags） | `AI-Generated Content` `Cheating` `Gameplay` |
| 版本 | 0.3.2 |
| 游戏版本 | Granblue Fantasy: Relink（ER 2.0.5） |
| 主文件 | `dist/GBFR-Pre-Equipped-Sigils-0.3.2.zip`（内含 `GBFR.ReloadedSigilSlots` 文件夹） |
| 截图 | 至少 2–4 张：① 血条下 5 槽 buff 图标 ② 训练场/战斗 ③ 娜露梅状态面板 |

---

## 3. 描述模板（Nexus 页面，中文版）

> 说明：Nexus 页面可中英双语；此中文版为正式版，英文版发布前由 AI 翻译、你人工确认后再用。

```markdown
# GBFR Pre-Equipped Sigils

给全角色扩展 5 个因子槽位并固定配装，词条全部15级，不占用本体 12 槽位。无需库存、不写存档。

**其他玩家看不到扩展因子，在线游玩时风险自负。**

[Mod 仓库](https://github.com/baagod/GBFR-Pre-Equipped-Sigils)

> **AI 辅助开发声明**：本 Mod 的代码由 AI 助手在人类指导下编写；需求设计、配装内容、游戏内验证与文档由人类主导。

## 当前配装

1. 觉醒+：该角色两个专属词条（如娜露梅：斩姬梦幻 + 斩姬武艺）
2. 激昂Ⅴ＋：激昂 + 角色战气（角色专属）
3. 豪胆Ⅴ＋：豪胆 + 自动复活
4. 不动Ⅴ＋：不动 + 躲避性能
5. 刚健Ⅴ＋：刚健 + 药水携带数

## 安装

1. 安装 [Reloaded-II](https://github.com/Reloaded-II/Reloaded-II)（1.30.3 以上）；
2. 把 zip 解压到 Reloaded-II 的 Mods 目录；
3. 启用 Mod 后启动游戏。

## 致谢（Credit）

- - 派生自 [GBFR Extra Sigil Slots](https://www.nexusmods.com/granbluefantasyrelink/mods/657) ( 作者 Hiyajomaho-num9 )，**经作者许可发布**。
- 数据核实参考社区工具链：[Nenkai/relink-modding](https://nenkai.github.io/relink-modding/) ( 官方 ID 表 ) 与 [GBFRDataTools](https://github.com/Nenkai/GBFRDataTools) ( 解包/导出 )。
```

---

## 4. 权限声明（Declaration）建议

发布表单的权限部分按以下推荐填写（可在页面 Permissions 页随时修改）：

| 权限项 | 推荐值 | 理由 |
|---|---|---|
| 允许他人上传本 mod 到其他网站 | **是（要求注明来源）** | 开放、与原作风格一致 |
| 允许他人修改并上传修改版 | **是（要求注明来源）** | 延续原作"欢迎接手者"的态度 |
| 允许他人在自己的 mod 中使用本 mod 的资产 | **是（要求注明来源）** | 同上 |
| 要求 Credit | **是** | 强制保留派生来源链 |
| 存档（Archiving） | 允许 | 社区惯例 |
| 捐赠积分（Donation Points） | 按个人意愿（可不开） | — |

> 说明：Nexus 发布即代表你声明"你拥有发布该内容的权利"。**已获原作者许可（见第 1 节）。**

---

## 5. 发布操作清单

> ✅ **已完成**（2026-09-01 发布成功）。以下为执行记录：
> 1. 截图后补（作者已截取 3 张：两张状态面板 + 一张战斗画面，发布后上传）；
> 2. 发布信息已按第 2 节填写（分类 `Miscellaneous`，标签 `AI-Generated Content` / `Cheating` / `Gameplay`）；
> 3. 已上传 `GBFR-Pre-Equipped-Sigils-0.3.2.zip`；
> 4. 页面检查通过（AI 声明、Credit、权限徽章）；
> 5. ✅ Nexus 地址已回填：mod README（`Nexus 页面` 链接）与本文件顶部。

## 6. 后续更新流程（交给 AI）

- 改配装/代码 → `build-release.ps1` → 更新 `ModVersion`（如 0.2.1）→ Nexus 文件页添加新版本 zip，同步版本号与更新说明；
- 描述里有配装表，**每次改配装记得同步描述**；
- 游戏更新后先回归（MAINTENANCE.md 第 6 节），不兼容时在页面顶部加警告，不要静默失效。
