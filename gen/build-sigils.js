// 最简生成：gen/extracted 的库 + 文本 → sigils.xlsx（当前目录）与 sigils.json（无中间文件）。规则见 gen/sigils.xlsx 生成文档.md。
// Usage: node gen/build-sigils.js [--data D] [--ids F] [--out-xlsx P] [--out-json P]
"use strict";
const fs = require("fs");
const path = require("path");
const { DatabaseSync } = require("node:sqlite");
const xl = require("./xlsx-lib");

const S = __dirname;
const arg = (n, d) => { const i = process.argv.indexOf("--" + n); return i >= 0 ? process.argv[i + 1] : d; };
const DATA = arg("data", path.join(S, "extracted"));
const IDS = arg("ids", path.join(S, "GBFRDataTools", "Data", "ids.txt"));
const OUT_XLSX = arg("out-xlsx", "sigils.xlsx");   // 当前目录
const OUT_JSON = arg("out-json", path.join(S, "..", "GBFR.PreEquippedSigils", "sigils.json"));

// ---------- 词典 / 文本 ----------
const ids = xl.readIds(IDS);
const toHash = (v) => { const s = String(v ?? "").trim(); if (!s) return ""; return /^[0-9A-F]{8}$/.test(s) ? s : (ids.get(s) ?? s); };
function parseMsg(dir) {
  const out = {};
  const walk = (d) => fs.readdirSync(d, { withFileTypes: true }).flatMap((e) => e.isDirectory() ? walk(path.join(d, e.name)) : e.name.endsWith(".msg") ? [path.join(d, e.name)] : []);
  const pstring = (b, pos) => {
    if (pos >= b.length) return null;
    const h = b[pos]; let len, nExtra;
    if (h >= 0xa0 && h < 0xc0) { len = h - 0xa0; nExtra = 0; }
    else if (h === 0xd9) { len = b[pos + 1]; nExtra = 1; }
    else if (h === 0xda) { len = b[pos + 1] * 0x100 + b[pos + 2]; nExtra = 2; }
    else return null;
    const start = pos + 1 + nExtra;
    if (!Number.isFinite(len) || start + len > b.length) return null;
    return { str: b.subarray(start, start + len), next: start + len };
  };
  for (const f of walk(dir)) {
    const b = fs.readFileSync(f);
    let p = 0;
    for (;;) {
      const i = b.indexOf("id_hash_", p);
      if (i < 0) break;
      const key = pstring(b, i + 8);
      if (!key) { p = i + 8; continue; }
      const id = key.str.toString("utf8").replace(/\u0000+$/g, "").replace(/\u0000+/g, "");
      if (!id) { p = key.next; continue; }
      const ti = b.indexOf("text_", key.next);
      let text = "";
      if (ti >= 0) { const s = pstring(b, ti + 5); if (s) text = s.str.toString("utf8").replace(/\u0000+$/g, ""); }
      out[id] = text;
      p = ti >= 0 ? ti + 5 : key.next;
    }
  }
  return out;
}
const en = parseMsg(path.join(DATA, "system", "table", "text", "en"));
const cs = parseMsg(path.join(DATA, "system", "table", "text", "cs"));

