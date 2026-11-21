"""Flag the icons a render drew cut by their box: a box smaller than its glyph, with the ink running to its edge.

usage: python scripts/icon-clip-check.py <png or folder>... [--font <ttf>] [--all]

[S-2377] (owner, 2026-11-19: "in the settings pages the icons show clipped"). Render with `--icon-census`
(`Themia.exe --render editbar <dir> --icon-census [--raster 100]`, `--render iconpicker <png> --icon-census`, ...): beside
each PNG the render writes `<png>.icons.json`, every icon it drew with its size as laid out, its own box, the box it is
laid out in cut to every slot round it (`clip`), the window it is seen through (`view`: a scroller's viewport) and, for
a glyph, its code point and size. For each icon wholly in view this script

  * works out the room its glyph takes in the PNG's pixels (its advance by its em in Segoe Fluent Icons, at its size,
    the PNG's scale and any transform round it: a preview's card drawn smaller) or, for a picture, its own box;
  * where the box is smaller than that on an axis, measures the ink inside the box on the PNG (pixels away from the
    ground round the box) and flags the icon `CUT` when the ink runs to the box's edge on that axis, naming the edges.

A glyph may fill its em to the edge (most of Segoe Fluent Icons' do), so ink at the edge of a box as large as the
glyph is no cut. Exit status 1 when anything is cut. --all lists every icon checked, not only those cut.
"""
import argparse
import json
import os
import sys

import numpy as np
from PIL import Image

FONT = r"C:\Windows\Fonts\SegoeIcons.ttf"

# Pixels further from the ground than this (summed over R, G, B) are ink.
INK = 60

# Half a pixel of give: layout rounds to whole pixels.
GIVE = 0.5


def glyph_bounds(font_path):
    """Each code point's outline bounds and advance in font units, and the units per em."""
    from fontTools.pens.boundsPen import BoundsPen
    from fontTools.ttLib import TTFont

    font = TTFont(font_path)
    glyphs = font.getGlyphSet()
    em = font['head'].unitsPerEm
    cmap = font.getBestCmap()
    bounds = {}

    def of(code):
        if code in bounds:
            return bounds[code]
        name = cmap.get(code)
        if name is None:
            bounds[code] = None
            return None
        pen = BoundsPen(glyphs)
        glyphs[name].draw(pen)
        bounds[code] = (pen.bounds, font['hmtx'][name][0])
        return bounds[code]

    return of, em


def needs(icon, scale, of, em):
    """The room the icon's glyph takes in the PNG's pixels (its advance by its em), or a picture's own box; None if unknown."""
    own, actual = icon['own'], icon.get('actual')
    # Pixels per DIP where it stands: the PNG's scale times any transform round it (a card's preview drawn smaller).
    per = own[2] / actual[0] if actual and actual[0] > 0 else scale
    if 'glyph' not in icon:
        return own[2], own[3]
    found = of(icon['glyph'])
    if found is None or found[0] is None:
        return None
    _, advance = found
    size = icon['size'] * per
    return advance / em * size, size


def measured(pixels, rect):
    """The ink's bounds inside rect (whole pixels), against the ground read round it; None for no ink."""
    h, w, _ = pixels.shape
    x0, y0, x1, y1 = (int(np.floor(rect[0])), int(np.floor(rect[1])), int(np.ceil(rect[2])), int(np.ceil(rect[3])))
    x0, y0, x1, y1 = max(0, x0), max(0, y0), min(w, x1), min(h, y1)
    if x1 <= x0 or y1 <= y0:
        return None

    # The ground: the commonest colour of a ring two pixels out of the box.
    gx0, gy0, gx1, gy1 = x0 - 2, y0 - 2, x1 + 2, y1 + 2
    gx0, gy0, gx1, gy1 = max(0, gx0), max(0, gy0), min(w, gx1), min(h, gy1)
    ring = np.concatenate([pixels[gy0, gx0:gx1], pixels[gy1 - 1, gx0:gx1], pixels[gy0:gy1, gx0], pixels[gy0:gy1, gx1 - 1]])
    colours, counts = np.unique(ring, axis=0, return_counts=True)
    ground = colours[np.argmax(counts)]

    patch = pixels[y0:y1, x0:x1]
    ink = np.abs(patch - ground).sum(axis=2) > INK
    if not ink.any():
        return None
    ys, xs = np.nonzero(ink)
    return x0 + xs.min(), y0 + ys.min(), x0 + xs.max() + 1, y0 + ys.max() + 1


