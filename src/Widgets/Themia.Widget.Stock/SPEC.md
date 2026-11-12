# Stock (OFF-E)

The module `stock` ([S-6911]): one instrument's **price** as a reading, its move and its session. An
atom ([S-11170]..[S-11172]): its chart is the `stockHistory` atom, the old Stocks card (id `stocks`,
gone) the ready-made group "Stocks": its Number style is this atom, its Chart style the group. Its tags
are this file's alone; the root `SPEC.md` holds the host's sections and what the host and the widget
share (Feed, stocks and the web widgets: the stored quotes [S-1300], the exchange's clock [S-1309],
one baseline for the colour, the change and the line [S-2775], in the SDK's `MarketQuote`,
`TickerQuote` and `ExchangeTime`; the family's blocks it is drawn with, the Readout's fold of a change
[S-6707] and the online widgets' state row [S-6734]; the background web sync [S-2663]; the market
cadence [S-3780]).

Owning files: everything in this folder: `StockModule.cs` (descriptor, view factory, menu lines,
JSON context), `StockConfig.cs` (over the SDK's `StockQuoteConfig`: the instrument and its period, which
the chart shares), `StockView.cs` (the quote through `IWidgetHost.Quotes`, the SDK's `SkyFeed`),
`StockLayout.cs` (its layout and size), `StockHolding.cs` (shares owned), `StockMenu.cs` (its lines: the
SDK's `StockQuoteLines` for the symbol, the period and the link, the holding and the closed market its
own), `Preview/` (`--state quote|falling|stale`: the SDK's `SampleQuote`), `Localization/Locales/` (its
own `StockStr`; the symbol's, the period's and the states' words moved to the SDK's `stocks` area with
their translations). Tests: `Tests/` (run by `tests/Themia.Modules.Tests`); the quote rules' own tests
stay Core's (`StockWidgetTests`, `S1307_`, `S1308_`, `S1309_`, `S1310_`, `S2775_`, and `MarketQuoteTests`).

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
- **[S-1310]** Chart options are the chart's ([S-11165]): its **High and low** markers. The colours are
  roles, not settings (family decision 2026-10-29, [S-6706]): a rise and an unchanged price are
  secondary (the change) and the accent (the chart), a fall is Critical in both; no green.
- **[S-1311]** The price (`StockLayout`), on the family's blocks and read as a meter reads ([S-1762]):
  the price a Readout on the family's 28 at the content's top ([S-2449]), start-aligned (or where the
  widget's alignment puts it, [S-6582]); the currency after it and the signed change (Body) beside it
  where they fit the card's width, else the currency gives way, else the change is the line under the
  price (`ReadoutRules.Fold`, [S-6707]); otherwise the line under says the market's session ("Market
  closed", "After hours", "Pre-market"; [S-1309]: its tooltip names the exchange's zone), the line kept
  whether said or not. The session is told every minute on the shared ticker (render only).
  **Refresh** in the card menu, with the quote's age; a failed refresh under the price on screen is the
  header's problem glyph ([S-7042]). No quote: the Status row at the top ([S-6734]): "“X” is not a
  symbol" for a refused symbol, "No quote for that symbol" for an empty answer, else the fetch's own
  sentence (the SDK's `StockQuoteLines.ShowState`). Header title: name, else the service's own name
  (listed), else the symbol.
- **[S-2770]** Stocks is concise ([S-666], user decision): the name, the price, its change, the session,
  the holding ([S-2777], only when set) and the extended-hours print ([S-1308], off by default); the
  period's shape is the chart atom's. No facts (previous close, ranges, volume, exchange) and no range
  chips: the period is the **Timeframe** line ([S-2778]).
- **[S-11160]** **An atom**: the reading and its rows, the same at every size in its range; no styles
  (the old Number style is this atom, the Chart style the ready-made group "Stocks", [S-11172]). The
  price and the chart, set to one instrument, period and extended hours, ask one quote and agree.
- **[S-11161]** **Sizes** (`StockLayout.Sizing`, with its header): the 28's line and the line under it,
  then 8 down a 28 row for the holding where one is set and one for the extended print where it is asked
  for (a listed symbol over one or five days), one height by those rows (100, 136, 164); 200..360 wide, a
  new one 240. A row with nothing to show yet keeps its place with the dash.
- **[S-2778]** Its options are all on the properties menu ([S-3719]); it has no settings page: Appearance
  ([S-3723]), what it quotes: **Symbol** (the search of [S-1307]; the line Set up opens: until a symbol
  is chosen the card's Set up row says "Choose a symbol", [S-1862]) and **Shares owned** ([S-2776]);
  after a divider the time it covers: **Timeframe** (1 day … 10 years), **Extended hours** (only a
  listed symbol over one or five days, [S-1308]) and **Dim when closed** (not for a coin); after another
  a link to **Update interval** (⚙ › Online updates › Markets, [S-3749]). Nothing under Style. (Tests
  `StockMenuTests`.)
- **[S-6165]**, **[S-6166]** *(Retired by [S-11160]: no styles; the chart is the `stockHistory` atom, [S-11165].)*
- **[S-2776]** **Shares owned** (`StockConfig.SharesOwned`, per widget, a line on its properties menu
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
- **[S-2777]** The holding is a fact row under the price ([S-11161]): the amount held as its name ("10
  held", secondary) and its worth with its code as its value (primary), the signed move added from 300
  wide ("4,202.70 USD +82.95", `StockHolding.Held`, `Value`); before the extended print. Numbers
  through `LocaleFormat`.
- **[S-2039]** A click on the card ([S-2000]) opens the symbol's own page: Yahoo Finance's quote
  (`https://finance.yahoo.com/quote/<symbol>/`) for a listed symbol, CoinGecko's coin page
  (`https://www.coingecko.com/en/coins/<id>`) for a coin; nothing with no symbol
  (`StockModule.Opens`).

Needs a live pass: the widget against the real services (a listed symbol through a session and its
extended hours, a coin), the symbol search, offline with a restored shelf.
