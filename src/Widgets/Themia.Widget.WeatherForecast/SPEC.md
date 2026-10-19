# Weather forecast (WEATHER)

The module `weatherForecast` ([S-6911]): the next few days at a place, from open-meteo. Its tags are
this file's alone; the root `SPEC.md` holds the host's sections and what both weather widgets share
(Weather: current conditions and forecast: the request [S-720], the day labels [S-721], the header's
place title [S-728], the stored answers [S-729], Refresh [S-730], the place row [S-2840]..[S-2848],
the glyphs [S-733]; the day-row parts [S-669]; the background web sync [S-2663]).

Owning files: everything in this folder: `WeatherForecastModule.cs` (descriptor, view factory,
settings rows, JSON context, its glyph), `WeatherForecastConfig.cs` (with its styles),
`WeatherForecastView.cs` and its `.Layouts.cs`, `.Curve.cs`, `.Grid.cs` parts (the forecast and the
temperature now through `IWidgetHost.Weather`), `WeatherForecastLayout.cs` (rows and columns),
`ForecastDays.cs` (the day parts' scale, the grid, the now mark), `ForecastCurve.cs`,
`WeatherForecastMenu.cs` (its properties-menu lines: the weather family's `WeatherPlaceRows.LocationLine`,
precipitation and days), `Preview/` (Debug renders: `--state sample`, the SDK's `WeatherSample`), `Localization/Locales/`
(its own `WeatherForecastStr`, 30 languages, its style names included). Tests: `Tests/` (run by
`tests/Themia.Modules.Tests`).

- **[S-724]** The forecast's look is its style ([S-6163]): Rows (the default), Columns, the
  temperature curve or the grid; what was its layout setting.
- **[S-725]** Rows (`WeatherForecastLayout.Rows`): as many of the first days as have the row floor
  (40), never more than the forecast has, stretched to fill the list and capped at 80; no style
  scrolls (rows of 32 in a hidden scroller showed two and a half slivers on a small tile). The list
  is padded 4 above and 10 below. Rows use the day-row parts ([S-669]), grown with the row (level 1
  from 46, 2 from 58, 3 from 70) while the width still holds the row's other columns: a taller row
  gets larger figures and glyph, never another fact. Columns by width: icon +
  low ≥ 160, range bar ≥ 300, sky ≥ 380, droplet + amount ≥ 420, chance and max wind ≥ 500. A row
  tall enough for the high's line and a caption line (+8) without a sky column puts the sky on a
  second line and the precipitation under the high. The rows are the widget at every size: no Today panel beside or above them (retired; it
  repeated the first row, concise widgets).
- **[S-726]** Columns (`WeatherForecastLayout.Columns`): as many of the first days as have 56 each,
  never scrolled, each at most 160 wide (the group centred past that). The largest `DayScale`
  whose whole core (day, glyph, high, a bar of at least 24, low) fits the column; at the base level
  day + high always, then (by height) icon, low, the bar. Then precipitation, sky (column ≥ 72),
  and at H ≥ Expanded the chance and the wind; the bar takes the leftover.
- **[S-727]** Today is marked by an accent, semibold label and a quiet fill under its row or
  column; a row's fill is inset 4 either side with the control radius, as Windows draws a selected
  list item, its text kept in line with the other days'. In a column the low stands right under
  the high; without a range bar the slack goes under the column, not between them. Precipitation is a droplet and the amount ("0.3"; "0.3 mm" under a curve column ≥ 72), blank on a dry day.
- **[S-6163]** The forecast's styles, `WeatherForecastConfig.Style` (`ForecastStyle`): **Rows**
  ([S-725], the default), **Columns** ([S-726]), **Temperature curve** and **Grid** ([S-6171]). What
  was the `Layout` setting: its Automatic (rows that turned into columns on a strip) is gone, and
  its Grid (the columns wrapped into cards) is now the grid style; the style is the user's choice
  at every size.
- **[S-6164]** **Temperature curve** (`ForecastCurve`): the days as columns (at least 48 wide;
  as many of the first days as fit, never scrolled, never more than the forecast has), each with
  its label; under them one chart across the columns: each day's high a dot on its column's middle
  with its figure over it, joined by a smooth curve in the accent colour, monotone between two
  days (Fritsch–Carlson, so its top is a day's high, never a bump the forecast did not say). Then
  by height: the sky's glyph under the label, the lows as a second curve under the highs on the
  same scale (tertiary colour, figures under their dots), the precipitation under each column (if
  shown). With the lows the scale is the coldest low to the warmest high; without, the highs' own
  range; one temperature all week is a flat line in the middle. A day without a value breaks the
  curve. Time runs left to right in every language; today has its quiet fill under its column, and
  the now mark ([S-6172]).
- **[S-6171]** **Grid** (`ForecastGrid.Plan`): the days as cells (weekday, the sky's glyph, the high
  then the low; no other fact), filled left to right then down, inset 8, 4 between cells. The
  arrangement is the columns × rows (never a whole row or column empty) whose cells are largest
  against the floor cell's shape (the smaller of width/floor and height/floor), ties to fewer empty
  cells: 7 days are 4 × 2 on L, 2 × 4 on Tall, 7 × 1 on Wide. A cell is never under the floor (the
  base parts: about 61 × 72); the grid shows fewer of the first days instead, one at the least (the
  smallest tile's lone cell sheds the glyph). The parts grow by `DayScale` ([S-669]) while the cell
  holds them. Today's cell carries today's fill and accent label ([S-727]).
- **[S-6172]** **Now** (`ForecastNow`): the temperature now is the current-conditions reading for
  the same place and unit that the sync or the offline shelf already holds (`WebSync.Reading`,
  never asked for by the forecast; a reading older than 2 h or from the future is not "now"). On
  the curve: a dashed accent hairline at the time across today's column (midnight at its left edge,
  noon on the day's dot), and with a temperature a ringed dot at it on the curves' scale (which
  takes it in, so it is never clamped) with its figure in accent beside it, right of it after noon
  and left before. On today's range bar (rows and columns): a dot at the temperature on the
  forecast's scale, clamped to the bar's ends, in the colour text takes on the accent and ringed
  in the accent. No reading: no dot, and only the hairline on the curve.
- **[S-6173]** Its options are all on the properties menu ([S-3719], user request 2026-10-19); it
  has no settings page: Appearance ([S-3723]), then behind a divider each Location (the weather family's search, [S-3722], as the
  current conditions', [S-6174]), and Days (1 to 14, the current one with the dot) and the precipitation
  switch. (Tests `WeatherForecastMenuTests`.)

Needs a live pass: the widget against the real service (1 and 14 days), offline with a restored shelf.
