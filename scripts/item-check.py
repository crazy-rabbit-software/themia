"""[S-12831] The item check: the owner's rules on a widget's items (docs/WIDGET-RULES.md WR-1..WR-6), measured on the
item boxes the render lays out, every kind, every state its renders offer, at its range, cell by cell round its
natural (`--sizes steps`: one to five cells more and fewer on each axis and odd heights of 11, 13 and 17 cells, the sizes
whole-item steps once forbade, [S-14500]) and at desktop sizes. Needs a Debug build of the app.

usage: python scripts/item-check.py --exe <Themia.exe> [--changed [range] | --kinds a,b | --all] [--out <dir>]
                                    [--sizes s1;s2] [--jobs 2] [--timeout 120] [--cache <dir>]

The kinds: what a change touches (`--changed`, the default), `--kinds` or `--all`, their states from the build's
catalog; the renders come from the one cached pass every check shares (scripts/renders.py, [S-14341] [S-14342]).
Each render is drawn with `--widget-png <kind> <png> --sizes <s> --transparent [--state <s>] --layout-json <json>`: the
JSON holds every card's box, its grid pitch, its items (rows, cells, tiles: `ItemMark`) as they show in it, grouped by
the panel that lays them, and the ink outside every item. For every group of items it judges:

- WR-3 edge to edge: the items of a column (a list) run from the card's left edge to its right edge; the items of a
  row (a strip, a week's days) run from the left edge to the right edge together; a grid's rows do both.
- WR-2 touching: along the axis they repeat, each item starts where the one before it ends (no gap, no overlap).
- WR-1 / WR-5 nothing above the first item: the first item of a column stands on the card's top edge, or directly
  under what stands over it in the card (a tool line, a search box: the ink outside the items right over it).
- WR-5 nothing left over under the last item (owner, 2026-11-20: "now the weather forecast widget always has one block
  space at the bottom. this should obviously not be"): the band between the lowest item (a row, an add row) and the
  card's foot is none, or whole rows of the list's pitch (its empty rows: "this is fine if it happens"), never a part
  row; what stands under the last item outside every item (a foot line, a control) takes that room as its own.
- WR-6 optimal and stretched (owner, 2026-11-20: "the items should have an optimal size and then they may stretch a
  little to fill up the remaining space"): the items of a group are alike, to a device pixel (they share the room evenly,
  their edges on whole pixels); a family row (its optimal dumped as `o`: its natural in whole grid cells) is never
  shorter than its optimal. How far rows stretch is the views' (one row of a single metric fills its card; a list shows
  as many rows as its room holds at their optimal), held by the unit tests, not judged here. A fence's icon boxes are
  desktop icons ("Desktop icons keep Windows' look"): whole boxes from the body's top-left, the cells past its icons
  (the sliver a box does not fill, the boxes no icon stands in) the desktop's empty grid.
- no item is cut by its card or its list's viewport;
- no mark outside the items (a status glyph, a busy ring) stands over an item's text (a stale facts list's warning
  over its first value); [S-14317] the text is its letters' box where the dump writes it (`ink`), not the side bearings
  and leading round them.

Writes <out>/items.json (every kind x state x size x card x group with its findings), <out>/items.md (the findings),
and exits 1 on any finding. A kind whose face is a picture or a video filling the card (docs/WIDGET-RULES.md
"Exemptions") has no items to judge.
"""
import argparse
import json
import os
import sys

import renders

TOL = 0.5  # DIP: layout rounding
PX = 1.0 + TOL  # DIP: items sharing a room evenly on whole pixels differ by a pixel at most
SIZES = ['range', 'steps', '330x130,350x70,350x80', '330x140,300x164,600x200', '520x600', '900x300', '300x100,300x200,300x300']


def near(a, b):
    return abs(a - b) <= TOL


def axis_of(items):
    if len(items) == 1:
        return 'column'
    xs = {round(i['x']) for i in items}
    ys = {round(i['y']) for i in items}
    if len(xs) == 1 and all(near(i['w'], items[0]['w']) for i in items):
        return 'column'
    if len(ys) == 1 and all(near(i['h'], items[0]['h']) for i in items):
        return 'row'
    return 'grid'


