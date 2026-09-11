// 全表引用扫描：一次遍历 DB 内所有表的所有列，收集「被引用的因子 key」及出处/次数。
// 不写死产出表；--exclude 可排除表（默认排除 gem 自身）。8 位十六进制同时按 u32 hash 匹配。
// Usage: node scan-refs.js <gbfr.db> <ids.txt> <out.json> [--exclude t1,t2]
"use strict";
const fs = require("fs");
const { DatabaseSync } = require("node:sqlite");

const [dbPath, idsPath, outPath] = process.argv.slice(2, 5);
const exIdx = process.argv.indexOf("--exclude");
const exclude = new Set(["gem", ...(exIdx >= 0 ? (process.argv[exIdx + 1] || "").split(",").filter(Boolean) : [])]);
if (!dbPath || !idsPath || !outPath) {
  console.error("用法: node scan-refs.js <gbfr.db> <ids.txt> <out.json> [--exclude t1,t2]");
  process.exit(2);
}

const ids = new Map();
for (const line of fs.readFileSync(idsPath, "utf8").split(/\r?\n/)) {
  const p = line.split("|");
  if (p.length >= 3 && p[2]) ids.set(p[2].trim(), p[0].trim());
}

const db = new DatabaseSync(dbPath);
const keys = db.prepare("select Key from gem").all().map((r) => r.Key);
const strMap = new Map();
const numMap = new Map();
for (const k of keys) {
  strMap.set(k.toUpperCase(), k);
  const h = /^[0-9A-F]{8}$/.test(k) ? k : ids.get(k);
  if (h) numMap.set(parseInt(h, 16) >>> 0, k);
}

const out = {};
const tables = db.prepare("select name from sqlite_master where type='table'").all().map((r) => r.name).filter((t) => !exclude.has(t));
const t0 = Date.now();
for (const t of tables) {
  const cols = db.prepare('pragma table_info("' + t + '")').all().map((c) => c.name);
  const rows = db.prepare('select * from "' + t + '"').all();
  for (const c of cols) {
    for (const row of rows) {
      const v = row[c];
      if (v === null || v === undefined || v === "") continue;
      const key = typeof v === "string" ? strMap.get(v.trim().toUpperCase())
        : typeof v === "number" ? numMap.get(v >>> 0)
        : undefined;
      if (key === undefined) continue;
      const e = out[key] || (out[key] = { n: 0, where: [] });
      e.n++;
      const tag = t + "." + c;
      const hit = e.where.find((w) => w.startsWith(tag + " x"));
      if (hit) e.where[e.where.indexOf(hit)] = tag + " x" + (Number(hit.slice(tag.length + 2)) + 1);
      else e.where.push(tag + " x1");
    }
  }
}
fs.writeFileSync(outPath, JSON.stringify(out, null, 2) + "\n", "utf8");
console.error("扫描 " + tables.length + " 张表 → " + Object.keys(out).length + "/" + keys.length + " 个因子被引用，用时 " + ((Date.now() - t0) / 1000).toFixed(1) + " 秒 → " + outPath);
