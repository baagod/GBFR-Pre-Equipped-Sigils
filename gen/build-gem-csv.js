// Builds gem-full.csv from extracted gem table + .msg text + ids.txt.
// Layout: Key, Hash, Name, SkillId1, SkillId2, remaining fields...
// Name = actual text parsed from .msg; Hash = ids.txt[Key] (GEEN_* only).
// Usage: node build-gem-csv.js <gem.csv> <msgDir> <ids.txt> <out.csv>
const fs = require("fs");
const path = require("path");
const xl = require("./xlsx-lib");

// ---- .msg parser (see parse-msg.js) ----
function parseMsgText(dir) {
  const out = {};
  function walk(d) {
    return fs.readdirSync(d, { withFileTypes: true }).flatMap((e) =>
      e.isDirectory() ? walk(path.join(d, e.name)) : e.name.endsWith(".msg") ? [path.join(d, e.name)] : []);
  }
  for (const f of walk(dir)) {
    const b = fs.readFileSync(f);
    function pstring(pos) {
      if (pos >= b.length) return null;
      const h = b[pos];
      let len, nExtra;
      if (h >= 0xa0 && h < 0xc0) { len = h - 0xa0; nExtra = 0; }
      else if (h === 0xd9) { len = b[pos + 1]; nExtra = 1; }
      else if (h === 0xda) { len = b[pos + 1] * 0x100 + b[pos + 2]; nExtra = 2; }
      else return null;
      const start = pos + 1 + nExtra;
      if (!Number.isFinite(len) || start + len > b.length) return null;
      return { str: b.subarray(start, start + len), next: start + len };
    }
    let p = 0;
    for (;;) {
      const i = b.indexOf("id_hash_", p);
      if (i < 0) break;
      const key = pstring(i + 8);
      if (!key) { p = i + 8; continue; }
      const id = key.str.toString("utf8").replace(/\u0000+$/g, "").replace(/\u0000+/g, "");
      if (!id) { p = key.next; continue; } // 空 id 行无效，不入表（防 text[""] 覆盖成垃圾）
      const ti = b.indexOf("text_", key.next);
      let text = "";
      if (ti >= 0) { const s = pstring(ti + 5); if (s) text = s.str.toString("utf8").replace(/\u0000+$/g, ""); }
      out[id] = text;
      p = key.next;
    }
  }
  return out;
}

const csv = xl.readCsv(process.argv[2]);
const text = parseMsgText(process.argv[3]);
const ids = xl.readIds(process.argv[4]);
const hdr = csv[0];
const idx = {
  key: hdr.indexOf("Key"), name: hdr.indexOf("Name"), desc: hdr.indexOf("Description"),
  s1: hdr.indexOf("SkillId1"), s2: hdr.indexOf("SkillId2"),
};
const rest = hdr.filter((h, i) => ![idx.key, idx.name, idx.desc, idx.s1, idx.s2].includes(i));
const outHdr = ["Key", "Hash", "Name", "SkillId1", "SkillId2", ...rest];

let filled = 0, missing = 0, hashFound = 0, hashMiss = 0;
const lines = [outHdr.join(",")];
for (const r of csv.slice(1)) {
  if (!r[idx.key]) continue;
  const key = r[idx.key];
  const nameKey = r[idx.name];
  const name = nameKey ? (text[nameKey] ?? "") : "";
  if (name) filled++; else missing++;
  let hash = key;
  if (!/^[0-9A-F]{8}$/.test(key)) {
    const h = ids.get(key);
    if (h) { hash = h; hashFound++; } else { hashMiss++; }
  }
  lines.push([key, hash, name, r[idx.s1], r[idx.s2], ...rest.map((c) => r[hdr.indexOf(c)])].map(xl.csvEscape).join(","));
}
fs.writeFileSync(process.argv[5], lines.join("\n") + "\n", "utf8");
console.error(`rows: ${lines.length - 1}, name filled: ${filled}, missing: ${missing}, hash via ids: ${hashFound}, hash miss: ${hashMiss}`);
