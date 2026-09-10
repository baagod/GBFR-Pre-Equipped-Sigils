// 按「活/死 + 同族池」判据算出条件保留/删除的因子 key（供 filter1/filter-nos2/filter-noslot 的 keep、filter-keys 的 drop）：
//   keep ① 被产出池引用且副技能不在同族 lot 池内的固定副行（F3/F4 的例外，§8.10）
//        ② 同名组恰为 {_04 基础版, _24 加号版} 两行、_24 零引用而 _04 有引用时的 _04（F9 换版）
//   drop ② 对应的 _24 行
// 依据：reward_lot.GemId + gacha_lot.ItemId（产出池引用）、skill_type_lot/skill_lot 展开的池。
// 注意：第 1 个参数须为 build-gem-csv.js 的输出（Name=显示名，用于同名组判定）。
// Usage: node build-keepmap.js <gem-full.csv> <ids.txt> <lotmap.json> <reward_lot.csv> <gacha_lot.csv> <out.json>
const fs = require("fs");
const xl = require("./xlsx-lib");

const gem = xl.readCsv(process.argv[2]);
const hdr = gem[0];
const col = (name) => hdr.indexOf(name);
const ids = xl.readIds(process.argv[3]);
const toHash = (v) => { const s = (v || "").trim(); if (!s) return ""; return /^[0-9A-F]{8}$/.test(s) ? s : (ids.get(s) ?? s); };
const lotmap = JSON.parse(fs.readFileSync(process.argv[4], "utf8"));

const refs = new Map();
for (const [file, column] of [[process.argv[5], "GemId"], [process.argv[6], "ItemId"]]) {
  const rows = xl.readCsv(file);
  const ci = rows[0].indexOf(column);
  if (ci < 0) continue;
  for (const r of rows.slice(1)) { const k = (r[ci] || "").trim(); if (k) refs.set(k, (refs.get(k) || 0) + 1); }
}

const cK = col("Key"), cN = col("Name"), cS1 = col("SkillId1"), cS2 = col("SkillId2");
const cReq = col("PlayerReq"), cMix = col("CanGemMix"), cOne = col("CanOnlyHoldOne"), cLot = col("SkillTypeLotIdForRandom2ndSkill");
const rows = gem.slice(1).filter((r) => r[cK]).map((r) => ({
  key: r[cK], name: r[cN] || "", s1: toHash(r[cS1]), s2: toHash(r[cS2]),
  req: (r[cReq] || "").trim(), mix: r[cMix], one: r[cOne], lot: (r[cLot] || "").trim(),
  refs: refs.get(r[cK]) || 0,
}));
const byKey = new Map(rows.map((r) => [r.key, r]));

// 同族池 = 同主技能、无副技能、被引用的池版因子（lot≠-1）；多个取引用最多者
const anchor = new Map();
for (const r of rows) {
  if (r.s2 || r.mix !== "1" || r.req || r.one !== "0" || r.lot === "" || r.lot === "-1" || !r.refs) continue;
  const cur = anchor.get(r.s1);
  if (!cur || r.refs > cur.refs) anchor.set(r.s1, r);
}

const keep = [];
const drop = [];
for (const r of rows) {
  if (!r.s2 || !r.refs) continue;
  const a = anchor.get(r.s1);
  if (a && !(lotmap[a.lot] || []).includes(r.s2)) keep.push(r.key);
}

const base = (n) => n.replace(/[＋+]$/, "").replace(/[ⅠⅡⅢⅣⅤⅥⅦⅧⅨⅩ]+$/, "").trim();
const groupSize = new Map();
for (const r of rows) { const b = base(r.name); groupSize.set(b, (groupSize.get(b) || 0) + 1); }
for (const r of rows) {
  const m = /^GEEN_(\d+)_24$/.exec(r.key);
  if (!m || r.mix !== "1" || r.refs) continue;
  const b = byKey.get("GEEN_" + m[1] + "_04");
  if (!b || b.s2 || !b.refs || groupSize.get(base(r.name)) !== 2) continue;
  keep.push(b.key);
  drop.push(r.key);
}

const uniq = (a) => [...new Set(a)];
fs.writeFileSync(process.argv[7], JSON.stringify({ keep: uniq(keep), drop: uniq(drop) }, null, 2) + "\n", "utf8");
console.error(`keep ${uniq(keep).length}: ${uniq(keep).join(",")}；drop ${uniq(drop).length}: ${uniq(drop).join(",")}`);
