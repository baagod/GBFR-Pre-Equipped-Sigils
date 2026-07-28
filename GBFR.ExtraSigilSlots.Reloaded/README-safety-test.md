# GBFR 扩展因子槽 0.7.6

这是 Reloaded-II 独立版，不依赖或修改 Luma、ReShade 与游戏存档。插件支持 1–24 个可配置扩展槽、命名预设、因子占用筛选、中文输入和键鼠独占／手柄直通。

## 安装与旧版迁移

1. 完全退出游戏。
2. 备份旧目录 `GBFR.ExtraSigilSlots20.Reloaded` 中的 `GBFR-ExtraSigilSlotsNumConfig.ini`、旧版 `GBFR-ExtraSigilSlots20.ini` 和 `GBFR-ExtraSigilSlots20.presets.json`。
3. 安装新目录 `GBFR.ExtraSigilSlots.Reloaded`，随后删除或禁用旧 ModId，不能让 Reloaded-II 同时加载两个版本。
4. 首次启动时，如果新配置仍是随包默认值且新预设尚不存在，插件会自动复制旧设置、角色选择与命名预设。旧文件不会删除，可继续作为备份。
5. 新文件名为 `GBFR-ExtraSigilSlotsNumConfig.ini` 与 `GBFR-ExtraSigilSlots.presets.json`。

## 基本验证

- 默认按 `F8` 打开或关闭界面；可在 Reloaded-II 的模组配置中更换快捷键。
- 界面只拦截键盘和鼠标；XInput、DirectInput 手柄及 Raw HID 继续传给游戏。
- 只在装备界面修改因子。游戏不支持战斗状态热更新因子。
- `VirtualSlotCount` 默认是 `8`，支持 `1` 至 `24`；非法值会按安全规则自动规范化。
- 插件不写 SaveData，也不修改 `GemData.WORN_BY`。

## Win11 Present 兼容性日志

正常启动应出现：

```text
DX11 Present-only backend enabled with a native original-Present boundary
```

如果原始 Present 链触发 `0xC0000005`，原生边界会拦截异常并在图形回调线程之外停用覆盖层 Hook，避免由托管回调直接导致 Fatal error。

## 游戏 EXE 兼容性门禁

启动关键路径不再同步扫描整个游戏 EXE。兼容性放行完全由全部必需的 RVA／字节预检决定：所有预检完成前不会写入补丁或安装游戏 Hook；任意一项不匹配都会拒绝整次安装，并在日志中报告失败阶段与 RVA。Hook 同步安装返回后，托管层才会在后台执行整文件 SHA-256 诊断，并以 `diagnostic_only=true` 记录实际哈希与已知哈希是否匹配；该结果不会放行、拒绝或回滚 Hook。这样既保留原有安全门，也避免冷文件缓存、杀毒软件或文件过滤驱动把 Reloaded-II 的同步启动长时间阻塞。

这允许代码布局完全相同、但整体文件哈希不同的 ER 2.0.2 EXE 使用扩展因子槽，同时仍会拒绝真正发生代码布局变化的版本。

日志还会明确输出 `由 Launcher 注入`、`由 .asi Bootstrapper 加载` 或 `source=unknown`。官方 Deploy ASI 的识别同时检查 `Reloaded.Mod.Loader.Bootstrapper.asi` 模块名与 `InitializeASI` 导出；不会把任意同名文件或普通 ASI 猜成 Reloaded-II Bootstrapper。
