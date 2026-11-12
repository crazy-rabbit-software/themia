# Exchange rate (ONLINE-2)

The module `exchangeRate` ([S-6911]): the ECB's reference rate of one pair as a **reading**. An atom
([S-11170]..[S-11172]): its 90 days are the `exchangeHistory` atom, the old Exchange rates card (id
`exchangeRates`, gone) the ready-made group "Exchange rates": its Number style is this atom, its Chart
style the group. Its tags are this file's
alone; the root `SPEC.md` holds the host's sections and what the online widgets share (Air quality,
rain and exchange rates: the ECB document's wire [S-5141], the published codes [S-5142] and the cross
rates [S-5145] in the SDK's `ExchangeRates.cs`, the header's stale rule [S-5160], the offline shelf
[S-5161]; the Readout's fold of a change [S-6707] and the state row [S-6734]; the background web sync
[S-2663]).

Owning files: everything in this folder: `ExchangeRateModule.cs` (descriptor, view factory, menu
lines, JSON context, its glyph), `ExchangeRateConfig.cs` (over the SDK's `ExchangePairConfig`, which the history shares), `ExchangeRateView.cs` (it
reads the table through `IWidgetHost.Rates`), `RatesLayout.cs` (its layout and size), `RatesText.cs`; its menu lines are the SDK's `ExchangePairLines`
(base, currency and the source line, with their words in the SDK's `rates` area); `Preview/` (Debug
renders: `--state sample|stale`, the SDK's `SampleRates`), `Localization/Locales/` (its own `ExchangeRateStr`). Tests: `Tests/ExchangeRateTests.cs` (run by `tests/Themia.Modules.Tests`); the document's
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
- **[S-5146]** The reading (`RatesLayout`), on the family's blocks and read as a meter reads
  ([S-1762]): the pair as a Readout on the family's 28 at the content's top ([S-2449]), start-aligned
  (or where the widget's alignment puts it, [S-6582]): the rate, the quote's code after it and the day
  change (Body) beside it where they fit the card's width, else the code gives way, else the change is
  the line under the rate (`ReadoutRules.Fold`, [S-6707]); otherwise "per CHF" is that line. A rise and
  an unchanged rate are secondary, a fall Critical (no green, [S-6706]). No table yet: the Status row at
  the top with the fetch's sentence ([S-6734]); the latest day lacking either code: the same row, "Not
  published". The header is titled with the pair ("Exchange rates · CHF/EUR") and reads "ECB, 9 Oct"
  (the rate's day, the header's informing label, the first part to give way), with the kit header's
  problem glyph beside it while the table on screen is one the last refresh could not improve on
  ([S-7042]). It needs no setup ([S-5140]).
- **[S-11150]** **An atom**: the reading alone, the same at every size in its range; no styles (the old
  Number style is this atom, the Chart style the ready-made group "Exchange rates", [S-11172]).
- **[S-11151]** **Sizes** (`RatesLayout.Sizing`, with its header): one height (100), 160..320 wide, a
  new one 200.
- **[S-5147]** One `RatesInterest` for all exchange-rate widgets; asked every three hours, no setting
  (`OnlineCadence.Rates`): the ECB publishes once per working day around 16:00 CET.
- **[S-5148]** Its options are all on the properties menu ([S-3719], user request 2026-10-19; the SDK's
  `ExchangePairLines`, the history's too); it has no settings page. Appearance ([S-3723]), then **Base currency** (Windows region first, naming
  it) and **Currency** (the default first, naming it), each a search ([S-3722]) over the published
  codes that start with what is typed, the one that holds with the dot; then the source's credit as
  a note. (Tests `ExchangeRateMenuTests`.)
- **[S-6167]**, **[S-6168]** *(Retired by [S-11150]: no styles; the 90 days are the `exchangeHistory` atom, [S-11155].)*

Needs a live pass: the widget against the real service (an ECB publication day and a weekend),
offline with a restored shelf, and the Windows region read (`GetUserDefaultGeoName`) in the packaged build.
