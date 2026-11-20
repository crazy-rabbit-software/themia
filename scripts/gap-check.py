"""[S-12815] [S-12831] The gap check: every widget kind, in every state its renders offer, at its range (its least size,
its natural and its large) and at real desktop sizes, measured for the empty band between its ink and its card's
edges, against the owner's rules as worded in docs/WIDGET-RULES.md. Needs a Debug build of the app.

usage: python scripts/gap-check.py [--exe <Themia.exe>] [--out <dir>] [--kinds a,b] [--jobs 4] [--timeout 120]
       python scripts/gap-check.py --self-test   # the judge's rules on made-up cards, no render

For each kind (`--changed`, the default, `--kinds` or `--all`) it draws each state the build's catalog lists, once a
build for every check (scripts/renders.py, [S-14341] [S-14342]),
with `--widget-png <kind> <png> --state <s> --sizes range --transparent --layout-json <json>` and again at the desktop
sizes below, crops every card at the box the render laid it in (the layout's x, y, w, h: [S-14300]), finds its first and
last row and column of ink (a pixel off the card's own colour), and judges:

- WR-1 top: the band over the first ink is at most the inset (12, the padding inside an item, WR-3) and the 4 DIP the
  measure may be off by (antialiasing, a glyph's own room in its box). Where the card's items stand on its top edge
  (the layout's item boxes, `ItemMark`), the band over the ink is the item's own padding and the item check
  (scripts/item-check.py) judges the boxes instead.
- WR-4 / WR-7 foot: the band under the last ink is at most the inset and the 4 DIP, but in a card with items: their
  empty rows may stand at the foot (WR-5), judged by the item check.
- sides: the band beside the ink, either side, is at most the inset and the 4 DIP. Where the card's item boxes reach
  both its edges (a list's rows), the band beside the ink is the items' own padding and a short title's room, and the
  item check judges the boxes instead (WR-3: "An item's box (its row band, cell, hover area) runs from the card's left
  edge to its right edge"), as for the foot; the ink rule stays for a card without items and one whose items do not
  reach both edges (owner, 2026-11-20, of this check flagging list rows whose short titles leave the right band wide:
  "you should choose what works").
- [S-6754] a graph's plot (the one chart's box, `plots` in the layout): the plot reaches the card's edges, left, right,
  top and foot (owner, 2026-11-19: "Plot edge to edge": the plot and its fill reach the card's edges, the labels stand
  inside it, over the plot); its top right under the card's compact Set up line where the card shows one ([S-12835],
  the layout's `setup`). Where it does, the plot is the card's top and foot (WR-1, WR-4: a line chart's line stands
  where its value is, its plot still reaches the edges) and, where it reaches the card's left and right edges, its sides
  too (WR-3: a flat zero line with only a status text over it is a plot edge to edge); a graph kind drawn with no plot is
  a finding (WR-10).
- [S-14303] a free field (`fields` in the layout: the note's editor, the scratchpad's pad) holds what its person put in
  it where they put it: it runs from the card's left edge to its right (a field short of either is a finding) and is the
  card's sides; where it reaches the card's foot it is the foot, and where it starts on the card's top the top, so the
  text or strokes in it are not judged by their bands there; what stands over or under it (a tool line, a pad's
  toolbar) is judged by its ink as any card's.
- [S-14302] a mark over the content (`marks`: the sources' problem glyph, KitStatus, over the content's top end) is no
  ink of the content's: it is left out of the measure.
- [S-14304] a text cut with its ellipsis at the width it was given (`cuts`) ends where its last whole letter and the
  ellipsis fit: where it ends the card's ink, the room after it is the cut's (less than the next letter), not a band, and
  the end side is read as on the inset.

The exemptions the owner granted (docs/WIDGET-RULES.md "Exemptions"): a kind whose face is a picture or a video
filling the card edge to edge (gallery, video stream, cover art) meets WR-1..WR-4 by that picture; a desktop icon keeps
Windows' icon cell (not judged); a tile (quick toggles, power buttons) and a one-line face (clock, date, header) may
stand centred: the bands over and under the face alike (within the tolerance), and [S-14305] its sides judged as any
card's (WR-3: each band beside it within the inset; tiles: "nothing else of WR-1..WR-4 is relaxed"); a one-line face
that fills the height (its line grows until the height stops it) stands across where its card's alignment puts it, as
a reading does ([S-14301]); a reading (a card
with no item and no plot) whose ink reaches the edges of one axis may stand centred on the other ([S-14301], owner
2026-11-20: "Centred is fine"): the two bands of that axis alike, not within the inset. Across, a reading that fills the
height stands where its card's alignment puts it (the layout's `align`, [S-6583]: a kind that aligns in its style draws
the alignment shown, any other is centred): centred, its two side bands alike; at the start, its left band within the
inset and its right free; at the end, mirrored. Down it is always centred. Every render runs
under a hard timeout and is killed when it overruns. Writes <out>/gaps.md (the whole matrix), <out>/gaps.json and
<out>/findings.txt; exits 1 on any finding.
"""
import argparse
import json
import os
import re
import sys

