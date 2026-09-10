
// 由最终 xlsx（.xlsx 文件或解包后的部件目录）生成 sigils.json（mod 运行时数据）。
// 注意：name/zh 取「技能基础名」——sigils.xlsx 的因子名带 ＋/罗马数字后缀，这里去掉
// （zh 去 ＋ 与中文罗马数字；name 再去英文罗马数字），与现 sigils.json 一致。
// 字段映射：A=key B=hash C=name D=zh E=skill1 F=sec(原 skill2) G=player
//           H=lot（空格分隔 → 数组）I=category L=onlyone→special(=1) M=cap（数字）N=character（非空才输出）
// 输出：{ "sigils": [ { key, hash, name, zh, skill1, sec, category, player, special, cap, [character,] lot }, … ] }
// 行序 = 输入行序（确定性；mod 侧按 key/hash 查表，与行序无关）。
// Usage: node make-sigils-json.js <sigils.xlsx|部件目录> <out.json> [--check]
//   --check：只比对不写文件（忽略行序，按 key 排序比较内容），不一致时退出码 1。
const fs = require("fs");
const os = require("os");
const path = require("path");
const { spawnSync } = require("child_process");
const xl = require("./xlsx-lib");

// 输入是 .xlsx（zip）时先解包：Windows 自带 bsdtar 能直接解，省掉扩展库依赖。
function partsDirOf(input) {
  if (!fs.statSync(input).isFile()) return { dir: input };
  const tmp = fs.mkdtempSync(path.join(os.tmpdir(), "sigils-xlsx-"));
  const r = spawnSync("tar", ["-xf", path.resolve(input), "-C", tmp], { encoding: "utf8" });
  if (r.status !== 0) throw new Error("解包 xlsx 失败：" + (r.stderr || r.error));
  return { dir: tmp, tmp };
}

const input = process.argv[2];
const outPath = process.argv[3];
const check = process.argv.includes("--check");
const parts = partsDirOf(input);
const sheet = xl.readSheet(parts.dir);
if (parts.tmp) fs.rmSync(parts.tmp, { recursive: true, force: true });   // 临时解包目录用完即删
const hdr = sheet.rows[0].cells;
const COLS = { key: "key", hash: "hash", name: "name", zh: "zh", skill1: "skill1", sec: "skill2", player: "player", lot: "lot", category: "category", onlyone: "onlyone", cap: "cap", character: "character" };
const col = {};
for (const [k, name] of Object.entries(COLS)) {
  col[k] = xl.findColumn(hdr, name);
  if (!col[k]) throw new Error("make-sigils-json: 缺少列 " + name);
}

const ROMAN_CN = /[ⅠⅡⅢⅣⅤⅥⅦⅧⅨⅩ]+$/;
const ROMAN_EN = /[IVX]+$/;
const shortName = (s) => (s || "").replace(/[＋+]$/, "").replace(ROMAN_CN, "").replace(ROMAN_EN, "").trim();

const sigils = sheet.rows.slice(1).map((row) => {
  const v = (c) => row.cells[c] ?? "";
  const o = {
    key: v(col.key),
    hash: v(col.hash),
    name: shortName(v(col.name)),
    zh: shortName(v(col.zh)),
    skill1: v(col.skill1),
    sec: v(col.sec),
    category: v(col.category),
    player: v(col.player),
    special: v(col.onlyone) === "1",
    cap: Number(v(col.cap)),
  };
  const character = v(col.character);
  if (character) o.character = character;
  o.lot = v(col.lot).trim() ? v(col.lot).trim().split(/\s+/) : [];
  return o;
});
const text = JSON.stringify({ sigils }, null, 2) + "\n";
if (check) {
  const byKey = (t) => JSON.stringify([...JSON.parse(t).sigils].sort((a, b) => (a.key < b.key ? -1 : a.key > b.key ? 1 : 0)));
  const current = fs.existsSync(outPath) ? fs.readFileSync(outPath, "utf8") : "";
  let same = false;
  try { same = current.length > 0 && byKey(current) === byKey(text); } catch { same = false; }
  if (!same) {
    console.error("不一致：sigils.json 与 " + input + " 的内容不符（请重跑生成器）");
    process.exit(1);
  }
  console.error("一致：sigils.json 与 " + input + " 内容相同（" + sigils.length + " 条）");
} else {
  fs.writeFileSync(outPath, text, "utf8");
  console.error("sigils.json：" + sigils.length + " 条 → " + outPath);
}
