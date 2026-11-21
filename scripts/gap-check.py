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
- WR-4 / WR-7 foot: the band under the last ink is at most the inset and the 4 DIP, but in a card with items: the room
  under the last item is theirs (the last cut by the foot, or empty under a short list: WR-4, WR-5, [S-14810]), judged by
  the item check.
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
- [S-14319] a held reading (`holds`: a running time held to its format's widest form, owner 2026-11-20: "the clock
  right now changes its layout depending on the time that it shows because not all of the chars have the same width")
  is judged across by its template's ink, the box every value it shows stands in: a "1:11" padded where "88:88" stands
  reaches the sides by that box, never by the narrower ink it shows now.
- [S-14304] a text cut with its ellipsis at the width it was given (`cuts`) ends where its last whole letter and the
  ellipsis fit: where it ends the card's ink, the room after it is the cut's (less than the next letter), not a band, and
  the end side is read as on the inset.
- [S-14312] a lower bound: no text's ink stands inside the inset. Each text the layout names (`ink` and `texts`: every
  TextBlock, outside items and in them) is measured on the PNG within its own box (its ink against the box's own ground),
  and ink closer than the inset less the tolerance to a card edge is a finding ("text in the inset"); a text in a plot or
  a free field, and a picture's or a desktop icon's card, are not judged (the plot, the field, the picture own the edge),
  nor a text in an item against a card edge the item's own box is (its padding stands there in the inset's place, WR-3:
  the item check judges the box), nor [S-14314] a text in a column's head (`heads`: the month's weekday letters, whose
  box is their column's across) against a card edge the head's box is. A text whose box reaches the edge but whose ink
  does not is fine.

The exemptions the owner granted (docs/WIDGET-RULES.md "Exemptions"): a kind whose face is a picture or a video
filling the card edge to edge (gallery, video stream, cover art) meets WR-1..WR-4 by that picture; a desktop icon keeps
Windows' icon cell (not judged); a tile (quick toggles, power buttons) and a one-line face (clock, date, header) may
stand centred: the bands over and under the face alike (within the tolerance), and [S-14305] its sides judged as any
card's (WR-3: each band beside it within the inset; tiles: "nothing else of WR-1..WR-4 is relaxed"); a one-line face or
[S-14306] a tile's face that fills the height (it grows as one until the height stops it) stands across where its
card's alignment puts it (a tile: centred), as a reading does ([S-14301]); a reading (a card
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
        left, right = (cols[0] + edge) / scale, ((r - l) - (cols[-1] + 1 + edge)) / scale
        # [S-14319] A held reading's sides are its template's ink: the box every value it shows stands in.
        for k in card.get('holds', []) or []:
            left, right = min(left, max(0.0, k['x'])), min(right, max(0.0, w - k['x'] - k['w']))
        out.append((w, h, (rows[0] + edge) / scale, ((b - t) - (rows[-1] + 1 + edge)) / scale, left, right))
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
        # Read as no more than the start's band, or the inset: a centred cut line leaves its room alike either side.
        right = min(right, max(INSET, left))
    if kind_class in ('tile', 'one-line'):
        # Centred, as the owner granted: the bands over and under the face alike. [S-14305] Its sides as any card's (tiles:
        # "nothing else of WR-1..WR-4 is relaxed"): it reaches them, or [S-14306] it fills the height (a one-line face, a
        # tile's face scaled up as one, WR-7) and stands across where its alignment puts it, as a reading ([S-14301]).
        if abs(top - foot) > TOLERANCE:
            found.append(f'centred face off centre {top:.1f}/{foot:.1f}')
        if left <= limit and right <= limit:
            return found
        if top <= limit and foot <= limit:
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
    # Under a card's items the room left is theirs (WR-4, WR-5: the last cut, or empty under a short list): the item check's.
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


