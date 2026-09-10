// 给 sigils.xlsx 追加两列：M=cap（技能等级上限 = skill_status.max(Level)，无表技能→15）、
// N=character（PlayerReq 的角色 hash；非专属留空）。现有行 cell 原样，行号不变。
// cap 对照（sigils 验证）：000=50 001=50 003=45 005=30 020=65 045=20 147=45 066=45 073=15 103=15 140=45 141=15 160=30 233/234=15 ✓
// Usage: node add-cap.js <解包目录> <ids.txt> <cap.csv> <capmap.json>
const fs = require("fs");
const xl = require("./xlsx-lib");
const dir = process.argv[2];

// capmap.json：[["GEEN_xxx", cap], ...]（build-capmap.js 产出：组内 SkillId1 的 max；max=1/无表→15）
const KEYCAP = new Map(JSON.parse(fs.readFileSync(process.argv[5], "utf8")));

const name2hash = xl.readIds(process.argv[3]);
// 参数 4（cap.csv）保留位置以兼容既有命令；cap 全部来自 KEYCAP。

const sheet = xl.readSheet(dir);
const shared = xl.sharedStringsWriter(dir);
shared.add("cap");
shared.add("character");
let filled = 0;
const outRows = [];
for (let i = 0; i < sheet.rows.length; i++) {
  const row = sheet.rows[i];
  const rn = i + 1;
  let cap = "", ch = "";
  if (i === 0) { cap = "cap"; ch = "character"; }
  else {
    cap = String(KEYCAP.get(row.cells.A) ?? 15); // 组内主技能 max（build-capmap）
    const req = row.cells.F ?? "";
    if (req.trim()) ch = name2hash.get(req) ?? "";
    if (cap || ch) filled++;
  }
  outRows.push(`<row r="${rn}">` + row.inner +
    (cap ? `<c r="M${rn}" t="s"><v>${shared.add(cap)}</v></c>` : `<c r="M${rn}"/>`) +
    (ch ? `<c r="N${rn}" t="s"><v>${shared.add(ch)}</v></c>` : `<c r="N${rn}"/>`) + `</row>`);
}
shared.write();
xl.writeSheet(dir, sheet, outRows);
console.error(`rows ${sheet.rows.length}；cap/character 已附加（非空 ${filled}）`);
