// The Microsoft Store's pictures, made from Themia's real ones: every widget, desktop and menu on them is a scene
// (store/scenes.json) drawn by the app itself (Themia.exe --scene-png, store/capture.ps1), set under its headline from
// store/listing/<lang>.json as panorama.mjs lays the pages out. A desktop's wallpaper is in its capture. Rendered by headless Chrome (or Edge) at the Store's sizes.
//
//   node store/compose.mjs              every language that has a listing
//   node store/compose.mjs en de        those languages
//
// Out: store/out/images/<lang>/screenshot-<n>-<shot>.png (3840 x 2160), and once for all languages
// store/out/images/hero-3840x2160.png, tile-300x300.png and pro-300x300.png. A language's own captures
// (store/out/shots/<lang>/<scene>.png) are used where it has every one, else the English ones.

import { existsSync, readdirSync, readFileSync, rmSync } from "node:fs";
import { join } from "node:path";
import { out, render, repo, url } from "./chrome.mjs";
import { display, frames, monitorParts, page, pieceRect, scenes, usedScenes } from "./panorama.mjs";

const shots = join(out, "shots");
const images = join(out, "images");
const logo = url(join(repo, "src", "Themia.App", "Assets", "themia-logo.png"));

// Fonts with each script's letters, after Segoe UI Variable's Latin, Greek and Cyrillic.
const scriptFonts = {
  ja: '"Yu Gothic UI"',
  "zh-Hans": '"Microsoft YaHei UI"',
  "zh-Hant": '"Microsoft JhengHei UI"',
  ko: '"Malgun Gothic"',
  hi: '"Nirmala UI"',
  mr: '"Nirmala UI"',
  bn: '"Nirmala UI"',
  te: '"Nirmala UI"',
  ta: '"Nirmala UI"',
  gu: '"Nirmala UI"',
  kn: '"Nirmala UI"',
  ml: '"Nirmala UI"',
  pa: '"Nirmala UI"',
  th: '"Leelawadee UI"',
  my: '"Myanmar Text"',
  am: '"Ebrima"',
};
const rightToLeft = new Set(["ar", "fa", "ur"]);
const fonts = (lang, family) => `${scriptFonts[lang] ? scriptFonts[lang] + ", " : ""}"Segoe UI Variable ${family}", "Segoe UI", sans-serif`;
const escape = (s) => s.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");

// The pages' ground: graphite, as the logo's tile, lit by the logo's cyan and a violet. The lights stand over the
// cuts between the pages, half on each, so the row of screenshots is one picture.
const lights = [
  { x: 0, y: -80, r: [1100, 640], rgb: [0, 150, 210], a: 0.5 },
  { x: 1920, y: 1160, r: [1000, 720], rgb: [96, 80, 230], a: 0.42 },
  { x: 3840, y: -80, r: [1100, 640], rgb: [0, 170, 220], a: 0.42 },
  { x: 5760, y: 1160, r: [1000, 720], rgb: [150, 70, 210], a: 0.36 },
  { x: 7680, y: -80, r: [1100, 640], rgb: [40, 120, 240], a: 0.45 },
  { x: 9600, y: 1160, r: [1000, 720], rgb: [0, 160, 210], a: 0.4 },
  { x: 11520, y: -80, r: [1100, 640], rgb: [110, 80, 235], a: 0.45 },
  { x: 13440, y: 1160, r: [1000, 720], rgb: [0, 150, 210], a: 0.4 },
  { x: 15360, y: -80, r: [1100, 640], rgb: [150, 70, 210], a: 0.38 },
  { x: 17280, y: 1160, r: [1000, 720], rgb: [40, 120, 240], a: 0.42 },
  { x: 19200, y: -80, r: [1100, 640], rgb: [0, 150, 210], a: 0.5 },
];
const ground = "linear-gradient(180deg, #10131c 0%, #08090d 100%)";

/**
 * The listing's screenshots as one picture (panorama.mjs), page n of it: the pages beside it laid out too, moved n
 * pages to the left, so what crosses a cut is the same on both pages.
 */