// ---------- 库 ----------
const db = new DatabaseSync(path.join(DATA, "gbfr.db"));
const gem = db.prepare("select Key, Name, SkillId1, SkillId2, PlayerReq, SkillTypeLotIdForRandom2ndSkill lot, Category, Rarity, CanGemMix, CanOnlyHoldOne from gem").all();
const caps = new Map();
for (const r of db.prepare("select Key, max(Level) m from skill_status group by Key").all()) { caps.set(r.Key, r.m); const h = ids.get(r.Key); if (h && !caps.has(h)) caps.set(h, r.m); }
const sub = new Map();
for (const r of db.prepare("select Key, SkillId from skill_lot").all()) { const k = String(r.Key ?? "").trim(); if (!k) continue; if (!sub.has(k)) sub.set(k, []); sub.get(k).push(toHash(r.SkillId)); }
const lotmap = new Map();
for (const r of db.prepare("select * from skill_type_lot").all()) {
  const lot = String(r.Key ?? "").trim(); if (!lot) continue;
  const list = [], seen = new Set();
  for (let i = 1; i <= 6; i++) { const k = String(r["SkillLotId" + i] ?? "").trim(); if (!k) continue; for (const s of sub.get(k) ?? []) if (s && !seen.has(s)) { seen.add(s); list.push(s); } }
  lotmap.set(lot, list);
}
// 引用扫描：全库所有表（排除 gem）
const strMap = new Map(), numMap = new Map();
for (const g of gem) { strMap.set(g.Key.toUpperCase(), g.Key); const h = toHash(g.Key); if (/^[0-9A-F]{8}$/.test(h)) numMap.set(parseInt(h, 16) >>> 0, g.Key); }
const refs = new Map();
for (const t of db.prepare("select name from sqlite_master where type='table'").all().map((r) => r.name)) {
  if (t === "gem") continue;
  const cols = db.prepare('pragma table_info("' + t + '")').all().map((c) => c.name);
  for (const row of db.prepare('select * from "' + t + '"').all()) for (const c of cols) {
    const v = row[c]; if (v === null || v === undefined || v === "") continue;
    const k = typeof v === "string" ? strMap.get(v.trim().toUpperCase()) : typeof v === "number" ? numMap.get(v >>> 0) : undefined;
    if (k !== undefined) refs.set(k, (refs.get(k) ?? 0) + 1);
  }
}

// ---------- 行（14 列）----------
const base = (n) => String(n ?? "").replace(/[＋+]$/, "").replace(/[ⅠⅡⅢⅣⅤⅥⅦⅧⅨⅩ]+$/, "").trim();
const capOf = (sk) => { const m = caps.get(sk); return m === undefined || m < 2 ? 15 : m; };
const rows = gem.map((g) => {
  const key = String(g.Key ?? "").trim();
  const lotId = String(g.lot ?? "").trim();
  const zh = cs[g.Name] ?? "";
  return {
    key, hash: toHash(key),
    name: en[g.Name] || zh, zh,
    skill1: toHash(g.SkillId1), skill2: toHash(g.SkillId2),
    player: String(g.PlayerReq ?? "").trim(),
    lotId, lot: lotId && lotId !== "-1" ? (lotmap.get(lotId) ?? []).join(" ") : "",
    category: String(g.Category ?? ""), rarity: String(g.Rarity ?? ""),
    mix: String(g.CanGemMix ?? ""), onlyone: String(g.CanOnlyHoldOne ?? ""),
    cap: capOf(toHash(g.SkillId1)),
    character: g.PlayerReq ? (ids.get(String(g.PlayerReq).trim()) ?? "") : "",
    refs: refs.get(key) ?? 0,
  };
});

// ---------- keepmap（引用 + 同族池例外）----------
const keepKeys = new Set(), dropKeys = new Set();
const anchor = new Map();
for (const r of rows) {
  if (r.skill2 || r.mix !== "1" || r.player || r.onlyone !== "0" || !r.lotId || r.lotId === "-1" || !r.refs) continue;
  const cur = anchor.get(r.skill1);
  if (!cur || r.refs > cur.refs) anchor.set(r.skill1, r);
}
for (const r of rows) { const a = anchor.get(r.skill1); if (r.skill2 && r.refs && a && !(lotmap.get(a.lotId) ?? []).includes(r.skill2)) keepKeys.add(r.key); }
{
  const byKey = new Map(rows.map((r) => [r.key, r]));
  const size = new Map();
  for (const r of rows) { const b = base(r.name); size.set(b, (size.get(b) ?? 0) + 1); }
  for (const r of rows) {
    const m = /^GEEN_(\d+)_24$/.exec(r.key);
    if (!m || r.mix !== "1" || r.refs) continue;
    const b = byKey.get("GEEN_" + m[1] + "_04");
    if (!b || b.skill2 || !b.refs || size.get(base(r.name)) !== 2) continue;
    keepKeys.add(b.key); dropKeys.add(r.key);
  }
}

