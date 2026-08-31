# Nexus Mods 发布手册（GBFR Reloaded Sigil Slots）

> 面向对象：发布本 mod 到 Nexus Mods 的操作者 / AI。
> 状态：**未发布**。发布前必须完成第 1 节"前置许可"步骤。

---

## 1. 前置：许可问题（法律门槛，必须先做）

本 mod 派生自 [GBFR Extra Sigil Slots](https://www.nexusmods.com/granbluefantasyrelink/mods/657)
（Nexus）/ [cajoxorize366-oss/GBFR-Extra-Sigil-Slots](https://github.com/cajoxorize366-oss/GBFR-Extra-Sigil-Slots)（GitHub，作者 Hiyajomaho-num9）。
**原仓库无 LICENSE 文件（默认保留所有权利）**，因此发布修改版前必须获得原作者许可。

### 1.1 发送入口（二选一）

- **GitHub Issue（推荐，可留档）**：https://github.com/cajoxorize366-oss/GBFR-Extra-Sigil-Slots/issues/new
  标题：`Permission request: publish a slimmed derivative (GBFR Reloaded Sigil Slots)`
- **Nexus 私信**：https://www.nexusmods.com/granbluefantasyrelink/mods/657 页面 Message 按钮（需登录）

### 1.2 申请模板（中文，直接复制）

**标题：请求允许发布精简派生版（GBFR Reloaded Sigil Slots）**

你好！

我基于你的 GBFR Extra Sigil Slots（0.8.6）做了一个精简版派生 mod（GBFR Reloaded Sigil Slots）：

- 删除了 F8 选择器 UI、Overlay Broker、输入捕获和预设机制；
- 改为**内置模板配装表自动注入**（全部 29 名角色各 5 个虚拟槽：角色觉醒＋含两个专属词条、激昂＋角色战气、豪胆＋自动复活、不动＋躲避性能、刚健＋药水携带数），运行时合成因子，**不依赖库存、不写存档**；
- 代码从 ~9000 行精简到 ~4000 行，仅保留核心 Hook、语义布局解析与 fail-closed 安全机制，已在本机 ER 2.0.5 验证正常（全部角色战斗注入确认）。

**如实说明**：代码由 AI 助手在人类指导下编写（需求设计、配装内容、游戏内验证与文档由人类主导），完整 git 提交历史与维护文档可查。你的机制与工程化设计是我们保留的核心基础。

想请问：**是否可以允许我将其发布到 Nexus Mods？** 发布时我会在页面明确注明派生来源和你的署名（Credit），并保持"允许修改、要求注明来源"的开放权限。

你的仓库交接文档写明欢迎后续维护者接手，希望得到你的许可。无论是否允许都感谢你的工作！🙏

### 1.3 发送后

1. 保存回复内容（截图或链接）——发布页 Credit 区要引用；
2. 拿到许可后按第 2–4 节填写发布信息；
3. 若 2–4 周无回复：可再跟进一次；仍无回复则自行判断（页面注明"作者未明确授权，如有异议请联系我下架"）。

---

## 2. 发布信息表

| 字段 | 值 |
|---|---|
| 名称（Mod Name） | `GBFR Reloaded Sigil Slots (Auto Loadout)` |
| 摘要（Summary） | `自动配装虚拟因子槽位：全部 29 名角色各 5 个虚拟槽（运行时合成因子），不依赖库存、不写存档、无界面。` |
| 分类（Category） | `Gameplay Effects` 或 `Cheats`（如实标注为增强/作弊类） |
| 标签（Tags） | `Reloaded-II` `Sigils` `Auto Loadout` `QoL` `Gameplay` `Cheat` |
| 版本 | 0.3.1 |
| 游戏版本 | Granblue Fantasy: Relink（ER 2.0.5） |
| 依赖 | [Reloaded-II](https://github.com/Reloaded-II/Reloaded-II) 1.30.3+（写在 Mod Dependencies） |
| 主文件 | `dist/GBFR-ReloadedSigilSlots-0.3.1.zip`（内含 `GBFR.ReloadedSigilSlots` 文件夹） |
| 截图 | 至少 2–4 张：① 血条下 5 槽 buff 图标 ② 训练场/战斗 ③ 娜露梅状态面板 ④ 日志验证行。**一律用游戏内真实截图，不要用 AI 生成图** |

---

## 3. 描述模板（Nexus 页面，中文版）

> 说明：Nexus 页面可中英双语；此中文版为正式版，英文版发布前由 AI 翻译、你人工确认后再用。

```markdown
# GBFR Reloaded Sigil Slots（自动配装）

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

- **派生自 [GBFR Extra Sigil Slots](https://www.nexusmods.com/granbluefantasyrelink/mods/657)（作者 Hiyajomaho-num9）**——原始 Hook 架构、语义布局解析与兼容表。经作者许可发布。
- [源码仓库](https://github.com/cajoxorize366-oss/GBFR-Extra-Sigil-Slots)

## 权限（Permissions）

- 按第 4 节填写后，此处会自动显示 Nexus 的权限徽章。
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
4. 描述文本发布前**必须由你人工审校确认**；
5. 许可申请模板（第 1.2 节）已包含 AI 辅助开发披露。

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

> 说明：Nexus 发布即代表你声明"你拥有发布该内容的权利"。**必须先完成第 1 节许可步骤再上传。**

---

## 5. 发布操作清单

1. 完成第 1 节许可并获得回复；
2. 准备游戏内截图（血条 buff、训练场、日志）；
3. 登录 Nexus → Upload → 填写第 2 节信息表 + 第 3 节描述 + 第 4 节权限 + **AI-Generated Content 标签**；
4. 上传 `GBFR-ReloadedSigilSlots-0.3.1.zip`；
5. 发布后检查：页面渲染正常、AI 声明与 Credit 区正确、权限徽章正确；
6. 在根 README 与 ModConfig 的 `ProjectUrl` 补上 Nexus 页面地址。

## 6. 后续更新流程（交给 AI）

- 改配装/代码 → `build-release.ps1` → 更新 `ModVersion`（如 0.2.1）→ Nexus 文件页添加新版本 zip，同步版本号与更新说明；
- 描述里有配装表，**每次改配装记得同步描述**；
- 游戏更新后先回归（MAINTENANCE.md 第 6 节），不兼容时在页面顶部加警告，不要静默失效。