function panoramaHtml(lang, listing, at, n) {
  const width = page.w * frames.length;
  const light = lights
    .map((b) => `radial-gradient(${b.r[0]}px ${b.r[1]}px at ${b.x}px ${b.y}px, rgba(${b.rgb.join(", ")}, ${b.a}), rgba(${b.rgb.join(", ")}, 0) 70%)`)
    .join(", ");
  const content = frames
    .map((frame, i) => {
      // Only what the page shows is drawn: itself and what runs over from the pages beside it.
      if (Math.abs(i - n) > 1) return "";
      const s = listing.screenshots[i];
      const left = i * page.w;
      let said = 0;
      const worded = (bubbles) => (bubbles ?? []).map((b) => ({ ...b, text: s.callouts[said++] }));
      const cards = (frame.cards ?? [])
        .map((c) => {
          const z = `z-index:${c.z ?? 1};`;
          if (c.screen) {
            const box = { x: left + c.x, y: c.y, w: c.w, h: Math.round((c.w * 9) / 16) };
            return `<div class="layer" style="${z}">${monitor(at, c.cuts ?? [c.screen], box, c.tilt ?? 0)}${bubblesHtml(worded(c.bubbles), c.screen, { x: 0, y: 0, w: display.w, h: display.h }, box, left)}</div>`;
          }
          const crop = pieceRect(scenes, c.piece);
          const box = { x: left + c.x, y: c.y, w: c.w, h: Math.round((c.w * crop.h) / crop.w) };
          const rays = c.lens === undefined ? "" : raysHtml(crop, { x: left + frame.cards[c.lens].x, y: frame.cards[c.lens].y, w: frame.cards[c.lens].w }, box);
          return `<div class="layer" style="${z}">${rays}${piece(at, c.piece, crop, box, c)}${bubblesHtml(worded(c.bubbles), c.piece[0], crop, box, left)}</div>`;
        })
        .join("");
      const wall = frame.wall ? wallHtml(frame.wall, left, at) : "";
      const lanes = frame.lanes ? lanesHtml(frame.lanes, left, s.callouts, at) : "";
      return `${wall}${cards}${lanes}<div class="text" style="left:${left + 160}px; width:${page.w - 320}px; top:44px;">
  <div class="label"><img src="${logo}" alt="">${escape(s.label)}</div>
  <h1>${escape(s.headline)}</h1>
  <p>${escape(s.sub)}</p>
</div>`;
    })
    .join("\n");
  const rtl = rightToLeft.has(lang) ? "rtl" : "ltr";
  return `<!doctype html>
<html lang="${lang}" dir="${rtl}"><head><meta charset="utf-8"><style>
html, body { margin: 0; width: ${page.w}px; height: ${page.h}px; overflow: hidden; background: #08090d; }
#world { position: absolute; left: ${-n * page.w}px; top: 0; width: ${width}px; height: ${page.h}px; direction: ltr;
  background: ${light}, ${ground}; color: #fff; font-family: ${fonts(lang, "Display")}; -webkit-font-smoothing: antialiased; }
.layer { position: absolute; inset: 0; pointer-events: none; }
.text { position: absolute; z-index: 20; text-align: center; direction: ${rtl}; }
.label { display: inline-flex; align-items: center; gap: 12px; font-family: ${fonts(lang, "Text")}; font-weight: 600; font-size: 22px; color: #7ad9ff; letter-spacing: 0.01em; }
.label img { width: 32px; height: 32px; border-radius: 8px; }
h1, p { text-wrap: balance; word-break: auto-phrase; }
h1 { margin: 10px 0 0; font-weight: 600; font-size: 60px; line-height: 1.08; letter-spacing: -0.012em; }
p { margin: 10px 0 0; font-family: ${fonts(lang, "Text")}; font-size: 26px; line-height: 1.35; color: rgba(228, 236, 248, 0.74); }
.piece { position: absolute; filter: drop-shadow(0 22px 36px rgba(0, 0, 0, 0.5)); }
.piece.flat { filter: none; }
.piece > div { position: absolute; inset: 0; overflow: hidden; }
.piece img { position: absolute; }
.bubble { position: absolute; transform: translate(-50%, -50%); white-space: nowrap; padding: 10px 18px 12px; border-radius: 999px; background: #60cdff;
  color: #04202e; font: 600 22px/1 ${fonts(lang, "Text")}; box-shadow: 0 8px 24px rgba(0, 0, 0, 0.45); z-index: 6; direction: ${rtl}; }
.lead { position: absolute; background: rgba(122, 217, 255, 0.9); z-index: 5; }
.dot { position: absolute; width: 14px; height: 14px; border-radius: 50%; background: #60cdff; box-shadow: 0 0 0 4px rgba(96, 205, 255, 0.32); z-index: 6; }
.monitor { position: absolute; background: #04050a;
  box-shadow: 0 0 0 1.5px rgba(214, 224, 240, 0.42), 0 0 0 3px rgba(8, 10, 16, 0.9), 0 0 90px rgba(64, 180, 255, 0.18), 0 46px 110px rgba(0, 0, 0, 0.62); }
.neck { position: absolute; background: linear-gradient(180deg, rgba(0, 0, 0, 0.55), rgba(0, 0, 0, 0) 45%), linear-gradient(90deg, #4d535f, #a4abb8 38%, #c6ccd6 50%, #a4abb8 62%, #4d535f); }
.foot { position: absolute; border-radius: 999px 999px 6px 6px / 14px 14px 6px 6px; background: linear-gradient(180deg, #d5dae2, #8d94a1 55%, #5a606c);
  box-shadow: inset 0 1px 0 rgba(255, 255, 255, 0.7); }
.floor { position: absolute; border-radius: 50%; background: radial-gradient(closest-side, rgba(0, 0, 0, 0.55), rgba(0, 0, 0, 0)); }
.screen { position: absolute; overflow: hidden; border-radius: 6px; background: #000; }
.screen img { position: absolute; left: 0; top: 0; width: 100%; height: 100%; }
.glass { position: absolute; inset: 0; background: linear-gradient(118deg, rgba(255, 255, 255, 0.06), rgba(255, 255, 255, 0) 26%); box-shadow: inset 0 0 0 1px rgba(255, 255, 255, 0.05); }
.screen svg { position: absolute; left: 0; top: 0; }
.rays { position: absolute; }
.marked { position: absolute; border-radius: 6px; box-shadow: 0 0 0 2px #7ad9ff, 0 0 18px rgba(96, 205, 255, 0.7); }
.wall { position: absolute; top: 0; z-index: 2; }
.lane { position: absolute; z-index: 10; border-radius: 30px; background: linear-gradient(180deg, #1a2030, #0e111a);
  box-shadow: 0 0 0 1.5px rgba(255, 255, 255, 0.12), 0 30px 80px rgba(0, 0, 0, 0.45); direction: ${rtl}; }
.gives { position: absolute; top: 20px; height: 68px; display: flex; align-items: center; gap: 14px; font: 600 30px/1.08 ${fonts(lang, "Display")}; }
.gives i { flex: none; display: inline-flex; align-items: center; justify-content: center; width: 48px; height: 48px; border-radius: 14px; background: #60cdff; color: #04202e;
  font: normal 26px/1 "Segoe Fluent Icons", "Segoe MDL2 Assets"; box-shadow: 0 6px 16px rgba(0, 0, 0, 0.35); }
.gives span { text-wrap: balance; }
.flow { position: absolute; width: 2px; border-radius: 1px; background: linear-gradient(180deg, #7ad9ff, rgba(122, 217, 255, 0.15)); }
.flow::before { content: ""; position: absolute; left: -6px; top: -4px; width: 14px; height: 14px; border-radius: 50%; background: #60cdff; box-shadow: 0 0 0 4px rgba(96, 205, 255, 0.3); }
.sources { position: absolute; bottom: 26px; display: flex; flex-direction: column; gap: 12px; direction: ltr; }
.source { display: flex; align-items: center; gap: 14px; font: 600 25px/1 ${fonts("en", "Text")}; color: rgba(236, 242, 255, 0.92); white-space: nowrap; }
.logo { display: inline-flex; align-items: center; justify-content: center; width: 44px; height: 44px; border-radius: 12px; flex: none;
  box-shadow: inset 0 0 0 1px rgba(255, 255, 255, 0.18), 0 6px 16px rgba(0, 0, 0, 0.35); }
.logo.plain { background: #fff; }
.logo.glyph { background: rgba(255, 255, 255, 0.14); }
.logo.glyph i { font: normal 24px/1 "Segoe Fluent Icons", "Segoe MDL2 Assets"; }
</style></head><body><div id="world">
${content}
</div></body></html>`;
}

