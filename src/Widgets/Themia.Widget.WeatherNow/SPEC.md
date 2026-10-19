# Weather now (WEATHER)

The module `weatherNow` ([S-6911]): the weather where the user says they are, from open-meteo. Its
tags are this file's alone; the root `SPEC.md` holds the host's sections and what both weather
widgets share (Weather: current conditions and forecast: the request [S-720], the header's place
title [S-728], the stored answers [S-729], Refresh [S-730], the place row [S-2840]..[S-2848], the
glyphs [S-733]; the hero readout [S-667], the stat parts [S-668]; the background web sync [S-2663]).

Owning files: everything in this folder: `WeatherNowModule.cs` (descriptor, view factory, settings
rows, JSON context), `WeatherNowConfig.cs` (with its styles and the row only one style offers),
`WeatherNowView.cs` and `WeatherNowView.Styles.cs` (they read the reading through
`IWidgetHost.Weather`), `WeatherNowLayout.cs` (the standard style's ladder), `WeatherNowStyles.cs`
(the other two styles' fits), `WeatherFacts.cs` (its readings), `WeatherNowMenu.cs` (its properties-menu
lines: the weather family's `WeatherPlaceRows.LocationLine` and Show detail), `Preview/` (Debug renders: `--state sample`,
the SDK's `WeatherSample`), `Localization/Locales/` (its own `WeatherNowStr`, 30 languages, its style
names included). Tests: `Tests/WeatherNowTests.cs` (run by `tests/Themia.Modules.Tests`); host rules it
is the example of are in the module test host (`OnlineHostTests`, `SyncHousekeepingTests`,
`ConfigSnapshotTests`).

- **[S-722]** The current readings, in order: Feels like, Humidity, Wind ("11 km/h NW"),
  Precipitation (the last hour's amount while it rains, else today's chance), Pressure, UV index,
  Cloud cover, Gusts, Sunrise, Sunset. A reading the service did not send is left out, not "--".
- **[S-723]** Current-conditions ladder (`WeatherNowLayout.Plan`): the temperature always, as the
  hero readout ([S-667]: icon beside once it fits; the condition, today's range and the Feels
  token are its caption lines, beside or under, [S-656]); then — a strip, or a tile Expanded wide
  (not tall) with H ≥ Regular: the readings as stat lines ([S-668]) in a column beside the reading,
  one column while they fit and two only when not; a tall column (H ≥ Expanded): today's high/low,
  then up to 8 stat lines under it; otherwise a flat row of stat cells (no boxes, original S-07)
  once H ≥ Regular and three cells fit, else a single "Feels 17°C" caption line under the
  condition; today's "H 19°  L 11°" (Least ≥ Regular, or beside on a strip); a second row of cells
  at Huge both ways where the cells are under the reading. The reading and its rungs are one group,
  centred in the tile, inset 16 either side and 12 above and below (the bottom readings used to
  run within 4px of the card's edge). No next-days strip and no next-24-hours chart at any size (retired, [S-666]:
  the hours and the days are the forecast widget's job); what the rungs leave is the reading's.
  "Show detail" off = the reading alone at every size.
- **[S-6160]** The current conditions' styles ([S-6000]), `WeatherNowConfig.Style`
  (`WeatherNowStyle`): **Standard** (the ladder, [S-723]), **Large icon** and **Minimal**, in that
  order. "Show detail" is the standard style's; the place and the location name are every style's.
- **[S-6161]** **Large icon** (`WeatherNowStyles.LargeIcon`): the sky's glyph is the picture, as tall
  as the inset box (at most 42 % of its width, 24..200), the temperature beside it in the width left
  (Subtitle to Display, then filled past it, [S-659]); the condition, then today's range, under the
  temperature where the height has their lines. A tall column puts the glyph above (70 % of the
  width, 60 % of the height left) and the text centred under it. No readings at any size.
- **[S-6162]** **Minimal** (`WeatherNowStyles.Minimal`): the temperature and its unit alone, in
  Segoe's light weight, as large as the inset box holds it (the ramp, then filled past TitleLarge,
  [S-659], as the minimal clock, [S-6014]); no glyph, condition, range or readings.
- **[S-6174]** Its options are all on the properties menu ([S-3719], user request 2026-10-19); it
  has no settings page. Under Style the readings line's switch (the standard style only, [S-6074]);
  then Appearance ([S-3723]) and **Location**, the weather family's search ([S-3722],
  `WeatherPlaceRows.LocationLine`): the chosen place's city at the end of its line (nothing without one), the geocoder asked once typing pauses ([S-2841]), its places as lines (a click picks one,
  resolved once, [S-2840]) or what it said in their place ([S-2843]); under them Clear location
  (greyed without a place, [S-2848]) and the location-name switch ([S-728]). (Tests `WeatherNowMenuTests`.)

Needs a live pass: the widget against the real service, offline with a restored shelf, the first-run
desktop's bare large-icon reading.
