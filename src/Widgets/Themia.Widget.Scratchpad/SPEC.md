# Scratchpad (PJ)

The module `scratchpad` ([S-6911]): a pad to draw on, with the original's inks and nibs, an eraser,
undo, clear and full screen; the drawing is the widget's config. Its tags are this file's alone; the
root `SPEC.md` holds the host's sections (the widgets' one confirmation [S-1500], the keyboard lease
[S-800], the colour picker [S-1549], the ladder rules [S-660]..[S-666]).

Owning files: everything in this folder: `ScratchpadModule.cs` (descriptor, view factory, settings
rows, JSON context, glyph), `ScratchpadConfig.cs`, `InkGeometry.cs` (the strokes as stored, thinning,
the eraser), `InkPalette.cs` (the inks and nibs), `ScratchpadView*.cs`, `ScratchpadSettings.cs` (its
page), `Preview/` (Debug renders), `Localization/Locales/` (its own `ScratchpadStr`, 30 languages).
Tests: `Tests/` (run by `tests/Themia.Modules.Tests`).

- **[S-651]** The scratchpad's header tools shed eraser, then clear; undo stays. A hidden eraser is
  switched off.
- **[S-1497]** Scratchpad ink bar (original S-02 toolbar): six preset inks (`InkPalette`, shared
  with the settings picker) and the original's three nibs 2/4/8 px at the foot of the pad from
  Regular × Regular; nibs only when the width holds both; nothing more on a larger pad (the stroke
  count at W ≥ Expanded is retired, concise widgets). Picking an
  ink or a nib leaves the eraser. Header tools still shed per S-651. Not ported: export as PNG.
- **[S-1498]** The eraser (user requirement: an erasing stylus) is a tool of the ink bar, after the
  nibs, drawn with Windows' eraser glyph; it and the header's toggle are one tool, and one of them
  is on screen: the bar's while the bar shows, else the header's where it fits (the header sheds
  the eraser first, then clear, then full screen; undo stays). A hidden eraser never leaves the pad
  erasing. It removes the whole strokes it touches, as before. A pen's eraser end erases whatever
  tool is chosen, as in Windows Ink.
- **[S-1499]** **Full screen** (user requirement; a header tool): the same pad moves into a windowed
  popup over the whole display and back, so the drawing, the undo history and the gesture are the
  pad's own, nothing copied. On the theme's base fill, a toolbar on the flyouts' chrome at the top
  (the inks, the nibs and the eraser, then undo, clear and Exit full screen, always shown whatever
  Show tools says), and under it the pad on a card at the tile's own shape (the strokes are in pad
  units; another shape would stretch them), as large as the display holds. The tile's button
  leaves it; so does the widget detaching (a view switch, a removal). Needs a live pass: pointer and
  pen input in the popup, a top or side taskbar, mixed DPI.

Needs a manual pass: pen, touch and mouse drawing on the live desktop at every tier, and full
screen on a top or side taskbar and mixed DPI ([S-1499]).