/**
 * A whole desktop in a monitor, its screen box: its bezel round it, its stand under it (panorama.mjs's monitorParts).
 * Several scenes (cuts) share the screen in strips side by side, each cut leaning tilt of the screen's width, its top
 * that much right of its foot: the same desktop in each, so the strips are one screen restyled.
 */
function monitor(at, cuts, box, tilt) {
  const { bezel, neck, foot } = monitorParts(box.w);
  const bottom = box.y + box.h + bezel;
  const n = cuts.length;
  const lean = tilt * box.w;
  const edge = (k, top) => (k === 0 ? -lean - 4 : k === n ? box.w + lean + 4 : (k * box.w) / n + (top ? lean : -lean));
  const strips = cuts
    .map((name, k) => `<img src="${url(at(name))}" alt="" style="${n === 1 ? "" : `clip-path: polygon(${edge(k, true)}px 0, ${edge(k + 1, true)}px 0, ${edge(k + 1, false)}px 100%, ${edge(k, false)}px 100%);`}">`)
    .join("");
  const seams = n === 1 ? "" : `<svg width="${box.w}" height="${box.h}" viewBox="0 0 ${box.w} ${box.h}">${Array.from({ length: n - 1 }, (_, i) => i + 1)
    .map((k) => `<line x1="${edge(k, true)}" y1="0" x2="${edge(k, false)}" y2="${box.h}" stroke="rgba(0, 0, 0, 0.35)" stroke-width="8"/><line x1="${edge(k, true)}" y1="0" x2="${edge(k, false)}" y2="${box.h}" stroke="#fff" stroke-width="3"/>`)
    .join("")}</svg>`;
  const middle = box.x + box.w / 2;
  // The neck narrows upward a little, and the screen's shadow lies on its top.
  const taper = Math.round(neck.w * 0.08);
  return `<div class="floor" style="left:${middle - foot.w * 0.8}px; top:${bottom + neck.h + foot.h - 6}px; width:${foot.w * 1.6}px; height:${foot.h * 3}px;"></div>
    <div class="neck" style="left:${middle - neck.w / 2}px; top:${bottom - 2}px; width:${neck.w}px; height:${neck.h + 2}px; clip-path: polygon(${taper}px 0, ${neck.w - taper}px 0, 100% 100%, 0 100%);"></div>
    <div class="foot" style="left:${middle - foot.w / 2}px; top:${bottom + neck.h}px; width:${foot.w}px; height:${foot.h}px;"></div>
    <div class="monitor" style="left:${box.x - bezel}px; top:${box.y - bezel}px; width:${box.w + 2 * bezel}px; height:${box.h + 2 * bezel}px; border-radius:${bezel + 8}px;"></div>
    <div class="screen" style="left:${box.x}px; top:${box.y}px; width:${box.w}px; height:${box.h}px;">${strips}${seams}<div class="glass"></div></div>`;
}

