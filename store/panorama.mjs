// The listing's screenshots as one long picture, cut into the Store's pages (compose.mjs): side by side in the Store's
// row they are one scene. Three parts, in the order somebody meets Themia: what it is (widgets on the desktop; small
// ones that join into panels; ready-made groups to start from), what it shows (your accounts, your PC, your files,
// and the same thing lit in every widget) and how the desktop becomes yours (arranging, the look, a desktop for
// every part of the day). Words and monitors never cross a cut.
//
// Everything shown is a scene of store/scenes.json, drawn by the app (store/capture.ps1): the scenes' display in DIPs (display), a desktop
// (the widgets on a wallpaper, Windows' taskbar under them) or a sheet of widgets on nothing. A frame is one page, 1920 x 1080 px, in the
// order of listing/<lang>.json's screenshots (its shot the same):
//   cards  a screen: a whole scene on its wallpaper in a monitor, 16:9, the whole monitor and its stand on its page
//          (monitorBox): x, y and w its screen's box; cuts the scenes sharing it in strips, left to right, each cut
//          leaning tilt of its width. A piece: a part of a scene, [scene, widget id] (the widget's box as the app drew it: the scene's layout),
//          [scene, [x, y, w, h]] (DIPs) or [scene] (all of it): x and y in the page's px (less than 0 or past 1920 is
//          in the page before or after), w its width (its height follows), r a turn in degrees, z over or under its
//          neighbours, ground a piece of a desktop with its wallpaper, its corners rounded here (else the widget
//          alone, a shadow under it), lens enlarging that piece of the screen it names (that card's place among the frame's
//          cards), marked there, light widening to it. bubbles: a card's callouts, each at a widget of its scene (on:
//          its id; at: fractions of its box) or at a point (at, DIPs), the bubble dx, dy page px from there or at a
//          place of the page (to), worded by
//          the listing's callouts in the frame's order
//   wall   lists of pieces side by side, each w wide and gap apart from x, y (a list's dy lower), the whole turned r
//          degrees about the page's middle, fading in between fade's two heights (under the headline): the widgets,
//          more than a page holds
//   lanes  what Themia reads from the accounts, a lane each, w wide, h tall and gap apart from x, y, pad inside: what
//          it gives (the listing's callout of its place, and a Segoe Fluent Icons glyph), that as Themia shows it (a
//          piece), and the services it comes from, each a name and its logo (compose.mjs's logos)
import { existsSync, readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

export const page = { w: 1920, h: 1080 };

/** A monitor's parts round a screen w px wide: its thin bezel all round, the neck and the foot of its stand. */
export function monitorParts(w) {
  return {
    bezel: Math.round(w * 0.0075),
    neck: { w: Math.round(w * 0.1), h: Math.round(w * 0.05) },
    foot: { w: Math.round(w * 0.28), h: Math.round(w * 0.012) },
  };
}

/** Where a screen card's whole monitor stands in its page, the stand's foot included. */
export function monitorBox(card) {
  const { bezel, neck, foot } = monitorParts(card.w);
  const h = Math.round((card.w * 9) / 16);
  return { left: card.x - bezel, top: card.y - bezel, right: card.x + card.w + bezel, bottom: card.y + h + bezel + neck.h + foot.h };
}

/**
 * scenes.json as a map: each scene by its name with what it starts from (like) filled in, its wallpaper, and its
 * widgets' boxes by id as the app drew them (layout: a scene's name to its <scene>.layout.json, which --scene-png
 * writes beside the picture; a ready-made group's widgets are id-kind there). A scene with no layout yet has the
 * boxes scenes.json itself gives.
 */
export function scenesOf(doc, layout = () => undefined) {
  const raw = new Map(doc.scenes.map((s) => [s.name, s]));
  const whole = (name, seen = []) => {
    const scene = raw.get(name);
    if (!scene) throw new Error(`scenes: no scene ${name}`);
    if (seen.includes(name)) throw new Error(`scenes: ${[...seen, name].join(" > ")} goes round`);
    return scene.like ? { ...whole(scene.like, [...seen, name]), ...scene } : scene;
  };
  return new Map(doc.scenes.map((s) => {
    const scene = whole(s.name);
    const drawn = layout(s.name);
    const widgets = new Map();
    const kinds = [];
    for (const w of drawn?.widgets ?? scene.widgets.filter((w) => w.kind)) {
      const id = w.id ?? w.kind;
      if (widgets.has(id)) throw new Error(`scenes: ${s.name} has two widgets ${id}: give one an id`);
      const [x, y, width, height] = w.at;
      widgets.set(id, { x, y, w: width, h: height });
      kinds.push(w.kind);
    }
    return [s.name, { name: s.name, wallpaper: scene.wallpaper, taskbar: Boolean(scene.taskbar), edit: Boolean(scene.edit), drawn: Boolean(drawn), kinds, widgets }];
  }));
}

/** A piece's box in its scene (DIPs): a widget's by its id, several widgets' together, a given box, or the whole display. */
export function pieceRect(scenes, [name, part]) {
  const scene = scenes.get(name);
  if (!scene) throw new Error(`panorama: no scene ${name}`);
  if (part === undefined) return { x: 0, y: 0, w: display.w, h: display.h };
  if (Array.isArray(part) && typeof part[0] === "number") return { x: part[0], y: part[1], w: part[2], h: part[3] };
  const rects = (Array.isArray(part) ? part : [part]).map((id) => {
    const rect = scene.widgets.get(id);
    if (!rect) throw new Error(`panorama: scene ${name} has no widget ${id} (has: ${[...scene.widgets.keys()].join(", ")})`);
    return rect;
  });
  const x = Math.min(...rects.map((r) => r.x));
  const y = Math.min(...rects.map((r) => r.y));
  return { x, y, w: Math.max(...rects.map((r) => r.x + r.w)) - x, h: Math.max(...rects.map((r) => r.y + r.h)) - y };
}

const here = dirname(fileURLToPath(import.meta.url));
const doc = JSON.parse(readFileSync(join(here, "scenes.json"), "utf8"));

/** The scenes' display, in DIPs: what a whole scene's picture shows. */
export const display = { w: doc.screen?.width ?? 1920, h: doc.screen?.height ?? 1080 };

/** A scene's layout as the app drew it (store/capture.ps1 writes it beside the English picture), or none yet. */
function layoutOf(name) {
  const file = join(here, "out", "shots", "en", name + ".layout.json");
  return existsSync(file) ? JSON.parse(readFileSync(file, "utf8")) : undefined;
}

export const scenes = scenesOf(doc, layoutOf);

// Every band page's monitor: the same place, the same size.
const screen = { x: 320, y: 248, w: 1280 };

export const frames = [
  {
    shot: "desktop",
    cards: [{ screen: "desktop", ...screen }],
  },
  {
    shot: "compose",
    cards: [
      // Three small widgets apart, then the same three side by side: one panel.
      { piece: ["compose", "a-cpu"], x: 120, y: 330, w: 330, r: -4, bubbles: [{ on: "a-cpu", at: [0.25, 0], dy: -52 }] },
      { piece: ["compose", "a-history"], x: 170, y: 520, w: 330, r: 3, bubbles: [{ on: "a-history", at: [0.2, 1], dy: 56 }] },
      { piece: ["compose", "a-facts"], x: 110, y: 690, w: 330, r: -2, bubbles: [{ on: "a-facts", at: [0.3, 1], dy: 56 }] },
      { piece: ["compose", ["b-cpu", "b-history", "b-facts"]], x: 560, y: 360, w: 380, z: 2, bubbles: [{ on: "b-cpu", at: [0.5, 0], dy: -56 }] },
      // A bigger one, and two the app has ready.
      { piece: ["compose", ["b-station-weatherNow", "b-station-weatherFacts", "b-station-weatherForecast", "b-station-rainNextHour"]], x: 1040, y: 300, w: 380, z: 2, bubbles: [{ on: "b-station-weatherNow", at: [0.5, 0], dy: -56 }] },
      { piece: ["compose", ["b-playing-musicCover", "b-playing-music", "b-playing-musicProgress"]], x: 1470, y: 380, w: 400, r: 3 },
      { piece: ["compose", ["b-quick-wifiToggle", "b-quick-bluetoothToggle", "b-quick-doNotDisturbToggle", "b-quick-nightLightToggle"]], x: 1450, y: 640, w: 430, r: -3 },
    ],
  },
  {
    shot: "add",
    cards: [
      {
        screen: "add",
        ...screen,
        // The add page hangs under the bar: its ready-made groups, its subjects, its search.
        bubbles: [
          { at: [300, 104], to: [150, 420] },
          { at: [300, 250], to: [150, 640] },
          { at: [860, 104], to: [1770, 420] },
        ],
      },
    ],
  },
  {
    shot: "accounts",
    lanes: {
      x: 70,
      y: 250,
      w: 340,
      h: 790,
      gap: 20,
      pad: 16,
      lanes: [
        { glyph: "", piece: ["accounts", "calendar"], from: [["Microsoft 365", "microsoft"], ["Outlook.com", "microsoftoutlook"], ["iCloud", "icloud"], ["CalDAV", "calendar"]] },
        { glyph: "", piece: ["accounts", "email"], from: [["Microsoft 365", "microsoft"], ["Outlook.com", "microsoftoutlook"], ["iCloud", "icloud"], ["IMAP", "mail"]] },
        { glyph: "", piece: ["accounts", "tasks"], from: [["Microsoft To Do", "microsoft"], ["CalDAV", "tasks"], ["On this PC", "pc"]] },
        { glyph: "", piece: ["accounts", "contacts"], from: [["Microsoft 365", "microsoft"], ["Outlook.com", "microsoftoutlook"], ["iCloud", "icloud"], ["CardDAV", "contacts"]] },
        { glyph: "", piece: ["accounts", "meeting-nextMeeting"], from: [["Teams", "microsoftteams"], ["Zoom", "zoom"], ["Webex", "webex"]] },
      ],
    },
  },
  {
    shot: "pc",
    cards: [
      {
        screen: "pc",
        ...screen,
        // On the open wallpaper beside what they name; the moment pointed at runs down every chart.
        bubbles: [
          { on: "cpuHistory", at: [0.6, 0.5], to: [1764, 300] },
          { on: "memory", at: [1, 0.5], to: [1764, 560] },
          { on: "processes", at: [0, 0.4], to: [640, 360] },
          { on: "pingFacts", at: [0, 0.5], to: [640, 650] },
        ],
      },
    ],
  },
  {
    shot: "files",
    cards: [
      {
        screen: "files",
        ...screen,
        bubbles: [
          { on: "files-folder", at: [0, 0.4], to: [860, 390] },
          { on: "files-shortcuts", at: [1, 0.5], to: [1764, 300] },
          { on: "files-recentFiles", at: [1, 0.6], to: [1764, 470] },
          { on: "notes", at: [0, 0.5], to: [880, 640] },
          { on: "system-drive", at: [1, 0.5], to: [1764, 640] },
        ],
      },
    ],
  },
  {
    shot: "days",
    cards: [
      {
        screen: "days",
        ...screen,
        // A day pointed at in the calendar is lit in the forecast and the planner too.
        bubbles: [
          { on: "calendar", at: [0, 0.55], to: [1000, 330] },
          { on: "forecast", at: [1, 0.5], to: [1764, 560] },
          { on: "planner", at: [0, 0.3], to: [860, 700] },
        ],
      },
    ],
  },
  {
    shot: "edit",
    cards: [
      {
        screen: "edit",
        ...screen,
        // The bar, the group's own entry on it, and its page: one setting for all of its widgets.
        bubbles: [
          { at: [420, 33], to: [150, 330] },
          { at: [854, 33], to: [1770, 300] },
          { at: [700, 269], to: [150, 560] },
          { at: [900, 468], to: [1770, 640] },
        ],
      },
    ],
  },
  {
    shot: "looks",
    cards: [
      // One desktop, cut into the look it comes with and three of its others, each on a wallpaper of its own.
      { screen: "looks", cuts: ["looks", "look-daylight", "look-glass", "look-terminal"], tilt: 0.07, ...screen },
    ],
  },
  {
    shot: "views",
    cards: [
      // Three desktops of one PC, fanned: the evening's, the day's on top, the one for focus; each named.
      { piece: ["evening"], ground: true, x: 30, y: 500, w: 820, r: -6, z: 2, bubbles: [{ at: [336, 45], dy: -44 }] },
      { piece: ["desktop"], ground: true, x: 510, y: 300, w: 900, z: 3, bubbles: [{ at: [768, 0], dy: -44 }] },
      { piece: ["focus"], ground: true, x: 1070, y: 500, w: 820, r: 6, z: 2, bubbles: [{ at: [1200, 60], dy: -44 }] },
    ],
  },
];

/** The scenes the frames are made of. */
export function usedScenes() {
  const names = new Set();
  for (const frame of frames) {
    for (const c of frame.cards ?? []) {
      for (const name of c.cuts ?? [c.screen ?? c.piece[0]]) names.add(name);
    }
    for (const list of frame.wall?.lists ?? []) for (const p of list.pieces) names.add(p[0]);
    for (const lane of frame.lanes?.lanes ?? []) names.add(lane.piece[0]);
  }
  return [...names];
}
