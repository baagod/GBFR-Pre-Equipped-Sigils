// 组内 max 制的 cap 映射：按同名组（base 名，去罗马/＋），组内所有行的 SkillId1/SkillId2 技能等级表 max 取最大；
// 无任何表（单组全无表）→ 15。输出 Key→cap JSON，供 add-cap.js 使用。
// 霸体案例：组 {044_04: SKILL_044_00=15 级表, 044_24: SKILL_023_00=1 行} → cap=15（游戏"1+15"=023(1)+044(15)）✓
// Usage: node build-capmap.js <gem-all.csv> <cap.csv> <out.json>
const fs = require("fs");
const xl = require("./xlsx-lib");
const gem = xl.readCsv(process.argv[2]).slice(1); // Key, Name(文本键), SkillId1, SkillId2
const capmap = new Map();
for (const l of fs.readFileSync(process.argv[3], "utf8").split(/\r?\n/).slice(1)) {
  const p = l.split(",");
  if (p.length >= 2 && p[0]) capmap.set(p[0].trim(), +p[1].trim());
}
const ROM = /[ⅠⅡⅢⅣⅤⅥⅦⅧⅨⅩ]+$/;
const base = (n) => (n || "").replace(/[＋+]$/, "").replace(ROM, "").trim();
// 组：base 名（文本键去 TXT_/前缀同构——用 SkillId1 推导？用 Name 键去后 3 位）
const groups = new Map();
for (const r of gem) {
  const key = r[0], namekey = r[1], s1 = r[2], s2 = r[3] || "";
  const b = base(namekey.replace(/^TXT_/, "").replace(/_[0-9]{2}$/, ""));
  (groups.get(b) ?? groups.set(b, []).get(b)).push({ key, s1, s2 });
}
const out = new Map();
for (const [b, arr] of groups) {
  let m = 0;
  for (const r of arr) {
    m = Math.max(m, capmap.get(r.s1) ?? 0); // 只按主技能（SkillId1）取 max；固定副（SkillId2）不参与
  }
  const cap = m > 1 ? m : 15; // 组内 max；单级/无表 → 15
  for (const r of arr) out.set(r.key, cap);
}
fs.writeFileSync(process.argv[4], JSON.stringify([...out]), "utf8");
console.error(`cap 映射 ${out.size} 个（组数 ${groups.size}）`);
