// 行级剔除（xlsx 一次过，按顺序）：
//   ① 觉醒：PlayerReq 非空 且 CanOnlyHoldOne=1
//   ② 死条目：_34/hash 形态同因子版本（产出池零引用）+ keepmap.drop（霸体＋044_24 换版）
// 行序不变。
// Usage: node filter-drop.js <解包目录> [keepmap.json]
const fs = require("fs");
const xl = require("./xlsx-lib");
const dir = process.argv[2];

const sheet = xl.readSheet(dir);
const rows = sheet.rows;
const cKey = xl.findColumn(rows[0].cells, "Key");
const cReq = xl.findColumn(rows[0].cells, "PlayerReq");
const cOne = xl.findColumn(rows[0].cells, "CanOnlyHoldOne");

// 21 个同名 hash 版 + 3 个明文 _34（均有 GEEN_xxx_24 配对、全库零引用）
const DEAD = new Set([
  "70051799","80C94A24","2679A4F0","B506B258","98E33F8E","1C166ABC","0890CD68","33AC7F74",
  "3ED16FB2","04AC2281","6CBA6B0D","AB70208C","D340651C","3BA37635","837B3D64","BA16F729","BB49C8F6",
  "113035D8","8E20B20C","6DB307D5","49EBEBEB",
  "GEEN_146_34","GEEN_233_34","GEEN_234_34",
]);
if (process.argv[3] && fs.existsSync(process.argv[3])) {
  for (const k of JSON.parse(fs.readFileSync(process.argv[3], "utf8")).drop ?? []) DEAD.add(k);
}

let awake = 0, dead = 0;
const outRows = [rows[0].xml];
for (let i = 1; i < rows.length; i++) {
  const r = rows[i];
  if ((r.cells[cReq] ?? "").trim() && r.cells[cOne] === "1") { awake++; continue; }
  if (DEAD.has(r.cells[cKey])) { dead++; continue; }
  outRows.push(xl.renumberRow(r.xml, outRows.length + 1));
}
xl.writeSheet(dir, sheet, outRows);
console.error(`行级剔除：觉醒 -${awake}、死条目 -${dead} → ${outRows.length - 1} 行`);
