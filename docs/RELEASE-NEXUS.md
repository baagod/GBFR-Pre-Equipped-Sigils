# Nexus Mods 发布手册（GBFR Pre-Equipped Sigils）

> 面向对象：发布本 mod 到 Nexus Mods 的操作者 / AI。
> 状态：**已获原作者许可**（Hiyajomaho-num9 已同意发布）。尚未发布；截图待游戏内获取后补充。

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
| 分类（Category） | GBFR 仅三个分类可选：`Miscellaneous`（**选这个**）/ `Model` / `Weapons` |
| 标签（Tags） | `Reloaded-II` `Sigils` `Pre-Equipped` `QoL` `Gameplay` `Cheat` |
| 版本 | 0.3.2 |
| 游戏版本 | Granblue Fantasy: Relink（ER 2.0.5） |
| 依赖 | [Reloaded-II](https://github.com/Reloaded-II/Reloaded-II) 1.30.3+（写在 Mod Dependencies） |
| 主文件 | `dist/GBFR-Pre-Equipped-Sigils-0.3.2.zip`（内含 `GBFR.ReloadedSigilSlots` 文件夹） |
| 截图 | 至少 2–4 张：① 血条下 5 槽 buff 图标 ② 训练场/战斗 ③ 娜露梅状态面板 ④ 日志验证行。**一律用游戏内真实截图，不要用 AI 生成图** |

---

## 3. 描述模板（Nexus 页面，中文版）

> 说明：Nexus 页面可中英双语；此中文版为正式版，英文版发布前由 AI 翻译、你人工确认后再用。

```markdown
# GBFR Pre-Equipped Sigils

通过 **运行时合成因子** 自动给角色套用固定配装，不需要库存物品、不修改存档。直接把合成因子注入本地状态计算的词条链路（与游戏 12 个本体槽同一套机制，额外扩展 5 个虚拟槽）。

[截图 1] [截图 2]

## 当前配装（v0.3）——全部 29 名角色

每个角色自动获得 5 个虚拟槽（全部词条 15 级）：

| 槽位 | 因子 | 词条 |
|---|---|---|
| 1 | 角色觉醒＋（每角色专属） | 该角色两个专属词条（如娜露梅：斩姬梦幻 + 斩姬武艺） |
| 2 | 激昂Ⅴ＋ | 激昂 + 角色战气（每角色专属） |
| 3 | 豪胆Ⅴ＋ | 豪胆 + 自动复活 |
| 4 | 不动Ⅴ＋ | 不动 + 躲避性能 |
| 5 | 刚健Ⅴ＋ | 刚健 + 药水携带数 |

> 姬塔与古兰共享主角专属。

## 安装

1. 安装 [Reloaded-II](https://github.com/Reloaded-II/Reloaded-II)（1.30.3 以上）；
2. 把 zip 解压到 Reloaded-II 的 `Mods` 目录（压缩包内含 `GBFR.ReloadedSigilSlots` 文件夹）；
3. 启用 mod 后启动游戏。

## 验证

- mod 目录下日志 `ReloadedSigilSlots.Reloaded.log` 应出现：
  `Installed 5 built-in template loadout selection(s)` 与
  `Live battle Trait contribution confirmed for 0xE7053919: 5/5 ...`；
- 血条下方出现注入词条的 buff 图标。

## 兼容性与注意事项

- 仅支持 ER 2.0.5（不支持的版本会安全失败：不装 Hook、不碰存档）；
- **在线游玩：额外词条是真实的本地战斗效果（作弊级），风险自负**——其他玩家看不到槽位，但数值真实生效；
- 与本体 12 槽配装不冲突；不碰存档与库存。

## AI 使用声明（必填）

> **AI 辅助开发声明**：本 mod 的代码由 AI 助手在人类指导下编写；需求设计、配装内容、游戏内验证与文档由人类主导。完整 git 历史与维护手册见源码仓库。

## 致谢（Credit）

- **派生自 [GBFR Extra Sigil Slots](https://www.nexusmods.com/granbluefantasyrelink/mods/657)（作者 Hiyajomaho-num9）**——原始 Hook 架构、语义布局解析与兼容表。经作者许可发布。源码：[cajoxorize366-oss/GBFR-Extra-Sigil-Slots](https://github.com/cajoxorize366-oss/GBFR-Extra-Sigil-Slots)。
- [本 mod 源码仓库](https://github.com/baagod/GBFR-Pre-Equipped-Sigils)
- 数据核实参考社区工具链：[Nenkai/relink-modding](https://nenkai.github.io/relink-modding/)（官方 ID 表）与 [GBFRDataTools](https://github.com/Nenkai/GBFRDataTools)（解包/导出）。
```

---

## 3.5 AI 使用声明（必须，Nexus 强制）

Nexus 官方政策（[Generative AI Categorisation & Tagging](https://forums.nexusmods.com/topic/13542525-an-update-on-generative-ai-categorisation-tagging/)）：
**未披露生成式 AI 使用 = 违反 ToS，会被审核处理**；错误标记同样会被处理。

三个标签（Generative AI Usage 分类）：

| 标签 | 定义 | 本项目适用性 |
|---|---|---|
| **AI-Generated Content** | AI 生成的代码/UI/语音/翻译/游戏内资产；**"mod 主要由 AI 生成"时使用** | ✅ **本项目打这个**：代码主体由 AI 编写 |
| AI Media | AI 宣传图/缩略图/视频/**AI 生成的页面描述** | ⚠️ 描述需人工确认后再发布 |
| AI Assisted | 开发者主导 + AI 参与有限，**要求显著证据**（git 历史、开发文档、可解释代码） | ❌ 不适用（代码大头是 AI 写的，硬标会被审核替换） |

**发布对策**：

1. 上传时选择 **AI-Generated Content** 标签（Generative AI Usage 分类下）；
2. 描述开头保留"AI 使用声明"段（见第 3 节模板）；
3. 截图一律用游戏内真实截图（避免触发 AI Media 标签）；
4. 描述文本发布前**必须由你人工审校确认**。

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

1. ~~准备游戏内截图~~（**暂缓**：先发布，发布后补传真实截图）；
2. 登录 Nexus → Upload → 填写第 2 节信息表 + 第 3 节描述 + 第 4 节权限 + **AI-Generated Content 标签**；
3. 上传 `GBFR-Pre-Equipped-Sigils-0.3.2.zip`；
4. 发布后检查：页面渲染正常、AI 声明与 Credit 区正确、权限徽章正确；
5. 在根 README 与 ModConfig 的 `ProjectUrl` 补上 Nexus 页面地址。

## 6. 后续更新流程（交给 AI）

- 改配装/代码 → `build-release.ps1` → 更新 `ModVersion`（如 0.2.1）→ Nexus 文件页添加新版本 zip，同步版本号与更新说明；
- 描述里有配装表，**每次改配装记得同步描述**；
- 游戏更新后先回归（MAINTENANCE.md 第 6 节），不兼容时在页面顶部加警告，不要静默失效。
