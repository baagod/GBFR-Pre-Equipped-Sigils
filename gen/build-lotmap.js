// lot → 技能 hash 列表：双层展开（skill_type_lot 子池 → skill_lot 技能，合并去重），供 add-lot.js / build-keepmap.js 使用。
// Usage: node build-lotmap.js <skill_type_lot.csv> <skill_lot.csv> <ids.txt> <out.json>
const fs = require("fs");
const xl = require("./xlsx-lib");

const typeRows = xl.readCsv(process.argv[2]);
const skillRows = xl.readCsv(process.argv[3]);
const ids = xl.readIds(process.argv[4]);
const toHash = (v) => { const s = (v || "").trim(); if (!s) return ""; return /^[0-9A-F]{8}$/.test(s) ? s : (ids.get(s) ?? s); };

const sub = new Map();
for (const r of skillRows.slice(1)) {
  const key = (r[0] || "").trim();
  if (!key) continue;
  if (!sub.has(key)) sub.set(key, []);
  sub.get(key).push(toHash(r[1]));
}

const hdr = typeRows[0];
const cLot = hdr.indexOf("Key");
const subCols = [1, 2, 3, 4, 5, 6].map((n) => hdr.indexOf("SkillLotId" + n)).filter((i) => i >= 0);
const out = {};
for (const r of typeRows.slice(1)) {
  const lot = (r[cLot] || "").trim();
  if (!lot) continue;
  const list = [];
  const seen = new Set();
  for (const ci of subCols) for (const s of sub.get(r[ci]) || []) if (s && !seen.has(s)) { seen.add(s); list.push(s); }
  out[lot] = list;
}
fs.writeFileSync(process.argv[5], JSON.stringify(out), "utf8");
console.error(`lot 映射 ${Object.keys(out).length} 个（lot6=${(out["6"] || []).length} lot7=${(out["7"] || []).length} lot16=${(out["16"] || []).length}）`);
