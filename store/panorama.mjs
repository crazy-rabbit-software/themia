// The listing's screenshots as one long picture, cut into the Store's pages (compose.mjs): side by side in the Store's
// row they are one scene. Four parts, in the order somebody meets Themia: what it is (widgets on the desktop, how many
// kinds), what it shows (your accounts, your PC, your files), how a widget adapts (its sizes, its styles) and how the
// desktop becomes yours (arranging, the look, a desktop for every part of the day). From page to page other widgets:
// a kind is shown once where it can be. The wall runs over the cuts into the pages beside it; words and monitors
// never do.
//
// Everything shown is a scene of store/scenes.json, drawn by the app (store/capture.ps1): 1920 x 1080 DIPs, the widgets
// on its wallpaper (store/wallpaper.mjs) or, a sheet of widgets, on nothing. A frame is one page, 1920 x 1080 px, in the
// order of listing/<lang>.json's screenshots (its shot the same):
//   cards  a screen: a whole scene on its wallpaper in a monitor, 16:9, the whole monitor and its stand on its page
//          (monitorBox): x, y and w its screen's box; cuts the scenes sharing it in strips, left to right, each cut
//          leaning tilt of its width. A piece: a part of a scene, [scene, widget id] (the widget's box in scenes.json),
//          [scene, [x, y, w, h]] (DIPs) or [scene] (all of it): x and y in the page's px (less than 0 or past 1920 is
//          in the page before or after), w its width (its height follows), r a turn in degrees, z over or under its
//          neighbours, ground a piece of a desktop with its wallpaper, its corners rounded here (else the widget
//          alone, a shadow under it), lens enlarging that piece of the screen it names (that card's place among the frame's
//          cards), marked there, light widening to it. bubbles: a card's callouts, each at a widget of its scene (on:
//          its id; at: fractions of its box) or at a point (at, DIPs), the bubble dx, dy page px from there, worded by
//          the listing's callouts in the frame's order
//   wall   lists of pieces side by side, each w wide and gap apart from x, y (a list's dy lower), the whole turned r
//          degrees about the page's middle, fading in between fade's two heights (under the headline): the widgets,
//          more than a page holds
//   lanes  what Themia reads from the accounts, a lane each, w wide, h tall and gap apart from x, y, pad inside: what
//          it gives (the listing's callout of its place, and a Segoe Fluent Icons glyph), that as Themia shows it (a
//          piece), and the services it comes from, each a name and its logo (compose.mjs's logos)
import { readFileSync } from "node:fs";
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
 * scenes.json as a map: each scene by its name with what it starts from (like) filled in, its wallpaper and its
 * widgets' boxes by id (a widget's id, or its kind where it gives none).
 */
export function scenesOf(doc) {
  const raw = new Map(doc.scenes.map((s) => [s.name, s]));
  const whole = (name, seen = []) => {
    const scene = raw.get(name);
    if (!scene) throw new Error(`scenes: no scene ${name}`);
    if (seen.includes(name)) throw new Error(`scenes: ${[...seen, name].join(" > ")} goes round`);
    return scene.like ? { ...whole(scene.like, [...seen, name]), ...scene } : scene;
  };
  return new Map(doc.scenes.map((s) => {
    const scene = whole(s.name);
    const widgets = new Map();
    for (const w of scene.widgets) {
      const id = w.id ?? w.kind;
      if (widgets.has(id)) throw new Error(`scenes: ${s.name} has two widgets ${id}: give one an id`);
      const [x, y, width, height] = w.at;
      widgets.set(id, { x, y, w: width, h: height });
    }
    return [s.name, { name: s.name, wallpaper: scene.wallpaper, edit: Boolean(scene.edit), widgets }];
  }));
}

/** A piece's box in its scene (DIPs): a widget's by its id, a given box, or the whole screen. */
export function pieceRect(scenes, [name, part]) {
  const scene = scenes.get(name);
  if (!scene) throw new Error(`panorama: no scene ${name}`);
  if (part === undefined) return { x: 0, y: 0, w: page.w, h: page.h };
  if (Array.isArray(part)) return { x: part[0], y: part[1], w: part[2], h: part[3] };
  const rect = scene.widgets.get(part);
  if (!rect) throw new Error(`panorama: scene ${name} has no widget ${part}`);
  return rect;
}

export const scenes = scenesOf(JSON.parse(readFileSync(join(dirname(fileURLToPath(import.meta.url)), "scenes.json"), "utf8")));

/** Every widget of a scene as a piece, laid out as in the scene: k times its size, the scene's corner at x, y. */
const sheet = (name, x, y, k) => [...scenes.get(name).widgets].map(([id, r]) => ({ piece: [name, id], x: Math.round(x + r.x * k), y: Math.round(y + r.y * k), w: Math.round(r.w * k) }));

