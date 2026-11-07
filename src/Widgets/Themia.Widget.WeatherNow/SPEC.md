# Weather now (WEATHER)

The module `weatherNow` ([S-6911]): the weather where the user says they are, from open-meteo. Its
tags are this file's alone; the root `SPEC.md` holds the host's sections and what both weather
widgets share (Weather: current conditions and forecast: the request [S-720], the header's place
title [S-728], the stored answers [S-729], Refresh [S-730], the place row [S-2840]..[S-2848], the
glyphs [S-733]; the background web sync [S-2663]; the family's blocks [S-6702], [S-6711], [S-6714] and the
online widgets' state row [S-6734]).

Owning files: everything in this folder: `WeatherNowModule.cs` (descriptor, view factory, settings
rows, JSON context), `WeatherNowConfig.cs` (with its styles and the row only one style offers),
`WeatherNowView.cs` and `WeatherNowView.Styles.cs` (they read the reading through
`IWidgetHost.Weather`), `WeatherNowLayout.cs` (the standard style's face), `WeatherNowStyles.cs`
(the other two styles' steps), `WeatherFacts.cs` (its readings), `WeatherNowMenu.cs` (its properties-menu
lines: the weather family's `WeatherPlaceRows.LocationLine` and Show detail), `Preview/` (Debug renders: `--state sample|stale|failed`,
the SDK's `WeatherSample`), `Localization/Locales/` (its own `WeatherNowStr`, 30 languages, its style
names included). Tests: `Tests/WeatherNowTests.cs` (run by `tests/Themia.Modules.Tests`); host rules it
is the example of are in the module test host (`OnlineHostTests`, `SyncHousekeepingTests`,
`ConfigSnapshotTests`).

- **[S-722]** The current readings, in order: Today ("H 16°  L 8°", today's high and low, when the
  answer has today), Feels like, Humidity, Wind ("11 km/h NW"), Precipitation (the last hour's amount
  while it rains, else today's chance), Pressure, UV index, Cloud cover, Gusts, Sunrise, Sunset. A
  reading the service did not send is left out, not "--".
- **[S-723]** The standard style's face (`WeatherNowLayout.Plan`), the detail widgets' plan ([S-2402],
  `DetailFace`) on the family's blocks: the temperature is the Readout at the view's top, start-aligned
  (or where the widget's alignment puts it in its column, [S-6583]), led by the sky's glyph at its step,
  the condition its line under, on the step the widest reading ("-18°F" with its glyph and line under)
  takes: 28 (XS, S), 40 (M, Wide, Tall), 68 (L, XL) where it fits, never larger ([S-6702], [S-666]).
  The readings are fact rows ([S-6711]: their 16 glyph, the name secondary, the value primary), only
  whole rows: in a column beside the reading from the top on an expanded width (the reading's column
  two fifths of the card), else 8 under it; XS and S are the reading alone. Room left at L and XL stays
  empty. No next days and no next hours at any size ([S-666]: the forecast widget's job). "Show detail"
  off is the reading alone at every size. A reading kept through a failed refresh stays and the header's
  problem glyph says so ([S-7042]); with no reading the Status row at the top says why ([S-6734]).
- **[S-6160]** The current conditions' styles ([S-6000]), `WeatherNowConfig.Style`
  (`WeatherNowStyle`): **Standard** (the ladder, [S-723]), **Large icon** and **Minimal**, in that
  order. "Show detail" is the standard style's; the place and the location name are every style's.
- **[S-6161]** **Large icon** (`WeatherNowStyles.LargeIcon`): the sky's glyph beside the temperature,
  the condition under the temperature: the largest Readout step (within the area's ceiling, 28, 40, 68)
  whose block (its line and the condition's) fits the content's height with a glyph beside it; the glyph
  the largest family glyph size (16, 28, 32, 40, 48, 68) no taller than that block that leaves the
  temperature its width, 8 between; the condition ends in an ellipsis in the width it has. The pair
  stands at the view's top, centred unless the widget is aligned elsewhere ([S-6582]). No readings and
  no range at any size.
- **[S-6162]** **Minimal** (`WeatherNowStyles.Minimal`): the temperature and its unit alone, the
  Readout on the step its content box takes (28, 40, 68, [S-6702]; no light weight, never filling the
  box), at the view's top, centred unless aligned elsewhere; no glyph, condition, range or readings.
- **[S-6174]** Its options are all on the properties menu ([S-3719], user request 2026-10-19); it
  has no settings page. Under Style the readings line's switch (the standard style only, [S-6074]);
  then Appearance ([S-3723]) and behind a divider **Location**, the weather family's search ([S-3722],
  `WeatherPlaceRows.LocationLine`): the chosen place's city at the end of its line (nothing without one), the geocoder asked once typing pauses ([S-2841]), its places as lines (a click picks one,
  resolved once, [S-2840]) or what it said in their place ([S-2843]); under them Clear location
  (greyed without a place, [S-2848]); the place's city is the header's title ([S-728]). Then behind a
  divider the links to what it follows and does not set ([S-3749], [S-3590]): **Temperature unit**
  (⚙ › Language & region) and **Update interval** (⚙ › Online updates › Weather), each with what is in
  force. (Tests `WeatherNowMenuTests`.)
- **[S-2035]** A click on the card ([S-2000]) opens MSN Weather's forecast for the widget's place,
  in the UI language's market and the unit shown ([S-2006], `WeatherNowModule.Opens`); nothing while
  it has no place (it shows Set up then).

Needs a live pass: the widget against the real service, offline with a restored shelf, the first-run
desktop's bare large-icon reading.
