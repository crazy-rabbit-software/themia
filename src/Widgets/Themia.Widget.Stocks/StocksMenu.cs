using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Themia.Widget.Stocks;

namespace Themia.App.Shell.Config;

/// <summary>
/// [S-2778] The Stocks widget's options, all on its properties menu ([S-3719]): under Style the
/// chart's markers (the chart style only, [S-6074]); then Appearance ([S-3723]), the symbol as a
/// search ([S-1307], [S-3722]) and the holding; after a divider the period with the closed market and
/// the extended hours where they apply; after another the colours. It has no settings page.
/// </summary>
internal static class StocksMenu
{
    private const string Scope = "stocks-menu";

    /// <summary>How long typing has to pause before the search asks. Rate limiting for two free services, not timing.</summary>
    private static readonly TimeSpan SearchPause = TimeSpan.FromMilliseconds(300);

    public static void Lines(WidgetMenuLines menu, StocksConfig config) => Lines(menu, config, SymbolLookup.FindAsync);

    /// <summary>The lines, with the symbol search's lookup given (a test's in place of the host's two services).</summary>
    internal static void Lines(WidgetMenuLines menu, StocksConfig config, Func<string, CancellationToken, Task<IReadOnlyList<SymbolHit>>> lookup)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(lookup);

        if (config.Offers(StocksSetting.MinMax))
        {
            menu.StyleLines.Add(menu.Toggle(null, StocksStr.Stocks_ShowMinMax.Text(), config.ShowMinMax, on => config.ShowMinMax = on));
        }

        menu.PlaceCard();
        var lines = menu.Lines;

        // [S-3790] The symbol is the whole choice: its service and its name come with the pick.
        lines.Add(WidgetMenuLines.SetsUp(SymbolLine(menu, config, lookup)));

        // [S-2776] How many the user owns: the holding's worth under the price.
        lines.Add(HoldingLine(menu, config));

        // [S-2778] The time it covers: the period, then what depends on it and on the market.
        lines.Add(ItemMenuEntry.Separator);
        lines.Add(menu.Choice(Glyphs.Hours, StocksStr.Stocks_Timeframe.Text(),
            [
                (QuoteRange.Day, StocksStr.WidgetsC_Stocks_Period_Days.Plural(1)),
                (QuoteRange.FiveDays, StocksStr.WidgetsC_Stocks_Period_Days.Plural(5)),
                (QuoteRange.Month, StocksStr.WidgetsC_Stocks_Period_Months.Plural(1)),
                (QuoteRange.ThreeMonths, StocksStr.WidgetsC_Stocks_Period_Months.Plural(3)),
                (QuoteRange.SixMonths, StocksStr.WidgetsC_Stocks_Period_Months.Plural(6)),
                (QuoteRange.Year, StocksStr.WidgetsC_Stocks_Period_Years.Plural(1)),
                (QuoteRange.TwoYears, StocksStr.WidgetsC_Stocks_Period_Years.Plural(2)),
                (QuoteRange.FiveYears, StocksStr.WidgetsC_Stocks_Period_Years.Plural(5)),
                (QuoteRange.TenYears, StocksStr.WidgetsC_Stocks_Period_Years.Plural(10)),
            ],
            config.Range, value => config.Range = value));

        // [S-6074] Only where the market closes, and where extended hours exist and fit the period.
        if (config.Offers(StocksSetting.MarketState))
        {
            lines.Add(menu.Toggle(Glyphs.Moon, StocksStr.Stocks_DimWhenClosed.Text(), config.ShowMarketState, on => config.ShowMarketState = on));
        }

        if (config.Offers(StocksSetting.ExtendedHours))
        {
            lines.Add(menu.Toggle(Glyphs.Sun, StocksStr.Stocks_ExtendedHours.Text(), config.ShowExtendedHours, on => config.ShowExtendedHours = on));
        }

