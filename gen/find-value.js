// 全表查找：某个值（因子 key/hash、文本键、任意字符串）出现在哪些表·列。
// 扫描 DB 内所有表的所有列；字符串按原样（大小写不敏感），8 位十六进制同时按 u32 hash 匹配。
// Usage: node find-value.js <值...> [--db <sqlite>]   （默认 gen\extracted\gbfr.db）
"use strict";
const fs = require("fs");
const path = require("path");
const { DatabaseSync } = require("node:sqlite");

const argv = process.argv.slice(2);
const dbFlag = argv.indexOf("--db");
let dbPath = path.join(__dirname, "extracted", "gbfr.db");
let values = argv;
if (dbFlag >= 0) {
  dbPath = argv[dbFlag + 1];
  values = argv.filter((_, i) => i !== dbFlag && i !== dbFlag + 1);
}
if (values.length === 0) {
  console.error("用法: node find-value.js <值...> [--db <sqlite>]");
  process.exit(2);
}
if (!fs.existsSync(dbPath)) {
  console.error("数据库不存在: " + dbPath);
  process.exit(2);
}

const db = new DatabaseSync(dbPath);
const tables = db.prepare("select name from sqlite_master where type='table'").all().map((r) => r.name);
const strMap = new Map(); // 大写字符串 -> 原值
const numMap = new Map(); // u32 -> 原值
for (const v of values) {
  strMap.set(v.toUpperCase(), v);
  if (/^[0-9A-Fa-f]{8}$/.test(v)) numMap.set(parseInt(v, 16) >>> 0, v);
}
const hits = new Map(values.map((v) => [v, []]));
const t0 = Date.now();
for (const t of tables) {
  const cols = db.prepare('pragma table_info("' + t + '")').all().map((c) => c.name);
  const rows = db.prepare('select * from "' + t + '"').all();
  for (const c of cols) {
    const counts = new Map();
    for (const row of rows) {
      const v = row[c];
      if (v === null || v === undefined || v === "") continue;
      const raw = typeof v === "string" ? strMap.get(v.trim().toUpperCase())
        : typeof v === "number" ? numMap.get(v >>> 0)
        : undefined;
      if (raw !== undefined) counts.set(raw, (counts.get(raw) || 0) + 1);
    }
    for (const [raw, n] of counts) hits.get(raw).push(t + "." + c + " x" + n);
  }
}
for (const v of values) {
  const list = hits.get(v);
  console.log(v + (list.length ? "\n    " + list.join("\n    ") : "  → 未命中"));
}
console.error("扫描 " + tables.length + " 张表，用时 " + ((Date.now() - t0) / 1000).toFixed(1) + " 秒");