import numpy as np
from PIL import Image

import renders

INSET = 12.0
TOLERANCE = 4.0  # DIP the ink measure may be off by
SCALE = 1.5  # the renders' raster scale
# Desktop sizes, a few to a render so the page fits the screen and keeps the raster scale.
SIZES = ['330x130,350x70,350x80', '330x140,300x164,600x200', '520x600', '900x300']

# docs/WIDGET-RULES.md, "Exemptions granted by the owner": a picture or a video filling the card edge to edge.
PICTURES = {
    'gallery': 'a picture fills the card edge to edge',
    'videoStream': 'a video fills the card edge to edge',
    'musicCover': 'the cover art fills the card edge to edge',
}

# docs/WIDGET-RULES.md, "Exemptions granted by the owner" (2026-11-19): desktop icons keep Windows' icon cell.
WINDOWS_LOOK = {'desktopItem': "a desktop icon is drawn as Windows' own: icon and name in its cell"}

# docs/WIDGET-RULES.md, "Exemptions granted by the owner" (2026-11-19): a tile's face and a one-line face stand centred.
TILE = re.compile(r'Toggle$')
POWER = {'sleep', 'signOut', 'shutdown', 'screenOff', 'restart', 'lock', 'hibernate'}
ONE_LINE = {'clock', 'date', 'header'}

# [S-6754] The graph kinds: drawn on the one chart, their plot edge to edge in every state.
GRAPH = re.compile(r'(History|Graph)$')
GRAPHS = {'rainNextHour'}
EDGE = 0.5  # DIP: layout rounding


def classify(kind):
    """[S-14305] The one place a kind's class is read, from its name: 'picture' and 'windows' (the owner's exemptions,
    not judged), 'tile' and 'one-line' (centred down, judged across), 'graph' (its plot edge to edge) or 'card' (any
    other). A catalog of the kinds' traits can stand in for it here alone."""
    if kind in PICTURES:
        return 'picture'
    if kind in WINDOWS_LOOK:
        return 'windows'
    if TILE.search(kind) or kind in POWER:
        return 'tile'
    if kind in ONE_LINE:
        return 'one-line'
    if GRAPH.search(kind) or kind in GRAPHS:
        return 'graph'
    return 'card'


def graph(kind):
    return classify(kind) == 'graph'


def plot_box(layout):
    """The plots' union in the card (DIPs), or None without a plot."""
    plots = (layout or {}).get('plots', [])
    if not plots:
        return None
    return (min(p['x'] for p in plots), min(p['y'] for p in plots),
            max(p['x'] + p['w'] for p in plots), max(p['y'] + p['h'] for p in plots))


def field_box(layout):
    """[S-14303] The free fields' union in the card (DIPs), or None without one."""
    fields = (layout or {}).get('fields', [])
    if not fields:
        return None
    return (min(f['x'] for f in fields), min(f['y'] for f in fields),
            max(f['x'] + f['w'] for f in fields), max(f['y'] + f['h'] for f in fields))


