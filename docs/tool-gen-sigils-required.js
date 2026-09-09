// 已停用（不再运行，保留作历史说明）。
//
// 原因：本脚本基于旧 sigils.json 布局（"gem"/"player"/"sort" 字段；专属行
// 的 hash 必为 3 内建模板 gem 之一）编写。数据改为 gem.xlsx 表头字段
// （key/hash/skill1/lot/sec，见 gen\数据表说明.md §2）并新增普通专属因子后：
//   - "sort" 行已删除 → 旧版直接抛 "no sort line"；
//   - 即使改锚点，"专属行必为模板 3 gem" 这一剔除合体版的假设也已失效：
//     实测把新增的普通专属因子误判为合体版并删除（3 行）。
// sigils.json 现由 gen 管道（gem.xlsx → CSV/筛选 → sigils.json）生成，
// character 字段已内嵌；字符专属表由 docs/tool-gen-loadout.ps1 生成。
// 若需要重新规范化 sigils.json，请从 gem.xlsx 重建，勿再运行本脚本。

console.log("tool-gen-sigils-required.js 已停用（原因见文件头注释）；未做任何修改。");
process.exit(0);
