// node --test store/listing.test.mjs
// Every listing fits Partner Center's limits and has English's shape, so compose.mjs and fill-csv.mjs find the same
// fields and the same screenshots in every language; and what a listing counts is what the app has.
import assert from "node:assert/strict";
import { readdirSync, readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { test } from "node:test";
import { fileURLToPath } from "node:url";

const store = dirname(fileURLToPath(import.meta.url));
const dir = join(store, "listing");
const read = (file) => readFileSync(join(dir, file));
const en = JSON.parse(read("en.json").toString("utf8"));
const shape = (l) => Object.fromEntries(Object.entries(l).map(([k, v]) => [k, Array.isArray(v) ? v.length : typeof v]));
const words = (list) => list.reduce((n, k) => n + k.trim().split(/\s+/).length, 0);

for (const file of readdirSync(dir).filter((f) => f.endsWith(".json"))) {
  test(file, () => {
    const raw = read(file);
    assert.notDeepEqual([...raw.subarray(0, 3)], [0xef, 0xbb, 0xbf], "no BOM");
    const l = JSON.parse(raw.toString("utf8"));
    assert.deepEqual(shape(l), shape(en));
    assert.deepEqual(l.screenshots.map((s) => s.shot), en.screenshots.map((s) => s.shot));
    // compose.mjs places a picture's callouts by their order: as many in every language, each short enough for its bubble.
    assert.deepEqual(l.screenshots.map((s) => s.callouts?.length ?? 0), en.screenshots.map((s) => s.callouts?.length ?? 0));
    assert.equal(l.title, en.title);
    assert.ok(l.shortDescription.length <= 270, "short description: the first 270 characters show");
    assert.ok(l.description.join("\n").length <= 10000, "description");
    for (const f of l.features) assert.ok(f.length <= 200, `feature: ${f}`);
    assert.ok(l.features.length <= 20);
    assert.ok(l.keywords.length <= 7, "keywords");
    for (const k of l.keywords) assert.ok(k.length <= 40, `keyword: ${k}`);
    assert.ok(words(l.keywords) <= 21, "keywords: 21 words in all");
    assert.ok(l.screenshots.length <= 10, "the Store shows ten screenshots at most");
    for (const s of l.screenshots) {
      assert.ok(s.caption.length <= 200, `caption: ${s.caption}`);
      assert.ok(s.headline.length <= 55, `headline too long for the picture: ${s.headline}`);
      for (const c of s.callouts ?? []) assert.ok(c.length <= 28, `callout too long for its bubble: ${c}`);
    }
    assert.ok(l.addOn.title.length <= 100 && l.addOn.description.length <= 200, "add-on");
    assert.ok(l.addOn.keywords.length <= 10 && l.addOn.keywords.every((k) => k.length <= 30), "add-on keywords");
  });
}

test("the listing counts the widgets the app has", () => {
  // A widget kind is a module folder (CLAUDE.md): the number the listing names is theirs.
  const kinds = readdirSync(join(store, "..", "src", "Widgets")).filter((d) => d.startsWith("Themia.Widget.")).length;
  const text = [en.shortDescription, ...en.description, ...en.features, ...en.screenshots.flatMap((s) => [s.label, s.headline])].join("\n");
  // "109 widgets" is the number itself; "more than 100 widgets" a round number under it, within twenty. Not the free
  // version's "up to 20 widgets".
  const named = [...text.matchAll(/(more than )?(\d+) widgets?\b/gi)].map((m) => ({ over: Boolean(m[1]), n: Number(m[2]) })).filter((m) => m.n !== 20);
  assert.ok(named.length > 0, "the listing says how many widgets there are");
  for (const { over, n } of named) {
    if (over) assert.ok(n < kinds && kinds - n <= 20, `the listing says more than ${n} widgets, the app has ${kinds}`);
    else assert.equal(n, kinds, `the listing says ${n} widgets, the app has ${kinds}`);
  }
});

test("the listing does not name what Themia does not offer", () => {
  // Google accounts are held back until Google has verified the app (SPEC [S-3562]).
  for (const file of readdirSync(dir).filter((f) => f.endsWith(".json"))) {
    assert.doesNotMatch(read(file).toString("utf8"), /google|gmail/i, `${file} names Google`);
  }
});
