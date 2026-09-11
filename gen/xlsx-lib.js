// Shared read-only helpers for the gen pipeline: unzip a workbook, read a sheet,
// map ids.txt by name, and pack a parts directory back into .xlsx.
// All scripts operate on an unzipped workbook directory. Rows are scanned with
// a tag-aware parser (not a /<row>.*<\/row>/ regex), so a literal "</row>"
// inside a cell's text can never split a row.
// Usage: const xl = require("./xlsx-lib");
"use strict";
const fs = require("fs");
const path = require("path");
const cp = require("child_process");

const SHEET_PART = "xl/worksheets/sheet1.xml";
const SHARED_PART = "xl/sharedStrings.xml";

const sheetPath = (dir) => path.join(dir, SHEET_PART);
const sharedStringsPath = (dir) => path.join(dir, SHARED_PART);

/** Reads xl/sharedStrings.xml into a string[] (empty when the file is absent). */
function readSharedStrings(dir) {
  const file = sharedStringsPath(dir);
  if (!fs.existsSync(file)) return [];
  return [...fs.readFileSync(file, "utf8").matchAll(/<si>([\s\S]*?)<\/si>/g)]
    .map((m) => [...m[1].matchAll(/<t[^>]*>([\s\S]*?)<\/t>/g)].map((x) => x[1]).join(""));
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

/** Column letter of the header cell whose value equals name (undefined if none). */
function findColumn(cells, name) {
  for (const [col, value] of Object.entries(cells)) if (value === name) return col;
  return undefined;
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

/** 去掉因子名尾部的 ＋ 与罗马数字后缀（中文罗马数字与英文罗马数字各一次）。 */
const ROMAN_CN = /[ⅠⅡⅢⅣⅤⅥⅦⅧⅨⅩ]+$/;
const ROMAN_EN = /[IVX]+$/;
function shortName(s) {
  return String(s ?? "").replace(/[＋+]$/, "").replace(ROMAN_CN, "").replace(ROMAN_EN, "").trim();
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

module.exports = {
  readSheet, readIds, findColumn, packXlsx, colName, shortName,
};
