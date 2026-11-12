// The Store desktops' wallpapers, fetched: each picture of store/wallpapers.json from its source, fitted to
// 3840 x 2160 (covering, centred) as store/wallpapers/<name>.png. The folder is git-ignored: the pictures are
// other people's illustrations, free to use under the licence each entry names, and the repository keeps the
// list, not the files.
//
//   node store/wallpapers.mjs            every picture that is missing
//   node store/wallpapers.mjs --all      every picture again
//   node store/wallpapers.mjs <name>...  these
//
// A downloaded file is data: it is written to store/out/wallpapers/fetched/ and shown as an <img> (an SVG's
// scripts do not run there), never opened as a page.
import { existsSync, mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { render, url, repo, out } from "./chrome.mjs";

const list = JSON.parse(readFileSync(join(repo, "store", "wallpapers.json"), "utf8")).wallpapers;
const folder = join(repo, "store", "wallpapers");
const fetched = join(out, "wallpapers", "fetched");

/** The file a picture is kept in: wallpapers/<name>.png, as a scene names it. */
export const fileOf = (name) => join(folder, name + ".png");

const kinds = [
  ["image/png", "png"],
  ["image/jpeg", "jpg"],
  ["image/webp", "webp"],
  ["image/svg+xml", "svg"],
];

async function download(entry) {
  const response = await fetch(entry.download, { headers: { "User-Agent": "store-wallpapers" }, redirect: "follow" });
  if (!response.ok) throw new Error(`${entry.name}: ${response.status} from ${new URL(entry.download).host}`);
  const type = (response.headers.get("content-type") ?? "").split(";")[0].trim().toLowerCase();
  const kind = kinds.find(([mime]) => mime === type);
  if (!kind) throw new Error(`${entry.name}: ${type || "no content type"} is not a picture`);
  mkdirSync(fetched, { recursive: true });
  const file = join(fetched, `${entry.name}.${kind[1]}`);
  writeFileSync(file, Buffer.from(await response.arrayBuffer()));
  return file;
}

function fit(entry, file) {
  const html = `<!doctype html><meta http-equiv="Content-Security-Policy" content="default-src 'none'; img-src file:; style-src 'unsafe-inline'">`
    + `<style>html,body{margin:0;background:#000}img{width:1920px;height:1080px;object-fit:cover;display:block}</style><img src="${url(file)}">`;
  render("wallpaper-" + entry.name, html, fileOf(entry.name), 1920, 1280, 2, { w: 3840, h: 2160 });
}

if (process.argv[1] && import.meta.url === url(process.argv[1])) {
  const args = process.argv.slice(2);
  const all = args.includes("--all");
  const names = args.filter((a) => !a.startsWith("--"));
  const unknown = names.filter((n) => !list.some((e) => e.name === n));
  if (unknown.length) throw new Error(`no wallpaper ${unknown.join(", ")} (has: ${list.map((e) => e.name).join(", ")})`);
  mkdirSync(folder, { recursive: true });
  for (const entry of list) {
    if (names.length ? !names.includes(entry.name) : !all && existsSync(fileOf(entry.name))) continue;
    fit(entry, await download(entry));
  }
}