def judge_column(items, card, ink, found, sliver=0.0):
    w, h = card['w'], card['h']
    items = sorted(items, key=lambda i: i['y'])
    for i in items:
        if i['x'] > TOL or i['x'] + i['w'] < w - TOL - sliver:
            found.append(f"WR-3 not edge to edge: {i['t']} spans {i['x']:.1f}..{i['x'] + i['w']:.1f} of {w:.1f}")
            break
    for a, b in zip(items, items[1:]):
        d = b['y'] - (a['y'] + a['h'])
        if abs(d) > TOL:
            found.append(f"WR-2 {'gap' if d > 0 else 'overlap'} of {abs(d):.1f} between items at {a['y']:.1f} and {b['y']:.1f}")
            break
    first = items[0]
    if first['y'] > TOL:
        over = [k for k in ink if k['y'] + k['h'] <= first['y'] + TOL and k['x'] < first['x'] + first['w'] and k['x'] + k['w'] > first['x']]
        under = max((k['y'] + k['h'] for k in over), default=0.0)
        if first['y'] - under > TOL:
            found.append(f"WR-1 space of {first['y'] - under:.1f} above the first item")
    sizes = [i['h'] for i in items]
    if max(sizes) - min(sizes) > PX:
        found.append(f"WR-6 items unlike: {min(sizes):.1f}..{max(sizes):.1f}")
    optimal(items, card, ink, found)
    last = items[-1]
    if last['y'] + last['h'] > h + TOL:
        found.append(f"item past the card's foot ({last['y'] + last['h']:.1f} of {h:.1f})")


def optimal(items, card, ink, found):
    """WR-6 (2026-11-20): a family row never under its optimal."""
    opts = [i['o'] for i in items if 'o' in i]
    if not opts:
        return
    o = max(opts)
    sizes = [i['h'] for i in items if 'o' in i]
    if min(sizes) < o - PX:
        found.append(f"WR-6 a row {min(sizes):.1f} tall is under its optimal {o:.1f}")


def judge_row(items, card, found, line=None, sliver=0.0):
    w = card['w']
    items = sorted(items, key=lambda i: i['x'])
    if items[0]['x'] > TOL or items[-1]['x'] + items[-1]['w'] < w - TOL - sliver:
        found.append(f"WR-3 not edge to edge{line or ''}: {items[0]['x']:.1f}..{items[-1]['x'] + items[-1]['w']:.1f} of {w:.1f}")
    for a, b in zip(items, items[1:]):
        d = b['x'] - (a['x'] + a['w'])
        if abs(d) > TOL:
            found.append(f"WR-2 {'gap' if d > 0 else 'overlap'} of {abs(d):.1f} between items{line or ''}")
            break
    # Across, the items share the card's width evenly: alike to a pixel.
    sizes = [i['w'] for i in items]
    if max(sizes) - min(sizes) > PX:
        found.append(f"WR-6 items unlike: {min(sizes):.1f}..{max(sizes):.1f} wide{line or ''}")


def judge_grid(items, card, ink, found, sliver=(0.0, 0.0)):
    rows = {}
    for i in items:
        rows.setdefault(round(i['y']), []).append(i)
    lines = [sorted(v, key=lambda i: i['x']) for _, v in sorted(rows.items())]
    for n, line in enumerate(lines):
        judge_row(line, card, found, line=f' (line {n + 1})', sliver=sliver[0])
    heads = [line[0] | {'w': card['w'], 'x': 0.0} for line in lines]
    judge_column(heads, card, ink, found)


def merge_rows(groups):
    """Rows of items laid by panels of their own (a month's weeks) whose items stand in the same columns are one grid."""
    merged, used = {}, set()
    keys = sorted(groups)
    for k in keys:
        if k in used:
            continue
        items = list(groups[k])
        if axis_of(items) == 'row' and len(items) > 1:
            cols = [round(i['x']) for i in sorted(items, key=lambda i: i['x'])]
            for other in keys:
                if other != k and other not in used and axis_of(groups[other]) == 'row'                         and [round(i['x']) for i in sorted(groups[other], key=lambda i: i['x'])] == cols:
                    items.extend(groups[other])
                    used.add(other)
        used.add(k)
        merged[k] = items
    return merged


