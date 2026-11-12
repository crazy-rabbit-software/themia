// The stand-in desktops' wallpapers: drawn here, so no picture of anyone else's is in a Store screenshot.
// Soft light over a dark or a pale ground, calm enough for widgets to be read on it.
//
//   node store/wallpaper.mjs            every wallpaper, to store/wallpapers/<name>.jpg (1920 x 1080: they are
//                                       soft light, and a scene fills its display with one at any size)
//
// The pictures are checked in, as the scenes name them (scenes.json's "wallpaper"): run this after changing one.
//
// A wallpaper is its ground (a gradient top to bottom) and lights: ellipses of colour (x, y their middle and rx, ry
// their half sizes in px on a 1920 x 1080 page; a their strength), blurred into each other.
import { join } from "node:path";
import { fileURLToPath } from "node:url";
import { out, render, repo, toJpeg } from "./chrome.mjs";

export const wallpapers = {
  // The night the listing's pages stand in: graphite to deep blue, Themia's cyan low on the left, violet on the right.
  dusk: {
    ground: ["#0b1020", "#131a36", "#0a0d1c"],
    lights: [
      { x: 260, y: 880, rx: 900, ry: 520, rgb: [0, 164, 220], a: 0.55 },
      { x: 1500, y: 260, rx: 820, ry: 520, rgb: [104, 78, 230], a: 0.6 },
      { x: 1820, y: 1000, rx: 700, ry: 420, rgb: [214, 76, 160], a: 0.42 },
      { x: 880, y: 420, rx: 520, ry: 300, rgb: [40, 110, 255], a: 0.35 },
    ],
  },
  // A pale morning for the light looks.
  dawn: {
    ground: ["#dfe9f6", "#f3e9e4", "#f6dcc8"],
    lights: [
      { x: 300, y: 240, rx: 900, ry: 520, rgb: [150, 196, 255], a: 0.7 },
      { x: 1600, y: 860, rx: 900, ry: 520, rgb: [255, 170, 120], a: 0.6 },
      { x: 1100, y: 420, rx: 600, ry: 340, rgb: [255, 255, 255], a: 0.7 },
    ],
  },
  // Woods at night, for the evening's desktop.
  pine: {
    ground: ["#071613", "#0d2a25", "#06100f"],
    lights: [
      { x: 380, y: 300, rx: 880, ry: 520, rgb: [0, 170, 130], a: 0.5 },
      { x: 1560, y: 820, rx: 900, ry: 520, rgb: [20, 110, 170], a: 0.5 },
      { x: 1040, y: 1080, rx: 700, ry: 320, rgb: [230, 190, 90], a: 0.22 },
    ],
  },
  // Embers, for the focus desktop.
  ember: {
    ground: ["#1a0d12", "#2b1220", "#12080d"],
    lights: [
      { x: 1500, y: 300, rx: 860, ry: 520, rgb: [255, 110, 70], a: 0.5 },
      { x: 320, y: 860, rx: 900, ry: 520, rgb: [170, 60, 190], a: 0.48 },
      { x: 1040, y: 560, rx: 520, ry: 300, rgb: [255, 190, 110], a: 0.2 },
    ],
  },
};

const page = { w: 1920, h: 1080 };

export function wallpaperHtml({ ground, lights }) {
  const blobs = lights
    .map((l) => `<ellipse cx="${l.x}" cy="${l.y}" rx="${l.rx}" ry="${l.ry}" fill="rgba(${l.rgb.join(",")},${l.a})"/>`)
    .join("");
  return `<!doctype html><html><head><meta charset="utf-8"><style>
html, body { margin: 0; width: ${page.w}px; height: ${page.h}px; overflow: hidden; }
body { background: linear-gradient(160deg, ${ground[0]} 0%, ${ground[1]} 52%, ${ground[2]} 100%); }
svg { position: absolute; left: 0; top: 0; }
</style></head><body>
<svg width="${page.w}" height="${page.h}" viewBox="0 0 ${page.w} ${page.h}">
  <defs>
    <filter id="soft" x="-50%" y="-50%" width="200%" height="200%"><feGaussianBlur stdDeviation="150"/></filter>
    <filter id="grain"><feTurbulence type="fractalNoise" baseFrequency="0.9" numOctaves="2" seed="7"/><feColorMatrix values="0 0 0 0 1  0 0 0 0 1  0 0 0 0 1  0 0 0 0.07 0"/></filter>
  </defs>
  <g filter="url(#soft)">${blobs}</g>
  <rect width="100%" height="100%" filter="url(#grain)"/>
</svg></body></html>`;
}

export const wallpaperPath = (name) => join(repo, "store", "wallpapers", name + ".jpg");

if (process.argv[1] === fileURLToPath(import.meta.url)) {
  for (const [name, wallpaper] of Object.entries(wallpapers)) {
    // A window larger than the page, cut back to it (chrome.mjs).
    const png = join(out, "compose", `wallpaper-${name}.png`);
    render(`wallpaper-${name}`, wallpaperHtml(wallpaper), png, page.w + 200, page.h + 200, 1, { w: page.w, h: page.h });
    toJpeg(png, wallpaperPath(name), 92);
  }
}
