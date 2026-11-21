# Icon (icon)

The module `icon` ([S-6911]), named **Icon**: one icon the person picks (owner, 2026-11-21: "you should also create a new
widget that just shows a selectable icon."), an icon kind on the icon base (root `SPEC.md`, Icon widgets (ICONS),
[S-15000]..[S-15006]). Its tags are [S-15010]..[S-15019], this file's alone; the root `SPEC.md` holds the host's sections.

Owning files: everything in this folder: `IconWidgetModule.cs` (the kind, its module, its one line, the JSON context),
`IconWidgetConfig.cs` (the pick and its repair, `IconChoice`), `IconWidgetView.cs` (its view on the icon face),
`Samples/` (the star), `Preview/` (Debug renders: `--state sample|picked`), `Localization/Locales/` (its own `IconStr`).
Tests: `Tests/IconTests.cs` (run by `tests/Themia.Modules.Tests`).

- **[S-15010]** **What it shows** is its icon and nothing else, the icon base's face ([S-15001], [S-15002]): the glyph
  alone, filling its card, centred, a 76 square when new, down to 32 and as large as the person makes it ([S-15000]). Its
  colour is the item text's (Appearance › Item text, [S-6540]), as every glyph face's. It reads nothing, so it is never
  unavailable; it opens and presses nothing, so it lights nothing under the pointer ([S-15005]). At home under Notes &
  tools, after the Header widget ([S-9600]): a decoration the person sets, as a title is.
- **[S-15011]** **The icon** is its config's (`IconWidgetConfig.Glyph`): one character of the icon font, a Segoe Fluent
  glyph from the one icon picker; none (a new one, or Default picked) draws the star. A stored glyph that is not one
  character of a private use area (a hand-edited file) is cleared to the default, loudly (`IconChoice.IsGlyph`). A copy
  keeps the pick.
- **[S-15012]** **Its one line** is **Icon**: the one icon picker inline in its card ([S-1803], [S-9602]), searched, its
  first cell Default (the star); folded, its head shows the icon chosen ([S-14322]). A pick sets and saves at once. Its
  other options are the Appearance page's ([S-3723]).

Needs a manual pass: its min, natural and max on the live desktop (showcase views, [S-1810]).
