// 生成最小 xlsx（无样式、无列宽设置；字符串走 sharedStrings，供后续 add-* 脚本读取）。
// Usage: node make-xlsx.js <in.csv> <outDir>  (outDir 内写 xl/… 部件，由外部压缩为 .xlsx)
const fs = require("fs");
const xl = require("./xlsx-lib");

const rows = xl.readCsv(process.argv[2]);
const ncol = Math.max(...rows.map(r => r.length));
const colName = (n) => { let s = ""; n++; while (n) { s = String.fromCharCode(65 + (n - 1) % 26) + s; n = Math.floor((n - 1) / 26); } return s; };
const esc = (s) => s.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
// Shared-string table: every downstream script (add-cap/add-skills/add-zh/
// rename-headers/replace-skills) reads xl/sharedStrings.xml, so the generated
// workbook must contain one (an inline-string sheet cannot be processed).
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

const out = process.argv[3];
fs.mkdirSync(out + "/xl/worksheets", { recursive: true });
fs.mkdirSync(out + "/xl/_rels", { recursive: true });
fs.mkdirSync(out + "/_rels", { recursive: true });
fs.writeFileSync(out + "/[Content_Types].xml", `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/><Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/><Override PartName="/xl/sharedStrings.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sharedStrings+xml"/></Types>`);
fs.writeFileSync(out + "/_rels/.rels", `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>`);
fs.writeFileSync(out + "/xl/workbook.xml", `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="Sheet1" sheetId="1" r:id="rId1"/></sheets></workbook>`);
fs.writeFileSync(out + "/xl/_rels/workbook.xml.rels", `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/><Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/><Relationship Id="rId3" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/sharedStrings" Target="sharedStrings.xml"/></Relationships>`);
fs.writeFileSync(out + "/xl/styles.xml", `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><fonts count="1"><font><sz val="11"/><name val="Calibri"/></font></fonts><fills count="1"><fill><patternFill patternType="none"/></fill></fills><borders count="1"><border/></borders><cellStyleXfs count="1"><xf/></cellStyleXfs><cellXfs count="1"><xf/></cellXfs></styleSheet>`);
const sheetRows = rows.map((r, ri) => `<row r="${ri + 1}">` + Array.from({ length: ncol }, (_, ci) => {
  const value = r[ci] ?? "";
  return value === ""
    ? `<c r="${colName(ci)}${ri + 1}"/>`
    : `<c r="${colName(ci)}${ri + 1}" t="s"><v>${sharedId(value)}</v></c>`;
}).join("") + `</row>`).join("");
fs.writeFileSync(out + "/xl/worksheets/sheet1.xml", `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><dimension ref="A1:${colName(ncol - 1)}${rows.length}"/><sheetViews><sheetView workbookViewId="0"/></sheetViews><sheetData>${sheetRows}</sheetData></worksheet>`);
const sharedXml = shared.map((value) => `<si><t xml:space="preserve">${esc(value)}</t></si>`).join("");
fs.writeFileSync(out + "/xl/sharedStrings.xml", `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<sst xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" count="${sharedCellCount}" uniqueCount="${shared.length}">${sharedXml}</sst>`);
console.error(`xlsx parts written: ${rows.length} rows x ${ncol} cols, ${shared.length} shared strings -> ${out}`);
