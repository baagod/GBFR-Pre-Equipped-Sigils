
// 由最终 xlsx 部件（14 列）生成 sigils.json（mod 运行时数据）。
// 注意：name/zh 取「技能基础名」——gem.xlsx 的因子名带 ＋/罗马数字后缀，这里去掉
// （zh 去 ＋ 与中文罗马数字；name 再去英文罗马数字），与现 sigils.json 一致。
// 字段映射：A=key B=hash C=name D=zh E=skill1 F=sec(原 skill2) G=player
//           H=lot（空格分隔 → 数组）I=category L=one→special(=1) M=cap（数字）N=character（非空才输出）
// 输出：{ "sigils": [ { key, hash, name, zh, skill1, sec, category, player, special, cap, [character,] lot }, … ] }
// 行序 = 部件行序（确定性；mod 侧按 key/hash 查表，与行序无关）。
// Usage: node make-sigils-json.js <部件目录> <out.json>
const fs = require("fs");
const xl = require("./xlsx-lib");
const dir = process.argv[2];
const sheet = xl.readSheet(dir);
const hdr = sheet.rows[0].cells;
const COLS = { key: "key", hash: "hash", name: "name", zh: "zh", skill1: "skill1", sec: "skill2", player: "player", lot: "lot", category: "category", one: "one", cap: "cap", character: "character" };
const col = {};
for (const [k, name] of Object.entries(COLS)) {
  col[k] = xl.findColumn(hdr, name);
  if (!col[k]) throw new Error("make-sigils-json: 缺少列 " + name);
}

const ROMAN_CN = /[ⅠⅡⅢⅣⅤⅥⅦⅧⅨⅩ]+$/;
const ROMAN_EN = /[IVX]+$/;
const shortName = (s) => (s || "").replace(/[＋+]$/, "").replace(ROMAN_CN, "").replace(ROMAN_EN, "").trim();

const sigils = sheet.rows.slice(1).map((row) => {
  const v = (c) => row.cells[c] ?? "";
  const o = {
    key: v(col.key),
    hash: v(col.hash),
    name: shortName(v(col.name)),
    zh: shortName(v(col.zh)),
    skill1: v(col.skill1),
    sec: v(col.sec),
    category: v(col.category),
    player: v(col.player),
    special: v(col.one) === "1",
    cap: Number(v(col.cap)),
  };
  const character = v(col.character);
  if (character) o.character = character;
  o.lot = v(col.lot).trim() ? v(col.lot).trim().split(/\s+/) : [];
  return o;
});
fs.writeFileSync(process.argv[3], JSON.stringify({ sigils }, null, 2) + "\n", "utf8");
console.error("sigils.json：" + sigils.length + " 条 → " + process.argv[3]);
