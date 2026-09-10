// 把 xlsx 的 sharedStrings 中所有 SKILL_xxx_yy 明文替换为 hash（ids.txt 词典），
// sheet1.xml 引用不变，仅改字符串池。行/样式/宽高不动。
// Usage: node replace-skills.js <解包目录> <ids.txt>
const fs = require("fs");
const path = require("path");
const xl = require("./xlsx-lib");
const dir = process.argv[2];
const ids = xl.readIds(process.argv[3]);
const f = path.join(dir, "xl/sharedStrings.xml");
const xml = fs.readFileSync(f, "utf8");
let replaced = 0, missing = [];
const out = xml.replace(/<si>([\s\S]*?)<\/si>/g, (m, si) => {
  let content = "";
  for (const t of si.matchAll(/<t[^>]*>([\s\S]*?)<\/t>/g)) content += t[1];
  if (!/^SKILL_[0-9]+_[0-9]+$/.test(content)) return m;
  const h = ids.get(content);
  if (!h) { missing.push(content); return m; }
  replaced++;
  return si.includes(">") && m.startsWith("<si>") && /<t[^>]*>([\s\S]*?)<\/t>/g.test(si)
    ? m.replace(/<t[^>]*>([\s\S]*?)<\/t>/, `<t>${h}</t>`)  // 替换首个 <t …>…</t>（容忍属性）
    : m;
});
if (missing.length) console.error("缺失词典:", missing.join(","));
fs.writeFileSync(f, out, "utf8");
console.error(`SKILL 明文 → hash 替换 ${replaced} 条`);
