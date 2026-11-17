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
  header's gap of 4 under it, so over a group mate it reads as a card's header did (the mate's content 4
  under its line). Any width from 96 to 960, natural 240; the relaxed range lets it be stretched
  ([S-12411]). [S-12483] Over a mate joined under it the block stands at its strip's foot, the gap of 4
  under it; otherwise in the middle of the whole strip (the card's top inset given back), so a lone
  header at 36 has 8 over and under its line.
- **[S-9602]** Its page: Title and Caption (each a line of text asked beside the page, empty clears it) and
  Icon, the one icon picker ([S-1803]) inline in its card (`WidgetMenuLines.Icon`, `MenuIcon`), its first
  cell None.
- **[S-9604]** **The place of the widgets it heads** (owner, 2026-11-17: "a user would just put the location in
  the header if he wants to display it"): `HeaderConfig` holds the place every widget about one shares
  (`IPlaced`: Use my location, the place and its name; a group's page sets it once on the header and its mates,
  [S-11753]) and, while **Show the place** is on (`showsPlace`, off for a new header), shows its city after the
  title on its one line ("Weather · Zürich"; the device's "My location"); without a place the title alone. It
  never waits for setup. The widgets under it name no place of their own ([S-12801]). The ready-made groups about
  a place (Weather, Weather station, Sun, Moon, Air quality, Weather graphs) place their header with it on
  ([S-12802]); its picture names the sample place while it has none. Its page: the title, icon and caption, a
  divider, Show the place and the place's lines (`WeatherPlaceRows`, marked shared). Load repair trims the
  place's name and clears a place with one coordinate or one out of range. Tests `PlaceClearTests.S9604_`,
  `ReadyMadeGroupsTests.S12802_`.
- **[S-9603]** On the add page among the Notes subject's widgets (a title is words the person writes);
  every ready-made group comes with one preplaced at its top ([S-12485]). Groupable like any widget: in a
  group it joins its mates as one panel ([S-2249]).

Needs a manual pass: its min, natural and max on the live desktop (showcase views, [S-1810]).
