# Scratchpad (PJ)

The module `scratchpad` ([S-6911]): a pad to draw on, with the original's inks and nibs, an eraser,
undo, clear and full screen; the drawing is the widget's config. Its tags are this file's alone; the
root `SPEC.md` holds the host's sections (the widgets' one confirmation [S-1500], the keyboard lease
[S-800], the colour picker [S-1549], the ladder rules [S-660]..[S-666]).

Owning files: everything in this folder: `ScratchpadModule.cs` (descriptor, view factory, menu
lines, JSON context, glyph), `ScratchpadConfig.cs`, `InkGeometry.cs` (the strokes as stored, thinning,
the eraser), `InkPalette.cs` (the inks and nibs), `ScratchpadLayout.cs` (the ink bar's ladder), `ScratchpadView*.cs`, `ScratchpadMenu.cs` (its
properties-menu lines), `Preview/` (Debug renders), `Localization/Locales/` (its own `ScratchpadStr`, 30 languages).
Tests: `Tests/` (run by `tests/Themia.Modules.Tests`).

- **[S-651]** The scratchpad's Undo (greyed with nothing to undo) and Full screen are the ink bar's last
  group, after the eraser ([S-12481]: no card header to stand in; they come and go with Show tools). Clear the drawing is the card menu's command
  while there is a drawing, behind the widgets' one confirmation asked beside the widget ([S-1500]);
  the ink bar's eraser is the one ([S-1498]). A hidden eraser is switched off.
- **[S-1497]** Scratchpad ink bar (original S-02 toolbar): six preset inks (`InkPalette`, shared
  with the pen colour's swatches on the properties menu, [S-1501]) and the original's three nibs
  2/4/8 px at the foot of the pad, whole, while Ink bar is on ([S-11205]); nothing more on a larger
  pad (the stroke count at W ≥ Expanded is retired, concise widgets). The bar is a
  row of 28 tools on the card's bottom inset ([S-6716]), each a subtle button (the subtle fills on
  hover and press, no border): an ink a 16 dot in its colour (a data colour, `Family.Data`,
  [S-6705]), a nib a dot of its width + 2 in the secondary role, the chosen ink and nib ringed by a 24
  ring of stroke 2 in the accent; inks, nibs and the eraser in groups 8 apart. The inks are six
  swatches of the host's one palette ([S-1549]), named by it: Default blue, Pale red, Gold, Mint
  light, Iris spring and Light gray (G6: they were the module's own Blue, Rose, Amber, Emerald,
  Violet and Chalk; a stroke keeps the colour it was drawn in). Picking an ink or a nib leaves the
  eraser. An empty pad says "Draw here" in the Status row at its top ([S-6714]), never taking the
  pointer. Not ported: export as PNG.
- **[S-1498]** The eraser (user requirement: an erasing stylus) is a tool of the ink bar, after the
  nibs, drawn with Windows' eraser glyph, on in the accent's fill with its glyph on it ([S-6706]);
  it is on screen while the bar shows (and on full screen's toolbar). A hidden eraser never leaves
  the pad erasing. It removes the whole strokes it touches, as before. A pen's eraser end erases whatever
  tool is chosen, as in Windows Ink.
- **[S-1499]** **Full screen** (user requirement; an ink bar tool): the same pad moves into a windowed
  popup over the whole display and back, so the drawing, the undo history and the gesture are the
  pad's own, nothing copied. On the solid role's fill, a toolbar on the flyouts' chrome at the top
  (the inks, the nibs and the eraser, then undo, clear and Exit full screen, always shown whatever
  Show tools says), and under it the pad at the tile's own shape inside a hairline in the track role
  at the family's radius (the strokes are in pad units; another shape would stretch them), as large
  as the display holds. The tile's button
  leaves it; so does the widget detaching (a view switch, a removal). Needs a live pass: pointer and
  pen input in the popup, a top or side taskbar, mixed DPI.

- **[S-1501]** Its options are all on the properties menu ([S-3719], user request 2026-10-19); it
  has no settings page. Appearance ([S-3723]), then **Pen color** (the one colour submenu, [S-3724]:
  the six inks first, no Default since a pen always has a colour, Custom colour and the eyedropper),
  **Pen size** (typed, 1–24 px, [S-3726]) and, under a divider (the pad's tools apart from the pen,
  user request 2026-10-19), **Show tools** (a switch: the ink bar with Undo and Full screen, [S-651],
  [S-1497]). Clearing the drawing stays on the pad (its card menu), behind its
  confirmation ([S-1500]). It paints no item fill, so it has no Item background or Item text
  ([S-6570], [S-6571]).
  (Tests `ScratchpadMenuTests`.)
- **[S-11205]** Converted to the sizing ([S-2438]): a free range, 200..800 wide and 160..600 high (a new
  one 384 × 260 with the ink bar, 320 × 260 without, `ScratchpadLayout.Sizing`); the pad takes what the card has. The ink bar is Show tools'
  (on by default): one row of 28 at every size, whole (inks, nibs, eraser, undo, full screen) or not there, so with it on the
  card is never narrower than the bar in its inset (384). No tier shows or sheds a part of it any more.

Needs a manual pass: pen, touch and mouse drawing on the live desktop at every tier, and full
screen on a top or side taskbar and mixed DPI ([S-1499]).