        // [S-1310] The colours of a rise and a fall.
        lines.Add(ItemMenuEntry.Separator);
        lines.Add(menu.Colour(StocksStr.Stocks_UpColor.Text(), config.UpColor, value => config.UpColor = value,
            new ColorChoiceOptions { DefaultLabel = StocksStr.WidgetsC_Stocks_Settings_WindowsGreen.Text(), DefaultColor = () => ThemeColor("SystemFillColorSuccessBrush") }));
        lines.Add(menu.Colour(StocksStr.Stocks_DownColor.Text(), config.DownColor, value => config.DownColor = value,
            new ColorChoiceOptions { DefaultLabel = StocksStr.WidgetsC_Stocks_Settings_WindowsRed.Text(), DefaultColor = () => ThemeColor("SystemFillColorCriticalBrush") }));
    }

    /// <summary>
    /// [S-1307] The symbol: a search asking Yahoo and CoinGecko together once typing pauses; a pick
    /// sets the symbol, its market and its name in one go. What is typed, if it is a plausible
    /// symbol, is offered first as itself ([S-3790]: a listed one), so a symbol typed in full still
    /// works. Refused, not repaired: a symbol with characters no symbol has is said to be one, never
    /// stripped into something the widget would quote in its place.
    /// </summary>
    private static ItemMenuEntry SymbolLine(WidgetMenuLines menu, StocksConfig config, Func<string, CancellationToken, Task<IReadOnlyList<SymbolHit>>> lookup)
    {
        async Task<MenuSearchAnswer> Find(string typed, CancellationToken cancel)
        {
            var query = typed.Trim();
            if (!MarketSearch.IsQuery(query))
            {
                return new MenuSearchAnswer([]);
            }

            IReadOnlyList<SymbolHit> hits;
            try
            {
                hits = await lookup(query, cancel).ConfigureAwait(true);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A symbol typed in full still works without the search.
                Log.Warn(Scope, $"symbol search failed: {ex.GetType().Name}");
                hits = [];
            }

            var lines = new List<ItemMenuEntry>();
            if (WebInput.IsPlausibleSymbol(query))
            {
                lines.Add(new ItemMenuEntry(Glyphs.Edit, StocksStr.Stocks_Menu_UseTyped.Format(("symbol", query)), () =>
                {
                    if (!string.Equals(query, config.Symbol, StringComparison.Ordinal))
                    {
                        config.ChooseTyped(query);
                        menu.Save();
                    }
                }));
            }

            lines.AddRange(hits.Select(hit => new ItemMenuEntry(null, Describe(hit), () =>
            {
                config.Choose(hit);
                menu.Save();
            })));

            return lines.Count > 0
                ? new MenuSearchAnswer(lines)
                : new MenuSearchAnswer([], StocksStr.WidgetsC_Stocks_Settings_SymbolRefused.Text());
        }

        var shown = config.EffectiveSymbol is { Length: > 0 } symbol ? symbol.ToUpperInvariant() : null;
        return WidgetMenuLines.Search(Glyphs.Search, StocksStr.Stocks_Tab_Symbols.Text(), shown,
            new MenuSearch("AAPL, ^GSPC, EURUSD=X, BTC-USD", Find, [], SearchPause));
    }

    /// <summary>
    /// [S-2776] "Shares owned", asked beside the menu ([S-3721]): an amount in the language's own
    /// decimals, refused rather than coerced (<see cref="StockHolding.TryParse"/>), the setting keeps
    /// its last good value. Empty or 0 is no holding.
    /// </summary>
    private static ItemMenuEntry HoldingLine(WidgetMenuLines menu, StocksConfig config)
    {
        var locale = Strings.Current;
        var owned = config.SharesOwned is { } amount ? StockHolding.Amount(locale, amount) : null;

        return new ItemMenuEntry(Glyphs.Calculator, StocksStr.WidgetsC_Stocks_Settings_Holding_Label.Text(), () => menu.Ask(new WidgetTextAsk(
            StocksStr.WidgetsC_Stocks_Settings_Holding_Label.Text(),
            StockHolding.Amount(locale, 0),
            Str.Common_Save.Text(),
            HoldingRefusal,
            typed =>
            {
                if (StockHolding.TryParse(Strings.Current, typed, out var parsed) && parsed != config.SharesOwned)
                {
                    config.SharesOwned = parsed;
                    menu.Save();
                }
            },
            Initial: owned ?? string.Empty,
            AllowEmpty: true)), Value: owned);
    }

    /// <summary>[S-2776] Why a typed holding cannot be used, or null when it can.</summary>
    internal static string? HoldingRefusal(string typed)
        => StockHolding.TryParse(Strings.Current, typed, out _)
            ? null
            : StocksStr.WidgetsC_Stocks_Settings_Holding_Refused.Format(("zero", 0), ("example", StockHolding.Amount(Strings.Current, 12.5)));

    /// <summary>What a theme brush currently paints, for a default swatch.</summary>
    private static Windows.UI.Color? ThemeColor(string key)
        => ThemeBrush.Of(key, Microsoft.UI.Colors.Gray) is Microsoft.UI.Xaml.Media.SolidColorBrush brush ? brush.Color : null;

    /// <summary>One answer: "Microsoft Corporation — MSFT · NASDAQ · Shares".</summary>
    internal static string Describe(SymbolHit hit)
    {
        var parts = new List<string> { hit.Symbol.ToUpperInvariant() };

        if (hit.Exchange.Length > 0)
        {
            parts.Add(hit.Exchange);
        }

        parts.Add(MarketSearch.Label(hit.Asset));
        return $"{hit.Name} — {string.Join(" · ", parts)}";
    }
}
