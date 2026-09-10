// 列重排：C=Name(英文, en 文本表), D=Zh(中文, 原 Name 值)；后续列右移（SkillId1..character → E..N），IsLuciliusGem 列丢弃（14 列）。
// 文本键定位：因子行 Key→gem 表 Name 键（gem-all.csv）；技能行 Key→skill 表 Name 键（skillnames.csv）。
// Usage: node add-zh.js <解包目录> <en.json> <gem-all.csv> <skillnames.csv>
const fs = require("fs");
const xl = require("./xlsx-lib");
const dir = process.argv[2];
const en = JSON.parse(fs.readFileSync(process.argv[3], "utf8"));
const gemKey2Name = new Map(xl.readCsv(process.argv[4]).slice(1).map(r => [r[0], r[1]]));
const skillKey2Name = new Map(xl.readCsv(process.argv[5]).slice(1).map(r => [r[0], r[1]]));

const sheet = xl.readSheet(dir);
const shared = xl.sharedStringsWriter(dir);

// 列映射：旧 D..N → 新 E..O（A/B/C 特殊：A=Key, B=Hash, C=Name(EN), D=Zh）
const MAP = { D: "E", E: "F", F: "G", H: "H", I: "I", J: "J", K: "K", L: "L", M: "M", N: "N" };
const NEWHDR = ["Key", "Hash", "Name", "Zh", "SkillId1", "SkillId2", "PlayerReq", "SkillTypeLotIdForRandom2ndSkill", "Category", "Rarity", "CanGemMix", "CanOnlyHoldOne", "cap", "character"];

const rows = sheet.rows;

const outRows = [];
for (let i = 0; i < rows.length; i++) {
  const cells = rows[i].objects;
  const rn = i + 1;
  if (i === 0) {
    let s = "";
    NEWHDR.forEach((n, ci) => { s += `<c r="${String.fromCharCode(65 + ci)}${rn}" t="s"><v>${shared.add(n)}</v></c>`; });
    outRows.push(`<row r="${rn}">` + s + `</row>`);
    continue;
  }
  const key = cells.A.val;
  const tkey = gemKey2Name.get(key) || skillKey2Name.get(key) || "";
  const zh = cells.C.val;                       // 原 Name（中文）
  const enName = tkey && en[tkey] ? en[tkey] : "";
  let s = `<c r="A${rn}" t="s"><v>${shared.add(key)}</v></c><c r="B${rn}" t="s"><v>${shared.add(cells.B.val)}</v></c>` +
    `<c r="C${rn}" t="s"><v>${shared.add(enName || zh)}</v></c>` +
    `<c r="D${rn}" t="s"><v>${shared.add(zh)}</v></c>`;
  for (const [oc, nc] of Object.entries(MAP)) {
    const c = cells[oc];
    if (c && c.val !== "") s += `<c r="${nc}${rn}" t="s"><v>${shared.add(c.val)}</v></c>`;
    else s += `<c r="${nc}${rn}"/>`;
  }
  outRows.push(`<row r="${rn}">` + s + `</row>`);
}
shared.write();
xl.writeSheet(dir, sheet, outRows);
console.error(`rows ${rows.length}；列重排完成（C=Name(EN), D=Zh, 14 列）`);
