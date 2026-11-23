# Metric (metrics)

The module `metrics` ([S-6911]). Its tags are [S-16300]..[S-16399], this file's alone; the root `SPEC.md` holds the host's
sections.

Owner, 2026-11-23: "i want to create a metrics widget that should replace many of the current widgets. lets do a fresh from the
start implementation for it though. -metrics should bascially show a metric, optionally in time series format, into the future
or into the past. -the metric should have the name and an icon for the metric on the topleft and then the current value of the
metric on the topright -then you should be able to configure a graph for the metric as well over which you can also hover to
show the value on the topright of the graph. the graph should be shown on the bottom and should be the full width without space
and go all the way to the bottom without space. -you should be able to customize the graph type, candlestick, bar, line, area,
etc. -you should be able to select from many metrics, look at what data we are already displaying and basically make all of
that available. (daily temperature with min max bar chart for example, pollen forecast, uv forecast, cpu history, gpu history,
drive activity history, air quality) it should also support combinations like min max temperature daily or read write drive
activity. snow or rain forecast daily or hourly etc. try to keep it open and extendable. -it should scale to the size properly
but it should not change its layout drastically. -when the height is really small but the graph is shown (graph should be
optional) the graph should still be shown the full width but it should go under / behind the text. the text should nicely fade
over the graph and the graph should only be a little visible behind the text -the metric with no graph should actually be a
separate metric style altogether." It is written beside the single metrics (METRICS, the `*Facts` projects), which stay: the
owner decides what it replaces once he has seen it.

Owning files: everything in this folder. `MetricCatalog.cs` (the metrics as data), `MetricSeries.cs` (a series and the
adapters), `MetricGraphRules.cs` (the graph's geometry), `MetricFaceRules.cs` (the face's rules and the sizing),
`MetricFormat.cs`, `MetricsConfig.cs`, `MetricsMenu.cs`, `MetricsView*.cs` and `MetricsPlot.cs` (the face, written for it:
not on the metric, meter, atom, text or icon bases). Tests: `Tests/MetricsTests.cs` (run by `tests/Themia.Modules.Tests`) and
`tests/Themia.Core.Tests/MetricDataTests.cs` (the host data it added).

## The catalog

- **[S-16300]** **One widget, any metric.** The widget shows one metric: its name and glyph, its reading now, and in its graph
  style a graph of it back (history) or ahead (forecast). What it shows is its config; the same parts at every size ([S-11800]).
- **[S-16301]** **An open catalog** (`MetricCatalog`, pure data): a metric is an id (what the config stores), a subject (This PC,
  Weather, Air quality, Markets: the page's groups, in that order), a name, a glyph, a unit and its digits, what it needs set
  (`MetricNeed`: a place, a drive, an adapter, a host, a GPU, a symbol, a currency pair, or nothing) and one **feed** a step it
  has: its source (`MetricSource`: the hub's series, a place's hours, its days, its air hours, a quote, the rates), its step, its
  **shape** (one value; a pair: low and high, read and write, down and up, wind and gusts; a candle: open, high, low, close), its
  spans, how its slots fold and its default graph, and what of each record of the source is its value (and its second). Adding a
  metric is one entry, and an adapter only where its source is new; nothing in the view. The metrics: CPU, GPU, memory, drive
  activity (reads and writes, one drive or every drive), network (down and up, an adapter or all), ping, battery; temperature
  (hourly; daily its low and high), feels like, precipitation and snowfall (hourly and daily), chance of rain (hourly and daily),
  wind (hourly with its gusts; daily its strongest), humidity, pressure, UV (hourly and daily), cloud cover, sunshine; the air
  quality index (European and US), PM2.5, PM10, ozone, nitrogen dioxide, pollen (the worst type, or each of the six); a stock
  (candles) and an exchange rate.
- **[S-16302]** **Step, way and span.** A metric with two steps (temperature, precipitation, snowfall, chance, wind, UV: hourly
  and daily) is read at the step chosen; a feed looks back, ahead, or either way, each with its spans, the first the default:
  the hub five minutes back (one, fifteen, thirty minutes and an hour, all its history holds); a place's hours a day ahead (twelve
  hours or two days; half a day or a day back); its days a week ahead (or two); its air a day ahead (two or four days; a day
  back); a quote a day back (five days, a month, three, six, a year); a rate a month back (or ninety days). A stored step, way or
  span the metric has not is its first, loudly (`MetricsConfig.Repair`). Choosing a metric, or its step, starts it at its first
  span and its default graph.
