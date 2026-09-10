// Parses GBF Relink .msg text tables into an id_hash -> text map (JSON).
// Field = [lenHdr][bytes]: 0xA0..0xBF = len-0xA0; 0xD9 = len next byte; 0xDA = len next 2 bytes (BE).
// Rows are back-to-back: ... "id_hash_" <key> ... "text_" <value> ... next row starts with [DF 00 00 00 01].
// Robust via anchors; unsupported/foreign rows are skipped.
// Usage: node parse-msg.js <file.msg|dir> [out.json]  (default stdout = JSON map)
const fs = require("fs");
const path = require("path");

function parseFile(file) {
const b = fs.readFileSync(file);

function pstring(pos) {
  if (pos >= b.length) return null;
  const h = b[pos];
  let len, nExtra;
  if (h >= 0xa0 && h < 0xc0) { len = h - 0xa0; nExtra = 0; }
  else if (h === 0xd9) { len = b[pos + 1]; nExtra = 1; }
  else if (h === 0xda) { len = b[pos + 1] * 0x100 + b[pos + 2]; nExtra = 2; }
  else return null;
  const start = pos + 1 + nExtra;
  // A truncated file leaves len undefined (NaN) and next would be NaN, which
  // makes indexOf(pat, NaN) restart from 0 and loop forever.
  if (!Number.isFinite(len) || start + len > b.length) return null;
  return { str: b.subarray(start, start + len), next: start + len };
}
const find = (pat, from) => b.indexOf(pat, from);

const out = {};
let rows = 0;
let p = 0;
for (;;) {
  const i = find(Buffer.from("id_hash_"), p);
  if (i < 0) break;
  const key = pstring(i + 8);
  if (!key) { p = i + 8; continue; }
  const id = key.str.toString("utf8").replace(/\u0000+$/g, "").replace(/\u0000+/g, "");
  const df = find(Buffer.from([0xdf, 0, 0, 0]), key.next);
  const ti = find(Buffer.from("text_"), key.next);
  let text = "";
  if (ti >= 0 && (df < 0 || ti < df)) {
    const s = pstring(ti + 5);
    if (s) text = s.str.toString("utf8").replace(/\u0000+$/g, "");
    else {
      const vEnd = df < 0 ? b.length : df;
      text = b.subarray(ti + 6, vEnd).toString("utf8").replace(/[\u0000\uFFFD]+$/g, "");
    }
  }
  out[id] = text;
  rows++;
  p = (df < 0 ? key.next : df + 4);
}
return { out, rows };
}

const src = process.argv[2];
const files = fs.statSync(src).isDirectory()
  ? (function walk(d) {
      return fs.readdirSync(d, { withFileTypes: true }).flatMap((e) =>
        e.isDirectory() ? walk(path.join(d, e.name)) : e.name.endsWith(".msg") ? [path.join(d, e.name)] : []);
    })(src)
  : [src];

const merged = {};
let total = 0;
for (const f of files) {
  const r = parseFile(f);
  Object.assign(merged, r.out);
  total += r.rows;
  console.error(`${f.split(path.sep).pop()}: ${r.rows} rows`);
}
const outStr = JSON.stringify(merged);
if (process.argv[3]) fs.writeFileSync(process.argv[3], outStr, "utf8");
else process.stdout.write(outStr);
console.error(`total rows: ${total}, unique keys: ${Object.keys(merged).length}${total === 0 ? " (FAILED - check format)" : " OK"}`);
