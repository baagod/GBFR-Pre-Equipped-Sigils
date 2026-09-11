// Shared helpers for the gen pipeline: xlsx part I/O and CSV parsing.
// All scripts operate on an unzipped workbook directory. Rows are scanned with
// a tag-aware parser (not a /<row>.*<\/row>/ regex), so a literal "</row>"
// inside a cell's text can never split a row.
// Usage: const xl = require("./xlsx-lib");
"use strict";
const fs = require("fs");
const path = require("path");
const os = require("os");
const cp = require("child_process");

const SHEET_PART = "xl/worksheets/sheet1.xml";
const SHARED_PART = "xl/sharedStrings.xml";

const sheetPath = (dir) => path.join(dir, SHEET_PART);
const sharedStringsPath = (dir) => path.join(dir, SHARED_PART);

/** XML-escapes a value for a <t> text node (matches the scripts' siXml). */
function escapeXml(value) {
  return String(value).replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
}

/** Reads xl/sharedStrings.xml into a string[] (empty when the file is absent). */
function readSharedStrings(dir) {
  const file = sharedStringsPath(dir);
  if (!fs.existsSync(file)) return [];
  return [...fs.readFileSync(file, "utf8").matchAll(/<si>([\s\S]*?)<\/si>/g)]
    .map((m) => [...m[1].matchAll(/<t[^>]*>([\s\S]*?)<\/t>/g)].map((x) => x[1]).join(""));
}

/**
 * Append-only shared-string writer: add(value) returns the index of an
 * existing entry or appends a new <si>; write() patches xl/sharedStrings.xml
 * once (no-op when nothing was added).
 */
function sharedStringsWriter(dir) {
  const file = sharedStringsPath(dir);
  const source = fs.readFileSync(file, "utf8");
  const values = readSharedStrings(dir);
  const originalCount = values.length;
  const index = new Map();
  values.forEach((value, i) => { if (!index.has(value)) index.set(value, i); });
  return {
    values,
    add(value) {
      const key = String(value);
      const existing = index.get(key);
      if (existing !== undefined) return existing;
      index.set(key, values.length);
      values.push(key);
      return values.length - 1;
    },
    write() {
      const added = values.slice(originalCount)
        .map((value) => `<si><t>${escapeXml(value)}</t></si>`).join("");
      if (added) fs.writeFileSync(file, source.replace(/<\/sst>\s*$/, added + "</sst>"), "utf8");
    },
  };
}

/** Index of the '>' closing the tag that starts at '<'. Quotes are respected. */
function tagEnd(xml, start) {
  let quote = "";
  for (let i = start + 1; i < xml.length; i++) {
    const ch = xml[i];
    if (quote) { if (ch === quote) quote = ""; continue; }
    if (ch === '"' || ch === "'") { quote = ch; continue; }
    if (ch === ">") return i;
  }
  return xml.length - 1;
}

function skipPast(xml, marker, from) {
  const at = xml.indexOf(marker, from);
  return at < 0 ? xml.length : at + marker.length;
}

/** Finds the next '<name' element start at or after 'from' (0 when absent). */
function findOpenTag(xml, from, name) {
  let at = from;
  while (true) {
    at = xml.indexOf("<" + name, at);
    if (at < 0) return -1;
    const next = xml[at + 1 + name.length];
    if (next === undefined || /[\s/>]/.test(next)) return at;
    at += 1 + name.length;
  }
}

/**
 * Index just past the matching '</name>' for the element whose open tag ends
 * at openEnd. Stray close tags (a literal "</row>" inside text) are ignored
 * instead of terminating the element.
 */
