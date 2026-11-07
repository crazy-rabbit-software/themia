# Weather forecast (WEATHER)

The module `weatherForecast` ([S-6911]): the next few days at a place, from open-meteo. Its tags are
this file's alone; the root `SPEC.md` holds the host's sections and what both weather widgets share
(Weather: current conditions and forecast: the request [S-720], the day labels [S-721], the header's
place title [S-728], the stored answers [S-729], Refresh [S-730], the place row [S-2840]..[S-2848],
the glyphs [S-733]; the background web sync [S-2663]; the family's blocks [S-6700]..[S-6734]).

Owning files: everything in this folder: `WeatherForecastModule.cs` (descriptor, view factory,
settings rows, JSON context, its glyph), `WeatherForecastConfig.cs` (with its styles),
`WeatherForecastView.cs` and its `.Layouts.cs`, `.Curve.cs`, `.Grid.cs` parts (the forecast through
`IWidgetHost.Weather`, drawn with the family's Row, Chart and Status blocks), `WeatherForecastLayout.cs`
(rows and columns), `ForecastDays.cs` (the grid), `ForecastCurve.cs`,
`WeatherForecastMenu.cs` (its properties-menu lines: the weather family's `WeatherPlaceRows.LocationLine`,
precipitation and days), `Preview/` (Debug renders: `--state sample`, the SDK's `WeatherSample`), `Localization/Locales/`
(its own `WeatherForecastStr`, 30 languages, its style names included). Tests: `Tests/` (run by
`tests/Themia.Modules.Tests`).

- **[S-724]** The forecast's look is its style ([S-6163]): Rows (the default), Columns, the
  temperature curve or the grid; what was its layout setting.
- **[S-725]** Rows (`WeatherForecastLayout.Rows`): one Row block per day ([S-6711]): the day's sky
  glyph as its 16 lead, the day as its name ("Today" in BodyStrong, never the accent), a range bar
  (the Chart block's track, the day's low to its high on the scale of the days shown) between the name
  and the value from a content 240 wide (32 % of it, at most 140), and "low high" as its value. The
  lines come from the tier ([S-6712]): one while the area is compact either way, two from regular
  both ways, the second the sky in words and, with the precipitation on, its chance and amount where
  there is any ("Light rain · 80% · 4.2 mm"). As many whole rows of 32 or 48 as the content holds,
  never more than the forecast has; nothing stretches and room left stays empty ([S-666]); no style
  scrolls. Under a content of 120 (the smallest tile) a row is the day and its high, and none leads
  ([S-658]). The wind and a column of chances are gone with the old ladder (the tooltip has the day).
- **[S-726]** Columns (`WeatherForecastLayout.Columns`): the days side by side in equal columns across
  the content, each at least 56, never scrolled. Every column has its day (Body, today strong) and its
  high (Body primary); then, as the height allows, the sky's glyph (16, or 32 from a regular height),
  the low (Body secondary), the range standing up between them (the Chart block's column, the day's low
  to its high, taking the room left; at least 24) and the precipitation's amount (Caption secondary) at
  the foot, a fixed line rain or not so the columns line up. Parts stand 4 apart; without a bar they
  keep together at the top.
- **[S-727]** Today is its day's name in BodyStrong (rows, columns, the curve, the grid): no accent,
  no fill under it (the accent is for a selection, and today is not one, [S-6706]). Precipitation is
  text in the secondary role, never the accent: the amount ("0.3"), blank on a dry day.
- **[S-6163]** The forecast's styles, `WeatherForecastConfig.Style` (`ForecastStyle`): **Rows**
  ([S-725], the default), **Columns** ([S-726]), **Temperature curve** and **Grid** ([S-6171]). What
  was the `Layout` setting: its Automatic (rows that turned into columns on a strip) is gone, and
  its Grid (the columns wrapped into cards) is now the grid style; the style is the user's choice
  at every size.
- **[S-6164]** **Temperature curve** (`ForecastCurve`): the days as columns (at least 48 wide; as many
  of the first days as fit, never scrolled, never more than the forecast has), each with its day; under
  them one chart across the columns (the Chart block's series, [S-6717]): each day's high a point on its
  column's middle with its figure (Caption, primary) over it, joined by the accent line with its wash,
  smooth and monotone between two days (Fritsch–Carlson, drawn as twelve straight steps a day, so its
  top is a day's high, never a bump the forecast did not say). Then by height: the sky's 16 glyph under
  the day, the lows as the series' second line (tertiary) under the highs on the same scale, their
  figures (Caption, secondary) under their points, the precipitation's amount under each column (if
  shown). With the lows the scale is the coldest low to the warmest high; without, the highs' own
  range; one temperature all week is a flat line in the middle. A day without a value breaks the line.
  Time runs left to right in every language.
- **[S-6171]** **Grid** (`ForecastGrid.Plan`): the days as cells (the day, the sky's glyph, the high
  then the low; no other fact), filled left to right then down, 12 apart as the family's cells
  ([S-6713]), each centred in its cell. The arrangement is the columns × rows (never a whole row or
  column empty) whose cells are largest against the floor cell's shape (the smaller of width/floor and
  height/floor), ties to fewer empty cells: 7 days are 4 × 2 on L, 7 × 1 on Wide. A cell is never under
  the floor (56 × 80: the day, a 32 glyph and the figures, 4 apart); the grid shows fewer of the first
  days instead, one at the least (the smallest tile's lone cell sheds the glyph). The glyph is 48 from
  an expanded width where the cell holds it, 32 otherwise; nothing else grows.
- **[S-6172]** *(Retired 2026-10-29, group G3: the temperature now is the current conditions' fact, not
  the forecast's ([S-666]); the curve's now mark and the dot on today's range bar are gone, and the
  forecast no longer reads the current-conditions reading.)*
- **[S-6173]** Its options are all on the properties menu ([S-3719], user request 2026-10-19); it
  has no settings page: Appearance ([S-3723]), then behind a divider each Location (the weather family's search, [S-3722], as the
  current conditions', [S-6174]), Days (1 to 14, the current one with the dot) and the precipitation
  switch, and the links to Temperature unit and Update interval ([S-3749], as the current
  conditions'). (Tests `WeatherForecastMenuTests`.)
- **[S-2036]** A click on the card ([S-2000]) opens MSN Weather's forecast for the widget's place,
  in the UI language's market and the unit shown ([S-2006], `WeatherForecastModule.Opens`); nothing
  while it has no place (it shows Set up then).

Needs a live pass: the widget against the real service (1 and 14 days), offline with a restored shelf.