def inside(a, b):
    """Whether rect a (x, y, w, h) stands wholly inside rect b."""
    return a[0] >= b[0] - GIVE and a[1] >= b[1] - GIVE and a[0] + a[2] <= b[0] + b[2] + GIVE and a[1] + a[3] <= b[1] + b[3] + GIVE


def check(png, of, em, show_all):
    with open(png + '.icons.json', encoding='utf-8-sig') as f:
        census = json.load(f)
    with Image.open(png) as im:
        pixels = np.asarray(im.convert('RGB'), dtype=np.int16)

    lines = []
    checked = 0
    for icon in census['icons']:
        own, clip, view = icon['own'], icon['clip'], icon['view']
        if own[2] <= 0 or own[3] <= 0 or not inside(own, view):
            continue  # scrolled away or under the page's edge: not this check's
        need = needs(icon, census['scale'], of, em)
        if need is None:
            continue
        checked += 1
        name = f"{icon.get('row') or '?'} {icon['type']}" + (f" U+{icon['glyph']:04X} at {icon['size']:g}" if 'glyph' in icon else '')

        # A box narrower or lower than the glyph (or a picture's own box) cuts it on that axis.
        short_w = need[0] - clip[2] > GIVE or own[0] < clip[0] - GIVE or own[0] + own[2] > clip[0] + clip[2] + GIVE
        short_h = need[1] - clip[3] > GIVE or own[1] < clip[1] - GIVE or own[1] + own[3] > clip[1] + clip[3] + GIVE
        sides = []
        if short_w or short_h:
            # Measured: the ink that shows inside the box, and whether it runs to the box's edge there.
            cx0, cy0, cx1, cy1 = clip[0], clip[1], clip[0] + clip[2], clip[1] + clip[3]
            got = measured(pixels, (cx0, cy0, cx1, cy1))
            if got is not None:
                touches = {
                    'left': short_w and got[0] <= cx0 + 1,
                    'right': short_w and got[2] >= cx1 - 1,
                    'top': short_h and got[1] <= cy0 + 1,
                    'bottom': short_h and got[3] >= cy1 - 1,
                }
                sides = [side for side in ('left', 'top', 'right', 'bottom') if touches[side]]
        if sides:
            lines.append(f"  CUT {name}: box {clip[2]:g}x{clip[3]:g}px < glyph {need[0]:.0f}x{need[1]:.0f}px, ink at its {', '.join(sides)} edge ({fmt(clip)})")
        elif show_all:
            lines.append(f"  ok  {name}: box {clip[2]:g}x{clip[3]:g}px, glyph {need[0]:.0f}x{need[1]:.0f}px")
    return checked, lines


def fmt(rect):
    return f"{rect[2]:g}x{rect[3]:g}@{rect[0]:g},{rect[1]:g}"


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('paths', nargs='+')
    parser.add_argument('--font', default=FONT)
    parser.add_argument('--all', action='store_true')
    args = parser.parse_args()

    of, em = glyph_bounds(args.font)
    pngs = []
    for path in args.paths:
        if os.path.isdir(path):
            pngs += sorted(os.path.join(path, n) for n in os.listdir(path) if n.endswith('.png') and os.path.exists(os.path.join(path, n + '.icons.json')))
        else:
            pngs.append(path)

    total, flagged = 0, 0
    for png in pngs:
        checked, lines = check(png, of, em, args.all)
        total += checked
        bad = sum(1 for line in lines if line.startswith('  CUT'))
        flagged += bad
        if lines:
            print(f"{os.path.basename(png)}: {checked} icons, {bad} cut")
            for line in lines:
                print(line)
    print(f"{len(pngs)} pictures, {total} icons, {flagged} cut")
    return 1 if flagged else 0


if __name__ == '__main__':
    sys.exit(main())