/** Pieces side by side on one line: their feet at y, k times their size, 24 px apart, about the page's middle. */
function row(pieces, y, k) {
  const boxes = pieces.map((p) => pieceRect(scenes, p));
  let x = Math.round((page.w - boxes.reduce((w, r) => w + r.w * k, 0) - 24 * (pieces.length - 1)) / 2);
  return pieces.map((piece, i) => {
    const card = { piece, x, y: Math.round(y - boxes[i].h * k), w: Math.round(boxes[i].w * k) };
    x += card.w + 24;
    return card;
  });
}

// Every band page's monitor: the same place, the same size.
const screen = { x: 320, y: 248, w: 1280 };

export const frames = [
  {
    shot: "desktop",
    cards: [{ screen: "desktop", ...screen }],
  },
  {
    shot: "widgets",
    // Five lists of widgets the other pages don't show, tilted, running on into the pages beside.
    wall: {
      x: -90,
      y: 110,
      w: 400,
      gap: 20,
      r: -5,
      fade: [200, 300],
      lists: [
        { dy: 60, pieces: [["more", "habitTracker"], ["more", "stopwatch"], ["more", "airQuality"], ["more", "volume"], ["more", "quote"]] },
        { dy: 0, pieces: [["more", "sun"], ["more", "screenTimeChart"], ["more", "alarm"], ["more", "notifications"], ["more", "brightness"]] },
        { dy: 80, pieces: [["more", "countdown"], ["more", "exchangeRates"], ["more", "mixer"], ["more", "bluetooth"]] },
        { dy: 20, pieces: [["more", "rainNextHour"], ["more", "moon"], ["more", "screenTimeApps"], ["more", "scratchpad"]] },
        { dy: 100, pieces: [["more", "focusSession"], ["more", "videoStream"], ["more", "audioVisualizer"], ["accounts", "weekPlanner"]] },
      ],
    },
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
        { glyph: "", piece: ["accounts", "nextMeeting"], from: [["Teams", "microsoftteams"], ["Zoom", "zoom"], ["Webex", "webex"]] },
      ],
    },
  },
  {
    shot: "pc",
    cards: [
      {
        screen: "pc",
        ...screen,
        bubbles: [
          { on: "cores", at: [0.5, 0.1], dy: -130 },
          { on: "memory", at: [0.5, 0.1], dy: -70 },
          { on: "gpu", at: [0.5, 0.1], dy: -130 },
          { on: "network", at: [0.5, 0.1], dy: -70 },
          { on: "topProcesses", at: [0.02, 0.5], dx: -190, dy: 0 },
          { on: "battery", at: [0.98, 0.5], dx: 170, dy: 0 },
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
          { on: "folder", at: [0.5, 0.06], dy: -75 },
          { on: "shortcuts", at: [0.5, 0.08], dy: -125 },
          { on: "drive", at: [0.6, 0.1], dy: -75 },
          { on: "recentFiles", at: [0.02, 0.6], dx: -560, dy: 40 },
          { on: "gallery", at: [0.98, 0.5], dx: 150, dy: 0 },
        ],
      },
    ],
  },
  {
    shot: "sizes",
    // One widget from its smallest to a large one, standing on one line; two more under it.
    cards: [
      ...row([["sizes", "xs"], ["sizes", "s"], ["sizes", "m"], ["sizes", "l"]], 640, 0.9),
      ...row([["sizes", "cal-s"], ["sizes", "cal-m"], ["sizes", "cal-l"], ["sizes", "cpu-s"], ["sizes", "cpu-m"]], 1050, 0.75),
    ],
  },
  {
    shot: "styles",
    cards: sheet("styles", 96, 290, 0.9),
  },
  {
    shot: "edit",
    cards: [
      { screen: "edit", ...screen },
      // The edit bar and the selected widget's menu enlarged, each over its place on the screen.
      {
        piece: ["edit", [462, 4, 996, 60]],
        ground: true,
        radius: 12,
        lens: 0,
        x: 130,
        y: 440,
        w: 1210,
        z: 3,
        bubbles: [{ at: [620, 12], dy: -52 }, { at: [1092, 12], dy: -52 }, { at: [1236, 56], dy: 52 }, { at: [1339, 12], dy: -52 }],
      },
      {
        piece: ["edit", [981, 88, 326, 446]],
        ground: true,
        radius: 12,
        lens: 0,
        x: 1420,
        y: 400,
        w: 380,
        z: 3,
        bubbles: [{ at: [1144, 96], dy: -56 }],
      },
    ],
  },
  {
    shot: "looks",
    cards: [
      // One desktop, cut into the look it comes with and three of its others.
      { screen: "desktop", cuts: ["desktop", "look-daylight", "look-glass", "look-terminal"], tilt: 0.07, ...screen },
    ],
  },
  {
    shot: "views",
    cards: [
      // Three desktops of one PC, fanned: the evening's, the day's on top, the one for focus.
      { piece: ["evening"], ground: true, x: 90, y: 400, w: 760, r: -5, z: 2 },
      { piece: ["focus"], ground: true, x: 1070, y: 400, w: 760, r: 5, z: 2 },
      { piece: ["desktop"], ground: true, x: 500, y: 290, w: 920, z: 3 },
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