function matchingClose(xml, openEnd, name) {
  const stack = [name];
  let i = openEnd + 1;
  while (i < xml.length) {
    const lt = xml.indexOf("<", i);
    if (lt < 0) break;
    if (xml.startsWith("<!--", lt)) { i = skipPast(xml, "-->", lt); continue; }
    if (xml.startsWith("<![CDATA[", lt)) { i = skipPast(xml, "]]>", lt); continue; }
    if (xml.startsWith("<?", lt)) { i = skipPast(xml, "?>", lt); continue; }
    const end = tagEnd(xml, lt);
    const tag = xml.slice(lt, end + 1);
    const isClose = tag[1] === "/";
    const selfClosing = /\/\s*>$/.test(tag);
    const tagName = tag.replace(/^<\/?\s*/, "").replace(/[\s/>].*$/, "");
    if (!isClose) {
      if (!selfClosing) stack.push(tagName);
    } else if (stack[stack.length - 1] === tagName) {
      stack.pop();
      if (stack.length === 0) return end + 1;
    }
    i = end + 1;
  }
  return xml.length;
}

/** Parses one row body into { cells, objects }; cells are shared-string resolved. */
function parseCells(inner, shared) {
  const cells = {};
  const objects = {};
  let i = 0;
  while (i < inner.length) {
    const lt = inner.indexOf("<c", i);
    if (lt < 0) break;
    const next = inner[lt + 2];
    if (!(next === undefined || /[\s/>]/.test(next))) { i = lt + 2; continue; }
    const end = tagEnd(inner, lt);
    const tag = inner.slice(lt, end + 1);
    const selfClosing = /\/\s*>$/.test(tag);
    const col = (tag.match(/\sr="([A-Z]+)\d+"/) || [])[1];
    if (col) {
      const body = selfClosing ? "" : inner.slice(end + 1, matchingClose(inner, end, "c") - "</c>".length);
      const t = (tag.match(/\st="(\w+)"/) || [])[1] || "n";
      const v = selfClosing ? "" : ((body.match(/<v>([\s\S]*?)<\/v>/) || [])[1] ?? "");
      const val = t === "s" ? (shared[+v] ?? "") : v;
      objects[col] = { raw: tag + body + (selfClosing ? "" : "</c>"), t, v, val };
      cells[col] = val;
    }
    i = end + 1;
  }
  return { cells, objects };
}

/** Reads the workbook sheet: { xml, head, tail, rows: [{ xml, cells, objects }] }. */
function readSheet(dir) {
  const shared = readSharedStrings(dir);
  const xml = fs.readFileSync(sheetPath(dir), "utf8");
  const open = xml.indexOf("<sheetData>");
  const close = xml.indexOf("</sheetData>");
  if (open < 0 || close < 0) throw new Error("sheet1.xml: <sheetData> section not found");
  const body = xml.slice(open + "<sheetData>".length, close);
  const rows = [];
  let cursor = 0;
  while (true) {
    const start = findOpenTag(body, cursor, "row");
    if (start < 0) break;
    const openEnd = tagEnd(body, start);
    const after = matchingClose(body, openEnd, "row");
    const inner = body.slice(openEnd + 1, after - "</row>".length);
    const parsed = parseCells(inner, shared);
    rows.push({ xml: body.slice(start, after), inner, cells: parsed.cells, objects: parsed.objects });
    cursor = after;
  }
  return {
    xml, body, rows, shared,
    head: xml.slice(0, open),
    tail: xml.slice(close + "</sheetData>".length),
  };
}

/** Writes the sheet back: head + rows + tail (rows are full <row> strings). */
function writeSheet(dir, sheet, rowXmls) {
  fs.writeFileSync(sheetPath(dir),
    sheet.head + "<sheetData>" + rowXmls.join("") + "</sheetData>" + sheet.tail, "utf8");
}

/** Rewrites every cell ref and the row ref to rowNumber. */
function renumberRow(rowXml, rowNumber) {
  return rowXml
    .replace(/r="([A-Z]+)\d+"/g, (m, col) => `r="${col}${rowNumber}"`)
    .replace(/<row r="\d+"/, `<row r="${rowNumber}"`);
}