def sliver_of(card, kind):
    """A fence's icon boxes stand on the desktop icon grid from the body's top-left ([S-14501], "Desktop icons keep
    Windows' look"): past its icons the body is the desktop's empty grid, the sliver a box does not fill at the far edge
    and the foot and the boxes no icon stands in alike. What may stand empty past the last box across and down: for a
    fence the card's whole width and height, any other kind none."""
    if kind != 'fence':
        return (0.0, 0.0)
    return (card['w'], card['h'])


def judge(card, kind=None):
    groups = {}
    for i in card.get('items', []):
        groups.setdefault(i['g'], []).append(i)
    groups = merge_rows(groups)
    out = []
    for g, items in sorted(groups.items()):
        found = []
        axis = axis_of(items)
        sliver = sliver_of(card, kind)
        # What stands over a group: the ink outside the items and the other groups' items (a list over its add row).
        over = card.get('ink', []) + [i for k, v in groups.items() if k != g for i in v]
        if axis == 'column':
            judge_column(items, card, over, found, sliver=sliver[0])
        elif axis == 'row':
            judge_row(items, card, found, sliver=sliver[0])
        else:
            judge_grid(items, card, over, found, sliver=sliver)
        if any(i.get('cut') for i in items):
            found.append('an item is cut by its viewport')
        out.append({'group': g, 'axis': axis, 'items': len(items), 'findings': found})
    hidden = covered(card)
    if hidden:
        out.append({'group': 'marks', 'axis': '-', 'items': len(marks), 'findings': hidden})
    left = leftover(card, groups, sliver_of(card, kind)[1])
    if left:
        out.append({'group': 'foot', 'axis': '-', 'items': len(card.get('items', [])), 'findings': [left]})
    return out


def covered(card):
    """A mark outside the items (a status glyph, a ring) standing over an item's text hides it. [S-14317] The text is its
    letters (`ink`: its box less its side bearings and its line's leading), where the dump has them: a reading placed by its
    ink at the room's end reaches into the status glyph's corner with its box alone (a stale UV index's "1", whose box ran
    10 past its letters under the glyph)."""
    marks = [k for k in card.get('ink', []) if k['t'] in ('FontIcon', 'ProgressRing', 'Image')]
    hidden = []
    for m in marks:
        for text in card.get('texts', []):
            t = text.get('ink', text)
            dx = min(m['x'] + m['w'], t['x'] + t['w']) - max(m['x'], t['x'])
            dy = min(m['y'] + m['h'], t['y'] + t['h']) - max(m['y'], t['y'])
            if dx > 1 and dy > 1:
                hidden.append(f"a {m['t']} at {m['x']:.0f},{m['y']:.0f} stands over an item's text")
                break
    return hidden


def self_test():
    """[S-14317] The marks rule on made-up cards."""
    glyph = {'t': 'FontIcon', 'x': 302, 'y': 16, 'w': 16, 'h': 16}
    box = {'x': 268, 'y': 11, 'w': 50, 'h': 110}
    cases = [
        ('a glyph over a text box with no letters known', {'ink': [glyph], 'texts': [dict(box)]}, True),
        ('a glyph over the box of a reading beside its letters',
         {'ink': [glyph], 'texts': [dict(box, ink={'x': 280, 'y': 36, 'w': 22, 'h': 70})]}, False),
        ('a glyph over the letters', {'ink': [glyph], 'texts': [dict(box, ink={'x': 280, 'y': 20, 'w': 30, 'h': 70})]}, True),
    ]
    failed = 0
    for name, card, finds in cases:
        if bool(covered(card)) != finds:
            failed += 1
            print(f'FAIL {name}')
    print(f'item-check self-test: {len(cases) - failed} of {len(cases)} passed')
    sys.exit(1 if failed else 0)


