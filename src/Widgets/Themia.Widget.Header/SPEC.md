# Header (header)

The module `header` ([S-6911]). Its tags are [S-9600]..[S-9699], this file's alone; the root
`SPEC.md` holds the host's sections.

Owning files: everything in this folder. Tests: `Tests/HeaderTests.cs` (run by
`tests/Themia.Modules.Tests`).

- **[S-9600]** A title of the person's own over the widgets under it (owner, 2026-11-16: "you should be
  able to add your own header widget without it being tied to a widget"). No card has a header of its own
  ([S-12480]); this is the one way a title stands on the desktop. Its config: the title (`text`; empty
  draws the word "Title", so a new one is never a blank strip), the glyph before it (`glyph`, one Segoe
  Fluent glyph or none) and a caption under it (`caption`, a date, a note; none by default). Where it lies
  across its strip is the widget's alignment ([S-6581]: start, middle, end). Load repair keeps each text
  one line of at most 80 characters and drops a glyph that is not one private-use character.
- **[S-9601]** Drawn with the family's header look (`HeaderBlock`: a 16 glyph, 8, the name in BodyStrong
  at the secondary level with its ellipsis; the caption in Caption, tertiary). Sized as a thin strip
  (`HeaderLayout`): the card's inset over it, the line of 20, a Caption line with a caption, and the
  header's gap of 4 under it. Any width from 96 to 960, natural 240; the relaxed range lets it be stretched
  ([S-12411]). [S-12483] The block stands in the middle of the whole strip (the card's top inset given back),
  joined to the cards under it or alone alike, the band over and under its line's ink alike (11 at 36). [S-14307] In a
  larger card the line grows as one (`ReadingFit`) until the width stops it, centred top and foot.
- **[S-9602]** Its page: Title and Caption, each typed in place in its box (owner, 2026-11-19: "the title and
  caption for example should show inline not under the additional menu"; `WidgetMenuLines.Text`, [S-2332]; empty
  clears it, kept as load repair keeps it), and Icon, the one icon picker ([S-1803]) inline in its card
  (`WidgetMenuLines.Icon`, `MenuIcon`), its first cell None. Test `HeaderTests.S9602_`.
- **[S-9604]** **The place of the widgets it heads** (owner, 2026-11-17: "a user would just put the location in
  the header if he wants to display it"): `HeaderConfig` holds the place every widget about one shares
  (`IPlaced`: the place and its name, or the device's; a group's page sets it once on the header and its mates,
  [S-11753]) and, while it holds one, shows its city after the title on its one line ("Weather · Zürich"; the
  device's "My location"); without a place the title alone. It never waits for setup. The widgets under it name no
  place of their own ([S-12801]). Its page names the place by its **Location** alone, under a divider apart from the
  title and caption (`WeatherPlaceRows.Named`, marked shared; a place found replaces the device's): no Show the place
  switch and no Use my location (owner, 2026-11-19: "show the place should not be a caption setting, use my location
  neither"). The ready-made groups about a place place their header with no place yet; it names one once the
  group's is set ([S-12802]). Load repair trims the place's name and clears a place with one coordinate or one out
  of range. Tests `PlaceClearTests.S9604_`, `ReadyMadeGroupsTests.S12802_`, `HeaderTests.S9602_`.
- **[S-9603]** On the add page among the Notes subject's widgets (a title is words the person writes);
  every ready-made group comes with one preplaced at its top ([S-12485]). Groupable like any widget: in a
  group it joins its mates as one panel ([S-2249]).

Needs a manual pass: its min, natural and max on the live desktop (showcase views, [S-1810]).
