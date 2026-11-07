# Stocks (OFF-E)

The module `stocks` ([S-6911]): one instrument's price, its move and the shape of the period. Its tags
are this file's alone; the root `SPEC.md` holds the host's sections and what the host and the widget
share (Feed, stocks and the web widgets: the stored quotes [S-1300], the exchange's clock [S-1309],
one baseline for the colour, the change and the line [S-2775], in the SDK's `MarketQuote`,
`TickerQuote` and `ExchangeTime`; the family's blocks it is drawn with, the Readout's fold of a change
[S-6707] and the online widgets' state row [S-6734]; the background web sync [S-2663]; the market
cadence [S-3780]).

Owning files: everything in this folder: `StocksModule.cs` (descriptor, view factory, menu lines,
JSON context), `StocksConfig.cs` (with its styles and the rows only one style offers),
`StocksView.cs` and `StocksView.Chart.cs` (the quote through `IWidgetHost.Quotes`), `StockLayout.cs`
(both styles' layouts), `StockChartLabels.cs` (the chart's captions), `StockHolding.cs` (shares owned),
`StocksMenu.cs` (its properties-menu lines: the symbol search through the SDK's `SymbolLookup`, the
holding and the period), `Preview/` (Debug renders: `--state quote|falling|stale`, the bars from a pinned
moment), `Localization/Locales/` (its
own `StocksStr`, 30 languages, its style names included). Tests: `Tests/` (run by
`tests/Themia.Modules.Tests`); the quote rules' own tests stay Core's (`StockWidgetTests`, `S1307_`,
`S1308_`, `S1309_`, `S1310_`, `S2775_`, and `MarketQuoteTests`).

- **[S-1307]** Stocks symbol search on the properties menu (classic `SymbolSearch.tsx`, S-38): the
  Symbol line's search ([S-3722]) asks Yahoo `v1/finance/search` and CoinGecko `search` together after a 300 ms
  typing pause (the previous search abandoned), keeps only chartable kinds (EQUITY, ETF, MUTUALFUND,
  INDEX, CURRENCY, FUTURE), at most 8 per service, alternating, 12 in all; each line says name,
  symbol, exchange and class. A pick sets symbol, market and name; typed text that is a plausible symbol
  is offered first as "Use “…”", a listed symbol ([S-3790]); text no symbol has is said to be one in
  place of the answers, never stripped. The chosen symbol is at the line's end. A failing service
  contributes nothing (logged); when neither answers (blocked where the user is, offline: issue 187)
  a note under the typed symbol says why (`MarketSearch.Unanswered`), never "no matches".
