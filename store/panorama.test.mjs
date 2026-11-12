// The panorama's frames (panorama.mjs) and the scenes they are made of (scenes.json):
// node --test store/panorama.test.mjs
import assert from "node:assert/strict";
import { existsSync, readFileSync } from "node:fs";
import { test } from "node:test";
import { frames, monitorBox, page, pieceRect, scenes, usedScenes } from "./panorama.mjs";

const english = JSON.parse(readFileSync(new URL("./listing/en.json", import.meta.url), "utf8"));
const doc = JSON.parse(readFileSync(new URL("./scenes.json", import.meta.url), "utf8"));
const cardsOf = (frame) => frame.cards ?? [];
const piecesOf = (frame) => [
  ...cardsOf(frame).filter((c) => c.piece).map((c) => c.piece),
  ...(frame.wall?.lists ?? []).flatMap((l) => l.pieces),
  ...(frame.lanes?.lanes ?? []).map((l) => l.piece),
];

test("the frames are the listing's screenshots, in their order", () => {
  assert.deepEqual(frames.map((f) => f.shot), english.screenshots.map((s) => s.shot));
});

test("every monitor stands whole on its own page, its stand included", () => {
  for (const frame of frames) {
    for (const card of cardsOf(frame).filter((c) => c.screen)) {
      const box = monitorBox(card);
      assert.ok(box.left >= 0 && box.right <= page.w, `${frame.shot}: the monitor crosses a cut (${box.left}..${box.right})`);
      assert.ok(box.top >= 0 && box.bottom <= page.h, `${frame.shot}: the monitor's stand leaves the page (${box.top}..${box.bottom})`);
    }
  }
});

test("a screen's cuts are one desktop restyled: the first its own, the others laid out like it", () => {
  for (const card of frames.flatMap(cardsOf).filter((c) => c.cuts)) {
    assert.equal(card.cuts[0], card.screen);
    const boxes = (name) => JSON.stringify([...scenes.get(name).widgets]);
    for (const cut of card.cuts) assert.equal(boxes(cut), boxes(card.screen), `${cut} is laid out otherwise than ${card.screen}`);
  }
});

test("a lens enlarges a screen of its own page, a piece of that screen's scene", () => {
  for (const frame of frames) {
    for (const card of cardsOf(frame).filter((c) => c.lens !== undefined)) {
      const screen = frame.cards[card.lens];
      assert.ok(screen?.screen, `${frame.shot}: a lens of no screen`);
      assert.equal(card.piece[0], screen.screen, `${frame.shot}: a lens of another scene than its screen's`);
    }
  }
});

test("every piece is a part of its scene, inside the screen", () => {
  for (const frame of frames) {
    for (const piece of piecesOf(frame)) {
      const r = pieceRect(scenes, piece);
      assert.ok(r.x >= 0 && r.y >= 0 && r.x + r.w <= page.w && r.y + r.h <= page.h && r.w > 0 && r.h > 0, `${frame.shot}: ${piece.join(" ")} leaves its scene`);
    }
  }
});

test("every callout of a page has one bubble or lane, and each of those a callout", () => {
  frames.forEach((frame, i) => {
    const said = cardsOf(frame).reduce((n, c) => n + (c.bubbles?.length ?? 0), 0) + (frame.lanes?.lanes.length ?? 0);
    assert.equal(said, english.screenshots[i].callouts?.length ?? 0, `${frame.shot}: ${said} bubbles and lanes for its callouts`);
  });
});

test("every bubble points at a widget of its card's scene, or at a point of it", () => {
  for (const frame of frames) {
    for (const card of cardsOf(frame)) {
      const scene = card.screen ?? card.piece[0];
      for (const bubble of card.bubbles ?? []) {
        if (bubble.on) pieceRect(scenes, [scene, bubble.on]);
        else assert.equal(bubble.at?.length, 2, `${frame.shot}: a bubble at nothing`);
      }
    }
  }
});

test("the wall shows no widget twice, and none the first page shows", () => {
  const first = new Set(doc.scenes.find((s) => s.name === frames[0].shot).widgets.map((w) => w.kind));
  for (const frame of frames.filter((f) => f.wall)) {
    const kinds = frame.wall.lists.flatMap((l) => l.pieces).map(([name, id]) => {
      const scene = doc.scenes.find((s) => s.name === name);
      return scene.widgets.find((w) => (w.id ?? w.kind) === id).kind;
    });
    assert.deepEqual(kinds, [...new Set(kinds)], `${frame.shot}: a widget twice on the wall`);
    assert.deepEqual(kinds.filter((k) => first.has(k)), [], `${frame.shot}: the wall repeats the first page's widgets`);
  }
});

test("a scene's widgets lie inside the screen and on none of its others", () => {
  for (const [name, scene] of scenes) {
    const boxes = [...scene.widgets];
    for (const [id, r] of boxes) {
      assert.ok(r.x >= 0 && r.y >= 0 && r.x + r.w <= page.w && r.y + r.h <= page.h, `${name}: ${id} leaves the screen`);
    }
    for (let a = 0; a < boxes.length; a++) {
      for (let b = a + 1; b < boxes.length; b++) {
        const [p, q] = [boxes[a][1], boxes[b][1]];
        const apart = p.x + p.w <= q.x || q.x + q.w <= p.x || p.y + p.h <= q.y || q.y + q.h <= p.y;
        assert.ok(apart, `${name}: ${boxes[a][0]} lies on ${boxes[b][0]}`);
      }
    }
  }
});

test("every desktop a page shows has a wallpaper, and the picture is there", () => {
  for (const frame of frames) {
    for (const card of cardsOf(frame)) {
      const names = card.cuts ?? (card.screen ? [card.screen] : card.ground ? [card.piece[0]] : []);
      for (const name of names) assert.ok(scenes.get(name).wallpaper, `${frame.shot}: ${name} has no wallpaper`);
    }
  }
  for (const name of usedScenes()) {
    const wallpaper = scenes.get(name).wallpaper;
    if (wallpaper) assert.ok(existsSync(new URL("./" + wallpaper, import.meta.url)), `${name}: no ${wallpaper} (node store/wallpaper.mjs)`);
  }
});

test("a sheet of widgets, cut into pieces, stands on nothing", () => {
  // A widget alone keeps its own corners and transparency only where its scene has no wallpaper behind it.
  for (const frame of frames) {
    const alone = [
      ...cardsOf(frame).filter((c) => c.piece && !c.ground && c.lens === undefined).map((c) => c.piece),
      ...(frame.wall?.lists ?? []).flatMap((l) => l.pieces),
      ...(frame.lanes?.lanes ?? []).map((l) => l.piece),
    ];
    for (const [name] of alone) assert.ok(!scenes.get(name).wallpaper, `${frame.shot}: a widget cut from ${name}, which has a wallpaper`);
  }
});
