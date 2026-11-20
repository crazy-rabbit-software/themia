"""[S-12815] [S-12831] The gap check: every widget kind, in every state its renders offer, at its range (its least size,
its natural and its large) and at real desktop sizes, measured for the empty band between its ink and its card's
edges, against the owner's rules as worded in docs/WIDGET-RULES.md. Needs a Debug build of the app.

usage: python scripts/gap-check.py [--exe <Themia.exe>] [--out <dir>] [--kinds a,b] [--jobs 4] [--timeout 120]

For each kind it asks the render for the kind's states (`--state` with an unknown name lists them), draws each state
with `--widget-png <kind> <png> --state <s> --sizes range --transparent --layout-json <json>` and again at the desktop
sizes below, finds every card in the PNG by its alpha, its first and last row of ink (a pixel off the card's own
colour), and judges:

- WR-1 top: the band over the first ink is at most the inset (12, the padding inside an item, WR-3) and the 4 DIP the
  measure may be off by (antialiasing, a glyph's own room in its box). Where the card's items stand on its top edge
  (the layout's item boxes, `ItemMark`), the band over the ink is the item's own padding and the item check
  (scripts/item-check.py) judges the boxes instead.
- WR-4 / WR-7 foot: the band under the last ink is at most the inset and the 4 DIP, but in a card with items: their
  leftover whole cells and empty stated rows may stand at the foot (WR-5), judged by the item check.
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
  where its value is, its plot still reaches the edges); a graph kind drawn with no plot is a finding (WR-10).

The exemptions the owner granted (docs/WIDGET-RULES.md "Exemptions"): a kind whose face is a picture or a video
filling the card edge to edge (gallery, video stream, cover art) meets WR-1..WR-4 by that picture; a desktop icon keeps
Windows' icon cell (not judged); a tile (quick toggles, power buttons) and a one-line face (clock, date, header) may
stand centred: the bands over and under the face alike (within the tolerance), its sides as any card's. Every render runs
under a hard timeout and is killed when it overruns. Writes <out>/gaps.md (the whole matrix), <out>/gaps.json and
<out>/findings.txt; exits 1 on any finding.
"""
import argparse
import concurrent.futures as futures
import glob
import json
import os
import re
import subprocess
import sys

import numpy as np
from PIL import Image

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


def graph(kind):
    return bool(GRAPH.search(kind)) or kind in GRAPHS


def plot_box(layout):
    """The plots' union in the card (DIPs), or None without a plot."""
    plots = (layout or {}).get('plots', [])
    if not plots:
        return None
    return (min(p['x'] for p in plots), min(p['y'] for p in plots),
            max(p['x'] + p['w'] for p in plots), max(p['y'] + p['h'] for p in plots))


def centred(kind):
    return bool(TILE.search(kind)) or kind in POWER or kind in ONE_LINE


def kinds_in(root):
    """Every kind the modules declare (`[assembly: ThemiaWidget("id", ...)]`), a family's each (the weather facts' eleven)."""
    names = []
    for d in sorted(glob.glob(os.path.join(root, 'src', 'Widgets', 'Themia.Widget.*'))):
        if not glob.glob(os.path.join(d, '*.csproj')):
            continue
        declared = []
        for f in sorted(glob.glob(os.path.join(d, '*.cs'))):
            with open(f, encoding='utf-8') as src:
                declared += re.findall(r'\[assembly: ThemiaWidget\("(\w+)"', src.read())
        if not declared:
            n = os.path.basename(d)[len('Themia.Widget.'):]
            declared = [n[0].lower() + n[1:]]
        names += [k for k in declared if k not in names]
    return names


def run(cmd, timeout):
    try:
        p = subprocess.run(cmd, capture_output=True, text=True, timeout=timeout)
        return p.stdout + p.stderr
    except subprocess.TimeoutExpired:
        return 'TIMEOUT'


def states(exe, kind, out, timeout):
    text = run([exe, '--widget-png', kind, os.path.join(out, '_probe.png'), '--state', '__none__'], timeout)
    m = re.search(r'\(has: ([^)]*)\)', text)
    return [s.strip() for s in m.group(1).split(',')] if m else ['']


def cards(alpha):
    cols = (alpha > 200).any(axis=0)
    spans, x, w = [], 0, len(cols)
    while x < w:
        if cols[x]:
            s = x
            while x < w and cols[x]:
                x += 1
            spans.append((s, x))
        else:
            x += 1
    found = []
    for s, e in spans:
        if e - s < 20:
            continue
        rows = np.where((alpha[:, s:e] > 200).any(axis=1))[0]
        found.append((s, e, rows[0], rows[-1] + 1))
    return found


def measure(path):
    im = np.asarray(Image.open(path).convert('RGBA')).astype(int)
    out = []
    for (l, r, t, b) in cards(im[:, :, 3]):
        edge = 6
        sub = im[t + edge:b - edge, l + edge:r - edge, :3]
        if sub.size == 0:
            continue
        vals, counts = np.unique(sub.reshape(-1, 3), axis=0, return_counts=True)
        bg = vals[counts.argmax()]
        mask = np.abs(sub - bg).sum(axis=2) > 30
        rows = np.where(mask.any(axis=1))[0]
        cols = np.where(mask.any(axis=0))[0]
        w, h = (r - l) / SCALE, (b - t) / SCALE
        if len(rows) == 0:
            out.append((w, h, None, None, None, None))
            continue
        out.append((w, h, (rows[0] + edge) / SCALE, ((b - t) - (rows[-1] + 1 + edge)) / SCALE,
                    (cols[0] + edge) / SCALE, ((r - l) - (cols[-1] + 1 + edge)) / SCALE))
    return out