// ---------- 筛选 ----------
const DROP_NAMES = new Set(["7net", "幸运甘露", "修炼甘露", "强健甘露"]);
let cur = rows.slice();
{ // 规则 1-3：同名组最高等级（plus 优先）+ 无效名/名单
  const groups = new Map();
  cur.forEach((r, i) => { const b = base(r.zh) || "\u0000#" + i; (groups.get(b) ?? groups.set(b, []).get(b)).push(i); });
  const keepIdx = new Set();
  cur.forEach((r, i) => { if (keepKeys.has(r.key)) keepIdx.add(i); });
  for (const arr of groups.values()) {
    if (arr.length === 1) { keepIdx.add(arr[0]); continue; }
    const plus = arr.filter((i) => /[＋+]$/.test(cur[i].zh));
    const pool = plus.length ? plus : arr;
    const maxR = Math.max(...pool.map((i) => +cur[i].rarity));
    for (const i of pool.filter((i) => +cur[i].rarity === maxR)) keepIdx.add(i);
  }
  cur = cur.filter((r, i) => keepIdx.has(i) && (r.name ?? "").trim() && !DROP_NAMES.has(r.zh));
}
{ // 规则 4-5：同名组去固定副 → 去 lot=-1（整组兜底）
  const byGroup = (list, ok, keys) => {
    const g = new Map();
    list.forEach((r, i) => { const b = base(r.zh) || "\u0000#" + i; (g.get(b) ?? g.set(b, []).get(b)).push(r); });
    const out = [];
    for (const r of list) if (keys.has(r.key)) out.push(r);
    for (const arr of g.values()) {
      if (arr.length === 1) { if (!out.includes(arr[0])) out.push(arr[0]); continue; }
      const pass = arr.filter(ok);
      for (const r of pass.length ? pass : arr) if (!out.includes(r)) out.push(r);
    }
    return list.filter((r) => out.includes(r));
  };
  cur = byGroup(cur, (r) => !(r.skill2 ?? "").trim(), keepKeys);
  cur = byGroup(cur, (r) => (r.lotId ?? "") !== "-1", keepKeys);
}
const DEAD = new Set(["70051799","80C94A24","2679A4F0","B506B258","98E33F8E","1C166ABC","0890CD68","33AC7F74","3ED16FB2","04AC2281","6CBA6B0D","AB70208C","D340651C","3BA37635","837B3D64","BA16F729","BB49C8F6","113035D8","8E20B20C","6DB307D5","49EBEBEB","GEEN_146_34","GEEN_233_34","GEEN_234_34"]);
for (const k of dropKeys) DEAD.add(k);
cur = cur.filter((r) => !((r.player ?? "").trim() && r.onlyone === "1") && !DEAD.has(r.key)); // 规则 3 觉醒 + 死条目

// ---------- lot 填充 / 清空 ----------
for (const r of cur) {
  const filled = r.lotId && r.lotId !== "-1" && (r.player || (r.mix === "1" && r.onlyone === "0" && !r.skill2));
  r.lot = filled ? (lotmap.get(r.lotId) ?? []).join(" ") : "";
}

// ---------- 表尾 12 条非物品技能条目 ----------
const SKILLS = [["SKILL_113_00","57E8A93F","因子强化","2"],["SKILL_143_00","40223C28","浩劫","25"],["1E1CECCE","1E1CECCE","浩劫新星","35"],["SKILL_311_00","3B71AF12","伤害上限·轰天","15"],["SKILL_312_00","FFF8CF64","伤害上限·疾天","15"],["SKILL_314_00","AEFEB1BC","伤害上限·苍天","15"],["0151CF9E","0151CF9E","伤害上限·红天","15"],["235D86EF","235D86EF","超新星","15"],["BBD77C33","BBD77C33","超凡强击","15"],["020DB733","020DB733","超凡技艺","15"],["3F682593","3F682593","超凡奥秘","15"],["79027FC8","79027FC8","超凡破限","55"]];
const empty = "";
for (const [key, hash, zh, cap] of SKILLS) {
  const sk = db.prepare("select Name from skill where Key = ?").get(key);
  cur.push({ key, hash, name: (sk && en[sk.Name]) || zh, zh, skill1: hash, skill2: empty, player: empty, lotId: empty, lot: empty, category: empty, rarity: empty, mix: empty, onlyone: empty, cap, character: empty, refs: 0, skill: true });
}