/**
 * A piece of a scene on the page: crop (the scene's DIPs) in box (page px). A widget is drawn as the app drew it, its
 * own corners and transparency, a shadow under it; a piece of a desktop (ground: its wallpaper is in the capture) has
 * its corners rounded here.
 */
function piece(at, [name], crop, box, c = {}) {
  const k = box.w / crop.w;
  const turn = c.r ? `transform: rotate(${c.r}deg);` : "";
  return `<div class="piece${c.flat ? " flat" : ""}" style="left:${box.x}px; top:${box.y}px; width:${box.w}px; height:${box.h}px; ${turn}">
    <div style="${c.ground ? `border-radius:${c.radius ?? 14}px; box-shadow: inset 0 0 0 1.5px rgba(255, 255, 255, 0.16);` : ""}"><img src="${url(at(name))}" alt="" style="left:${-crop.x * k}px; top:${-crop.y * k}px; width:${display.w * k}px; height:${display.h * k}px;"></div></div>`;
}

/**
 * A lens: what a card enlarges of a screen (its crop), marked on the screen (its box on the page), and the light
 * widening from there to the card over or under it.
 */
function raysHtml(crop, screen, box) {
  const k = screen.w / display.w;
  const from = { x: screen.x + crop.x * k, y: screen.y + crop.y * k, w: crop.w * k, h: crop.h * k };
  // Over or under what it enlarges, the light runs down or up; beside it, across.
  const beside = box.x + box.w <= from.x + 8 || box.x >= from.x + from.w - 8;
  let corners;
  let fades;
  if (beside) {
    const left = box.x < from.x;
    const [near, far] = left ? [from.x, box.x + box.w] : [from.x + from.w, box.x];
    corners = [[near, from.y], [near, from.y + from.h], [far, box.y + box.h], [far, box.y]];
    fades = left ? ["1", "0"] : ["0", "1"];
  } else {
    const over = box.y + box.h / 2 < from.y + from.h / 2;
    const [near, far] = over ? [from.y, box.y + box.h] : [from.y + from.h, box.y];
    corners = [[from.x, near], [from.x + from.w, near], [box.x + box.w, far], [box.x, far]];
    fades = over ? ["0", "1"] : ["1", "0"];
  }
  const x0 = Math.min(...corners.map((c) => c[0])) - 4;
  const y0 = Math.min(...corners.map((c) => c[1])) - 4;
  const w = Math.max(...corners.map((c) => c[0])) + 4 - x0;
  const h = Math.max(...corners.map((c) => c[1])) + 4 - y0;
  const points = corners.map(([x, y]) => `${(x - x0).toFixed(1)},${(y - y0).toFixed(1)}`).join(" ");
  // Bright at the card, faint at what it enlarges.
  const axis = beside ? `x1="${fades[0]}" y1="0" x2="${fades[1]}" y2="0"` : `x1="0" y1="${fades[0]}" x2="0" y2="${fades[1]}"`;
  return `<svg class="rays" style="left:${x0}px; top:${y0}px;" width="${w}" height="${h}" viewBox="0 0 ${w} ${h}">
      <defs><linearGradient id="ray" ${axis}><stop offset="0" stop-color="rgba(122, 217, 255, 0.3)"/><stop offset="1" stop-color="rgba(122, 217, 255, 0.05)"/></linearGradient></defs>
      <polygon points="${points}" fill="url(#ray)"/></svg>
    <div class="marked" style="left:${from.x - 3}px; top:${from.y - 3}px; width:${from.w + 6}px; height:${from.h + 6}px;"></div>`;
}

