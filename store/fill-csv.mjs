// Fills a Store listing CSV exported from Partner Center (Store listings › Export listings) with every
// language of store/listing/*.json, and lays out the folder Partner Center's "Import folder" takes:
//
//   node store/compose.mjs                          the pictures first
//   node store/fill-csv.mjs <exported.csv>          → store/out/import/themia-store/
//
// Partner Center wants its own Field, ID and Type columns back untouched, so the file is always one it
// exported: rows are matched by Field, cells are filled per language column (a language the export has
// no column for gets one), and a field this script doesn't know is left as it was and listed at the end.
// Pictures are referenced as themia-store/<lang>/<file>.png, inside the folder that is imported.

import { copyFileSync, existsSync, mkdirSync, readdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { parse, write } from "./csv.mjs";

const repo = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const images = join(repo, "store", "out", "images");
const root = "themia-store";
const out = join(repo, "store", "out", "import", root);

const exported = process.argv[2];
if (!exported || !existsSync(exported)) {
  console.error("usage: node store/fill-csv.mjs <listing CSV exported from Partner Center>");
  process.exit(1);
}

const listings = readdirSync(join(repo, "store", "listing"))
  .filter((f) => f.endsWith(".json"))
  .map((f) => ({ code: f.slice(0, -5), ...JSON.parse(readFileSync(join(repo, "store", "listing", f), "utf8")) }));

/** A picture of the language's own set, copied into the import folder; its path there. */
function picture(lang, file) {
  const from = existsSync(join(images, lang, file)) ? join(images, lang, file) : join(images, "en", file);
  if (!existsSync(from)) return null;
  mkdirSync(join(out, lang), { recursive: true });
  copyFileSync(from, join(out, lang, file));
  return `${root}/${lang}/${file}`;
}

function shared(file) {
  const from = join(images, file);
  if (!existsSync(from)) return null;
  mkdirSync(out, { recursive: true });
  copyFileSync(from, join(out, file));
  return `${root}/${file}`;
}

/** What a field holds in one language; undefined for a field this script doesn't fill. */
function value(field, l) {
  const f = field.toLowerCase().replace(/[^a-z0-9]/g, "");
  const n = (prefix) => (f.startsWith(prefix) && /^\d+$/.test(f.slice(prefix.length)) ? Number(f.slice(prefix.length)) - 1 : -1);
  // The screenshots there are pictures of, in order: one not made yet leaves no gap.
  const shots = l.screenshots
    .map((s, i) => ({ ...s, file: `screenshot-${String(i + 1).padStart(2, "0")}-${s.shot}.png` }))
    .filter((s) => existsSync(join(images, l.code, s.file)) || existsSync(join(images, "en", s.file)));
  if (f === "description") return l.description.join("\n");
  if (f === "shortdescription") return l.shortDescription;
  if (f === "copyrightandtrademark" || f === "copyrightandtrademarkinfo") return l.copyright;
  if (f === "devstudio" || f === "developedby") return l.developedBy;
  for (const prefix of ["feature", "productfeature"]) {
    if (n(prefix) >= 0) return l.features[n(prefix)] ?? "";
  }
  for (const prefix of ["searchterm", "keyword", "keywords"]) {
    if (n(prefix) >= 0) return l.keywords[n(prefix)] ?? "";
  }
  for (const prefix of ["minimumhardwarereq", "minimumhardwarerequirement"]) {
    if (n(prefix) >= 0) return l.minimumHardware[n(prefix)] ?? "";
  }
  for (const prefix of ["recommendedhardwarereq", "recommendedhardwarerequirement"]) {
    if (n(prefix) >= 0) return l.recommendedHardware[n(prefix)] ?? "";
  }
  if (n("desktopscreenshot") >= 0) {
    const s = shots[n("desktopscreenshot")];
    return s ? picture(l.code, s.file) ?? "" : "";
  }
  const caption = f.match(/^desktopscreenshot(?:caption|comment|description)(\d+)$/) ?? f.match(/^desktopscreenshot(\d+)(?:caption|comment|description)$/);
  if (caption) return shots[Number(caption[1]) - 1]?.caption ?? "";
  if (f === "storelogo300x300" || f === "apptileicon300x300" || f === "storelogosquare") return shared("tile-300x300.png") ?? undefined;
  if (f.startsWith("superheroart") || f === "superhero1920x1080" || f === "superhero3840x2160") return shared("hero-3840x2160.png") ?? undefined;
  return undefined;
}

const rows = parse(readFileSync(exported, "utf8").replace(/^﻿/, ""));
const header = rows[0];
const at = (name) => header.findIndex((h) => h.trim().toLowerCase() === name);
const fieldColumn = at("field");
if (fieldColumn < 0) throw new Error("fill-csv: no Field column; is this a Partner Center listing export?");

// Each listing's column, made where the export has none.
for (const l of listings) {
  if (at(l.language) < 0) {
    header.push(l.language);
    for (const r of rows.slice(1)) r.push("");
  }
}

const unknown = new Set();
for (const r of rows.slice(1)) {
  const field = r[fieldColumn];
  if (!field) continue;
  for (const l of listings) {
    const v = value(field, l);
    if (v === undefined) {
      unknown.add(field);
      continue;
    }
    r[at(l.language)] = v;
  }
}

mkdirSync(out, { recursive: true });
writeFileSync(join(out, "listings.csv"), write(rows));
console.log(`${join(out, "listings.csv")}: ${listings.length} languages`);
if (unknown.size) console.log(`left as exported: ${[...unknown].join(", ")}`);
console.log(`In Partner Center: Store listings › Import listings › Import folder, and pick ${out}`);
