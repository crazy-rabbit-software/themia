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
  table the rate reads, so the two agree) as the Chart block's series ([S-6717]), in the card's inset on
  every side: the line and its wash in the accent, Critical while the latest day fell ([S-6706]); time
  left to right in every language. Nothing else at any size. It starts headerless (a chart that sits
  under its reading); a header it is given names the pair ("Rate history · CHF/EUR") with the kit
  header's problem glyph ([S-7042]). No table: the Status row ([S-6734]); a pair not published or of
  fewer than two days: "Not published".
- **[S-11156]** **Sizes** (headerless): 200..480 wide (a new one 280), the chart's band 48..200 tall (a
  new one 72) in the inset: the chart only gets longer or taller.
- **[S-11157]** Its options are all on the properties menu ([S-3719]): the pair, as the rate's
  ([S-5148], `ExchangePairLines`).

Needs a live pass: against the real service, a header given to it.