/**
 * A wall of widgets (panorama.mjs's wall): lists of pieces side by side, each w wide and gap apart, the whole of it
 * turned r degrees about its page's middle and fading out under the headline. Its ends run on into the pages beside it.
 */
function wallHtml(wall, left, at) {
  const room = 600;
  const lists = wall.lists
    .map((list, i) => {
      let y = list.dy ?? 0;
      return list.pieces
        .map((p) => {
          const crop = pieceRect(scenes, p);
          const box = { x: i * (wall.w + wall.gap), y, w: wall.w, h: Math.round((wall.w * crop.h) / crop.w) };
          y += box.h + wall.gap;
          return piece(at, p, crop, box);
        })
        .join("");
    })
    .join("");
  return `<div class="wall" style="left:${left - room}px; width:${page.w + 2 * room}px; height:${page.h}px;
      -webkit-mask-image: linear-gradient(to bottom, transparent ${wall.fade[0]}px, #000 ${wall.fade[1]}px);">
    <div style="position:absolute; left:${room + wall.x}px; top:${wall.y}px; transform-origin:${page.w / 2 - wall.x}px ${page.h / 2 - wall.y}px; transform:rotate(${wall.r}deg);">${lists}</div></div>`;
}

// The services' logos (store/logos, Simple Icons' SVGs), each white on its brand's colour; Microsoft's four squares in
// their own colours; a server with no logo of its own as what it gives, a Segoe Fluent Icons glyph.
const logos = {
  microsoft: { squares: ["#F25022", "#7FBA00", "#00A4EF", "#FFB900"] },
  microsoftoutlook: { colour: "#0078D4" },
  microsoftteams: { colour: "#5B5FC7" },
  icloud: { colour: "#3693F3" },
  fastmail: { colour: "#0067B9" },
  nextcloud: { colour: "#0082C9" },
  // Zoom's is its word: larger, to read as well as the others.
  zoom: { colour: "#0B5CFF", size: 38 },
  webex: { colour: "#00A0D1" },
  mail: { glyph: "" },
  calendar: { glyph: "" },
  contacts: { glyph: "" },
  tasks: { glyph: "" },
  pc: { glyph: "" },
};