def texts_in_inset(path, layouts, kind):
    """[S-14312] Per card, the texts whose ink stands inside the inset: each text box measured on the PNG within its box."""
    if classify(kind) in ('picture', 'windows'):
        return [[] for _ in layouts]
    im = np.asarray(Image.open(path).convert('RGBA')).astype(int)
    out = []
    for card in layouts:
        scale = card.get('scale', SCALE)
        owners = (card.get('plots', []) or []) + (card.get('fields', []) or [])
        inks = []
        # A glyph's own text (the FontIcon's template draws it in a TextBlock of its box) is no text.
        glyphs = [g for g in (card.get('ink', []) or []) if g.get('t') == 'FontIcon']
        # The card's own rim (its outline, its antialiased corners) is no ink, as in the measure.
        rim = max(1, min(int(round(4 * scale)), int(card['w'] * scale) // 3, int(card['h'] * scale) // 3))
        cl, ct = int(round(card['x'] * scale)) + rim, int(round(card['y'] * scale)) + rim
        cr, cb = int(round((card['x'] + card['w']) * scale)) - rim, int(round((card['y'] + card['h']) * scale)) - rim
        for t in (card.get('ink', []) or []) + (card.get('texts', []) or []):
            if t.get('t') != 'TextBlock' or t['w'] <= 0 or t['h'] <= 0:
                continue
            if any(inside(t, o) for o in owners):
                continue
            if any(all(abs(t[k] - g[k]) <= EDGE for k in ('x', 'y', 'w', 'h')) for g in glyphs):
                continue
            # An item's own box edge on a card edge is the item's: its padding stands there in the inset's place (a row centres
            # its text in its band, a month's cell holds its number in its corner). [S-14314] So is a column's head's (the
            # month's weekday letters, on their column's padding in line with its numbers).
            held = [i for i in (card.get('items', []) or []) + (card.get('heads', []) or []) + bands(card.get('items', []) or [])
                    if inside(t, i)]
            # [S-14810] A text in an item whose list runs on past it to the card's foot (the last item cut there, WR-4), or whose
            # item ends less than the inset over the foot (the room under a short list's last item, WR-5), has the item's own
            # padding or the next item under it, not the inset: the foot is the items' (the item check's).
            items = card.get('items', []) or []
            runs_on = held and (any(i.get('cut') and i['y'] + i['h'] >= card['h'] - EDGE for i in items)
                                or any(i['y'] + i['h'] >= card['h'] - INSET for i in held))
            # A list scrolled to bring a later item into sight has its first item cut at the card's top: the top is the items'.
            scrolled = held and any(i.get('cut') and i['y'] <= EDGE for i in items)
            free = {'left': not any(i['x'] <= EDGE for i in held), 'top': not scrolled and not any(i['y'] <= EDGE for i in held),
                    'right': not any(i['x'] + i['w'] >= card['w'] - EDGE for i in held),
                    'foot': not runs_on and not any(i['y'] + i['h'] >= card['h'] - EDGE for i in held)}
            x0, y0 = int(round((card['x'] + t['x']) * scale)), int(round((card['y'] + t['y']) * scale))
            x1, y1 = int(round((card['x'] + t['x'] + t['w']) * scale)), int(round((card['y'] + t['y'] + t['h']) * scale))
            x0, y0, x1, y1 = max(x0, cl), max(y0, ct), min(x1, cr), min(y1, cb)
            sub = im[max(0, y0):max(0, y1), max(0, x0):max(0, x1), :3]
            if sub.size == 0 or x1 <= x0 or y1 <= y0:
                continue
            vals, counts = np.unique(sub.reshape(-1, 3), axis=0, return_counts=True)
            ground = vals[counts.argmax()]
            mask = np.abs(sub - ground).sum(axis=2) > 30
            rows = np.where(mask.any(axis=1))[0]
            cols = np.where(mask.any(axis=0))[0]
            if len(rows) == 0:
                continue
            inks.append((max(0, x0) / scale - card['x'] + cols[0] / scale, max(0, y0) / scale - card['y'] + rows[0] / scale,
                         max(0, x0) / scale - card['x'] + (cols[-1] + 1) / scale, max(0, y0) / scale - card['y'] + (rows[-1] + 1) / scale,
                         free))
        out.append(inset_findings(card['w'], card['h'], inks))
    return out


def bands(items):
    """[S-14314] The rows of items that tile a band together (a month's week of day cells), each as one box: a text laid
    across several of them (a span's chip over the days it covers) stands in their padding as one in a single cell does."""
    rows = {}
    for i in items:
        rows.setdefault((round(i['y'], 1), round(i['h'], 1)), []).append(i)
    out = []
    for row in rows.values():
        row = sorted(row, key=lambda i: i['x'])
        if len(row) > 1 and all(abs(a['x'] + a['w'] - b['x']) <= EDGE for a, b in zip(row, row[1:])):
            out.append({'x': row[0]['x'], 'y': row[0]['y'], 'w': row[-1]['x'] + row[-1]['w'] - row[0]['x'], 'h': row[0]['h']})
    return out


def inside(box, owner):
    """Whether a text's box stands within an owner's (a plot's, a field's), its edges included."""
    return (box['x'] >= owner['x'] - EDGE and box['y'] >= owner['y'] - EDGE
            and box['x'] + box['w'] <= owner['x'] + owner['w'] + EDGE and box['y'] + box['h'] <= owner['y'] + owner['h'] + EDGE)


def inset_findings(w, h, inks):
    """[S-14312] The texts' ink boxes (left, top, right, bottom in the card's DIPs) that stand inside the inset."""
    floor = INSET - TOLERANCE
    found = []
    for l, t, r, b, *rest in inks:
        free = rest[0] if rest else {}
        bands = {'left': l, 'top': t, 'right': w - r, 'foot': h - b}
        near = [f'{edge} {band:.1f}' for edge, band in bands.items() if band < floor and free.get(edge, True)]
        if near:
            found.append('text in the inset ' + ', '.join(near))
    return sorted(set(found))


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
            for card, layout, inset in zip(measure(png, layouts), layouts, texts_in_inset(png, layouts, kind)):
                w, h, top, foot = card[:4]
                box = f'{w:.0f}x{h:.0f}'
                fmt = lambda v: '-' if v is None else f'{v:.1f}'
                lines.append((kind, state, label, box, fmt(top), fmt(foot), judge(kind, card, layout) + inset))
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
        # [S-6754] A plot edge to edge is the card's sides too: stockHistory's "No data yet" over a flat zero line.
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
        ('a centred name cut on its second line', card(200, 200, 12, 12, 36.7, 36.0, cuts=[{'x': 30, 'y': 120, 'w': 140, 'h': 60}]), ['hibernate'], False),
        ('an editor short of the foot', card(520, 600, 12, 416.7, 12, 14, fields=[{'x': 0, 'y': 34, 'w': 520, 'h': 400}]), [], True),
        # [S-14305] A tile and a one-line face: centred down, their sides judged as any card's.
        ('a tile centred both ways', card(100, 100, 30, 30, 30, 30), ['wifiToggle'], True),
        ('a tile whose face fills its width', card(160, 100, 30, 31, 12, 12), ['wifiToggle'], False),
        ('a tile whose face fills its height, centred', card(116, 116, 12, 12, 34.7, 36.7), ['wifiToggle'], False),
        ('a tile whose face fills its height, off centre', card(232, 232, 12, 12.7, 60.7, 50), ['sleep'], True),
        ('a clock line the width stops', card(600, 200, 50, 51, 12, 13), ['clock'], False),
        ('a clock line the height stops, centred', card(900, 160, 12, 12, 200, 201, align='center'), ['clock'], False),
        ('a clock line the height stops, aligned left but centred', card(900, 160, 12, 12, 200, 201, align='left'), ['clock'], True),
        ('a short date line in a wide card', card(600, 200, 50, 50, 150, 150), ['date'], True),
    ]
    # [S-14312] The lower bound: a text's ink inside the inset; a box reaching the edge with its ink inset is fine.
    for name, inks, finds in [
        ('the quote cut at 4 from the edge', [(12, 60, 326, 76)], True),
        ('a title on its inset', [(12, 12, 200, 30)], False),
        ('a label 6 under the top', [(40, 6, 120, 20)], True),
        ('a text whose ink stands 9 from the foot', [(12, 100, 80, 121)], False),
        ('a row of 30 whose text stands 7 from the top, the row on the edge', [(12, 7, 120, 23, {'top': False, 'foot': False})], False),
        ('a row text 4 from the start, the row short of the start', [(4, 7, 120, 23, {'top': False, 'foot': False})], True),
        ('a month cell number 4 from the start, the cell on the edge', [(4, 7, 20, 23, {'left': False, 'top': False})], False),
        # [S-14314] A weekday letter in its column's head, the head on the start edge; the same letter in no head.
        ('a weekday letter 4 from the start in its column head', [(4, 30, 20, 42, {'left': False})], False),
        ('a weekday letter 4 from the start in no head', [(4, 30, 20, 42)], True),
    ]:
        cases.append((name, None, inks, finds))
    # [S-14314] A chip spanning three day cells of the last week, 4 from the foot: in the week's band.
    week = [{'x': 86.0 * n, 'y': 410, 'w': 86.0, 'h': 70} for n in range(7)]
    chip = {'x': 180, 'y': 460, 'w': 110, 'h': 16}
    if not any(inside(chip, b) for b in bands(week)) or any(inside(chip, i) for i in week):
        failed_bands = 1
        print('FAIL a chip spanning cells stands in their band')
    else:
        failed_bands = 0
    failed = failed_bands
    for name, card_layout, kinds, finds in cases:
        if card_layout is None:
            found = inset_findings(330, 130, kinds)
            if bool(found) != finds:
                failed += 1
                print(f'FAIL {name}: {found}')
            continue
        measured, layout = card_layout
        kind = kinds[0] if kinds else 'stockHistory' if layout['plots'] else 'notes' if layout.get('fields') else 'recycleBin'
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
