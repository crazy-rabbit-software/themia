// The listing's screenshots as one long picture, cut into the Store's pages (compose.mjs): side by side in the Store's
// row they are one scene. Three parts, in the order somebody meets Themia: what it is (widgets on the desktop, how many
// kinds), what it shows (your accounts, your PC, your files) and how the desktop becomes yours (a widget's styles,
// arranging, the look, a desktop for every part of the day). From page to page other widgets:
// a kind is shown once where it can be. The wall runs over the cuts into the pages beside it; words and monitors
// never do.
//
// Everything shown is a scene of store/scenes.json, drawn by the app (store/capture.ps1): 1920 x 1080 DIPs, a desktop
// (the widgets on a wallpaper, Windows' taskbar under them) or a sheet of widgets on nothing. A frame is one page, 1920 x 1080 px, in the
// order of listing/<lang>.json's screenshots (its shot the same):
//   cards  a screen: a whole scene on its wallpaper in a monitor, 16:9, the whole monitor and its stand on its page
//          (monitorBox): x, y and w its screen's box; cuts the scenes sharing it in strips, left to right, each cut
//          leaning tilt of its width. A piece: a part of a scene, [scene, widget id] (the widget's box in scenes.json),
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
    return [s.name, { name: s.name, wallpaper: scene.wallpaper, taskbar: Boolean(scene.taskbar), edit: Boolean(scene.edit), kinds: scene.widgets.map((w) => w.kind), widgets }];
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
        { dy: 100, pieces: [["more", "focusSession"], ["more", "recycleBin"], ["more", "audioVisualizer"], ["accounts", "weekPlanner"]] },
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
        // Two on the open wallpaper beside what they name, two beside the monitor.
        bubbles: [
          { on: "cores", at: [0, 0.5], to: [700, 470] },
          { on: "topProcesses", at: [0, 0.4], to: [640, 760] },
          { on: "network", at: [1, 0.5], to: [1764, 330] },
          { on: "battery", at: [1, 0.5], to: [1764, 700] },
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
          { on: "folder", at: [1, 0.3], to: [930, 370] },
          { on: "pictures", at: [1, 0.5], to: [880, 720] },
          { on: "usb", at: [1, 0.5], to: [1764, 330] },
          { on: "shortcuts", at: [0, 0.5], to: [1090, 500] },
          { on: "recentFiles", at: [0, 0.5], to: [1150, 850] },
        ],
      },
    ],
  },
  {
    shot: "styles",
    cards: sheet("styles", 96, 290, 0.9),
  },
  {
    shot: "edit",
    cards: [
      {
        screen: "edit",
        ...screen,
        // On the open wallpaper under the edit bar, a line up to each part of it; one beside the widget's menu.
        bubbles: [
          { at: [620, 58], to: [790, 334] },
          { at: [1092, 58], to: [1010, 352] },
          { at: [1236, 58], to: [1130, 432] },
          { at: [1339, 58], to: [1215, 352] },
          { at: [873, 420], to: [1100, 530] },
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
      { piece: ["evening"], ground: true, x: 30, y: 500, w: 820, r: -6, z: 2, bubbles: [{ at: [420, 56], dy: -44 }] },
      { piece: ["desktop"], ground: true, x: 510, y: 300, w: 900, z: 3, bubbles: [{ at: [960, 0], dy: -44 }] },
      { piece: ["focus"], ground: true, x: 1070, y: 500, w: 820, r: 6, z: 2, bubbles: [{ at: [1500, 74], dy: -44 }] },
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
