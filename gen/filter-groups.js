// 同名组筛选（xlsx 一次过，按顺序）：
//   ① 去固定副：同名组内只留 SkillId2 空的行；组内全为有副版时整组保留
//   ② 去 lot=-1：同名组内只留 lot≠-1 的行；组内全 -1 时整组保留；单行组不适用
// keepmap 例外在两轮都放行；行序不变。
// Usage: node filter-groups.js <解包目录> [keepmap.json]
const fs = require("fs");
const xl = require("./xlsx-lib");
const dir = process.argv[2];

const sheet = xl.readSheet(dir);
const rows = sheet.rows;
const cName = xl.findColumn(rows[0].cells, "Name");
const cS2 = xl.findColumn(rows[0].cells, "SkillId2");
const cLot = xl.findColumn(rows[0].cells, "SkillTypeLotIdForRandom2ndSkill");
const cKey = xl.findColumn(rows[0].cells, "Key");
const keepKeys = new Set();
if (process.argv[3] && fs.existsSync(process.argv[3])) {
  for (const k of JSON.parse(fs.readFileSync(process.argv[3], "utf8")).keep ?? []) keepKeys.add(k);
}

const ROM = /[ⅠⅡⅢⅣⅤⅥⅦⅧⅨⅩ]+$/;
const base = (n) => (n || "").replace(/[＋+]$/, "").replace(ROM, "").trim();
function keepByGroup(list, ok) {
  const groups = new Map();
  for (let i = 0; i < list.length; i++) {
    const b = base(list[i].cells[cName]) || "\u0000#" + i;
    (groups.get(b) ?? groups.set(b, []).get(b)).push(i);
  }
  const keep = new Set();
  for (let i = 0; i < list.length; i++) if (keepKeys.has(list[i].cells[cKey])) keep.add(i); // keepmap 例外
  for (const arr of groups.values()) {
    if (arr.length === 1) { keep.add(arr[0]); continue; }
    const pass = arr.filter((i) => ok(list[i]));
    if (!pass.length) { arr.forEach((i) => keep.add(i)); continue; } // 整组保留兜底
    for (const i of pass) keep.add(i);
  }
  return keep;
}

const data = rows.slice(1);
const keepS2 = keepByGroup(data, (r) => !(r.cells[cS2] ?? "").trim());
const afterS2 = data.filter((_, i) => keepS2.has(i));
const keepLot = keepByGroup(afterS2, (r) => (r.cells[cLot] ?? "") !== "-1");
const kept = afterS2.filter((_, i) => keepLot.has(i));

const outRows = [rows[0].xml];
for (const r of kept) outRows.push(xl.renumberRow(r.xml, outRows.length + 1));
xl.writeSheet(dir, sheet, outRows);
console.error(`同名组筛选：${data.length} → 去固定副 ${afterS2.length}（-${data.length - afterS2.length}）→ 去 lot=-1 ${kept.length}（-${afterS2.length - kept.length}）`);
