"""[S-12815] The gap check: every widget kind, in every state its renders offer, at its range and at real desktop
sizes, measured for the empty band between its ink and its card's edges. Needs a Debug build of the app.

usage: python scripts/gap-check.py [--exe <Themia.exe>] [--out <dir>] [--kinds a,b] [--jobs 4] [--timeout 120]

For each kind it asks the render for the kind's states (`--state` with an unknown name lists them), draws each state
with `--widget-png <kind> <png> --state <s> --sizes range --transparent` and again at the desktop sizes below, finds
every card in the PNG by its alpha and its first and last row of ink (a pixel off the card's own colour), and judges:

- top: the band over the first ink is at most the inset + 4 DIP, for every kind and state (owner, 2026-11-19: "there
  should not be a space at the top");
- foot: the band under the last ink is at most the inset + 4 DIP, but for a list whose config's rows outnumber its
  items ([S-12800]: the rows it states keep their places, the rest stay empty at the foot), declared in LISTS;
- balance: top and foot differ by at most 4 DIP, with the same list exception;
- a card one line tall (two insets and a row) whose bands are alike is fine: its one line is centred;
- sides: for a kind whose content spans the width (STRETCH), in its sample, each side band is at most the inset + 4
  DIP and 4 more for a lead glyph drawn inside its box.

A kind whose frame is a picture, a video or a chart's own is declared in EXCEPT with its reason and judged on the top
alone. Every render runs under a hard timeout and is killed when it overruns. Writes <out>/gaps.md (the whole matrix)
and <out>/findings.txt; exits 1 on any finding.
"""
import argparse
import concurrent.futures as futures
import glob
import os
import re
import subprocess
import sys

import numpy as np
from PIL import Image

INSET = 12.0
SLACK = 4.0
SIDE = 4.0  # a row's lead glyph draws inside its 16 box: up to 4 more at the sides
SCALE = 1.5  # the renders' raster scale
# Desktop sizes, a few to a render so the page fits the screen and keeps the raster scale.
SIZES = ['330x130,350x70,350x80', '330x140,300x164,600x200', '520x600', '900x300']

# Lists whose config states its rows: fewer items than rows leave the rest empty at the foot ([S-12800]).
LISTS = {
    'bluetooth', 'clipboard', 'contacts', 'deviceBatteries', 'email', 'folder', 'habitTracker', 'mixer',
    'networkAdapters', 'notifications', 'recentFiles', 'removableDrives', 'rss', 'tasks', 'topProcesses', 'upNext',
    'weekPlanner', 'calendar', 'screenTimeApps', 'gpuEngines', 'cpuCores', 'fence', 'notes', 'scratchpad', 'stopwatch',
}

# Kinds whose own frame stands where it does: judged on the top alone.
EXCEPT = {
    'gallery': 'a picture fills the card edge to edge',
    'videoStream': 'a video fills the card edge to edge',
    'musicCover': 'a picture fills the card edge to edge',
    'desktopItem': 'an icon cell on the desktop grid, no card content',
    'audioVisualizer': 'a spectrum chart whose frame is the inset',
    'sun': 'a picture of the sky whose frame is the inset',
    'screenTimeChart': 'a chart whose frame is the inset',
    'screenTimeDonut': 'a chart whose frame is the inset',
    'stockHistory': 'a chart whose frame is the inset (the graph widgets\' own)',
    'exchangeHistory': 'a chart whose frame is the inset (the graph widgets\' own)',
    'header': 'one line, its card one line tall',
}
GRAPH = re.compile(r'(Graph|History)$')

# Kinds whose content spans the width: rows, facts, bars.
STRETCH = LISTS | {'airFacts', 'weatherFacts', 'cpuFacts', 'gpuFacts', 'memoryFacts', 'networkFacts', 'pingFacts',
                   'batteryFacts', 'driveFacts', 'sunTimes', 'moonTimes', 'weatherForecast'}


def kinds_in(root):
    names = []
    for d in sorted(glob.glob(os.path.join(root, 'src', 'Widgets', 'Themia.Widget.*'))):
        if os.path.isdir(d):
            n = os.path.basename(d)[len('Themia.Widget.'):]
            names.append(n[0].lower() + n[1:])
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


def judge(kind, state, card):
    w, h, top, foot, left, right = card
    found = []
    if top is None:
        return ['nothing drawn']
    limit = INSET + SLACK
    # A card one line tall centres its one line: its bands are the line's own, alike over and under it.
    if h <= 2 * INSET + 28 + SLACK and abs(top - foot) <= SLACK:
        return found
    if top > limit:
        found.append(f'top {top:.1f}')
    if kind in EXCEPT or GRAPH.search(kind):
        return found
    listed = kind in LISTS
    if foot > limit and not listed:
        found.append(f'foot {foot:.1f}')
    if abs(top - foot) > SLACK and not listed:
        found.append(f'balance {top:.1f}/{foot:.1f}')
    if kind in STRETCH and state in ('', 'sample') and (left > limit + SIDE or right > limit + SIDE):
        found.append(f'sides {left:.1f}/{right:.1f}')
    return found


def check(exe, kind, out, timeout):
    lines = []
    for state in states(exe, kind, out, timeout):
        for label, sizes in [('range', 'range')] + [(f'desk{i}', group) for i, group in enumerate(SIZES)]:
            png = os.path.join(out, 'png', f'{kind}.{state or "default"}.{label}.png')
            cmd = [exe, '--widget-png', kind, png, '--theme', 'dark', '--sizes', sizes, '--transparent']
            if state:
                cmd += ['--state', state]
            text = run(cmd, timeout)
            if text == 'TIMEOUT' or not os.path.exists(png):
                lines.append((kind, state, label, '-', '-', '-', ['render failed' if text != 'TIMEOUT' else 'render timed out']))
                continue
            for card in measure(png):
                w, h, top, foot = card[:4]
                box = f'{w:.0f}x{h:.0f}'
                fmt = lambda v: '-' if v is None else f'{v:.1f}'
                lines.append((kind, state, label, box, fmt(top), fmt(foot), judge(kind, state, card)))
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
    with open(os.path.join(a.out, 'findings.txt'), 'w', encoding='utf-8') as f:
        for k, s, label, box, top, foot, fs in findings:
            f.write(f'{k}\t{s or "default"}\t{label}\t{box}\t{"; ".join(fs)}\n')
    print(f'gap-check: {len(findings)} findings in {len({r[0] for r in findings})} of {len(kinds)} kinds, {len(rows)} cards measured')
    for k, s, label, box, top, foot, fs in findings[:200]:
        print(f'  {k}\t{s or "default"}\t{label}\t{box}\t{"; ".join(fs)}')
    sys.exit(1 if findings else 0)


if __name__ == '__main__':
    main()
