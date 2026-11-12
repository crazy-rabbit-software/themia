# Weather forecast (WEATHER)

The module `weatherForecast` ([S-6911]): the next few days at a place, from open-meteo. Its tags are
this file's alone; the root `SPEC.md` holds the host's sections and what both weather widgets share
(Weather: current conditions and forecast: the request [S-720], the day labels [S-721], the header's
place label [S-728], [S-2260], the stored answers [S-729], Refresh [S-730], the place row [S-2840]..[S-2848],
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
  (the Chart block's track, the day's low to its high on the scale of the days shown, 32 % of the
  row, at most 140: it grows with the card) and "low high" as its value. Lines per row is a setting
  ([S-11147]): one, or two (the second the sky in words and, with the precipitation on, its chance and
  amount where there is any, "Light rain · 80% · 4.2 mm"). The days are the config's, one row each.
- **[S-726]** Columns (`WeatherForecastLayout.Columns`): the days side by side in equal columns across
  the content. Every column has, 4 apart, its day (Body, today strong), the sky's 32 glyph, its high
  (Body primary), the range standing up (the Chart block's column, the day's low to its high, 32), the
  low (Body secondary) and, with the precipitation on, its amount (Caption secondary) at the foot, a
  fixed line rain or not so the columns line up.
- **[S-727]** Today is its day's name in BodyStrong (rows, columns, the curve, the grid): no accent,
  no fill under it (the accent is for a selection, and today is not one, [S-6706]). Precipitation is
  text in the secondary role, never the accent: the amount ("0.3"), blank on a dry day.
- **[S-6163]** The forecast's styles, `WeatherForecastConfig.Style` (`ForecastStyle`): **Rows**
  ([S-725], the default), **Columns** ([S-726]), **Temperature curve** and **Grid** ([S-6171]). What
  was the `Layout` setting: its Automatic (rows that turned into columns on a strip) is gone, and
  its Grid (the columns wrapped into cards) is now the grid style; the style is the user's choice
  at every size.
- **[S-6164]** **Temperature curve** (`ForecastCurve`): the days as columns, each with its day and the
  sky's 16 glyph under it; under them one chart across the columns (the Chart block's series, [S-6717]):
  each day's high a point on its column's middle with its figure (Caption, primary) over it, joined by
  the accent line with its wash, smooth and monotone between two days (Fritsch–Carlson, drawn as twelve
  straight steps a day, so its top is a day's high, never a bump the forecast did not say); the lows the
  series' second line (tertiary) on the same scale, their figures (Caption, secondary) under their
  points; the precipitation's amount under each column (if shown). The chart's band takes the height
  the other parts leave. The scale is the coldest low to the warmest high; one temperature all week is a
  flat line in the middle. A day without a value breaks the line. Time runs left to right in every
  language.
- **[S-6171]** **Grid** (`ForecastGrid`): the days as cells (the day, the sky's 32 glyph, the high
  then the low; no other fact), filled left to right then down, 12 apart as the family's cells
  ([S-6713]), each centred in its cell, in a fixed arrangement by the days: one row up to four days,
  two up to eight, three past (`ForecastGrid.Arrangement`).
- **[S-6172]** *(Retired 2026-10-29, group G3: the temperature now is the current conditions' fact, not
  the forecast's ([S-666]); the curve's now mark and the dot on today's range bar are gone, and the
  forecast no longer reads the current-conditions reading.)*
- **[S-6173]** Its options are all on the properties menu ([S-3719], user request 2026-10-19); it
  has no settings page: Appearance ([S-3723]), then behind a divider each Location (the weather family's search, [S-3722], as the
  current conditions', [S-6174]), Days (1 to 14, the current one with the dot: the size follows) and the precipitation
  switch, and the links to Temperature unit and Update interval ([S-3749], as the current
  conditions'). (Tests `WeatherForecastMenuTests`.)
- **[S-2036]** A click on the card ([S-2000]) opens MSN Weather's forecast for the widget's place,
  in the UI language's market and the unit shown ([S-2006], `WeatherForecastModule.Opens`); nothing
  while it has no place (it shows Set up then).

- **[S-11145]** **An atom** ([S-11170]): its **Days** (1..14, a new one 5; `WeatherForecastConfig.Days`,
  also what it asks for) decide what it shows in every style; nothing sheds or appears with the box (a
  bar or the chart longer, a column or cell wider). Days and lines out of range are repaired.
- **[S-11146]** **Sizes** (`WeatherForecastLayout.Sizing`, with its header): **Rows** a 28 or 40 row a
  day, the height stepped by the days and following them, 220..420 wide (a new one 300 × 188);
  **Columns** a 56 column a day, the width stepped by the days and following them, one height (208 with
  the rain, 188 without); **Curve** 48..72 a day wide (a new one 56), the chart's band 72..144 (a new one
  96) under the days' parts; **Grid** cells 56..80 wide (a new one 64) in its arrangement, one height.
  Dragging the rows' bottom edge or the columns' side edge sets the days ([S-2442]).
- **[S-11147]** Under Style, the rows style's **Lines per row** (one or two, the kit's words); the
  other styles offer nothing there.

Needs a live pass: the widget against the real service (1 and 14 days), offline with a restored shelf.
- View links: a day publishes and lights the day under the pointer and marks the day selected on the view ([S-11924], [S-11927]).