- **[S-16303]** **Its figures** through the host's formats (`MetricFormat`): a share as a percent, a rate in bytes a second, a
  latency, a temperature as whole degrees in the shared scale, an amount with its unit, a price with its currency, a rate at the
  digits its size needs; a pair both ("8° / 16°"); none "--". The time a slot stands for: the hub's second, an hour's clock time,
  a day's name ("Today", "Tue"), a quote's bar its time within a day and its date over more.
- **[S-16304]** **Its graph types**, those its shape draws: one value Area, Line, Bars or Steps; a pair Line (the second the
  tertiary text's colour), Area, Low to high (a bar a slot from the low to the high) or Bars (the two side by side); a candle
  Candles, Line or Area of its close. A stored type the shape does not draw is the metric's default, loudly. Defaults: the PC's
  shares an area, its rates and latency lines, temperatures and pressure lines, amounts and chances bars, a day's temperatures
  low to high, a stock candles, a rate a line.

## The data

- **[S-16307]** **It reads only what its metric needs**, and shares what it reads: a system metric watches the hub's one series
  (`IReadsSystem`: the hub keeps it for every view that holds it); a weather metric the place's hours (one answer a place, asked
  in °C) or its fourteen days (one answer a place for every span); an air metric the place's air hours; a stock its quote over
  its span's range; a rate the ECB's one table. Its interests (`IInterests`) name that one source and nothing for any other;
  without a place or a symbol it asks for nothing. Never fetched from the view: it tells the sync what is shown and hears its
  answer; it redraws on each answer, each hub read, and every five minutes so the hour under way moves on.
- **[S-16310]** **A series** is a slot a step (oldest first, a slot without a reading a gap, never a 0), the reading now, and
  where its slots are not a step apart their own times (a quote's bars, the ECB's working days).
- **[S-16311]** **The adapters**, pure: the hub's newest points that fit the span right-aligned, now at the right edge (a
  history still filling grows in from it), a missed probe a gap, a share a fraction, a drive's reads its value and its writes
  its second; a place's hours on whole hours from the hour under way (a forecast) or up to it (history), its reading the hour
  under way's, a temperature read from the hours drawn in the shared scale; the days from the place's today, a pair its low and
  high; a quote a candle a bar (its own open, high, low and close where the service sends them, else its close), its last
  price now; a rate the working days within its span back from the newest. No answer yet is the span's empty slots: the graph
  still stands (WR-10).
- **[S-16330]** **The hours carry snow, cloud and sunshine**: the forecast's hourly request asks `snowfall` (cm), `cloud_cover`
  (%) and `sunshine_duration` (kept as minutes of the hour) beside its figures, each in its range or null; its daily request
  asks `snowfall_sum` (cm), kept with the day (and in the offline store).
- **[S-16331]** **The air hours carry the pollen forecast**: the air quality's hourly request asks the six pollen types; an hour
  keeps the types the reply has a number for (none outside Europe), never a 0 for one it has not, and an hour with pollen alone
  is an hour.
- **[S-16332]** **A day back**: both hourly requests ask the 24 hours before the hour under way (`past_hours=24`,
  `WeatherHoursWire.PastHours`), so a history of a place's weather or air is the reply's own; the graphs that look ahead start at
  the hour under way, as before. The sample hours reach a day back too, the hour under way reading as it did.
- **[S-16333]** **A drive's reads and writes apart**: a disk series whose instance starts `rw|` (`DiskScopes.Apart`) charts the
  drive's (or every drive's) reads as its value and its writes as its second, a ring of its own; the plain series still charts
  their total.

## The face

- **[S-16320]** **Two styles** (`MetricsStyle`): **Graph** (the default) and **Value**, a design of its own, not the graph with
  its graph hidden ([S-16323]).
