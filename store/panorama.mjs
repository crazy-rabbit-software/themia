// The listing's screenshots as one long picture, cut into the Store's pages (compose.mjs): side by side in the Store's
// row they are one scene. It says what Themia does, never how it is used (the owner, 2026-11-13: "you should not
// explain how to use the product, you should say what it can do"; nothing of the add page, the edit bar or how a
// panel is built): the promise (a desktop), the office (your day; your accounts), the beautiful desktop (your PC;
// the weather and the time), everything else (a wall of widgets), yours (from one clock to a dashboard; the looks;
// a desktop for every part of the day) and what Themia stands for. The pages hang together: a widget lies across
// every cut, half on each page, the wall runs into the pages beside it, and one floor runs under them all
// (compose.mjs). Words and monitors stay whole on their own page.
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
//   values what Themia stands for: the listing's points of that page as tiles (x, y, w, gap), a glyph each
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

/** A ready-made group of a sheet as one piece: every widget the scene names id-<kind>. */
const group = (scene, id) => [scene, [...(scenes.get(scene)?.widgets.keys() ?? [])].filter((k) => k.startsWith(id + "-"))];

/** A widget lying across the cut to the next page, half on each: what makes the row one picture. */
const across = (piece, w, y, r) => ({ piece, x: page.w - Math.round(w / 2), y, w, r, z: 4 });

export const frames = [
  {
    shot: "desktop",
    cards: [{ screen: "desktop", ...screen }, across(["more-a", "quote"], 380, 800, -4)],
  },
  {
    shot: "day",
    cards: [
      {
        screen: "day",
        ...screen,
        // In the open middle of the wallpaper, beside what they name.
        bubbles: [
          { on: "myDay-nextMeeting", at: [1, 0.5], to: [780, 470] },
          { on: "planner", at: [0, 0.6], to: [880, 660] },
          { on: "email", at: [0, 0.4], to: [1120, 860] },
        ],
      },
      across(["more-a", "notifications"], 300, 420, 3),
    ],
  },
  {
    shot: "accounts",
    lanes: {
      x: 250,
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
      ],
    },
    cards: [across(group("more-b", "battery"), 260, 640, -4)],
  },
  {
    shot: "pc",
    cards: [
      {
        screen: "pc",
        ...screen,
        bubbles: [
          { on: "cpuHistory", at: [0, 0.5], to: [640, 330] },
          { on: "processes", at: [0, 0.5], to: [600, 560] },
          { on: "network", at: [0, 0.5], to: [640, 790] },
        ],
      },
      across(group("more-b", "moon"), 300, 380, 4),
    ],
  },
  {
    shot: "weather",
    cards: [
      {
        screen: "weather",
        ...screen,
        bubbles: [
          { on: "sun-sun", at: [1, 0.4], to: [1010, 400] },
          { on: "station-rainNextHour", at: [0.5, 1], to: [560, 850] },
          { on: "alarm", at: [0, 0.5], to: [1150, 640] },
        ],
      },
    ],
  },
  {
    shot: "widgets",
    // Six lists of widgets no other page shows, tilted, running on into the pages beside.
    wall: {
      x: -250,
      y: 110,
      w: 400,
      gap: 20,
      r: -5,
      fade: [200, 300],
      lists: [
        { dy: 60, pieces: [group("more-a", "screenTime"), ["more-a", "clipboard"], ["more-b", "recycleBin"]] },
        { dy: 0, pieces: [group("more-b", "sun"), group("more-a", "sound"), ["more-b", "notes"]] },
        { dy: 80, pieces: [group("more-a", "nowPlayingLarge"), ["more-b", "bluetooth"], ["more-b", "countdown"]] },
        { dy: 20, pieces: [group("more-b", "airQuality"), ["more-a", "habitTracker"], ["more-b", "alarm"]] },
        { dy: 100, pieces: [group("more-b", "drive"), ["more-a", "rss"], ["more-b", "stopwatch"]] },
        { dy: 40, pieces: [["more-b", "contacts"], group("more-a", "stocks"), group("more-a", "exchangeRates")] },
      ],
    },
  },
  {
    shot: "amount",
    cards: [
      // Four desktops of four people: one clock, a few widgets, a column, everything.
      { piece: ["one"], ground: true, x: 370, y: 290, w: 570, z: 2, bubbles: [{ at: [150, 60], dx: 0, dy: -64 }] },
      { piece: ["few"], ground: true, x: 980, y: 290, w: 570, z: 2, bubbles: [{ at: [150, 60], dx: 0, dy: -64 }] },
      { piece: ["side"], ground: true, x: 370, y: 660, w: 570, z: 2, bubbles: [{ at: [150, 60], dx: 0, dy: -64 }] },
      { piece: ["full"], ground: true, x: 980, y: 660, w: 570, z: 2, bubbles: [{ at: [150, 60], dx: 0, dy: -64 }] },
      across(["more-b", "pomodoro"], 280, 760, -3),
    ],
  },
  {
    shot: "looks",
    cards: [
      // One desktop, cut into the look it comes with and three of its others, each on a wallpaper of its own.
      { screen: "looks", cuts: ["looks", "look-daylight", "look-glass", "look-terminal"], tilt: 0.07, ...screen },
      across(group("more-a", "quickSettings"), 420, 290, 3),
    ],
  },
  {
    shot: "views",
    cards: [
      // Three desktops of one PC, fanned: the evening's, the day's on top, the one for focus; each named.
      { piece: ["evening"], ground: true, x: 30, y: 500, w: 820, r: -6, z: 2, bubbles: [{ at: [336, 45], dy: -44 }] },
      { piece: ["desktop"], ground: true, x: 510, y: 300, w: 900, z: 3, bubbles: [{ at: [768, 0], dy: -44 }] },
      { piece: ["focus"], ground: true, x: 1070, y: 500, w: 820, r: 6, z: 2, bubbles: [{ at: [1200, 60], dy: -44 }] },
      across(group("more-b", "power"), 400, 300, -3),
    ],
  },
  {
    shot: "promise",
    // What Themia stands for: the listing's points, a tile each.
    values: { x: 150, y: 400, w: 300, h: 440, gap: 30, glyphs: ["\uE72E", "\uE77B", "\uE8C7", "\uE945", "\uE943"] },
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