def leftover(card, groups, sliver=0.0):
    """WR-5 (2026-11-20): the band under the card's lowest item is none or its list's empty rows (whole rows of the
    pitch of a column of items, to a pixel a row), never a part row; ink standing under the last item takes that room."""
    items = card.get('items', [])
    if not items:
        return None
    h, cell = card['h'], card['cell']
    last = max(i['y'] + i['h'] for i in items)
    if any(k['y'] >= last - TOL and k['h'] > TOL for k in card.get('ink', [])):
        return None
    band = h - last
    # One device pixel is the layout's quantum: rows sharing a room on whole pixels at a fractional scale may end a pixel
    # short of a card whose own box rounds the other way.
    pixel = max(TOL, 1.0 / card.get('scale', 1) + 0.01)
    if band <= pixel or band < sliver - TOL:
        return None
    # A list's pitch: its items' height (a column's rows, a row or a grid's line of cells: the week planner's days).
    pitches = {round(i['h'], 1) for i in items if i['h'] > TOL}
    for p in pitches:
        rows = round(band / p)
        if rows >= 1 and abs(band - rows * p) <= TOL + rows:
            return None
    return f"WR-5 leftover of {band:.1f} under the last item ({band / cell:.1f} cells of {cell:.2f})"


def check(kind, states, drawn, out, sizes):
    """[S-14341] Judges a kind's renders, drawn once for every check (scripts/renders.py), linked into <out>/png."""
    results = []
    for state in states:
        for n, size in enumerate(sizes):
            label = size if size in ('range', 'steps') else f'set{n}'
            png = os.path.join(out, 'png', f'{kind}.{state or "default"}.{label}.png')
            layout = png + '.layout.json'
            result = drawn[renders.Job(kind, state, size)]
            renders.place(result, png)
            row = {'kind': kind, 'state': state or 'default', 'sizes': size}
            if result.timed_out or not os.path.exists(layout):
                row['error'] = 'render timed out' if result.timed_out else 'render failed'
                results.append(row)
                continue
            with open(layout, encoding='utf-8') as f:
                data = json.load(f)
            row['cards'] = [{'tier': c['tier'], 'w': c['w'], 'h': c['h'], 'cell': c['cell'], 'groups': judge(c, kind)} for c in data['cards']]
            results.append(row)
    return results


def main():
    if '--self-test' in sys.argv[1:]:
        self_test()
    root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    ap = argparse.ArgumentParser()
    renders.add_arguments(ap)
    ap.add_argument('--out', default=os.path.join(root, 'TestResults', 'item-check'))
    ap.add_argument('--sizes', help='size sets separated by ";" (default: range and desktop sizes)')
    a = ap.parse_args()
    shared = renders.open_renders(a)
    kinds, _ = renders.pick(a, shared)
    os.makedirs(os.path.join(a.out, 'png'), exist_ok=True)
    sizes = a.sizes.split(';') if a.sizes else SIZES
    states = shared.states(kinds)
    drawn = shared.render([renders.Job(k, s, size) for k in kinds for s in states[k] for size in sizes])
    results = []
    for k in kinds:
        results.extend(check(k, states[k], drawn, a.out, sizes))
    with open(os.path.join(a.out, 'items.json'), 'w', encoding='utf-8') as f:
        json.dump(results, f, indent=1)
    lines, total, kinds_hit = [], 0, set()
    for r in results:
        if 'error' in r:
            lines.append(f"| {r['kind']} | {r['state']} | {r['sizes']} | - | - | {r['error']} |")
            total += 1
            kinds_hit.add(r['kind'])
            continue
        for c in r['cards']:
            for g in c['groups']:
                for finding in g['findings']:
                    lines.append(f"| {r['kind']} | {r['state']} | {c['tier']} {c['w']:.0f}x{c['h']:.0f} | {g['group']} | {g['axis']} | {finding} |")
                    total += 1
                    kinds_hit.add(r['kind'])
    with open(os.path.join(a.out, 'items.md'), 'w', encoding='utf-8') as f:
        f.write('| kind | state | card | group | axis | finding |\n|---|---|---|---|---|---|\n')
        f.write('\n'.join(lines) + '\n')
    print(f'item-check: {total} findings in {len(kinds_hit)} of {len(kinds)} kinds'
          f' ({shared.drawn} renders drawn in {shared.seconds:.0f} s, the rest from the cache)')
    for line in lines[:200]:
        print(' ', line)
    renders.swept(a)
    sys.exit(1 if total else 0)


if __name__ == '__main__':
    main()