/** Column letter of the header cell whose value equals name (undefined if none). */
function findColumn(cells, name) {
  for (const [col, value] of Object.entries(cells)) if (value === name) return col;
  return undefined;
}

/** RFC-4180-ish CSV reader (quoted fields may contain commas and newlines). */
function readCsv(file) {
  const rows = [];
  let row = [], field = "", quoted = false;
  for (const ch of fs.readFileSync(file, "utf8").replace(/^\uFEFF/, "")) {
    if (quoted) {
      if (ch === '"') { if (field.endsWith('""')) field = field.slice(0, -2) + '"'; else quoted = false; }
      else field += ch;
    } else if (ch === '"') quoted = true;
    else if (ch === ",") { row.push(field); field = ""; }
    else if (ch === "\n") { row.push(field); rows.push(row); row = []; field = ""; }
    else if (ch !== "\r") field += ch;
  }
  if (field !== "" || row.length) { row.push(field); rows.push(row); }
  return rows;
}

/** Quotes a CSV field when it contains a comma, quote or newline. */
function csvEscape(value) {
  const s = String(value);
  return /[",\n]/.test(s) ? '"' + s.replace(/"/g, '""') + '"' : s;
}

/**
 * Reads ids.txt (hash|ID|name) into a name→hash Map. One name can be listed
 * twice (e.g. PL2800 = 646C3168 / 80E0DE61); the first entry is the canonical
 * game id — it is what the native character table uses — so later duplicates
 * are ignored. Rows without a hash are skipped.
 */
function readIds(file) {
  const ids = new Map();
  for (const line of fs.readFileSync(file, "utf8").split(/\r?\n/)) {
    const p = line.split("|");
    if (p.length < 3 || !p[0] || !p[2]) continue;
    const name = p[2].trim();
    if (!ids.has(name)) ids.set(name, p[0]);
  }
  return ids;
}

/** 列号（0 起）→ 列名。 */
function colName(n) {
  let s = "";
  n++;
  while (n) { s = String.fromCharCode(65 + ((n - 1) % 26)) + s; n = Math.floor((n - 1) / 26); }
  return s;
}

/** 列名 → 列号（1 起）。 */
function colNum(c) { let n = 0; for (const ch of c) n = n * 26 + (ch.charCodeAt(0) - 64); return n; }

/** 二维数组 → xlsx 部件目录（无样式；字符串走 sharedStrings）。 */
function writeParts(rows, out) {
  const ncol = Math.max(1, ...rows.map((r) => r.length));
  const shared = [];
  const sharedIndex = new Map();
  let sharedCellCount = 0;
  const sharedId = (value) => {
    const key = String(value);
    sharedCellCount++;
    if (sharedIndex.has(key)) return sharedIndex.get(key);
    sharedIndex.set(key, shared.length);
    shared.push(key);
    return shared.length - 1;
  };
  fs.mkdirSync(path.join(out, 'xl', 'worksheets'), { recursive: true });
  fs.mkdirSync(path.join(out, 'xl', '_rels'), { recursive: true });
  fs.mkdirSync(path.join(out, '_rels'), { recursive: true });
  fs.writeFileSync(path.join(out, '[Content_Types].xml'), '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>' +
    '<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/><Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/><Override PartName="/xl/sharedStrings.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sharedStrings+xml"/></Types>');
  fs.writeFileSync(path.join(out, '_rels', '.rels'), '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>' +
    '<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>');
  fs.writeFileSync(path.join(out, 'xl', 'workbook.xml'), '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>' +
    '<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="Sheet1" sheetId="1" r:id="rId1"/></sheets></workbook>');
  fs.writeFileSync(path.join(out, 'xl', '_rels', 'workbook.xml.rels'), '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>' +
    '<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/><Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/><Relationship Id="rId3" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/sharedStrings" Target="sharedStrings.xml"/></Relationships>');
  fs.writeFileSync(path.join(out, 'xl', 'styles.xml'), '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>' +
    '<styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><fonts count="1"><font><sz val="11"/><name val="Calibri"/></font></fonts><fills count="1"><fill><patternFill patternType="none"/></fill></fills><borders count="1"><border/></borders><cellStyleXfs count="1"><xf/></cellStyleXfs><cellXfs count="1"><xf/></cellXfs></styleSheet>');
  const sheetRows = rows.map((r, ri) => '<row r="' + (ri + 1) + '">' + Array.from({ length: ncol }, (_, ci) => {
    const value = r[ci] ?? '';
    return value === '' ? '<c r="' + colName(ci) + (ri + 1) + '"/>' : '<c r="' + colName(ci) + (ri + 1) + '" t="s"><v>' + sharedId(value) + '</v></c>';
  }).join('') + '</row>').join('');
  fs.writeFileSync(path.join(out, 'xl', 'worksheets', 'sheet1.xml'), '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>' +
    '<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><dimension ref="A1:' + colName(ncol - 1) + rows.length + '"/><sheetViews><sheetView workbookViewId="0"/></sheetViews><sheetData>' + sheetRows + '</sheetData></worksheet>');
  const sharedXml = shared.map((value) => '<si><t xml:space="preserve">' + escapeXml(value) + '</t></si>').join('');
  fs.writeFileSync(path.join(out, 'xl', 'sharedStrings.xml'), '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>' +
    '<sst xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" count="' + sharedCellCount + '" uniqueCount="' + shared.length + '">' + sharedXml + '</sst>');
  return { rows: rows.length, cols: ncol, shared: shared.length };
}

/** 部件目录 → .xlsx（用 '*' 展开顶层条目；不能传 '.'，否则条目带 ./ 前缀）。 */
function packXlsx(partsDir, outXlsx) {
  fs.mkdirSync(path.dirname(path.resolve(outXlsx)), { recursive: true });
  // tar -a 按扩展名选格式：.xlsx 会被当成 tar，必须打包成 .zip 再改名
  const tmp = outXlsx + ".pack.zip";
  fs.rmSync(tmp, { force: true });
  cp.execFileSync('tar', ['-a', '-c', '-f', tmp, '-C', partsDir, '*'], { stdio: 'ignore' });
  fs.rmSync(outXlsx, { force: true });
  fs.renameSync(tmp, outXlsx);
}

/** 二维数组 → .xlsx（临时部件目录 + 打包）。 */
function writeTable(rows, outXlsx) {
  const tmp = fs.mkdtempSync(path.join(os.tmpdir(), 'table-'));
  try {
    const info = writeParts(rows, tmp);
    packXlsx(tmp, outXlsx);
    return info;
  } finally {
    fs.rmSync(tmp, { recursive: true, force: true });
  }
}

/** 读表：.xlsx（解包读共享字符串）或 .csv → 二维数组（含表头行）。 */
function readTable(file) {
  if (!/\.xlsx$/i.test(file)) return readCsv(file);
  const tmp = fs.mkdtempSync(path.join(os.tmpdir(), 'table-'));
  try {
    cp.execFileSync('tar', ['-xf', file, '-C', tmp], { stdio: 'ignore' });
    const sheet = readSheet(tmp);
    const width = (r) => Math.max(0, ...Object.keys(r.cells).map(colNum));
    const ncol = Math.max(0, ...sheet.rows.map(width));
    return sheet.rows.map((r) => Array.from({ length: Math.max(width(r), ncol) }, (_, i) => r.cells[colName(i)] ?? ''));
  } finally {
    fs.rmSync(tmp, { recursive: true, force: true });
  }
}
module.exports = {
  readSharedStrings, sharedStringsWriter, escapeXml, readSheet, writeSheet, readIds,
  renumberRow, findColumn, readCsv, csvEscape, sheetPath, sharedStringsPath,
  readTable, writeParts, writeTable, packXlsx, colName,
};
