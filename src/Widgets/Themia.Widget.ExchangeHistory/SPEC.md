# Exchange history (ONLINE-2)

The module `exchangeHistory` ([S-6911]): one exchange rate's last 90 days as a chart. An atom
([S-11170]): with the reading (`exchangeRate`) it makes the ready-made group "Exchange rates"
([S-11172]); it is what the old card's Chart style drew under the rate. Its tags are this file's; the
rate's `SPEC.md` and the root's hold what they share (the pair [S-5143], [S-5144], the table and its
cadence [S-5147], the menu [S-5148], the cross rates [S-5145]).

Owning files: everything in this folder: `ExchangeHistoryModule.cs` (descriptor, sizing, menu lines,
JSON context), `ExchangeHistoryConfig.cs` (the SDK's `ExchangePairConfig`), `ExchangeHistoryView.cs`,
`Preview/` (`--state sample|stale`: the SDK's `SampleRates`, the rate's), `Localization/Locales/` (its
own `ExchangeHistoryStr`). Tests: `Tests/ExchangeHistoryTests.cs`.

- **[S-11155]** **What it shows**: the pair's 90 days (`ExchangeRules.Cross`, the history of the
  table the rate reads, so the two agree) as the Chart block's series ([S-6717]), a history band edge to
  edge at the sides and the foot under the card's top inset, as the stock's ([S-11002]): the line and its wash in the accent, Critical while the latest day fell ([S-6706]); time
  left to right in every language. Nothing else at any size. A chart that sits under its reading (a
  Header widget over the group names the pair, [S-12481]); the status's problem glyph stands over its
  top end ([S-7042]). No table, a pair not published or of
  fewer than two days: the chart at its baseline with the sentence ("Not published", the answer's state) as its caption
  ([S-6753]). Its chart look ([S-6752]): Chart type Area (the default), Line or Candlestick (the days bucketed into
  periods) and Detail Simple or Detailed ([S-6751]: the grid and its rates, dates along the foot, the high, the low and
  the latest marked).
- **[S-11156]** **Sizes**: 200..480 wide (a new one 280), the chart's band 48..200 tall (a
  new one 72) under the top inset, edge to edge (until 2026-11-12 it stood in the inset on every side,
  unlike the stock's): the chart only gets longer or taller.
- **[S-11157]** Its options are all on the properties menu ([S-3719]): the pair, as the rate's
  ([S-5148], `ExchangePairLines`).

Needs a live pass: against the real service.
