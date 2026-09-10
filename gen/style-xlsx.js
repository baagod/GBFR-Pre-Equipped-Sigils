
// 给最终 xlsx 部件加样式（人工核对用）：
//   标题行      → 加粗 + #0070C0 背景 + 白字
//   lot 非空    → #9BC2E6
//   player 非空 → #7030A0（白字）
//   onlyone=1   → #FFC000
//   非物品技能  → #F4B084（hash == skill1）
// 四类互不重叠，按上述顺序判定；行序与单元格内容不变。
// Usage: node style-xlsx.js <部件目录>
const fs = require("fs");
const path = require("path");
const xl = require("./xlsx-lib");
const dir = process.argv[2];

const sheet = xl.readSheet(dir);
const hdr = sheet.rows[0].cells;
const col = {};
for (const [k, n] of Object.entries({ hash: "hash", skill1: "skill1", player: "player", lot: "lot", onlyone: "onlyone" })) {
  col[k] = xl.findColumn(hdr, n);
  if (!col[k]) throw new Error("style-xlsx: 缺少列 " + n);
}

const CELL_XFS = [
  "<xf/>",                                                            // 0 默认
  '<xf fillId="2" applyFill="1"/>',                                   // 1 lot
  '<xf fontId="1" fillId="3" applyFont="1" applyFill="1"/>',          // 2 player（白字）
  '<xf fillId="4" applyFill="1"/>',                                   // 3 onlyone
  '<xf fillId="5" applyFill="1"/>',                                   // 4 非物品技能
  '<xf fontId="2" fillId="6" applyFont="1" applyFill="1"/>',          // 5 标题行
];
const FILLS = [
  '<fill><patternFill patternType="none"/></fill>',
  '<fill><patternFill patternType="gray125"/></fill>',
  '<fill><patternFill patternType="solid"><fgColor rgb="FF9BC2E6"/><bgColor indexed="64"/></patternFill></fill>',
  '<fill><patternFill patternType="solid"><fgColor rgb="FF7030A0"/><bgColor indexed="64"/></patternFill></fill>',
  '<fill><patternFill patternType="solid"><fgColor rgb="FFFFC000"/><bgColor indexed="64"/></patternFill></fill>',
  '<fill><patternFill patternType="solid"><fgColor rgb="FFF4B084"/><bgColor indexed="64"/></patternFill></fill>',
  '<fill><patternFill patternType="solid"><fgColor rgb="FF0070C0"/><bgColor indexed="64"/></patternFill></fill>',
];
const FONTS = [
  '<font><sz val="11"/><name val="Calibri"/></font>',
  '<font><sz val="11"/><color rgb="FFFFFFFF"/><name val="Calibri"/></font>',
  '<font><b/><sz val="11"/><color rgb="FFFFFFFF"/><name val="Calibri"/></font>',
];

const HEADER_STYLE = 5;
const count = [0, 0, 0, 0];
const outRows = [sheet.rows[0].xml.replace(/<c /g, '<c s="' + HEADER_STYLE + '" ')];
for (let i = 1; i < sheet.rows.length; i++) {
  const r = sheet.rows[i];
  const v = (c) => r.cells[c] ?? "";
  let s = 0;
  if (v(col.lot).trim()) s = 1;
  else if (v(col.player).trim()) s = 2;
  else if (v(col.onlyone) === "1") s = 3;
  else if (v(col.hash) === v(col.skill1)) s = 4;
  if (s) count[s - 1]++;
  outRows.push(s ? r.xml.replace(/<c /g, '<c s="' + s + '" ') : r.xml);
}
xl.writeSheet(dir, sheet, outRows);
fs.writeFileSync(path.join(dir, "xl", "styles.xml"),
  '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>\n<styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">' +
  '<fonts count="' + FONTS.length + '">' + FONTS.join("") + "</fonts>" +
  '<fills count="' + FILLS.length + '">' + FILLS.join("") + "</fills>" +
  '<borders count="1"><border/></borders><cellStyleXfs count="1"><xf/></cellStyleXfs>' +
  '<cellXfs count="' + CELL_XFS.length + '">' + CELL_XFS.join("") + "</cellXfs></styleSheet>", "utf8");
console.error("样式：标题行 + lot " + count[0] + "、player " + count[1] + "、onlyone " + count[2] + "、技能条目 " + count[3]);
