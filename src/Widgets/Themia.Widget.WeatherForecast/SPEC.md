# Forecast curve (WEATHER)

The module `weatherForecast` ([S-6911]), labelled **Forecast curve**: the next few days' highs and lows at a place, from
open-meteo, as one temperature curve. Since [S-15540] (2026-11-21) the curve is its one look: its Rows, Columns and Grid
came back as ready-made repeaters (root `SPEC.md`, REPEATER), and it stands among the weather graphs. Its tags are
this file's alone; the root `SPEC.md` holds the host's sections and what both weather widgets share
(Weather: current conditions and forecast: the request [S-720], the day labels [S-721], the stored answers [S-729], Refresh [S-730], the place row [S-2840]..[S-2848],
the glyphs [S-733]; the background web sync [S-2663]; the family's blocks [S-6700]..[S-6734]).

Owning files: everything in this folder: `WeatherForecastModule.cs` (descriptor, view factory,
settings rows, JSON context, its glyph), `WeatherForecastConfig.cs` (no style),
`WeatherForecastView.cs` and its `.Curve.cs` part (the forecast through `IWidgetHost.Weather`, drawn with the family's
Chart and Status blocks), `ForecastCurve.cs` (the curve's geometry, its days, its sizing; `ForecastDefaults`),
`WeatherForecastMenu.cs` (its properties-menu lines: the weather family's `WeatherPlaceRows.LocationLine` and
precipitation), `Preview/` (Debug renders: `--state sample`, the SDK's `WeatherSample`), `Localization/Locales/`
(its own `WeatherForecastStr`; its label and description English-only). Tests: `Tests/` (run by
`tests/Themia.Modules.Tests`).

- **[S-724]** **Forecast curve**: the forecast's one look is the temperature curve ([S-6164]), no style ([S-15540],
  2026-11-21: its Rows, Columns and Grid styles retired; until then its look was its style, [S-6163]). Its label and
  description are English-only (`forecastCurve.label`, `forecastCurve.desc`); on the add page it stands in the Weather
  subject's run of graphs, after them (`AddPlacement(Weather, 2, 7)`), and stays one of Popular's handful. Without a
  forecast the curve keeps its days from today, the unknown sky and no line or figure, why as their tooltip ([S-12813],
  `ForecastDefaults.Unread`). Tests `OnlineStylesTests.S6160_`, `ForecastCurveTests.S724_`.
- **[S-725]** *(Retired 2026-11-21 by [S-15540]: the rows style is gone; a day a row is the ready-made repeater **Forecast**.)* Rows
  (`WeatherForecastLayout.Rows`): one Row block per day, its sky's glyph, the day, a range bar and "low high".
- **[S-726]** *(Retired 2026-11-21 by [S-15540]: the columns style is gone; the days side by side are the ready-made repeater **Forecast
  columns**.)* Columns (`WeatherForecastLayout.Columns`): the days side by side, each its day, glyph, high, standing
  range and low.
- **[S-727]** Today is its day's name in BodyStrong over its column: no accent,
  no fill under it (the accent is for a selection, and today is not one, [S-6706]). Precipitation is
  text in the secondary role, never the accent: the amount ("0.3"), blank on a dry day.
- **[S-6163]** *(Retired 2026-11-21 by [S-15540]: the forecast has one look, the curve, and no style; `ForecastStyle` and its
  `styles.forecast.*` names are gone.)* The forecast's styles: Rows, Columns, Temperature curve and Grid.
- **[S-6164]** **The curve** (`ForecastCurve`): the days as columns, each with its day and the
  sky's 16 glyph under it; under them one chart across the columns (the Chart block's series, [S-6717]):
  each day's high a point on its column's middle with its figure (Caption, primary) over it, joined by
  the accent line with its wash, smooth and monotone between two days (Fritsch–Carlson, drawn as twelve
  straight steps a day, so its top is a day's high, never a bump the forecast did not say); the lows the
  series' second line (tertiary) on the same scale, their figures (Caption, secondary) under their
  points; the precipitation's amount under each column (if shown). The chart's band takes the height
  the other parts leave. The scale is the coldest low to the warmest high; one temperature all week is a
  flat line in the middle. A day without a value breaks the line. Time runs left to right in every
  language.
- **[S-6171]** *(Retired 2026-11-21 by [S-15540]: the grid style is gone; the days as cells are the ready-made repeater **Forecast
  grid**.)* Grid (`ForecastGrid`): the days as cells in a fixed arrangement by the days.
- **[S-6172]** *(Retired 2026-10-29, group G3: the temperature now is the current conditions' fact, not
  the forecast's ([S-666]); the curve's now mark and the dot on today's range bar are gone, and the
  forecast no longer reads the current-conditions reading.)*
- **[S-6173]** Its options are all on the properties menu ([S-3719], user request 2026-10-19); it
  has no settings page: Appearance ([S-3723]; nothing under Style, one look, [S-15540]), then behind a divider each Location (the weather family's search, [S-3722], as the
  current conditions', [S-6174]) and the precipitation switch (how many days is the card's, [S-14410]: no Days
  line); the temperature unit and the update interval are ⚙'s, followed, no link row ([S-3590], [S-3749]). (Tests `WeatherForecastMenuTests`.)
- **[S-2036]** A click on the card ([S-2000]) opens MSN Weather's forecast for the widget's place,
  in the UI language's market and the unit shown ([S-2006], `WeatherForecastModule.Opens`); nothing
  while it has no place (it shows Set up then).

- **[S-11145]** **An atom** ([S-11170]): its **Days** (1..14, a new one 5; it asks for all fourteen,
  `WeatherForecastConfig.ForecastDays`) are the card's (owner, 2026-11-20: "Anything that's just 'how many of the same
  item' (days, hours, weeks, processes) follows the card's size", [S-14410]): no menu line sets them and none is stored
  ([S-14502]): the card shows every column it reaches at their one size, the last cut ([S-14810],
  `ForecastCurve.DaysIn`). Nothing else sheds or appears with the box (the chart taller). Tests
  `ForecastCurveTests.S11145_S14502_S14810_`.
- **[S-11146]** **Sizes** (`ForecastCurve.Sizing`): a 60 column a day (its 56 in whole grid cells) from the card's left
  edge, every day the width reaches at that one size, the last cut by its right edge (owner, 2026-11-20: "the widget items
  should actually have a fixed size ... the last item will be cutoff to some extent then", [S-14810]); the chart
  80..232 (a new one 104: the one band, [S-6755], 48..200 and a new one's 72, and the figures' two Caption lines) under the days' parts (the day, the 16 glyph and, if on, the rain line); a new one five days,
  300 wide. It takes every grid cell; a resize stores no days ([S-14502]). *(The rows', columns' and grid's sizes
  retired 2026-11-21 by [S-15540] with their styles.)* Tests `ForecastCurveTests.S11146_`,
  `FixedItemKindsTests.S14810_the_forecast_curve_`.
- **[S-11147]** *(Retired 2026-11-21 by [S-15540]: the rows style is gone, and with it its **Lines per row** under Style; the config's
  `lines` is gone.)*
- **[S-12403]** **The days fill the content** ([S-12400]): the curve's columns across the width from its left edge, the
  chart the height the other parts leave. *(The rows', columns' and grid's filling retired 2026-11-21 by [S-15540] with
  their styles.)*
- **[S-14815]** **The curve's days stand from the card's edges** (owner, 2026-11-21: "the weather forecast widget grid
  view items do not go from edge to edge, they have space everywhere. same for the columns view and maybe also other
  styles. fix this, also for other widgets if applicable."; docs/WIDGET-RULES.md WR-2, WR-3, WR-4). A curve's column is an
  item (`ItemMark`) from the card's left edge and from its top edge to its foot, the inset over and under its parts padding
  inside the column (the hover and a link light the column whole); no margin and no gap round or between them; every edge
  on a whole device pixel (`RowReach`). *(The rows', columns' and grid's edges retired 2026-11-21 by [S-15540] with their
  styles; the ready-made repeaters lay their atoms edge to edge.)* Renders judged by `scripts/item-check.py`.

Needs a live pass: the widget against the real service (1 and 14 days), offline with a restored shelf.
- View links: a day publishes and lights the day under the pointer and marks the day selected on the view ([S-11924], [S-11927]).