function logoHtml(key) {
  const found = logos[key];
  if (found.squares) {
    const [a, b, c, d] = found.squares;
    return `<span class="logo plain"><svg viewBox="0 0 23 23" width="26" height="26"><rect width="11" height="11" fill="${a}"/><rect x="12" width="11" height="11" fill="${b}"/><rect y="12" width="11" height="11" fill="${c}"/><rect x="12" y="12" width="11" height="11" fill="${d}"/></svg></span>`;
  }
  if (found.glyph) return `<span class="logo glyph"><i>${found.glyph}</i></span>`;
  const svg = readFileSync(join(repo, "store", "logos", key + ".svg"), "utf8").replace(/<title>.*?<\/title>/, "").replace("<svg ", `<svg fill="#fff" width="${found.size ?? 26}" height="${found.size ?? 26}" `);
  return `<span class="logo" style="background:${found.colour};">${svg}</span>`;
}

/**
 * What Themia reads from the person's accounts (panorama.mjs's lanes): a lane each, side by side. From its top: what
 * it gives, in the listing's words (the callout of its place); that as Themia shows it, a widget; and the services it
 * comes from, logo and name.
 */
function lanesHtml(lanes, left, words, at) {
  const inner = lanes.w - 2 * lanes.pad;
  return lanes.lanes
    .map((lane, i) => {
      const x = left + lanes.x + i * (lanes.w + lanes.gap);
      const crop = pieceRect(scenes, lane.piece);
      const box = { x: lanes.pad, y: 104, w: inner, h: Math.round((inner * crop.h) / crop.w) };
      const from = lane.from.map(([name, key]) => `<span class="source">${logoHtml(key)}<span>${escape(name)}</span></span>`).join("");
      // The services stand on the lane's floor, 56 px each; a line runs from them up into what they give.
      const top = box.y + box.h + 16;
      const foot = lanes.h - 26 - lane.from.length * 56 - 4;
      const flow = foot - top > 40 ? `<div class="flow" style="left:${lanes.pad + 21}px; top:${top}px; height:${foot - top}px;"></div>` : "";
      return `<div class="lane" style="left:${x}px; top:${lanes.y}px; width:${lanes.w}px; height:${lanes.h}px;">
        <div class="gives" style="left:${lanes.pad}px; width:${inner}px;"><i>${lane.glyph}</i><span>${escape(words[i])}</span></div>
        ${piece(at, lane.piece, crop, box, { flat: true })}${flow}
        <div class="sources" style="left:${lanes.pad}px; width:${inner}px;">${from}</div></div>`;
    })
    .join("");
}

/**
 * Callouts over a card showing crop (a scene's DIPs) in box: each bubble's words where its dx, dy (page px) put them
 * from its point, above it by default, or at a place of the page (to: its middle, page px); a line from the bubble to
 * a dot on the point. The point is a place in a widget
 * of the scene (on: its id; at: fractions of its box, its middle by default) or a point of the scene (at, DIPs).
 */
function bubblesHtml(bubbles, scene, crop, box, left) {
  const k = box.w / crop.w;
  return bubbles
    .map((bubble) => {
      let x;
      let y;
      if (bubble.on) {
        const r = pieceRect(scenes, [scene, bubble.on]);
        const [fx, fy] = bubble.at ?? [0.5, 0.5];
        x = r.x + fx * r.w;
        y = r.y + fy * r.h;
      } else {
        [x, y] = bubble.at;
      }
      const point = { x: box.x + (x - crop.x) * k, y: box.y + (y - crop.y) * k };
      const at = bubble.to ? { x: left + bubble.to[0], y: bubble.to[1] } : { x: point.x + (bubble.dx ?? 0), y: point.y + (bubble.dy ?? -80) };
      const length = Math.hypot(point.x - at.x, point.y - at.y);
      const angle = (Math.atan2(point.y - at.y, point.x - at.x) * 180) / Math.PI;
      return `<div class="lead" style="left:${at.x}px; top:${at.y - 1}px; width:${length}px; height:2px; transform-origin:0 50%; transform:rotate(${angle}deg);"></div>
        <div class="dot" style="left:${point.x - 7}px; top:${point.y - 7}px;"></div>
        <div class="bubble" style="left:${at.x}px; top:${at.y}px;">${escape(bubble.text)}</div>`;
    })
    .join("");
}

const backdrop = `background:
    radial-gradient(1100px 640px at 50% -8%, rgba(0, 150, 210, 0.5), rgba(0, 150, 210, 0) 70%),
    radial-gradient(900px 700px at 6% 110%, rgba(96, 80, 230, 0.4), rgba(96, 80, 230, 0) 70%),
    radial-gradient(900px 700px at 96% 105%, rgba(0, 170, 220, 0.26), rgba(0, 170, 220, 0) 70%),
    ${ground};`;

