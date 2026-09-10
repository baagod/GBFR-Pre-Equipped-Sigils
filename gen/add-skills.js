// 在 sigils.xlsx 尾部追加 12 条"非物品技能"条目（字段：Key=技能逻辑名, Hash=技能hash, Name=中文名,
// SkillId1=hash, 其余空, cap=技能表max）。因子行不动。
// Usage: node add-skills.js <解包目录>
const xl = require("./xlsx-lib");
const dir = process.argv[2];

const SKILLS = [
  ["SKILL_113_00", "57E8A93F", "因子强化", "2"],
  ["SKILL_143_00", "40223C28", "浩劫", "25"],
  ["1E1CECCE", "1E1CECCE", "浩劫新星", "35"],
  ["SKILL_311_00", "3B71AF12", "伤害上限·轰天", "15"],
  ["SKILL_312_00", "FFF8CF64", "伤害上限·疾天", "15"],
  ["SKILL_314_00", "AEFEB1BC", "伤害上限·苍天", "15"],
  ["0151CF9E", "0151CF9E", "伤害上限·红天", "15"],
  ["235D86EF", "235D86EF", "超新星", "15"],
  ["BBD77C33", "BBD77C33", "超凡强击", "15"],
  ["020DB733", "020DB733", "超凡技艺", "15"],
  ["3F682593", "3F682593", "超凡奥秘", "15"],
  ["79027FC8", "79027FC8", "超凡破限", "55"],
];

const sheet = xl.readSheet(dir);
const shared = xl.sharedStringsWriter(dir);
const newRows = SKILLS.map(([k, h, n, cap], i) => {
  const rn = sheet.rows.length + i + 1;
  const cell = (c, v) => v === undefined ? `<c r="${c}${rn}"/>` : `<c r="${c}${rn}" t="s"><v>${shared.add(v)}</v></c>`;
  return `<row r="${rn}">` + cell("A", k) + cell("B", h) + cell("C", n) + cell("D", h) +
    cell("E") + cell("F") + cell("G") + cell("H") + cell("I") + cell("J") + cell("K") + cell("L") +
    cell("M", cap) + cell("N") + `</row>`;
}).join("");
shared.write();
xl.writeSheet(dir, sheet, [sheet.body, newRows]);
console.error(`追加 ${SKILLS.length} 条技能条目（行 ${sheet.rows.length + 1}..${sheet.rows.length + SKILLS.length}）`);
