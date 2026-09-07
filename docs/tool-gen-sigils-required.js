// Normalizes sigils.json against character-exclusives.json (line-based patch,
// not JSON re-serialization, so the diff stays minimal and format-neutral):
//   1. Drops awakening (合体版) exclusive rows — exclusive (player != "")
//      but NOT one of the 3 built-in template gems (t1Gem/t2Gem/warGem).
//      Warning: some combos carry no "觉醒" word in the name (无态 / 涯之七星 /
//      涯之二王), so the filter is gem-based, never name-based.
//   2. Injects `character` (required_character_hash) on the remaining
//      exclusive rows, straight from character-exclusives.json.
//      Captain share (Gran/Djeeta use identical gems): the row keeps the Gran
//      hash (2A26B1B2) so one gem maps to one character, matching the old
//      compatibility table semantics (native IsCharacterCompatible treats
//      both captain hashes as compatible).
// - Idempotent: re-running yields the same file.
// - Assumes the current stable layout: sigils.json is an array of flat
//   objects with "gem", "player", "sort" fields, one per line.

const fs = require("fs");
const path = require("path");

const root = path.resolve(__dirname, "..");
const sigilsPath = path.join(root, "GBFR.PreEquippedSigils", "sigils.json");
const excl = JSON.parse(
  fs.readFileSync(path.join(root, "GBFR.PreEquippedSigils", "character-exclusives.json"), "utf8")
);

const gemToHash = new Map();
const templateGems = new Set();
for (const e of excl.exclusives) {
  for (const gem of [e.t1Gem, e.t2Gem, e.warGem]) {
    templateGems.add(gem);
    if (!gemToHash.has(gem)) gemToHash.set(gem, e.hash); // captain share: keep first (Gran)
  }
}

const raw = fs.readFileSync(sigilsPath, "utf8").split("\n");
const eol = raw[0].endsWith("\r") ? "\r" : ""; // match the file's own line ending
const out = [];
let objLines = [];
let gem = "";
let player = "";
let injected = 0;
let dropped = 0;

const flushObj = () => {
  if (objLines.length === 0) return;
  const isExclusive = player !== "";
  if (isExclusive) {
    if (!templateGems.has(gem)) {
      dropped++; // awakening combo row: drop the whole object
      objLines = [];
      gem = "";
      player = "";
      return;
    }
    const sortIdx = objLines.findIndex((l) => l.trim().startsWith('"sort"'));
    if (sortIdx < 0) throw new Error("no sort line in object gem=" + gem);
    const sortLine = objLines[sortIdx];
    if (!sortLine.trim().endsWith(",")) {
      objLines[sortIdx] = sortLine.replace(/\r$/, "") + "," + eol;
    }
    // Last field of the object: no trailing comma.
    objLines.splice(sortIdx + 1, 0, `   "character": "${gemToHash.get(gem)}"` + eol);
    injected++;
  }
  out.push(...objLines);
  objLines = [];
  gem = "";
  player = "";
};

for (const line of raw) {
  objLines.push(line);
  const m = line.match(/"gem":\s*"([^"]+)"/);
  if (m) gem = m[1];
  const p = line.match(/"player":\s*"([^"]*)"/);
  if (p) player = p[1];
  if (line.trim() === "}," || line.trim() === "}") flushObj();
}
out.push(...objLines); // tail (closing bracket)

fs.writeFileSync(sigilsPath, out.join("\n"), "utf8");
console.log(`dropped ${dropped} awakening combo row(s); injected character into ${injected} exclusive rows`);