- **[S-16321]** **The graph face's head**: the glyph and the name (Body, secondary, cut by its ellipsis) top left and the reading
  (Title) top right, on one baseline; one head scaled with the card (its scale the card's against 240 × 120, the smaller axis,
  up to 2.5: owner, "it should scale to the size properly but it should not change its layout drastically"; down to where its
  smallest text, the name, is the 9 DIP the family reads to, WR-14, `ScaleFloor.Least`, its glyph held at 12 under that,
  `MetricFaceRules.GlyphSize`), its first ink on the card's inset (WR-1), its sides on the insets. The name keeps its room
  first (WR-7): the reading is a Title line where the name, whole, and the metric's widest reading ([S-16324]) fit side by side,
  else a BodyStrong line on the name's baseline (`MetricFaceRules.ReadingStep`, decided on the widest reading so a running
  value never changes it); what still does not fit is cut with its ellipsis, the name first. Under it, a small gap down, the plot: the card's whole width, down to its foot
  ([S-6754]: "Plot edge to edge").
- **[S-16322]** **Behind the head on a low card**: where the head leaves the plot less than 24 DIP (`MetricFaceRules.LeastPlot`),
  the plot is the card's whole box behind the head, drawn faint (a third, `BehindShare`; owner: "the graph should only be a
  little visible behind the text"), the head over it, in the middle of a card too low for its inset over and under it. The same
  head, the same graph: only where the plot stands. The rule is `MetricFaceRules.Graph`, tested on its threshold.
- **[S-16323]** **The value face**: the glyph and the name (Body, secondary) over the reading (Big28), one block scaled as one
  until the width or the height stops it (WR-7), never under 0.65 (its caption at 9.1, WR-14), its glyph held at 12 DIP however
  far it scales down; its ink on the insets of the axis that stops it, centred on the
  other ("Centred is fine"). No graph, no hover.
- **[S-16324]** **A running reading keeps its block** (WR-15, as the clock): the reading stands in a held box, the metric's widest
  reading (`MetricFormat.Widest`: its largest form, every figure an 8: "888%", "-88° / -88°", "888 / 888 MB/s") drawn unseen
  under it (`HoldMark`); the block is laid out by that box, so "31%", "100%" and "--" stand in one place at one scale.
- **[S-16325]** **Its sizes** (`MetricFaceRules.Sizing`): the graph 180 × 32 at least (WR-14: as low as the grid's least widget,
  the head at its least scale over the graph behind it, its text at 9 DIP; as narrow as a short name and its widest reading still
  read whole side by side, a long name cut meaningfully; was 160 × 40, its head stopped at 0.75), 240 × 120 natural; the value
  style as wide at least as the catalog's widest reading (a pair's, "888 / 888 MB/s") needs at its least scale, one least for
  every metric (`MetricFaceRules.WidestReading`); the value 140 × 60 at least, 200 × 90 natural; relaxed by the host ([S-12411]).
  A style switched refits the card.