- **[S-1308]** Extended hours (classic S-34/S-35, `ShowExtendedHours`, off): listed symbols on the
  1-day and 5-day charts ask `includePrePost=true` (part of the cache key). The last pre-market /
  after-hours bar against the regular close is a fact row under the price (the name "After hours ·
  16:42", the value "412.10 −0.36%", primary): pre-market before the bell, after hours after it, the
  latest of the two while closed; the session is then not said again under the price. The chart's bars
  outside the regular session are its second series, in the tertiary role ([S-6717]); the regular
  session is the line and its wash. Timestamps are kept paired with closes (nulls drop both halves;
  out-of-order series are sorted).
- **[S-1310]** Chart options: **High and low** markers (a dot in the line's role and the price, an
  axis label in Caption tertiary, at the period's first highest and lowest bar, the price above the
  high and under the low, kept inside the chart's width, `StockChartLabels.Place`); the area under the
  line is always washed ([S-6717]; no setting, [S-3795]). The colours are roles, not settings (family
  decision 2026-10-29, [S-6706]): a rise and an unchanged price are secondary (the change) and the
  accent (the chart), a fall is Critical in both; no green. The former Rising and Falling colour
  settings are gone (a stored `upColor`/`downColor` is an unknown key, ignored).
- **[S-1311]** Stocks layout (`StockLayout.Plan`), on the family's blocks and read as a meter reads
  ([S-1762]): the price is a Readout at the view's top, start-aligned, on the step it takes in the area
  (28 at XS and S, 40 at M, Wide and Tall, 68 at L and XL, never larger, [S-6702]); the currency after
  it and the signed change (Body, [S-1310]'s role) beside it where they fit, else the currency gives
  way, else the change is the line under the price (`ReadoutRules.Fold`, [S-6707]); otherwise the line
  under says the market's session ("Market closed", "After hours", "Pre-market"; [S-1309]: its tooltip
  names the exchange's zone). The holding ([S-2777]) and the extended print ([S-1308]) are fact rows 8
  under it; the chart style's period runs 8 under them, edge to edge and down to the card's foot (the
  card's corners clip it, [S-215]); the chart wins over the rows: a row shows only while at least 24 of
  chart stays under it. Its times are the start and the end of the range only, Caption tertiary, from a
  regular height (an area of 128), inside the chart's bottom band at the text's inset; the trace keeps
  clear of them and of the markers' prices (`StockChartLabels.Band`), the wash does not. The markers
  from a chart of 48. No price axis (user decision 2026-10-11). No strip beside the chart: the readout
  is above it at every size. Pointer over the chart on an expanded width reads the bar under it (time
  · price, Caption on the solid pill). The session is told every minute on the shared ticker (render
  only). **Refresh** in the card menu, with the quote's age; a failed refresh under the price on screen
  is the header's problem glyph ([S-7042]). No quote: the Status row at the top ([S-6734]): "“X” is not
  a symbol" for a refused symbol, "No quote for that symbol" for an empty answer, else the fetch's own
  sentence. Header title: name, else the service's own name (listed), else the symbol.
- **[S-2770]** Stocks is concise ([S-666], user decision): the name, the price, its change, the
  session, the holding ([S-2777], only when set), the extended-hours print ([S-1308], off by default)
  and the chart, nothing else at any size; room left at L and XL is the chart's, never a bigger number.
  No facts (previous close, day and 52-week range, volume, exchange; `MarketQuote.Facts`/`Volume` are
  gone) and no range chips on the widget: the period is the **Timeframe** line on its properties menu
  (`Range`, default 1 day, [S-2778]). Those facts are not parsed or cached either (`QuoteStats` is gone;
  the previous close is read only as the change's baseline, [S-2775]).
- **[S-2778]** Its options are all on the properties menu ([S-3719], user request 2026-10-19); it
  has no settings page. Under Style the chart's **High and low** markers (the chart style only,
  [S-6074]); then Appearance ([S-3723]), what it quotes: **Symbol** (the search of [S-1307]; the line
  Set up opens: until a symbol is chosen the card's Set up row says "Choose a symbol", [S-1862]) and
  **Shares owned** ([S-2776]; the amount at the line's end); after a divider the time it covers:
  **Timeframe** (1 day … 10 years), **Dim when closed** (not for a coin, which never closes) and
  **Extended hours** (only a listed symbol over one or five days, [S-1308]: elsewhere there are no such
  bars to ask for, [S-6070]); after another a link to **Update interval** (⚙ › Online updates ›
  Markets, [S-3749], [S-3590]: never a cadence of the widget's own). (Tests `StocksMenuTests`.)
- **[S-6165]** The stock widget's styles ([S-6000]), `StocksConfig.Style` (`StocksStyle`): **Chart**
  ([S-1311], the default) and **Number**; what was its "Show chart" switch. The period is every
  style's, the markers the chart's ([S-2778]).
- **[S-6166]** **Number** (`StockLayout.Plan` without a chart): no chart at any size; the price as the
  chart style draws it ([S-1311], the same step, never larger), its rows (the holding, the extended
  print) under it in whole rows; room left is empty ([S-666]). The only style that aligns ([S-6581]):
  the price moves across the card, its rows keep the width.
- **[S-2776]** **Shares owned** (`StocksConfig.SharesOwned`, per widget, a line on its properties menu
  asked beside it, [S-3721]; user request: "put in an amount that you own"). Typed in the language's decimals: one
  separator, the language's, or a point where a point is not the language's grouping (German
  refuses "1.000" rather than guess a thousand or one); no sign, no grouping, no exponent, at most
  10^12; kept to 8 decimals (`StockHolding.TryParse`). Anything else is refused as it is typed
  with an example in the language ("such as 12,5"), the last good value kept. Empty or 0 is no
  holding (null, not shown). At load a negative, non-finite or too-large amount is repaired to none
  and logged; 0 to none silently. The worth is amount × price in the quote's currency, never
  converted, to the cent; a price in hundredths is counted in the whole currency (GBp/GBX → GBP,
  ZAc → ZAR, ILA → ILS) (`StockHolding.Worth`). Its move is amount × (price − the change's baseline,
  [S-2775]), so its sign is always the percent's: the day's move on the default 1-day period
  (`StockHolding.Change`).
- **[S-2777]** The holding is a fact row under the price ([S-1311]): the amount held as its name ("10
  held", secondary) and its worth with its code as its value (primary), the signed move added from 300
  wide ("4,202.70 USD +82.95", `StockHolding.Held`, `Value`); before the extended print, shown only
  while the chart keeps its 24 under it (so not on XS or S). Numbers through `LocaleFormat`.
- **[S-2039]** A click on the card ([S-2000]) opens the symbol's own page: Yahoo Finance's quote
  (`https://finance.yahoo.com/quote/<symbol>/`) for a listed symbol, CoinGecko's coin page
  (`https://www.coingecko.com/en/coins/<id>`) for a coin; nothing with no symbol
  (`StocksModule.Opens`).

Needs a live pass: the widget against the real services (a listed symbol through a session and its
extended hours, a coin), the symbol search, offline with a restored shelf.