// ---------- 排序 + 分组（重名行组内置顶）----------
const groupOf = (r) => r.hash === r.skill1 ? 5 : r.mix === "0" ? 1 : r.onlyone === "1" ? 3 : r.player ? 4 : 2;
const seq = [1, 2, 3, 4, 5];
const items = cur.filter((r) => !r.skill);   // 12 条技能条目按追加顺序留在表尾，不参与排序
const skills = cur.filter((r) => r.skill);
const sorted = items.slice().sort((a, b) => (a.name < b.name ? -1 : a.name > b.name ? 1 : 0));
const out = [];
for (const g of seq) {
  const arr = sorted.filter((r) => groupOf(r) === g);
  const cnt = new Map();
  for (const r of arr) cnt.set(r.name, (cnt.get(r.name) ?? 0) + 1);
  out.push(...arr.filter((r) => cnt.get(r.name) > 1), ...arr.filter((r) => cnt.get(r.name) === 1));
}
out.push(...skills);

// ---------- 写 xlsx ----------
const HEADER = ["key","hash","name","zh","skill1","skill2","player","lot","category","rarity","mix","onlyone","cap","character"];
const WIDTHS = [13.4285714285714,11.2857142857143,30.5714285714286,21.4285714285714,11.1428571428571,10.4285714285714,8.71428571428571,10.7142857142857,11.7142857142857,8.71428571428571,6.71428571428571,10.7142857142857,6.71428571428571,12.7142857142857];
const parts = fs.mkdtempSync(path.join(require("os").tmpdir(), "sigils-"));
const esc = (s) => String(s).replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
const shared = [], sIdx = new Map();
const sid = (v) => { const k = String(v); if (sIdx.has(k)) return sIdx.get(k); sIdx.set(k, shared.length); shared.push(k); return shared.length - 1; };
const col = (i) => xl.colName(i);
const rowsXml = [];
rowsXml.push('<row r="1" ht="20" customHeight="1" spans="1:14">' + HEADER.map((h, i) => '<c r="' + col(i) + '1" s="1" t="s"><v>' + sid(h) + "</v></c>").join("") + "</row>");
out.forEach((r, ri) => {
  const rn = ri + 2;
  const s = 1 + groupOf(r); // 2=白 3=蓝 4=橙 5=紫 6=粉
  const vals = [r.key, r.hash, r.name, r.zh, r.skill1, r.skill2, r.player, r.lot, r.category, r.rarity, r.mix, r.onlyone, String(r.cap), r.character];
  rowsXml.push('<row r="' + rn + '" ht="20" customHeight="1" spans="1:14">' + vals.map((v, i) => v === "" || v === undefined ? '<c r="' + col(i) + rn + '" s="' + s + '"/>' : '<c r="' + col(i) + rn + '" s="' + s + '" t="s"><v>' + sid(v) + "</v></c>").join("") + "</row>");
});
const mk = (p, s) => { fs.mkdirSync(path.dirname(path.join(parts, p)), { recursive: true }); fs.writeFileSync(path.join(parts, p), s, "utf8"); };
mk("[Content_Types].xml", '<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/><Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/><Override PartName="/xl/sharedStrings.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sharedStrings+xml"/></Types>');
mk("_rels/.rels", '<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>');
mk("xl/workbook.xml", '<?xml version="1.0" encoding="UTF-8" standalone="yes"?><workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="Sheet1" sheetId="1" r:id="rId1"/></sheets></workbook>');
mk("xl/_rels/workbook.xml.rels", '<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/><Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/><Relationship Id="rId3" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/sharedStrings" Target="sharedStrings.xml"/></Relationships>');
const FONTS = ['<font><sz val="11"/><name val="Calibri"/><charset val="134"/></font>', '<font><b/><sz val="10"/><color rgb="FFFFFFFF"/><name val="微软雅黑"/><charset val="134"/></font>', '<font><sz val="10"/><name val="微软雅黑"/><charset val="134"/></font>', '<font><sz val="10"/><color rgb="FFFFFFFF"/><name val="微软雅黑"/><charset val="134"/></font>'];
const FILLS = ['<fill><patternFill patternType="none"/></fill>', '<fill><patternFill patternType="gray125"/></fill>', '<fill><patternFill patternType="solid"><fgColor rgb="FF4472C4"/><bgColor indexed="64"/></patternFill></fill>', '<fill><patternFill patternType="solid"><fgColor rgb="FF9BC2E6"/><bgColor indexed="64"/></patternFill></fill>', '<fill><patternFill patternType="solid"><fgColor rgb="FFFFC000"/><bgColor indexed="64"/></patternFill></fill>', '<fill><patternFill patternType="solid"><fgColor rgb="FF7030A0"/><bgColor indexed="64"/></patternFill></fill>', '<fill><patternFill patternType="solid"><fgColor rgb="FFF4B084"/><bgColor indexed="64"/></patternFill></fill>'];
const BORDER = '<border><left style="thin"><color auto="1"/></left><right style="thin"><color auto="1"/></right><top style="thin"><color auto="1"/></top><bottom style="thin"><color auto="1"/></bottom><diagonal/></border>';
const AL = '<alignment horizontal="left" vertical="center"/>';
const XFS = ['<xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0" applyFont="1"><alignment vertical="center"/></xf>',
  '<xf numFmtId="0" fontId="1" fillId="2" borderId="1" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1">' + AL + "</xf>",
  '<xf numFmtId="0" fontId="2" fillId="0" borderId="1" xfId="0" applyFont="1" applyBorder="1" applyAlignment="1">' + AL + "</xf>",
  '<xf numFmtId="0" fontId="2" fillId="3" borderId="1" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1">' + AL + "</xf>",
  '<xf numFmtId="0" fontId="2" fillId="4" borderId="1" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1">' + AL + "</xf>",
  '<xf numFmtId="0" fontId="3" fillId="5" borderId="1" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1">' + AL + "</xf>",
  '<xf numFmtId="0" fontId="2" fillId="6" borderId="1" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1">' + AL + "</xf>"];