- **[S-16327]** **No answer, or a stale one, keeps the face** (WR-10, owner: "it should show the default values in the graph and not
  just not show the graph"): no answer is the graph's default across its foot ([S-16314]), the reading "--" (disabled) and why
  in the status's compact mark (the kit's problem glyph with the reason: offline, refused, the hub's own reason for no reading;
  its busy ring while the first answer is on its way); an answer kept through a failed refresh keeps its graph and its reading
  and says so with the mark. Without a reading the mark carries a compact line of why beside its glyph (`KitStatus.Say`: the
  first problem, "Offline", cut at a third of the card's width in the graph style, half in the value style). The reading stays
  clear of the mark in the head. The same in the value style.
- **[S-16326]** **Hover**: the pointer over the graph puts the value of the slot under it (a gap's nearest reading) and its time
  in the head's reading's place, and marks it: a hairline down the plot and a dot on each of its values. Nothing changes size
  (WR-12); off the graph the reading again. `--render widget metrics … --hover fx,fy` draws it (the plot's look at a point,
  [S-16341]).

## The graph

- **[S-16312]** **Folding**: a plot draws a line's point every 2 DIP at least and a mark (a bar, a range, a candle) every 6; a
  series with more slots folds runs of neighbours into one, fixed from the newest (the newest run stands at the right), a run's
  value its mean, its peak (a rate, a latency: a burst stays seen) or its sum (an amount), a candle the run's open, high, low and
  close; a run of gaps a gap. A quote's bars that are closes alone (each candle a point, a dash) fold at least four to a candle
  (`MetricGraphRules.MostSlots`), so the candles have bodies and wicks at every size (WR-11); real candles as many as fit.
- **[S-16313]** **The range**: from zero for an amount or a share (a share's top 100 %), from its own low for a temperature, a
  pressure or a price, with a little headroom; a flat series stands in the middle of a range round it; none at all 0..1.
- **[S-16314]** **Edge to edge**: a line's first point on the plot's left edge and its last on the right; bars, ranges and
  candles from the left edge to the right with a gap of 2 between each two (a column at most 24 wide, a range or a candle 12, the
  rest evenly between), never room at the plot's sides; area and bars stand on the card's foot. The series in the accent, a
  pair's second in the tertiary text's colour, a falling candle Critical, the wash the family's. Wherever the span has a slot
  without a reading (none yet, a history still filling, a gap) the graph draws its default values under the readings (WR-10,
  owner: "even if there is no cpu data available it should show the default values in the graph", `MetricGraphRules.DrawsDefault`):
  bars, ranges and candles an empty track (the track's colour, plot top to foot) for each such slot; a line, area or step its
  zero along the foot in the disabled text's colour, and with no reading at all its plot one empty track under it.
- **[S-16315]** **Down and under the pointer**: a value's height on its range from the plot's top to its foot (clamped); the slot
  under the pointer a line's nearest point or a mark's nearest middle.
- **[S-16316]** The plot is the card's whole box (`PlotMark`): the gap check holds it to the card's edges. The gap check judges
  the widget as a graph in its graph style (`GRAPH_STYLES`: a graph style drawn with no plot is "WR-10 no plot") and as a
  reading in its value style; the checks draw every state in both styles (`renders.EVERY_STATE_STYLES`).

## The page

- **[S-16305]** **Its config** (`MetricsConfig`): the metric, its step, way and span, the graph's type, the style, and what each
  source reads (the place, the drive, the network adapter, the ping host, the GPU, the symbol, the currency pair), each kept while
  another metric is shown; copied whole by `Clone`.
- **[S-16306]** **What it needs set**: it waits for setup (the card's Set up line) only for what its metric needs and has not: a
  place for a weather or air metric, a symbol for a stock.
- **[S-16308]** **The page** ([S-3719]): Subject, then Metric (the subject's metrics; a subject of more than eight found by
  typing, [S-3537]; picking a subject shows its first metric), then what the metric reads from (the place's lines, the drive's,
  the adapter's, the host's, the GPU's (with two or more), the price widgets' symbol search, the exchange rate's currencies),
  Resolution where it has two steps, Shows (History or Forecast) where its step looks both ways, Span where it offers more than
  one; under the Style line the graph's type, only those its shape draws (none in the value style). The source's credit last.
- **[S-16340]** **A shared setting only where it applies** (`IOffersShared`, the SDK's): the config holds the place, the drive,
  the adapter, the host or the GPU (as a group's page counts them, [S-11751]) only while its metric reads it, so a group's page
  sets a place on its weather metrics alone and the CPU's page shows none. The symbol and the pair are the widget's own lines,
  unshared.
- **[S-16341]** **A look at a point** (`PointerPreview.Register` with a point, the SDK's): a part whose look under the pointer
  depends on where the pointer is in it (the graph's value) registers that look; a render's `--hover` hands it the point in its
  own DIPs.
- **[S-16309]** **Its sample** (the add page's picture): the sample day's data for the metric's source (the place's hours and
  days, its air, MSFT's session, the ECB's table) at the sample moment, held by the view alone; a system metric reads the sample
  PC its host hands it. Debug states: `sample`; `stale` (the last graph and reading with the status's problem glyph) and `empty`
  (no answer: the graph's default, "--" and why), a system metric's from a made-up series of its own (the live hub has neither);
  and a state a graph type and shape, so the checks draw each: `line` (the temperature hourly), `bars` (UV), `step`
  (pressure), `range` (the days' low and high), `candles` (MSFT), `pair` (a drive's reads and writes).

Needs a manual pass: its min, natural and max on the live desktop (showcase views, [S-1810]); live hover with a real pointer.
