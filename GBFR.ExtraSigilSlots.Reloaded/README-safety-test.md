# GBFR 扩展因子槽 0.7.10

这是 Reloaded-II 独立版，不依赖或修改 Luma、ReShade 与游戏存档。插件支持 1–24 个可配置扩展槽、命名预设、因子占用筛选、中文输入和键鼠独占／手柄直通。

## 安装与旧版迁移

1. 完全退出游戏。
2. 如需从旧 ModId 迁移设置，请手动备份并复制旧目录中的 `GBFR-ExtraSigilSlotsNumConfig.ini`；命名预设会自动从旧 Mod 目录迁移到 Reloaded-II 的持久配置目录。
3. 安装新目录 `GBFR.ExtraSigilSlots.Reloaded`，随后删除或禁用旧 ModId，不能让 Reloaded-II 同时加载两个版本。
4. 发布包不再携带 NumConfig。目标文件不存在时，原生运行库会创建完整默认配置；文件完整合法时保持逐字节不变；文件非法时先生成带时间戳的 `.invalid-*.bak` 原始备份，再创建完整默认配置。
5. `GBFR-ExtraSigilSlotsNumConfig.ini` 保留在 Mod 目录；`GBFR-ExtraSigilSlots.presets.json` 存放在 Reloaded-II 的该 Mod 持久配置目录。损坏的预设 JSON 会先保存为 `.invalid-<digest>.bak`，再尝试从有效旧副本恢复。

## 基本验证

- 默认按 `F8` 打开或关闭界面；可在 Reloaded-II 的模组配置中更换快捷键。
- 界面只拦截键盘和鼠标；XInput、DirectInput 手柄及 Raw HID 继续传给游戏。
- 只在装备界面修改因子。游戏不支持战斗状态热更新因子。
- `VirtualSlotCount` 默认是 `8`，支持 `1` 至 `24`；任一配置项或角色槽位记录非法时，整份原文件先备份，再重建默认配置。`0` 与超过 `24` 的值都属于非法值。
- 插件不写 SaveData，也不修改 `GemData.WORN_BY`。

## Win11 Present 兼容性日志

正常启动应出现：

```text
DX11 Present-only backend enabled with a native original-Present boundary
```

如果原始 Present 链触发 `0xC0000005`，原生边界会拦截异常并在图形回调线程之外停用覆盖层 Hook，避免由托管回调直接导致 Fatal error。

## 游戏 EXE 兼容性门禁

启动时只进行一次内存中的语义布局解析，不再依赖固定 RVA，也不会定时或逐帧扫描。解析器从唯一指令锚点及其调用／引用关系推导 Hook 入口、全局指针和对象字段偏移，再校验每个局部字节契约；解析不完整、出现多重匹配或任一校验不通过时，都会拒绝整次游戏逻辑 Hook，且不会清空或重写已保存的因子选择。键鼠输入 Hook 是独立事务，因此仍可让界面显示兼容性错误；若输入事务自身只安装了一部分，则会完整回滚。Hook 同步安装返回后，托管层才会在后台执行整文件 SHA-256 诊断，并以 `diagnostic_only=true` 记录实际哈希与已知哈希是否匹配；该结果不会放行、拒绝或回滚 Hook。

该方案已用 ER 2.0.2 与 2.0.3 EXE 做差分核验：即使更新后的函数和全局地址并非统一偏移，只要全部语义关系和局部契约仍成立，就能重新定位；真正不兼容或含糊不清的版本仍会失败关闭。

日志还会明确输出 `由 Launcher 注入`、`由 .asi Bootstrapper 加载` 或 `source=unknown`。官方 Deploy ASI 的识别同时检查 `Reloaded.Mod.Loader.Bootstrapper.asi` 模块名与 `InitializeASI` 导出；不会把任意同名文件或普通 ASI 猜成 Reloaded-II Bootstrapper。
