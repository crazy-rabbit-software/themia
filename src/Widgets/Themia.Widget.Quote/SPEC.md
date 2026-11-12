# Quote (PJ)

The module `quote` ([S-6911]): a line worth rereading, from the user's own list (seeded with the
original's sixteen), on a rotation. Entirely offline: no quote service, no fetch, no cache. Its tags
are this file's alone; the root `SPEC.md` holds the host's sections (styles [S-6000], the rows a
style offers [S-6074], setup [S-1860], the keyboard lease [S-800], the ladder rules [S-660]..[S-666]).

Owning files: everything in this folder: `QuoteModule.cs` (descriptor, view factory, menu lines,
JSON context), `QuoteConfig.cs`, `QuoteRotation.cs` (which quote shows and when, the bundled list,
the key map `QuoteKeys`), `QuoteLayout.cs` (the size, the lines, the sizing, the marks), `QuoteView.cs`,
`QuoteMenu.cs` (its properties-menu lines), `Localization/Locales/` (its own `QuoteStr`, 30 languages).
Tests: `Tests/` (run by `tests/Themia.Modules.Tests`).

- **[S-653]** *(Retired: the quote on the step its area allows; its size is a setting, [S-11306].)*
- **[S-1495]** Quote: the bundled list is the original's sixteen in its order, so a daily unshuffled
  rotation shows the original's quote for the day (`QUOTES[localDay % 16]`, regression-tested). The
  header button shows a random *other* quote (`QuoteRotation.RandomStep`); a tap steps to the next.
  Both stay ephemeral (original S-02).
- **[S-5711]** It starts headerless ([S-2590]), so the header's Show another quote is on its card's
  right-click menu as well (`QuoteView.ContextActions`, the Reset glyph), only with two quotes or more,
  doing what the button does; the button comes back with the header.
- **[S-1990]** Quote keys (`QuoteKeys.Of`), only with two or more quotes: Right, Space or Enter
  the next quote (what a tap does), Left the one before. Ephemeral, like the tap.
- **[S-1496]** *(Retired: the ladder by the tile's size, the attribution given way and the lines that
  fit; [S-11306].)* The quote is primary, start-aligned from the view's top (the widget's alignment
  moves it, [S-6583]); nothing else is drawn (the rotation caption is retired). With its header on, the
  header is the Header block with one button, Show another quote ([S-1495]), wanted only with two quotes
  or more. While the list is empty the
  card's Set up row says "Add a quote" ([S-1862]). Debug renders: `--state sample|long` pin the bundled
  Newton and the plant-a-tree proverb.
- **[S-6404]** Quote's styles ([S-6000]), `QuoteConfig.Style` (`QuoteStyle`, `"style": "plain"`):
  **Plain** (the quote in the language's own marks) and **Large quotation marks**, in that order.
- **[S-6405]** Large quotation marks: one opening mark (the Quote glyph, mirrored right to left) in the
  secondary role (never the accent) above the quote, 8 over it, a glyph on the quote's own step (28, 40
  or 68; 16 over a quote on Title or Body, `QuoteLayout.MarkSize`); its row and its 8 stand over the
  quote's lines in the card ([S-11306]), and the quote then carries no inline marks.

- **[S-11306]** **Its size and its lines are settings** (under its Style): **Size** (Small, Medium,
  Large: the quote on Body, Title or 28) and **Lines** (2 to 6, a new one 3: the most lines the quote
  takes, a longer one ends in an ellipsis on the last). The attribution is always its line under (the
  step's line under, Caption under all three, secondary) while Show author is on, its line kept even
  for a quote without an author, so nothing moves as the quotes turn. The card follows
  (`QuoteLayout.Sizing`): the inset, the mark and its 8 on the Marks style, the lines, the attribution's
  line, the inset; stepped by a line and following Lines (dragging its bottom edge sets them,
  `QuoteLayout.SizeSets`), 200 to 520 wide (a new one 320), where the quote only rewraps. It starts
  without a header. No tier ladder is left. Tests `QuoteLayoutTests.S11306_`.
- **[S-1502]** Its options are all on the properties menu ([S-3719], user request 2026-10-19); it
  has no settings page. **Size** and **Lines** under its Style ([S-11306]), Appearance ([S-3723]), then **Quotes**, the editable list ([S-3729]) in the
  rotation's order: each quote by its text, a submenu of Rename… (its text; never empty), Author…
  (empty takes it off; the author at the end of its line) and Remove, the lines dragged to reorder ([S-3732]); while there
  are none the sentence that the widget will say so; Add… asks for a new quote's text (256 at most).
  Then **Show author** (a switch); under a divider, only with two quotes or more to turn between
  (one, or none, shows the same whatever they say: [S-6070]), **Shuffle** (a switch), **Reshuffle**
  (a new seed, only while shuffling, [S-6074]) and **Refresh** (On click only / Every hour / Every
  day).
  (Tests `QuoteMenuTests`.)

Needs a manual pass: both styles at their min, natural and max on the live desktop (showcase views, [S-1810]).
