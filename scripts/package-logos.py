"""
Draw every raster of Themia's mark from its one vector, scripts/themia-logo.svg:

  src/Themia.App/Assets/themia-logo.png   256 px, the mark the app shows (About, the editor, the Store pages)
  src/Themia.App/Assets/themia.ico        16..256 px, the exe's and the windows' icon ([S-1850])
  src/Themia.App/Assets/Package/*.png     the MSIX package logos ([S-1060])

The mark is one of Lometics' family: Lometics' square (its small corners, its folds, its white cards) in
Themia's amber, carrying Themia's five widgets. It fills its square to the edge; nothing is drawn around it.

Headless Chrome (or Edge) draws the vector once, large; every size is resampled from that. Up to 48 px the
widgets are drawn again on whole pixels over the resampled square, so a taskbar icon's edges are sharp.

Run after changing the mark:  python scripts/package-logos.py   (needs Pillow, and Chrome or Edge)
The rasters are checked in; building and packaging do not run this.
"""

import base64
import os
import subprocess
import tempfile
from pathlib import Path

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parent.parent
SVG = Path(__file__).resolve().parent / "themia-logo.svg"
ASSETS = ROOT / "src" / "Themia.App" / "Assets"
OUT = ASSETS / "Package"
MASTER = 2048  # px the vector is drawn at
ICO_SIZES = (16, 20, 24, 32, 40, 48, 64, 96, 128, 256)
SNAPPED = 48  # px up to which the widgets are drawn on whole pixels
# The mark's five widgets on its 64-unit grid, as the vector has them at 16 px a unit: x, y, w, h.
WIDGETS = [(11, 11, 18, 18), (33, 11, 20, 10), (33, 25, 20, 14), (11, 33, 14, 20), (29, 43, 24, 10)]


def browser():
    """Chrome, else Edge: whichever is installed."""
    candidates = [
        Path(os.environ.get("ProgramFiles", "")) / "Google" / "Chrome" / "Application" / "chrome.exe",
        Path(os.environ.get("ProgramFiles(x86)", "")) / "Microsoft" / "Edge" / "Application" / "msedge.exe",
        Path(os.environ.get("ProgramFiles", "")) / "Microsoft" / "Edge" / "Application" / "msedge.exe",
    ]
    for c in candidates:
        if c.is_file():
            return str(c)
    raise SystemExit("package-logos: neither Chrome nor Edge is installed")


def master(cards=True):
    """The vector drawn at MASTER px, transparent outside its corners. Without `cards`, its square and folds alone."""
    svg = SVG.read_text(encoding="utf-8")
    if not cards:
        # The vector is its square, one group of folds, then the cards: cut after the group.
        assert svg.count("</g>") == 1
        svg = svg.split("</g>")[0] + "</g></svg>"
    src = "data:image/svg+xml;base64," + base64.b64encode(svg.encode("utf-8")).decode("ascii")
    side = MASTER // 2
    html = (
        '<!doctype html><html><head><meta charset="utf-8"><style>html, body { margin: 0; background: transparent; } '
        f"img {{ display: block; width: {side}px; height: {side}px; }}</style></head>"
        f'<body><img src="{src}"></body></html>'
    )
    with tempfile.TemporaryDirectory() as tmp:
        page, png = Path(tmp) / "mark.html", Path(tmp) / "mark.png"
        page.write_text(html, encoding="utf-8")
        # A window taller than the picture: Chrome leaves a window's last rows undrawn. Cut back below.
        subprocess.run(
            [
                browser(), "--headless=new", "--disable-gpu", "--hide-scrollbars", "--default-background-color=00000000",
                "--force-device-scale-factor=2", f"--window-size={side},{side + 200}", f"--screenshot={png}", page.as_uri(),
            ],
            check=True, timeout=120, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
        )
        with Image.open(png) as shot:
            return shot.convert("RGBA").crop((0, 0, MASTER, MASTER))


class Art:
    """The mark drawn large, whole and without its widgets."""

    def __init__(self):
        self.whole, self.square = master(), master(cards=False)


def mark(art, size):
    """The mark, `size` px square: resampled, or up to SNAPPED px its widgets drawn on whole pixels."""
    if size > SNAPPED:
        return art.whole.resize((size, size), Image.LANCZOS)
    img = art.square.resize((size, size), Image.LANCZOS)
    draw, u = ImageDraw.Draw(img), size / 64
    for x, y, w, h in WIDGETS:
        draw.rectangle([round(x * u), round(y * u), round((x + w) * u) - 1, round((y + h) * u) - 1], fill=(255, 255, 255, 255))
    return img


def canvas(art, width, height, art_share):
    """A transparent canvas with the mark centred, `art_share` of the shorter side."""
    img = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    side = max(1, round(min(width, height) * art_share))
    img.alpha_composite(mark(art, side), ((width - side) // 2, (height - side) // 2))
    return img


def save(img, name):
    OUT.mkdir(parents=True, exist_ok=True)
    img.save(OUT / name, optimize=True)


def main():
    art = Art()

    # The app's own mark and icon.
    mark(art, 256).save(ASSETS / "themia-logo.png", optimize=True)
    mark(art, 256).save(ASSETS / "themia.ico", sizes=[(n, n) for n in ICO_SIZES], append_images=[mark(art, n) for n in ICO_SIZES[:-1]])

    scales = {100: 1.0, 125: 1.25, 150: 1.5, 200: 2.0, 400: 4.0}

    # App list / taskbar icon: plated (scale-*) and unplated (targetsize-*).
    for scale, f in scales.items():
        n = round(44 * f)
        save(canvas(art, n, n, 0.92), f"Square44x44Logo.scale-{scale}.png")
    for target in (16, 20, 24, 30, 32, 36, 40, 48, 60, 64, 72, 80, 96, 256):
        img = canvas(art, target, target, 1.0)
        save(img, f"Square44x44Logo.targetsize-{target}.png")
        save(img, f"Square44x44Logo.targetsize-{target}_altform-unplated.png")
        save(img, f"Square44x44Logo.targetsize-{target}_altform-lightunplated.png")

    for scale in (100, 125, 150, 200, 400):
        f = scales[scale]
        save(canvas(art, round(150 * f), round(150 * f), 0.6), f"Square150x150Logo.scale-{scale}.png")
        save(canvas(art, round(310 * f), round(150 * f), 0.6), f"Wide310x150Logo.scale-{scale}.png")
        save(canvas(art, round(71 * f), round(71 * f), 0.66), f"SmallTile.scale-{scale}.png")
        save(canvas(art, round(310 * f), round(310 * f), 0.5), f"LargeTile.scale-{scale}.png")
        save(canvas(art, round(50 * f), round(50 * f), 0.92), f"StoreLogo.scale-{scale}.png")
        save(canvas(art, round(620 * f), round(300 * f), 0.5), f"SplashScreen.scale-{scale}.png")


if __name__ == "__main__":
    main()
