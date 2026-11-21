# Stock chart (OFF-E)

The module `stockHistory` ([S-6911]): one instrument's price over its period as a chart. An atom
([S-11170]): with the price (`stock`) it makes the ready-made group "Stocks" ([S-11172]); it is what the
old card's Chart style drew under the price. Its tags are this file's; the price's `SPEC.md` and the
root's hold what they share (the instrument and its period [S-2778], the extended bars [S-1308], the
exchange's clock [S-1309], one baseline [S-2775], the menu's symbol search [S-1307], the page a click
opens [S-2039]).

Owning files: everything in this folder: `StockHistoryModule.cs` (descriptor, sizing, menu lines, JSON
context), `StockHistoryConfig.cs` (the SDK's `StockQuoteConfig` and the markers), `StockHistoryView.cs`
and `StockHistoryView.Chart.cs` (moved from the old card), `StockChartLabels.cs`, `Preview/` (`--state
quote|falling|stale`: the SDK's `SampleQuote`, the price's), `Localization/Locales/` (its own
`StockHistoryStr`: the markers' words moved here with their translations). Tests: `Tests/StockHistoryTests.cs`.

- **[S-11165]** **What it shows**: the period's bars as the Chart block's series ([S-6717]), the card's
  edge to edge, its top edge too (whose rounded corners clip it; [S-6754]): the line at 2 with its
  wash, the accent while the price rose or held, Critical while it fell ([S-6706]); the bars outside the
  regular session as the second series in the tertiary role ([S-1308]); the start and end times
  (Caption tertiary, the chart's ends) at the inset off the foot at every size, the trace kept clear of them
  (`ChartPlot.Frame`); the period's high and low marked where **High and low** is on ([S-1310]: a
  dot in the line's role and the price, Caption tertiary, kept inside the chart's text box,
  `ChartPlot.Across`). The pointer over the chart reads the bar under it (time · price, Caption on
  the solid pill at the text box's top). Nothing else at any size; no price axis. A chart that sits under its reading (a
  Header widget over the group names the instrument, [S-12481]); the status's problem glyph stands over
  its top end ([S-7042]). No
  quote, or fewer than two bars: the chart at its baseline with the state's sentence as its caption ([S-6753]).
  Its chart look ([S-6752]): Chart type Area (the default), Line or Candlestick (real candles from Yahoo's open, high
  and low; a coin's bucketed from its prices) and Detail Simple or Detailed ([S-6751]: the grid and its prices, a few
  times in place of the start and end, the high and the low marked whatever High and low says, the latest price's
  hairline and pill).
- **[S-11166]** **Sizes**: 200..480 wide (a new one 280), as tall as what the chart writes (the ends' line at the
  inset; the markers' lines where they are on, Detailed's room) and the trace's band, the one band sizing ([S-6755],
  `ChartSizing.Band`) 48..200 (a new one 72; until 2026-11-21 44..220, a new one 76): without the markers 80..232 (a new
  one 104); the markers or Detailed 44 taller (`StockHistoryModule.Sizing`, `Sized`): the chart only gets longer or
  taller, the trace never shorter.
- **[S-11167]** Its options are all on the properties menu ([S-3719]): **Symbol** (the line Set up
  opens), behind a divider **Timeframe**, **Extended hours** (where they exist) and **High and low** (the
  SDK's `StockQuoteLines`, the price's); the update interval is ⚙'s, followed, no link row ([S-3749]).

Needs a live pass: against the real services (a listed symbol through a session and its extended
hours, a coin), the pointer readout.
