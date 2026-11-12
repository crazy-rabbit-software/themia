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

- **[S-11165]** **What it shows**: the period's bars as the Chart block's series ([S-6717]), edge to edge
  and down to the card's foot (whose rounded corners clip it) under the inset: the line at 2 with its
  wash, the accent while the price rose or held, Critical while it fell ([S-6706]); the bars outside the
  regular session as the second series in the tertiary role ([S-1308]); the start and end times
  (Caption tertiary) inside its bottom band at every size, the trace kept clear of them
  (`StockChartLabels.Band`); the period's high and low marked where **High and low** is on ([S-1310]: a
  dot in the line's role and the price, Caption tertiary, kept inside the chart's width,
  `StockChartLabels.Place`). The pointer over the chart reads the bar under it (time · price, Caption on
  the solid pill). Nothing else at any size; no price axis. It starts headerless (a chart that sits
  under its reading); a header it is given names the instrument, with the problem glyph ([S-7042]). No
  quote, or fewer than two bars: the Status row ([S-6734]).
- **[S-11166]** **Sizes** (headerless): 200..480 wide (a new one 280), the chart's band 64..240 tall (a
  new one 96) under the 12 inset: the chart only gets longer or taller.
- **[S-11167]** Its options are all on the properties menu ([S-3719]): **Symbol** (the line Set up
  opens), behind a divider **Timeframe**, **Extended hours** (where they exist) and **High and low**,
  behind another the **Update interval** link (the SDK's `StockQuoteLines`, the price's).

Needs a live pass: against the real services (a listed symbol through a session and its extended
hours, a coin), the pointer readout.
