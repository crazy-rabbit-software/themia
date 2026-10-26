# Stocks (OFF-E)

The module `stocks` ([S-6911]): one instrument's price, its move and the shape of the period. Its tags
are this file's alone; the root `SPEC.md` holds the host's sections and what the host and the widget
share (Feed, stocks and the web widgets: the stored quotes [S-1300], the exchange's clock [S-1309],
one baseline for the colour, the change and the line [S-2775], in the SDK's `MarketQuote`,
`TickerQuote` and `ExchangeTime`; the hero readout [S-667]; the background web sync [S-2663]; the
market cadence [S-3780]).

Owning files: everything in this folder: `StocksModule.cs` (descriptor, view factory, menu lines,
JSON context), `StocksConfig.cs` (with its styles and the rows only one style offers),
`StocksView.cs` and `StocksView.Chart.cs` (the quote through `IWidgetHost.Quotes`), `StockLayout.cs`
(both styles' ladders), `StockChartLabels.cs` (the chart's captions), `StockHolding.cs` (shares owned),
`StocksMenu.cs` (its properties-menu lines: the symbol search through the SDK's `SymbolLookup`, the
holding, the period and colours), `Preview/` (Debug renders: `--state quote|falling`), `Localization/Locales/` (its
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
  contributes nothing (logged).
- **[S-1308]** Extended hours (classic S-34/S-35, `ShowExtendedHours`, off): listed symbols on the
  1-day and 5-day charts ask `includePrePost=true` (part of the cache key). The last pre-market /
  after-hours bar against the regular close is shown under the price ("After hours 412.10 −0.36% ·
  16:42", in its direction's colour): pre-market before the bell, after hours after it, the latest of
  the two while closed. The badge turns accent in an extended session with data; bars outside the
  regular session draw dashed at 55% opacity. Timestamps are kept paired with closes (nulls drop
  both halves; out-of-order series are sorted).
- **[S-1310]** Chart options: **High and low** markers (dot and price at the period's first highest
  and lowest bar, the price above the high and under the low, kept inside the chart's width,
  `StockChartLabels.Place`); the area under the line is always filled (WinUI's look; no setting,
  [S-3795]); **Rising/Falling colour** through the menu's colour submenu
  ([S-3724], never hex; empty = the theme's green/red; a hand-edited non-colour is repaired
  to empty at load).
- **[S-1311]** Stocks ladder (`StockLayout.Plan`, replacing the 34%/120px chart band): rung 0 price +
  signed change, drawn as the hero readout ([S-667]: the change and the session on the price's
  baseline, or on a line of their own under it where they do not fit beside); the currency (the
  readout's unit, after the price) and session glyph from 180px, the session's words from 300px;
  the holding ([S-2777]); the extended line; the chart (≥24px), full-bleed: edge
  to edge and down to the card's bottom edge, no inset, the card's rounded corners clipping it
  ([S-215]). Its times are the start and the end of the range only, from 128px high, written inside
  the chart's bottom band over the fill, in line with the text's inset; the trace keeps clear of them
  and of the markers' prices (`StockChartLabels.Band`), the fill does not. No price axis (user
  decision 2026-10-11): the high/low markers carry the period's prices, and an axis at the edge
  would need an inset of its own. A strip (≥400px, 2:1) puts the readout beside a full-height
  chart. The chart takes what is left; the price grows only to Title / TitleLarge / Display by the
  tile's smaller class. Pointer over the chart on L/XL reads the bar under it (time · price). The
  session badge re-evaluates every minute on the shared ticker (render only), so the badge flips at
  the bell whatever the refresh. **Refresh** in the card menu, with the quote's age ("Refresh ·
  updated 4 min ago"); a failed refresh under the price on screen is the header's problem glyph
  ([S-7042]), never an age drawn in the header. Header title: name, else the
  service's own name (listed), else the symbol.
- **[S-2770]** Stocks is concise ([S-666], user decision): the name, the price, its change, the
  session badge, the holding ([S-2777], only when set), the extended-hours line ([S-1308], off by
  default, one caption under the price) and the chart, nothing else at any size. No facts (previous close, day and 52-week range, volume,
  exchange; `MarketQuote.Facts`/`Volume` are gone) and no range chips on the widget: the period is
  the **Timeframe** line on its properties menu (`Range`, default 1 day, [S-2778]). What they took goes to the
  chart. Those facts are not parsed or cached either (`QuoteStats` is gone; the previous close is
  read only as the change's baseline, [S-2775]).
- **[S-2778]** Its options are all on the properties menu ([S-3719], user request 2026-10-19); it
  has no settings page. Under Style the chart's **High and low** markers (the chart style only,
  [S-6074]); then Appearance ([S-3723]), what it quotes: **Symbol** (the search of [S-1307]) and
  **Shares owned** ([S-2776]; the amount at the line's end); after a divider the time it covers:
  **Timeframe** (1 day … 10 years), **Dim when closed** (not for a coin, which never closes) and
  **Extended hours** (only a listed symbol over one or five days, [S-1308]: elsewhere there are no
  such bars to ask for, [S-6070]); after another **Rising** and **Falling colour** (Windows green /
  red by default, [S-1310]); after another a link to **Update interval** (⚙ › Online updates ›
  Markets, [S-3749], [S-3590]: never a cadence of the widget's own). (Tests `StocksMenuTests`.)
- **[S-6165]** The stock widget's styles ([S-6000]), `StocksConfig.Style` (`StocksStyle`): **Chart**
  ([S-1311], the default) and **Number**; what was its "Show chart" switch. The period and
  the colours are every style's, the markers the chart's ([S-2778]).
- **[S-6166]** **Number** (`StockLayout.Plan` with `Number`): no chart at any size; the price and,
  on a line of its own under it, the change, both as large as the tile holds: the price on the ramp
  from Subtitle to Display and then Display grown in tenths to 1.5×, the change at the largest ramp
  step within half the price, never under Body (`NumberChangeStep`); the holding and the extended
  print keep their caption lines under them. Centred in the tile.
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
- **[S-2777]** The holding shows as one caption line under the price and change, from 180px wide
  (shed on XS), before the extended line, its height taken from the chart: the worth with its code
  and the signed move in the change's colour ("4,202.70 USD  +82.95"), the amount first from 300px
  ("10 held · 4,202.70 USD  +82.95", `StockHolding.Caption`). Numbers through `LocaleFormat`.

Needs a live pass: the widget against the real services (a listed symbol through a session and its
extended hours, a coin), the symbol search, offline with a restored shelf.
