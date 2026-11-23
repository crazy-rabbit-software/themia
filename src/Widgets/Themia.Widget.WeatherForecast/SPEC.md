# Weather forecast (WEATHER)

The module `weatherForecast` ([S-6911]): the next few days at a place, from open-meteo. Its tags are
this file's alone; the root `SPEC.md` holds the host's sections and what both weather widgets share
(Weather: current conditions and forecast: the request [S-720], the day labels [S-721], the stored answers [S-729], Refresh [S-730], the place row [S-2840]..[S-2848],
the glyphs [S-733]; the background web sync [S-2663]; the family's blocks [S-6700]..[S-6734]).

Owning files: everything in this folder: `WeatherForecastModule.cs` (descriptor, view factory,
settings rows, JSON context, its glyph), `WeatherForecastConfig.cs` (with its styles),
`WeatherForecastView.cs` and its `.Layouts.cs`, `.Curve.cs` parts (the forecast through `IWidgetHost.Weather`, its days on
the item base's `ItemList`, the curve on the Chart block, the Status block), `ForecastItems.cs` (a day as the item base's
item in each style, its parts, its measures), `WeatherForecastLayout.cs` (each style's sizes), `ForecastDays.cs` (the days
with nothing read, the problem glyph's lines), `ForecastCurve.cs`,
`WeatherForecastMenu.cs` (its properties-menu lines: the weather family's `WeatherPlaceRows.LocationLine`,
precipitation and days), `Preview/` (Debug renders: `--state sample`, the SDK's `WeatherSample`), `Localization/Locales/`
(its own `WeatherForecastStr`, 30 languages, its style names included). Tests: `Tests/` (run by
`tests/Themia.Modules.Tests`).

- **[S-724]** The forecast's look is its style ([S-6163]): the item base's arrangements (Table, the default; Row list; Grid;
  List; Column list) or the temperature curve. Without a forecast the style keeps its days from today, "--" and the unknown
  sky, why as their tooltip ([S-12813], `ForecastDefaults.Unread`).
- **[S-725]** Table (what Rows was, owner 2026-11-23: "the current weather forecast row view will be the new table view"): a
  row a day, its parts in the item base's aligned columns ([S-16945], [S-16956]): the day's sky glyph as its 16 lead, the
  day ("Today" in BodyStrong, never the accent), the range bar (the day's low to its high on the scale of the days laid,
  what the row leaves and never under 32 % of the row, at most 140) and "low high" at its end; the sky in words with its
  chance and amount ("Light rain · 80% · 4.2 mm", the precipitation on) is the **Conditions** part, a column of its own
  where shown (what was the second line, [S-11147]). The parts and their order are the page's ([S-16950]); column titles
  are off by default.
- **[S-726]** Row list (what Columns was): the days side by side in columns of 60 from the card's left edge, from its top
  edge to its foot ([S-16942]): the day, the sky's 32 glyph, the high, the range standing up in the room the parts leave,
  the low and, with the precipitation on, its amount at the foot, a line kept blank on a dry day so the columns line up.
- **[S-727]** Today is its day's name in BodyStrong (rows, columns, the curve, the grid): no accent,
  no fill under it (the accent is for a selection, and today is not one, [S-6706]). Precipitation is
  text in the secondary role, never the accent: the amount ("0.3"), blank on a dry day.
- **[S-6163]** The forecast's styles, `WeatherForecastConfig.Style` (`ForecastStyle`): **Table** ([S-725], the default),
  **Row list** ([S-726]), **Temperature curve** ([S-6164]), **Grid** ([S-6171]), **List** and **Column list** (the item
  base's, [S-16941], [S-16943]). A stored `rows` or `columns` is no style any more and loads as the default.
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
- **[S-6171]** **Grid**: the days as the item base's cards of 80 × 100 ([S-16944]): the day, the sky's 32 glyph, the high
  beside the low (no other fact), centred, the whole columns the width holds, the lines down, the last cut by the foot.
- **[S-6172]** *(Retired 2026-10-29, group G3: the temperature now is the current conditions' fact, not
  the forecast's ([S-666]); the curve's now mark and the dot on today's range bar are gone, and the
  forecast no longer reads the current-conditions reading.)*
- **[S-6173]** Its options are all on the properties menu ([S-3719], user request 2026-10-19); it
  has no settings page: Appearance ([S-3723]), then behind a divider each Location (the weather family's search, [S-3722], as the
  current conditions', [S-6174]) and the precipitation switch (how many days is the card's, [S-14410]: no Days
  line); the temperature unit and the update interval are ⚙'s, followed, no link row ([S-3590], [S-3749]). (Tests `WeatherForecastMenuTests`.)
- **[S-2036]** A click on the card ([S-2000]) opens MSN Weather's forecast for the widget's place,
  in the UI language's market and the unit shown ([S-2006], `WeatherForecastModule.Opens`); nothing
  while it has no place (it shows Set up then).

- **[S-11145]** **An atom** ([S-11170]): its **Days** (1..14, a new one 5; `WeatherForecastConfig.Days`,
  also what it asks for) decide what it shows in every style, and they are the card's (owner, 2026-11-20: "Anything
  that's just 'how many of the same item' (days, hours, weeks, processes) follows the card's size", [S-14410]): no
  menu line sets them and none is stored ([S-14502]): the card shows every day it reaches at their one size, the last
  cut ([S-14810]). Nothing else sheds or appears with the box (a bar or
  the chart longer, a column or cell wider). Days and lines out of range are repaired.
- **[S-11146]** **Sizes** (`WeatherForecastLayout.Sizing`): the item base's in its arrangement ([S-16954]) with the days'
  measures (`ForecastItems.Cells`): the table a row of 30 a day under no titles (a new one 320 × 150), the row list a column
  of 60 a day at one height (190 with the rain, 170 without: its 184 and 164 in whole cells; a new one five, 300 wide), the grid cells of 80 × 100 (a new
  one 3 × 2), the list and the column list the base's; the curve a 60 column a day from the card's left edge, the chart
  80..232 (a new one 104) under the days' parts. Every style takes every grid cell; a resize stores no days ([S-14502]).
  Tests `ForecastItemsTests.S16974_S11146_`, `ForecastCurveTests.S11146_S14410_`.
- **[S-11147]** *(Retired by [S-16974]: the table's second line is its **Conditions** part, shown or not and ordered in the
  page's parts; a stored `lines` is dropped at load.)*
- **[S-12403]** **The days fill the content** ([S-12400]): in the table each range bar takes what the widest day and the widest
  value leave, the same length on every row, never under its share ([S-16956]); the row list's standing bars take the
  height the other parts leave; the curve the chart the height left; the grid's cells their one size.
- **[S-14815]** **Every style's days stand from the card's edges** (owner, 2026-11-21; docs/WIDGET-RULES.md WR-2, WR-3, WR-4): a
  day is the item base's item, its box from the card's edges with its padding inside it, touching or with the block gap
  ([S-16954]); the grid's columns the whole ones the width holds, the room past them empty at the right edge ([S-16438]);
  the curve's columns from the card's left edge and from its top edge to its foot. Every edge on a whole device pixel.
- **[S-16439]** **The curve's plot is edge to edge** (owner, 2026-11-19: "Plot edge to edge", the plot and its fill reach
  the card's edges, the labels stand inside it, over the plot): the highs' and the lows' lines are held out from the first
  day's point to the card's left edge and from the last day's to its right edge, the days' columns' end or the card's where
  it is wider than its days (`ForecastCurve.Edged`; a gap in the days stays a gap; one day alone a line across), and the
  chart's box runs from its band's top to the card's foot, its wash under the highs down to the foot, the rain's figures
  standing over it. Before, the fill stood 30 DIP in from each side, its foot 32 over the card's with the rain under it,
  and one day drew no plot. Test `ForecastCurveTests.S16439_`.
- **[S-16440]** **No forecast keeps the face and says why** (WR-10: "the widget keeps its face (its graph, its rows ...) and
  says why in a compact line"): the style's days from today stay, "--" and the unknown sky, every range bar its track (a
  row's and a column's), the curve a flat line across the plot in the disabled role (`ForecastCurve.Unread`); why is the
  Status block's one row on the card's top edge ([S-6734], `OnlineParts.ShowState`: the sentence and Retry where it offers
  it), the face right under it in the box it leaves, where the card holds the row over a day's (one row tall, the problem
  glyph in the first day's lead says it, [S-16441]: never the status line alone); not set up is the card's Set up row
  alone. Before, the bars and
  their tracks vanished and only a tooltip said why. Test `ForecastCurveTests.S16440_`.
- **[S-16441]** **The rows are the same in every state** (WR-11: "Rows look like rows everywhere (same heights, type,
  padding ...)"): in the rows style the problem glyph (a forecast kept through a failed refresh, or a failure in a card
  too short for the state's row) stands in the first day's lead in place of its sky, its lines its tooltip; no row gives
  up room for it (the first row's alone was cut to "T…"; a room every row gave up made the rows end 37 DIP from the edge
  stale against 13 fresh). The other styles keep the kit's status over the top end. `ForecastDefaults.Problems`; test
  `ForecastDaysTests.S16441_`.
- **[S-16590]** **The temperature bars line up** (owner, 2026-11-23: "for the weather forecast widget the temperature bars
  should be aligned to each other"): the table's columns are every row's ([S-16956]): the values in tabular figures, their
  column the widest value's, the bar before it, the lead one width whatever it holds (the sky or the problem glyph); the row
  list's standing bars share their top and foot (each part above and under them one line on every column). Test
  `ItemBaseTests.S16956_`.
- **[S-16591]** **The days past the card are scrolled to** (owner, 2026-11-23: "the weather forecast lists are not scrollable
  although they should be"): in every style but the curve every fetched day (all 14) is laid at its one size and scrolled to
  by whole days, the item base's scroll ([S-16955], `ItemScroll`): the wheel and the touchpad a day (or a grid's line) a
  notch, the keys a day, a page, the first or the last; the last turn stands the last day at the far edge; the rows fade
  at the foot while more are below. The bars scale against every day laid, so a scroll never redraws them. The curve is
  the week's one chart and does not scroll. Tests `ItemScrollTests`.

- **[S-16974]** **The forecast on the item base** (owner, 2026-11-23: "the weather should be integrated into the same component
  too ... weather should not loose any features"): a day is an item (`ForecastItems.Item`) in every style but the curve:
  its sky the lead (the problem glyph in the first day's place where the forecast is stale, in the table and the lists,
  [S-16441]), the day the title (today strong), the range bar ([S-16949]: its empty track without figures, [S-16440]), and in
  the table and the lists the conditions under it and "low high" at its end, in the row list the high over the standing bar,
  the low under it and the rain at its foot, in the grid the high beside the low. Its page: the place, the precipitation
  switch, then on the base's styles **Each item shows** (Sky, Day, Conditions, Range, Low and high in order: the table's
  columns) and the gap or the table's column titles. A click on the card opens the forecast's page ([S-2036]); a day publishes
  and lights its day on the view ([S-11924]). Tests `ForecastItemsTests`, `WeatherForecastMenuTests.S16974_`.

Needs a live pass: the widget against the real service (1 and 14 days), offline with a restored shelf.
- View links: a day publishes and lights the day under the pointer and marks the day selected on the view ([S-11924], [S-11927]).