mk("xl/styles.xml", '<?xml version="1.0" encoding="UTF-8" standalone="yes"?><styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><fonts count="' + FONTS.length + '">' + FONTS.join("") + '</fonts><fills count="' + FILLS.length + '">' + FILLS.join("") + '</fills><borders count="2"><border/>' + BORDER + '</borders><cellStyleXfs count="1"><xf/></cellStyleXfs><cellXfs count="' + XFS.length + '">' + XFS.join("") + "</cellXfs></styleSheet>");
const cols = "<cols>" + WIDTHS.map((w, i) => '<col min="' + (i + 1) + '" max="' + (i + 1) + '" width="' + w + '" customWidth="1"/>').join("") + "</cols>";
mk("xl/worksheets/sheet1.xml", '<?xml version="1.0" encoding="UTF-8" standalone="yes"?><worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><dimension ref="A1:N' + (out.length + 1) + '"/><sheetViews><sheetView tabSelected="1" workbookViewId="0"><pane ySplit="1" topLeftCell="A2" activePane="bottomLeft" state="frozen"/><selection pane="bottomLeft" activeCell="A2" sqref="A2"/></sheetView></sheetViews><sheetFormatPr defaultRowHeight="15"/>' + cols + '<sheetData>' + rowsXml.join("") + '</sheetData><pageMargins left="0.75" right="0.75" top="1" bottom="1" header="0.5" footer="0.5"/><headerFooter/></worksheet>');
mk("xl/sharedStrings.xml", '<?xml version="1.0" encoding="UTF-8" standalone="yes"?><sst xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" count="' + shared.length + '" uniqueCount="' + shared.length + '">' + shared.map((v) => "<si><t xml:space=\"preserve\">" + esc(v) + "</t></si>").join("") + "</sst>");
xl.packXlsx(parts, OUT_XLSX);
fs.rmSync(parts, { recursive: true, force: true });

// ---------- 写 sigils.json ----------
const shortName = (s) => String(s ?? "").replace(/[＋+]$/, "").replace(/[ⅠⅡⅢⅣⅤⅥⅦⅧⅨⅩ]+$/, "").replace(/[IVX]+$/, "").trim();
const sigils = out.map((r) => { const o = { key: r.key, hash: r.hash, name: shortName(r.name), zh: shortName(r.zh), skill1: r.skill1, sec: r.skill2, mix: r.mix, category: r.category, player: r.player, special: r.onlyone === "1", cap: Number(r.cap) }; if (r.character) o.character = r.character; o.lot = r.lot ? r.lot.split(/\s+/) : []; return o; });
fs.mkdirSync(path.dirname(path.resolve(OUT_JSON)), { recursive: true });
fs.writeFileSync(OUT_JSON, JSON.stringify({ sigils }, null, 2) + "\n", "utf8");
console.log("sigils.xlsx -> " + OUT_XLSX + "（" + out.length + " 行）");
console.log("sigils.json -> " + OUT_JSON + "（" + sigils.length + " 条）");