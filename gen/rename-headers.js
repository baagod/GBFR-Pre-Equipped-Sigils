// 表头字段名改小写（只替换 sharedStrings 中表头词串，样式/cell 引用不动）。
// 旧名 → 新名；其余字符串（数据值）不受影响（精确匹配整串）。
// Usage: node rename-headers.js <解包目录>
const fs = require("fs");
const path = require("path");
const dir = process.argv[2];
const REN = {
  "Key": "key", "Hash": "hash", "Name": "name", "Zh": "zh",
  "SkillId1": "skill1", "SkillId2": "skill2", "PlayerReq": "player",
  "IsLuciliusGem": "isluciliusgem", "SkillTypeLotIdForRandom2ndSkill": "lot",
  "Category": "category", "Rarity": "rarity", "CanGemMix": "mix",
  "CanOnlyHoldOne": "one", "cap": "cap", "character": "character",
};
const f = path.join(dir, "xl/sharedStrings.xml");
const xml = fs.readFileSync(f, "utf8");
let n = 0;
const out = xml.replace(/<si>([\s\S]*?)<\/si>/g, (m, si) => {
  const txt = [...si.matchAll(/<t[^>]*>([\s\S]*?)<\/t>/g)].map(x => x[1]).join("");
  if (REN[txt] !== undefined) { n++; return `<si><t>${REN[txt]}</t></si>`; }
  return m;
});
fs.writeFileSync(f, out, "utf8");
console.error(`表头词替换 ${n} 个`);
