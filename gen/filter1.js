// 筛选1（CSV 一次过，按顺序）：
//   ① 同名去重：最高 Rarity 版；同组有 plus 版则丢非 plus；plus 最高档多行全保留
//   ② 剔除 Name 空行（文本键不存在 = 隐藏因子）
//   ③ 剔除名单行（活动/联动，不参与配装）
// Usage: node filter1.js <in.csv> <out.csv> [keepmap.json]
const fs = require("fs");
const xl = require("./xlsx-lib");

const rows = xl.readCsv(process.argv[2]);
const hdr = rows[0];
const iC = hdr.indexOf("Name"), iL = hdr.indexOf("Rarity");
const data = rows.slice(1);
const iK = hdr.indexOf("Key");
const keepKeys = new Set();
if (process.argv[4] && fs.existsSync(process.argv[4])) {
  for (const k of JSON.parse(fs.readFileSync(process.argv[4], "utf8")).keep ?? []) keepKeys.add(k);
}

const DROP_NAMES = new Set(["7net", "幸运甘露", "修炼甘露", "强健甘露"]); // 活动/联动名单

const ROM = /[ⅠⅡⅢⅣⅤⅥⅦⅧⅨⅩ]+$/;
const base = (n) => n.replace(/[＋+]$/, "").replace(ROM, "").trim();
const groups = new Map();
for (let i = 0; i < data.length; i++) {
  const b = base(data[i][iC] ?? "");
  const g = b || "\u0000#" + i; // Name 空的行各自独立成组，原样保留
  (groups.get(g) ?? groups.set(g, []).get(g)).push(i);
}
const keepIdx = new Set();
for (let i = 0; i < data.length; i++) if (keepKeys.has(data[i][iK])) keepIdx.add(i); // keepmap 例外：条件保留行
for (const arr of groups.values()) {
  if (arr.length === 1) { keepIdx.add(arr[0]); continue; }
  const plus = arr.filter(i => /[＋+]$/.test(data[i][iC]));
  const pool = plus.length ? plus : arr;
  const maxR = Math.max(...pool.map(i => +data[i][iL]));
  for (const i of pool.filter(i => +data[i][iL] === maxR)) keepIdx.add(i);
}
const dedup = data.filter((_, i) => keepIdx.has(i));
const kept = dedup.filter((r) => (r[iC] ?? "").trim() && !DROP_NAMES.has(r[iC]));
const out = [hdr, ...kept].map(r => r.map(xl.csvEscape).join(","));
fs.writeFileSync(process.argv[3], out.join("\n") + "\n", "utf8");
console.error(`总行数 ${data.length} → 同名去重 ${dedup.length} → 剔除无效名 ${dedup.length - kept.length} → ${kept.length}（组数 ${groups.size}，keepmap 例外 ${keepKeys.size}）`);
