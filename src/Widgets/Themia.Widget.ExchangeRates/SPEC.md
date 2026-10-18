# Exchange rates (ONLINE-2)

The module `exchangeRates` ([S-6911]): the ECB's reference rate of one pair. Its tags are this file's
alone; the root `SPEC.md` holds the host's sections and what the online widgets share (Air quality,
rain and exchange rates: the ECB document's wire [S-5141], the published codes [S-5142] and the cross
rates [S-5145] in the SDK's `ExchangeRates.cs`, the header's age line [S-5160], the offline shelf
[S-5161]; the background web sync [S-2663]).

Owning files: everything in this folder: `ExchangeRatesModule.cs` (descriptor, view factory, settings
rows, JSON context, its glyph), `ExchangeRatesConfig.cs` (with its styles), `ExchangeRatesView.cs` (it
reads the table through `IWidgetHost.Rates`), `RatesLayout.cs` (both styles' ladders), `RatesText.cs`,
`ExchangeRatesSettings.cs` (its page: base, currency and the source line), `Preview/` (Debug renders:
`--state sample`), `Localization/Locales/` (its own `ExchangeRatesStr`, 30 languages, its style names
included). Tests: `Tests/ExchangeRatesTests.cs` (run by `tests/Themia.Modules.Tests`); the document's
and the cross rates' own tests stay Core's (`OnlineWidgetsTests`, `S5141_`, `S5143_`, `S5145_`).

- **[S-5140]** The ECB's euro reference rate of **one pair** (user decision; several pairs are several
  widgets): what one unit of the base buys of the quote. Works out of the box (no setup).
- **[S-5143]** The base is empty by default, meaning the Windows region's currency
  (`GetUserDefaultGeoName` → `RegionCurrency`, read with the regional format on every state change):
  CHF for Switzerland and Liechtenstein, USD for the US, the euro for the euro area and anywhere
  unmapped. The quote is empty by default, meaning the euro, or the dollar for a euro base
  (`ExchangeRules.DefaultQuote`); a quote that is the base itself is that default too
  (`EffectiveQuote`).
- **[S-5144]** Repair at load: a base or quote that is not a published code follows its default,
  said; a published one is upper-cased.
- **[S-5146]** **Number** (`RatesLayout.Rates`): the pair as the readout (rate, the quote's code,
  the day change beside it or under it, "per CHF") on the whole tile, from Subtitle as large as the
  tile allows (past Display, as a lone reading, [S-1762]); the Chart style's stops at the tier's step ([S-3860]). Up is the success colour, down critical. No table yet, or the latest day lacking
  either code: the empty state ("Not published"). The header is titled with the pair ("Exchange
  rates · CHF/EUR") and reads "ECB, 9 Oct" (the rate's day) in the caution colour when the last
  refresh failed or the table was restored.
- **[S-5147]** One `RatesInterest` for all exchange-rate widgets; asked every three hours, no setting
  (`OnlineCadence.Rates`): the ECB publishes once per working day around 16:00 CET.
- **[S-5148]** Settings: Base currency (Windows region first, naming it), Currency (the default
  first, naming it, then the published codes), and the source line.
- **[S-6167]** The exchange-rate widget's styles ([S-6000]), `ExchangeRatesConfig.Style`
  (`ExchangeRatesStyle`): **Chart** (the default and first, as for Stocks — user requirement: the
  rate shows with its graph) and **Number** ([S-5146]; what was **List** before one pair per widget,
  renamed since a list of one is a number). *(The list's rows and their
  small lines are retired with the list.)*
- **[S-6168]** **Chart** (`RatesLayout.Rates`): the pair as the readout (as [S-5146]), and its 90
  days filled under the line in what the readout leaves (it takes 40 % of the spare height), edge to
  edge and down to the card's bottom; a strip (≥ 400 wide, 2:1) puts the readout in a column on the
  left (42 % of the width, 150..280) beside a chart of the strip's height. Where the chart would
  have under 56, the readout alone.

Needs a live pass: the widget against the real service (an ECB publication day and a weekend),
offline with a restored shelf, and the Windows region read (`GetUserDefaultGeoName`) in the packaged build.
