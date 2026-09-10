// 规则：仅池版因子（mix=1 & player 空 & onlyone=0 & skill2 空）保留 lot，并把池号展开为技能 hash 列表（空格分隔）；其余行 lot 清空。
// Usage: node add-lot.js <解包目录> <lotmap.json>
const fs = require("fs");
const xl = require("./xlsx-lib");
const dir = process.argv[2];
const lotmap = JSON.parse(fs.readFileSync(process.argv[3], "utf8"));

const sheet = xl.readSheet(dir);
const hdr = sheet.rows[0].cells;
const cLot = xl.findColumn(hdr, "SkillTypeLotIdForRandom2ndSkill");
const cMix = xl.findColumn(hdr, "CanGemMix");
const cReq = xl.findColumn(hdr, "PlayerReq");
const cOne = xl.findColumn(hdr, "CanOnlyHoldOne");
const cS2 = xl.findColumn(hdr, "SkillId2");
if (!cLot || !cMix || !cReq || !cOne || !cS2) throw new Error("add-lot: 缺少必需列");

const colNum = (c) => { let n = 0; for (const ch of c) n = n * 26 + (ch.charCodeAt(0) - 64); return n; };
const shared = xl.sharedStringsWriter(dir);
let pools = 0;
const outRows = [sheet.rows[0].xml];
for (let i = 1; i < sheet.rows.length; i++) {
  const row = sheet.rows[i];
  const isPool = row.cells[cMix] === "1" && !(row.cells[cReq] || "").trim() && row.cells[cOne] === "0" && !(row.cells[cS2] || "").trim();
  const value = isPool ? (lotmap[(row.cells[cLot] || "").trim()] || []).join(" ") : "";
  if (value) pools++;
  const rn = i + 1;
  let xml = `<row r="${rn}">`;
  for (const c of Object.keys(row.objects).sort((a, b) => colNum(a) - colNum(b))) {
    xml += c === cLot
      ? (value ? `<c r="${c}${rn}" t="s"><v>${shared.add(value)}</v></c>` : `<c r="${c}${rn}"/>`)
      : row.objects[c].raw;
  }
  xml += "</row>";
  outRows.push(xl.renumberRow(xml, rn));
}
shared.write();
xl.writeSheet(dir, sheet, outRows);
console.error(`lot 展开/归一完成（池版因子 ${pools} 行保留并展开，其余清空）`);