def measure(path, layouts):
    """[S-14300] Each card's bands, measured inside the card's own box as the render laid it (the layout's x, y, w, h in
    the page's DIPs): never a card found by its alpha, which took a size label over a card for its top rows and skipped a
    card under 20 px (a quick toggle's least, 10x10), pairing every later card with the wrong layout. [S-14302] Its marks
    (the problem glyph over the content's top end) left out."""
    im = np.asarray(Image.open(path).convert('RGBA')).astype(int)
    out = []
    for card in layouts:
        scale = card.get('scale', SCALE)
        l, t = int(round(card['x'] * scale)), int(round(card['y'] * scale))
        r, b = int(round((card['x'] + card['w']) * scale)), int(round((card['y'] + card['h']) * scale))
        w, h = card['w'], card['h']
        # The card's own rim (its outline and its antialiased corners) is not ink: a band of 4 DIP, at most a third of
        # a small card's side so the least cards are measured too.
        edge = max(1, min(int(round(4 * scale)), (r - l) // 3, (b - t) // 3))
        sub = im[t + edge:b - edge, l + edge:r - edge, :3]
        if sub.size == 0:
            out.append((w, h, None, None, None, None))
            continue
        # The card's own colour: its commonest. Packed to one number a pixel (R, then G, then B: the same order, so the
        # same pick on a tie), as sorting rows of three took most of a sweep's time.
        flat = sub.reshape(-1, 3)
        keys, counts = np.unique((flat[:, 0] << 16) | (flat[:, 1] << 8) | flat[:, 2], return_counts=True)
        top = keys[counts.argmax()]
        bg = np.array([top >> 16, (top >> 8) & 255, top & 255])
        mask = np.abs(sub - bg).sum(axis=2) > 30
        # [S-14302] A mark over the content (the problem glyph) is not the content's ink.
        for m in card.get('marks', []):
            y0, x0 = int(m['y'] * scale) - edge, int(m['x'] * scale) - edge
            y1, x1 = int(np.ceil((m['y'] + m['h']) * scale)) - edge, int(np.ceil((m['x'] + m['w']) * scale)) - edge
            mask[max(0, y0):max(0, y1), max(0, x0):max(0, x1)] = False
        rows = np.where(mask.any(axis=1))[0]
        cols = np.where(mask.any(axis=0))[0]
        if len(rows) == 0:
            out.append((w, h, None, None, None, None))
            continue
        out.append((w, h, (rows[0] + edge) / scale, ((b - t) - (rows[-1] + 1 + edge)) / scale,
                    (cols[0] + edge) / scale, ((r - l) - (cols[-1] + 1 + edge)) / scale))
    return out


def judge(kind, card, layout):
    w, h, top, foot, left, right = card
    if top is None:
        return ['WR-10 nothing drawn']
    kind_class = classify(kind)
    if kind_class in ('picture', 'windows'):
        return []
    found = []
    limit = INSET + TOLERANCE
    # [S-14304] A text cut to the width it was given that ends the content's ink: the room after it is the cut's.
    cw = (layout or {}).get('w', w)
    if any(c['x'] + c['w'] >= cw - right - TOLERANCE for c in (layout or {}).get('cuts', [])):
        right = min(right, INSET)
    if kind_class in ('tile', 'one-line'):
        # Centred, as the owner granted: the bands over and under the face alike. [S-14305] Its sides as any card's (tiles:
        # "nothing else of WR-1..WR-4 is relaxed"); a one-line face that fills the height stands across where its
        # alignment puts it, as a reading ([S-14301]).
        if abs(top - foot) > TOLERANCE:
            found.append(f'centred face off centre {top:.1f}/{foot:.1f}')
        if left <= limit and right <= limit:
            return found
        if kind_class == 'one-line' and top <= limit and foot <= limit:
            return found + across(left, right, (layout or {}).get('align', 'center'), limit)
        return found + [f'WR-3 sides {left:.1f}/{right:.1f}']
    items = (layout or {}).get('items', [])
    on_top = any(i['y'] <= 0.5 for i in items)
    # [S-6754] A graph's plot reaches every edge of its card; where it does, it is the card's top and foot.
    plot = plot_box(layout)
    plot_top = plot_foot = False
    if layout is not None and graph(kind) and plot is None:
        found.append('WR-10 no plot')
    plot_sides = False
    if plot is not None:
        l, t, r, b = plot
        cw, ch = layout['w'], layout['h']
        top_edge = layout.get('setup', 0) + EDGE
        if l > EDGE or r < cw - EDGE or t > top_edge or b < ch - EDGE:
            found.append(f'plot not edge to edge {l:.1f}..{r:.1f} x {t:.1f}..{b:.1f} of {cw:.0f}x{ch:.0f}')
        plot_top, plot_foot = t <= top_edge, b >= ch - EDGE
        # [S-6754] A plot from the card's left edge to its right is the card's sides, as it is its top and foot (WR-3).
        plot_sides = l <= EDGE and r >= cw - EDGE
    # [S-14303] A free field (an editor, a drawing pad) is the card's content: what stands in it is its person's, where
    # they put it. Where the field reaches the card's sides and foot it is them (and its top where it starts there).
    field = field_box(layout)
    field_top = field_sides = field_foot = False
    if field is not None:
        l, t, r, b = field
        cw, ch = layout['w'], layout['h']
        field_sides = l <= EDGE and r >= cw - EDGE
        field_foot = b >= ch - EDGE
        field_top = t <= layout.get('setup', 0) + EDGE
        if not field_sides:
            found.append(f'field not edge to edge {l:.1f}..{r:.1f} x {t:.1f}..{b:.1f} of {cw:.0f}x{ch:.0f}')
    # [S-14301] A reading (no item, no plot) that fills one axis stands centred on the other (owner, 2026-11-20:
    # "Centred is fine"): that axis's bands alike, not within the inset; across, where its card's alignment puts it.
    if not items and plot is None and field is None:
        tall = top <= limit and foot <= limit
        wide = left <= limit and right <= limit
        if tall and not wide:
            return found + across(left, right, (layout or {}).get('align', 'center'), limit)
        if wide and not tall:
            return found + ([] if abs(top - foot) <= TOLERANCE else [f'reading off centre top/foot {top:.1f}/{foot:.1f}'])
    if top > limit and not on_top and not plot_top and not field_top:
        found.append(f'WR-1 top {top:.1f}')
    # Under a card's items the room left is theirs: empty rows at the foot (WR-5), judged by the item check.
    if foot > limit and not items and not plot_foot and not field_foot:
        found.append(f'WR-4 foot {foot:.1f}')
    # Item boxes from the card's left edge to its right: the item check's box rule judges them (WR-3), not their ink.
    width = (layout or {}).get('w', w)
    if not (items and spans(items, width)) and not plot_sides and not field_sides and (left > limit or right > limit):
        found.append(f'WR-3 sides {left:.1f}/{right:.1f}')
    return found


def across(left, right, align, limit):
    """[S-14301] The side bands of a reading that fills its card's height, judged by where its alignment puts it: centred,
    the two alike; at the start, the left within the inset and the right free; at the end, the right within it."""
    if align == 'left':
        return [] if left <= limit else [f'reading off the start {left:.1f}/{right:.1f}']
    if align == 'right':
        return [] if right <= limit else [f'reading off the end {left:.1f}/{right:.1f}']
    return [] if abs(left - right) <= TOLERANCE else [f'reading off centre sides {left:.1f}/{right:.1f}']


def spans(items, width):
    """Whether the card's item boxes reach both its edges (a list's rows, a row of cells together)."""
    return any(i['x'] <= 0.5 for i in items) and any(i['x'] + i['w'] >= width - 0.5 for i in items)


def sets():
    """The size sets every state is drawn at, by their labels."""
    return [('range', 'range')] + [(f'desk{i}', group) for i, group in enumerate(SIZES)]


def check(kind, states, drawn, out):
    """[S-14341] Judges a kind's renders, drawn once for every check (scripts/renders.py), linked into <out>/png."""
    lines = []
    for state in states:
        for label, sizes in sets():
            png = os.path.join(out, 'png', f'{kind}.{state or "default"}.{label}.png')
            layout_path = png + '.layout.json'
            result = drawn[renders.Job(kind, state, sizes)]
            renders.place(result, png)
            if result.timed_out or not os.path.exists(png):
                lines.append((kind, state, label, '-', '-', '-', ['render failed' if not result.timed_out else 'render timed out']))
                continue
            if not os.path.exists(layout_path):
                lines.append((kind, state, label, '-', '-', '-', ['render wrote no layout']))
                continue
            with open(layout_path, encoding='utf-8') as f:
                layouts = json.load(f).get('cards', [])
            for card, layout in zip(measure(png, layouts), layouts):
                w, h, top, foot = card[:4]
                box = f'{w:.0f}x{h:.0f}'
                fmt = lambda v: '-' if v is None else f'{v:.1f}'
                lines.append((kind, state, label, box, fmt(top), fmt(foot), judge(kind, card, layout)))
    return lines


def self_test():
    """The judge's rules on made-up cards (w, h, top, foot, left, right), each a case the renders met."""
    def card(w, h, top, foot, left, right, **layout):
        return (w, h, top, foot, left, right), dict({'w': w, 'h': h, 'items': [], 'plots': []}, **layout)

    cases = [
        # [S-14301] A reading that fills the height stands where its alignment puts it.
        ('recycleBin left in a wide card, its card centred', card(600, 200, 12, 12, 11.3, 423), [], True),
        ('the same reading at the start of a card aligned left', card(600, 200, 12, 12, 11.3, 423, align='left'), [], False),
        ('a reading aligned left off the start', card(600, 200, 12, 12, 40, 300, align='left'), [], True),
        ('a reading aligned right at the end', card(600, 200, 12, 12, 300, 12, align='right'), [], False),
        ('a reading aligned right standing at the start', card(600, 200, 12, 12, 12, 300, align='right'), [], True),
        ('a centred reading', card(600, 200, 12, 12, 150, 152), [], False),
        ('a centred reading, its card aligned centre', card(600, 200, 12, 12, 150, 152, align='center'), [], False),
        # Down it is always centred, whatever the alignment.
        ('a width-held reading centred down', card(300, 164, 30, 31, 12, 12, align='left'), [], False),
        ('a width-held reading high', card(300, 164, 42.7, 58, 12, 12, align='left'), [], True),
        # [S-6754] A plot edge to edge is the card's sides too: cpuHistory's "No data yet" over a flat zero line.
        ('a graph whose plot spans the card', card(330, 130, 12.7, 0, 12.7, 254.7,
                                                   plots=[{'x': 0, 'y': 0, 'w': 330, 'h': 130}]), [], False),
        ('a graph whose plot stops short', card(330, 130, 12.7, 0, 12.7, 254.7,
                                                plots=[{'x': 0, 'y': 0, 'w': 300, 'h': 130}]), [], True),
        # [S-14303] A note's text from the top of its editor, the editor the card's sides and foot.
        ('a short note in its editor', card(520, 600, 12, 416.7, 12, 14, fields=[{'x': 0, 'y': 34, 'w': 520, 'h': 566}]), [], False),
        ('a drawing low on its pad', card(384, 160, 34, 14, 8, 12.7, fields=[{'x': 0, 'y': 0, 'w': 384, 'h': 160}]), [], False),
        ('a pad over its toolbar', card(384, 160, 34, 14, 8, 12.7, fields=[{'x': 0, 'y': 0, 'w': 384, 'h': 120}]), [], False),
        ('an editor 8 in from the sides', card(330, 130, 12, 12, 12, 14, fields=[{'x': 8, 'y': 32, 'w': 314, 'h': 98}]), [], True),
        # [S-14304] The next meeting's title cut at the width it was given: "Design review:..." 39 from the end.
        ('a title cut to its width', card(350, 70, 12, 12, 15.3, 39.3, cuts=[{'x': 12, 'y': 0, 'w': 301.7, 'h': 56}]), [], False),
        ('a cut line under a wider one', card(350, 70, 12, 12, 15.3, 39.3, cuts=[{'x': 12, 'y': 0, 'w': 200, 'h': 56}]), [], True),
        ('a short title, not cut', card(350, 70, 12, 12, 15.3, 39.3), [], True),
        ('an editor short of the foot', card(520, 600, 12, 416.7, 12, 14, fields=[{'x': 0, 'y': 34, 'w': 520, 'h': 400}]), [], True),
        # [S-14305] A tile and a one-line face: centred down, their sides judged as any card's.
        ('a tile centred both ways', card(100, 100, 30, 30, 30, 30), ['wifiToggle'], True),
        ('a tile whose face fills its width', card(160, 100, 30, 31, 12, 12), ['wifiToggle'], False),
        ('a clock line the width stops', card(600, 200, 50, 51, 12, 13), ['clock'], False),
        ('a clock line the height stops, centred', card(900, 160, 12, 12, 200, 201, align='center'), ['clock'], False),
        ('a clock line the height stops, aligned left but centred', card(900, 160, 12, 12, 200, 201, align='left'), ['clock'], True),
        ('a short date line in a wide card', card(600, 200, 50, 50, 150, 150), ['date'], True),
    ]
    failed = 0
    for name, (measured, layout), kinds, finds in cases:
        kind = kinds[0] if kinds else 'cpuHistory' if layout['plots'] else 'notes' if layout.get('fields') else 'recycleBin'
        found = judge(kind, measured, layout)
        if bool(found) != finds:
            failed += 1
            print(f'FAIL {name}: {found}')
    print(f'gap-check self-test: {len(cases) - failed} of {len(cases)} passed')
    sys.exit(1 if failed else 0)


def main():
    if '--self-test' in sys.argv[1:]:
        self_test()
    root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    ap = argparse.ArgumentParser()
    renders.add_arguments(ap)
    ap.add_argument('--out', default=os.path.join(root, 'TestResults', 'gap-check'))
    a = ap.parse_args()
    shared = renders.open_renders(a)
    kinds, _ = renders.pick(a, shared)
    os.makedirs(os.path.join(a.out, 'png'), exist_ok=True)
    states = shared.states(kinds)
    drawn = shared.render([renders.Job(k, s, sizes) for k in kinds for s in states[k] for _, sizes in sets()])
    rows = []
    for k in kinds:
        rows.extend(check(k, states[k], drawn, a.out))
    findings = [r for r in rows if r[6]]
    with open(os.path.join(a.out, 'gaps.md'), 'w', encoding='utf-8') as f:
        f.write('| kind | state | set | card (DIP) | top | foot | findings |\n|---|---|---|---|---|---|---|\n')
        for k, s, label, box, top, foot, fs in rows:
            f.write(f'| {k} | {s or "default"} | {label} | {box} | {top} | {foot} | {"; ".join(fs)} |\n')
    with open(os.path.join(a.out, 'gaps.json'), 'w', encoding='utf-8') as f:
        json.dump([{'kind': k, 'state': s or 'default', 'set': label, 'card': box, 'top': top, 'foot': foot, 'findings': fs}
                   for k, s, label, box, top, foot, fs in rows], f, indent=1)
    with open(os.path.join(a.out, 'findings.txt'), 'w', encoding='utf-8') as f:
        for k, s, label, box, top, foot, fs in findings:
            f.write(f'{k}\t{s or "default"}\t{label}\t{box}\t{"; ".join(fs)}\n')
    print(f'gap-check: {len(findings)} findings in {len({r[0] for r in findings})} of {len(kinds)} kinds, {len(rows)} cards measured'
          f' ({shared.drawn} renders drawn in {shared.seconds:.0f} s, the rest from the cache)')
    for k, s, label, box, top, foot, fs in findings[:200]:
        print(f'  {k}\t{s or "default"}\t{label}\t{box}\t{"; ".join(fs)}')
    renders.swept(a)
    sys.exit(1 if findings else 0)


if __name__ == '__main__':
    main()