def judge(kind, card, layout):
    w, h, top, foot, left, right = card
    if top is None:
        return ['WR-10 nothing drawn']
    if kind in PICTURES:
        return []
    if kind in WINDOWS_LOOK:
        return []
    if centred(kind):
        # Centred, as the owner granted: the bands over and under the face alike; the sides as any card's.
        found = []
        if abs(top - foot) > TOLERANCE:
            found.append(f'centred face off centre {top:.1f}/{foot:.1f}')
        return found
    found = []
    limit = INSET + TOLERANCE
    items = (layout or {}).get('items', [])
    on_top = any(i['y'] <= 0.5 for i in items)
    # [S-6754] A graph's plot reaches every edge of its card; where it does, it is the card's top and foot.
    plot = plot_box(layout)
    plot_top = plot_foot = False
    if layout is not None and graph(kind) and plot is None:
        found.append('WR-10 no plot')
    if plot is not None:
        l, t, r, b = plot
        cw, ch = layout['w'], layout['h']
        top_edge = layout.get('setup', 0) + EDGE
        if l > EDGE or r < cw - EDGE or t > top_edge or b < ch - EDGE:
            found.append(f'plot not edge to edge {l:.1f}..{r:.1f} x {t:.1f}..{b:.1f} of {cw:.0f}x{ch:.0f}')
        plot_top, plot_foot = t <= top_edge, b >= ch - EDGE
    if top > limit and not on_top and not plot_top:
        found.append(f'WR-1 top {top:.1f}')
    # Under a card's items the room left is theirs: whole cells and empty stated rows at the foot (WR-5), judged by
    # the item check.
    if foot > limit and not items and not plot_foot:
        found.append(f'WR-4 foot {foot:.1f}')
    # Item boxes from the card's left edge to its right: the item check's box rule judges them (WR-3), not their ink.
    width = (layout or {}).get('w', w)
    if not (items and spans(items, width)) and (left > limit or right > limit):
        found.append(f'WR-3 sides {left:.1f}/{right:.1f}')
    return found


def spans(items, width):
    """Whether the card's item boxes reach both its edges (a list's rows, a row of cells together)."""
    return any(i['x'] <= 0.5 for i in items) and any(i['x'] + i['w'] >= width - 0.5 for i in items)


def check(exe, kind, out, timeout):
    lines = []
    for state in states(exe, kind, out, timeout):
        for label, sizes in [('range', 'range')] + [(f'desk{i}', group) for i, group in enumerate(SIZES)]:
            png = os.path.join(out, 'png', f'{kind}.{state or "default"}.{label}.png')
            layout_path = png + '.layout.json'
            cmd = [exe, '--widget-png', kind, png, '--theme', 'dark', '--sizes', sizes, '--transparent', '--layout-json', layout_path]
            if state:
                cmd += ['--state', state]
            text = run(cmd, timeout)
            if text == 'TIMEOUT' or not os.path.exists(png):
                lines.append((kind, state, label, '-', '-', '-', ['render failed' if text != 'TIMEOUT' else 'render timed out']))
                continue
            layouts = []
            if os.path.exists(layout_path):
                with open(layout_path, encoding='utf-8') as f:
                    layouts = json.load(f).get('cards', [])
            for n, card in enumerate(measure(png)):
                w, h, top, foot = card[:4]
                box = f'{w:.0f}x{h:.0f}'
                fmt = lambda v: '-' if v is None else f'{v:.1f}'
                layout = layouts[n] if n < len(layouts) else None
                lines.append((kind, state, label, box, fmt(top), fmt(foot), judge(kind, card, layout)))
    return lines


def main():
    root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    ap = argparse.ArgumentParser()
    ap.add_argument('--exe')
    ap.add_argument('--out', default=os.path.join(root, 'TestResults', 'gap-check'))
    ap.add_argument('--kinds')
    ap.add_argument('--jobs', type=int, default=4)
    ap.add_argument('--timeout', type=int, default=120)
    a = ap.parse_args()
    exe = a.exe
    if not exe:
        found = sorted(glob.glob(os.path.join(root, 'artifacts', '**', 'debug_win-x64', 'Themia.exe'), recursive=True),
                       key=os.path.getmtime, reverse=True)
        if not found:
            sys.exit('No Debug Themia.exe found: build src/Themia.App/Themia.App.csproj -c Debug, or pass --exe.')
        exe = found[0]
    os.makedirs(os.path.join(a.out, 'png'), exist_ok=True)
    kinds = a.kinds.split(',') if a.kinds else kinds_in(root)
    rows = []
    with futures.ThreadPoolExecutor(max_workers=a.jobs) as pool:
        for lines in pool.map(lambda k: check(exe, k, a.out, a.timeout), kinds):
            rows.extend(lines)
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
    print(f'gap-check: {len(findings)} findings in {len({r[0] for r in findings})} of {len(kinds)} kinds, {len(rows)} cards measured')
    for k, s, label, box, top, foot, fs in findings[:200]:
        print(f'  {k}\t{s or "default"}\t{label}\t{box}\t{"; ".join(fs)}')
    sys.exit(1 if findings else 0)


if __name__ == '__main__':
    main()
