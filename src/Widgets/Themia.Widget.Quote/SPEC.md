# Quote (PJ)

The module `quote` ([S-6911]): a line worth rereading, from the user's own list (seeded with the
original's sixteen), on a rotation. Entirely offline: no quote service, no fetch, no cache. Its tags
are this file's alone; the root `SPEC.md` holds the host's sections (styles [S-6000], the rows a
style offers [S-6074], setup [S-1860], the keyboard lease [S-800], the ladder rules [S-660]..[S-666]).

Owning files: everything in this folder: `QuoteModule.cs` (descriptor, view factory, menu lines,
JSON context), `QuoteConfig.cs`, `QuoteRotation.cs` (which quote shows and when, the bundled list,
the key map `QuoteKeys`), `QuoteLayout.cs` (the marks, the ladder's steps), `QuoteView.cs`,
`QuoteMenu.cs` (its properties-menu lines), `Localization/Locales/` (its own `QuoteStr`, 30 languages).
Tests: `Tests/` (run by `tests/Themia.Modules.Tests`).

- **[S-653]** The quote is the widget: it is set on the Big ramp (28, 40, 68) as far as the area's
  ceiling lets a data widget's number go (`ReadoutRules.Ceiling`: 28 at XS and S, 40 at M, Wide and
  Tall, 68 at L and XL), on Title or Body where not even 28 holds it, and is held to the lines that fit
  with an ellipsis on the last (G3, 2026-10-29: the family's steps replace the ramp to TitleLarge and
  Display).
- **[S-1495]** Quote: the bundled list is the original's sixteen in its order, so a daily unshuffled
  rotation shows the original's quote for the day (`QUOTES[localDay % 16]`, regression-tested). The
  header button shows a random *other* quote (`QuoteRotation.RandomStep`); a tap steps to the next.
  Both stay ephemeral (original S-02).
- **[S-1990]** Quote keys (`QuoteKeys.Of`), only with two or more quotes: Right, Space or Enter
  the next quote (what a tap does), Left the one before. Ephemeral, like the tap.
- **[S-1496]** Quote ladder (`QuoteLayout.Plan`, in the view's area: the card less its header, the
  content box 16 in either side and above the foot): the largest step of Body, Title and the Big steps
  under the ceiling on which the mark (Marks style), the quote's wrapped lines (`QuoteLayout.Lines`,
  word by word on `HeroFit.Ems`, a word longer than a line across as many as it needs) and the
  attribution on the step's line under (Caption under Body, Title and 28, Body under 40 and 68,
  secondary) fit; the attribution goes when not even Body holds the quote with it; a quote too long for
  Body alone ends in an ellipsis on the last whole line. The quote is primary, start-aligned from the
  view's top (the widget's alignment moves it, [S-6583]); room left on a large tile stays empty, nothing
  else is drawn at any size (the rotation caption is retired). The header is the Header block with one
  button, Show another quote ([S-1495]), wanted only with two quotes or more. While the list is empty the
  card's Set up row says "Add a quote" ([S-1862]). Debug renders: `--state sample|long` pin the bundled
  Newton and the plant-a-tree proverb.
- **[S-6404]** Quote's styles ([S-6000]), `QuoteConfig.Style` (`QuoteStyle`, `"style": "plain"`):
  **Plain** (the quote in the language's own marks) and **Large quotation marks**, in that order.
- **[S-6405]** Large quotation marks: one opening mark (the Quote glyph, mirrored right to left) in the
  secondary role (never the accent) above the quote, 8 over it, a glyph on the quote's own step (28, 40
  or 68; 16 over a quote on Title or Body, `QuoteLayout.MarkSize`); its row is taken off the top before
  the quote is fitted to the rest ([S-1496]), and the quote then carries no inline marks.

- **[S-1502]** Its options are all on the properties menu ([S-3719], user request 2026-10-19); it
  has no settings page. Appearance ([S-3723]), then **Quotes**, the editable list ([S-3729]) in the
  rotation's order: each quote by its text, a submenu of Rename… (its text; never empty), Author…
  (empty takes it off; the author at the end of its line) and Remove, the lines dragged to reorder ([S-3732]); while there
  are none the sentence that the widget will say so; Add… asks for a new quote's text (256 at most).
  Then **Show author** (a switch); under a divider, only with two quotes or more to turn between
  (one, or none, shows the same whatever they say: [S-6070]), **Shuffle** (a switch), **Reshuffle**
  (a new seed, only while shuffling, [S-6074]) and **Refresh** (On click only / Every hour / Every
  day).
  (Tests `QuoteMenuTests`.)

Needs a manual pass: both styles at every tier on the live desktop (showcase views, [S-1810]).
