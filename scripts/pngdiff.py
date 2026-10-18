"""Compare two render folders PNG by PNG: colour (RGB) and alpha, never alpha alone.

usage: python scripts/pngdiff.py <before> <after> [--noise <dir>]... [--slack px] [--quiet]

Walks <before>, pairs each PNG with the same relative path under <after> and prints one line per
PNG that differs: `size` (dimensions changed), `pixels` (with the bounding box of the change),
`missing` (absent in <after>) and, at the end, `extra` files only <after> has.

--noise <dir> is a second render of <before> made by the same build (render the set twice). The
pixels that differ between <before> and a noise run are the noise mask of that PNG (live data, the
clock, async loads). A change that falls wholly inside the mask, grown by --slack pixels (default 2),
is reported as `noise` and not counted as a difference; any changed pixel outside it is `pixels`.
Several --noise runs widen the mask. A PNG whose size differs between runs is noise as a whole.

(ImageChops.difference(...).getbbox() on an RGBA image looks at alpha only, so an opaque render
always compared equal: this script compares the colour and the alpha apart.)
"""
import argparse
import os
import sys

import numpy as np
from PIL import Image


def load(path):
    with Image.open(path) as im:
        return np.asarray(im.convert('RGBA'), dtype=np.int16)


def changed(a, b):
    """Mask of pixels whose colour or alpha differs."""
    return np.any(a != b, axis=2)


def grow(mask, px):
    if px <= 0 or not mask.any():
        return mask
    # a square dilation, separable: along x, then along y
    h, w = mask.shape
    row = mask.copy()
    for d in range(1, px + 1):
        row[:, d:] |= mask[:, :w - d]
        row[:, :w - d] |= mask[:, d:]
    out = row.copy()
    for d in range(1, px + 1):
        out[d:, :] |= row[:h - d, :]
        out[:h - d, :] |= row[d:, :]
    return out


def bbox(mask):
    ys, xs = np.nonzero(mask)
    return (int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1)


def main():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument('before')
    p.add_argument('after')
    p.add_argument('--noise', action='append', default=[], help='a second render of <before> by the same build')
    p.add_argument('--slack', type=int, default=2, help='pixels to grow the noise mask by')
    p.add_argument('--quiet', action='store_true', help='print only the summary')
    args = p.parse_args()

    same = differ = missing = noise = 0
    seen = set()
    for dp, _, fs in os.walk(args.before):
        for f in sorted(fs):
            if not f.lower().endswith('.png'):
                continue
            pa = os.path.join(dp, f)
            rel = os.path.relpath(pa, args.before)
            seen.add(rel)
            pb = os.path.join(args.after, rel)
            say = (lambda *m: None) if args.quiet else print
            if not os.path.exists(pb):
                say('missing', rel); missing += 1; continue
            a, b = load(pa), load(pb)
            # the noise mask: everything that moved between two runs of the before build
            mask = np.zeros(a.shape[:2], dtype=bool)
            whole = False
            for nd in args.noise:
                pn = os.path.join(nd, rel)
                if not os.path.exists(pn):
                    continue
                n = load(pn)
                if n.shape != a.shape:
                    whole = True
                    continue
                mask |= changed(a, n)
            if a.shape != b.shape:
                if whole:
                    say('noise', rel, 'size'); noise += 1
                else:
                    say('size', rel, a.shape[1::-1], b.shape[1::-1]); differ += 1
                continue
            d = changed(a, b)
            if not d.any():
                same += 1
                continue
            if whole:
                say('noise', rel, bbox(d)); noise += 1
                continue
            beyond = d & ~grow(mask, args.slack)
            if beyond.any():
                say('pixels', rel, bbox(beyond), int(beyond.sum())); differ += 1
            else:
                say('noise', rel, bbox(d)); noise += 1
    extra = 0
    for dp, _, fs in os.walk(args.after):
        for f in sorted(fs):
            if f.lower().endswith('.png'):
                rel = os.path.relpath(os.path.join(dp, f), args.after)
                if rel not in seen:
                    if not args.quiet:
                        print('extra', rel)
                    extra += 1
    print(f'same {same} differ {differ} noise {noise} missing {missing} extra {extra}')
    return 1 if differ or missing else 0


if __name__ == '__main__':
    sys.exit(main())