// The 16:9 hero: no words and no UI (the Store lays the title over it and crops it), the logo large in the middle
// of the upper two thirds.
function heroHtml() {
  return `<!doctype html><html><head><meta charset="utf-8"><style>
html, body { margin: 0; width: ${page.w}px; height: ${page.h}px; overflow: hidden; }
body { ${backdrop} }
.ring { position: absolute; border-radius: 50%; border: 1.5px solid rgba(122, 217, 255, 0.14); left: 50%; top: 40%; transform: translate(-50%, -50%); }
.glow { position: absolute; left: 50%; top: 40%; width: 760px; height: 760px; transform: translate(-50%, -50%); background: radial-gradient(closest-side, rgba(96, 205, 255, 0.4), rgba(96, 205, 255, 0)); }
.logo { position: absolute; left: 50%; top: 40%; width: 380px; transform: translate(-50%, -50%); border-radius: 84px; filter: drop-shadow(0 30px 60px rgba(0, 0, 0, 0.6)); }
</style></head><body>
<div class="ring" style="width:640px;height:640px"></div>
<div class="ring" style="width:900px;height:900px;opacity:.7"></div>
<div class="ring" style="width:1180px;height:1180px;opacity:.45"></div>
<div class="ring" style="width:1480px;height:1480px;opacity:.25"></div>
<div class="glow"></div>
<img class="logo" src="${logo}" alt="">
</body></html>`;
}

// The 300 x 300 tile icon: the logo, and the Pro add-on's with its word.
function tileHtml(pro) {
  return `<!doctype html><html><head><meta charset="utf-8"><style>
html, body { margin: 0; width: 300px; height: 300px; overflow: hidden; background: transparent; }
img { position: absolute; left: 18px; top: 18px; width: 264px; height: 264px; }
.pro { position: absolute; right: 10px; bottom: 16px; padding: 7px 16px 9px; border-radius: 999px; background: linear-gradient(180deg, #ffd45c, #f5a623);
  color: #3a2400; font: 700 34px/1 "Segoe UI Variable Display", "Segoe UI", sans-serif; letter-spacing: 0.04em; box-shadow: 0 4px 14px rgba(0, 0, 0, 0.35); }
</style></head><body><div style="position: relative; width: 300px; height: 300px;"><img src="${logo}" alt="">${pro ? '<div class="pro">PRO</div>' : ""}</div></body></html>`;
}

const languages = process.argv.length > 2
  ? process.argv.slice(2)
  : readdirSync(join(repo, "store", "listing")).filter((f) => f.endsWith(".json")).map((f) => f.slice(0, -5));

console.log("hero and icons");
render("hero", heroHtml(), join(images, "hero-3840x2160.png"), page.w, page.h + 200, 2, { w: page.w * 2, h: page.h * 2 });
render("tile", tileHtml(false), join(images, "tile-300x300.png"), 500, 500, 1, { w: 300, h: 300 });
render("pro", tileHtml(true), join(images, "pro-300x300.png"), 500, 500, 1, { w: 300, h: 300 });

const needed = usedScenes();

for (const lang of languages) {
  const listing = JSON.parse(readFileSync(join(repo, "store", "listing", lang + ".json"), "utf8"));
  console.log(lang);
  // A language's set made again whole: a picture renumbered or dropped leaves nothing behind.
  rmSync(join(images, lang), { recursive: true, force: true });
  const order = listing.screenshots.map((s) => s.shot).join(", ");
  if (order !== frames.map((f) => f.shot).join(", ")) throw new Error(`compose: ${lang}'s screenshots (${order}) aren't panorama.mjs's frames`);
  // The language's own captures where it has every one the panorama is made of, else the English ones.
  const own = needed.every((name) => existsSync(join(shots, lang, name + ".png"))) ? lang : "en";
  const at = (name) => join(shots, own, name + ".png");
  const missing = needed.filter((name) => !existsSync(at(name)));
  if (missing.length > 0) {
    console.warn(`  no capture ${missing.join(", ")}: store/capture.ps1`);
    continue;
  }
  frames.forEach((frame, i) => {
    const n = String(i + 1).padStart(2, "0");
    render(`${lang}-${n}`, panoramaHtml(lang, listing, at, i), join(images, lang, `screenshot-${n}-${frame.shot}.png`), page.w, page.h + 200, 2, { w: page.w * 2, h: page.h * 2 });
  });
}
