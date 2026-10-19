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

- **[S-653]** The quote may grow to TitleLarge, is held to the lines that fit with an ellipsis, and
  gives up its attribution line on a tile too small for the quote itself.
- **[S-1495]** Quote: the bundled list is the original's sixteen in its order, so a daily unshuffled
  rotation shows the original's quote for the day (`QUOTES[localDay % 16]`, regression-tested). The
  header button shows a random *other* quote (`QuoteRotation.RandomStep`); a tap steps to the next.
  Both stay ephemeral (original S-02).
- **[S-1990]** Quote keys (`QuoteKeys.Of`), only with two or more quotes: Right, Space or Enter
  the next quote (what a tap does), Left the one before. Ephemeral, like the tap.
- **[S-1496]** Quote ladder on `IWidgetSizeAware` + `Disclosure`: quote (Caption floor) → author;
  the author goes when the quote would not fit. Nothing else at any size: the rotation caption
  ("Quote of the day · 3 of 16") is retired, a larger tile sets the quote larger. Ceiling TitleLarge, Display on a Huge × Huge tile (replaces S-653's cap there).
- **[S-6404]** Quote's styles ([S-6000]), `QuoteConfig.Style` (`QuoteStyle`, `"style": "plain"`):
  **Plain** (the quote in the language's own marks) and **Large quotation marks**, in that order.
- **[S-6405]** Large quotation marks: one opening mark (the Quote glyph, mirrored right to left) in
  the accent above the quote, a ramp step by the card's smaller side (Title up to Regular,
  TitleLarge at Expanded, Display at Huge, `QuoteLayout.MarkSize`); its row is taken off the top
  before the quote is fitted to the rest ([S-1496]), and the quote then carries no inline marks.

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
