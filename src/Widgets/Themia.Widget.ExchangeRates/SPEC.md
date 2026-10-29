# Exchange rates (ONLINE-2)

The module `exchangeRates` ([S-6911]): the ECB's reference rate of one pair. Its tags are this file's
alone; the root `SPEC.md` holds the host's sections and what the online widgets share (Air quality,
rain and exchange rates: the ECB document's wire [S-5141], the published codes [S-5142] and the cross
rates [S-5145] in the SDK's `ExchangeRates.cs`, the header's stale rule [S-5160], the offline shelf
[S-5161]; the Readout's fold of a change [S-6707] and the state row [S-6734]; the background web sync
[S-2663]).

Owning files: everything in this folder: `ExchangeRatesModule.cs` (descriptor, view factory, menu
lines, JSON context, its glyph), `ExchangeRatesConfig.cs` (with its styles), `ExchangeRatesView.cs` (it
reads the table through `IWidgetHost.Rates`), `RatesLayout.cs` (both styles' layouts), `RatesText.cs`,
`ExchangeRatesMenu.cs` (its properties-menu lines: base, currency and the source line), `Preview/` (Debug renders:
`--state sample|stale`, the latest day pinned), `Localization/Locales/` (its own `ExchangeRatesStr`, 30 languages, its style names
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
- **[S-5146]** **Number** (`RatesLayout.Rates`), on the family's blocks and read as a meter reads
  ([S-1762]): the pair as a Readout at the view's top, start-aligned, on the step it takes in the area
  (28 at XS and S, 40 at M, Wide and Tall, 68 at L and XL, never larger, [S-6702]): the rate, the
  quote's code after it and the day change (Body) beside it where they fit, else the code gives way,
  else the change is the line under the rate (`ReadoutRules.Fold`, [S-6707]); otherwise "per CHF" is
  that line. Room left is empty ([S-666]). A rise and an unchanged rate are secondary, a fall Critical
  (no green, [S-6706]). No table yet: the Status row at the top with the fetch's sentence ([S-6734]); the
  latest day lacking either code: the same row, "Not published". The header is titled with the pair
  ("Exchange rates · CHF/EUR") and reads "ECB, 9 Oct" (the rate's day, the header's informing label,
  the first part to give way), with the kit header's problem glyph beside it while the table on screen
  is one the last refresh could not improve on ([S-7042]). It needs no setup, so it has no Set up
  sentence ([S-5140]).
- **[S-5147]** One `RatesInterest` for all exchange-rate widgets; asked every three hours, no setting
  (`OnlineCadence.Rates`): the ECB publishes once per working day around 16:00 CET.
- **[S-5148]** Its options are all on the properties menu ([S-3719], user request 2026-10-19); it
  has no settings page. Appearance ([S-3723]), then **Base currency** (Windows region first, naming
  it) and **Currency** (the default first, naming it), each a search ([S-3722]) over the published
  codes that start with what is typed, the one that holds with the dot; then the source's credit as
  a note. (Tests `ExchangeRatesMenuTests`.)
- **[S-6167]** The exchange-rate widget's styles ([S-6000]), `ExchangeRatesConfig.Style`
  (`ExchangeRatesStyle`): **Chart** (the default and first, as for Stocks — user requirement: the
  rate shows with its graph) and **Number** ([S-5146]; what was **List** before one pair per widget,
  renamed since a list of one is a number). *(The list's rows and their
  small lines are retired with the list.)*
- **[S-6168]** **Chart** (`RatesLayout.Rates`): the pair as the readout (as [S-5146]), and its 90 days
  8 under it, edge to edge and down to the card's foot (the Chart block's band, [S-6717]: the accent,
  Critical while the rate falls, washed under the line); from 24 tall, else the readout alone. No strip
  beside the chart: the readout is above it at every size.

Needs a live pass: the widget against the real service (an ECB publication day and a weekend),
offline with a restored shelf, and the Windows region read (`GetUserDefaultGeoName`) in the packaged build.
