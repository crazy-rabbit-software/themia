using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Sync;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI.Kit;

namespace Themia.App.Shell.Config;

/// <summary>
/// [S-2778] [S-11160] What the stock atoms share on their properties menus ([S-3719]): the instrument as
/// a search ([S-1307], [S-3722]; the line Set up opens), the period (Timeframe), Extended hours where
/// they exist and fit ([S-1308]) and the link to the markets' update interval ([S-3749]).
/// </summary>
public static class StockQuoteLines
{
    private const string Scope = "stocks-menu";

    /// <summary>How long typing has to pause before the search asks. Rate limiting for two free services, not timing.</summary>
    private static readonly TimeSpan SearchPause = TimeSpan.FromMilliseconds(300);

    /// <summary>[S-1307] The symbol's line, the line Set up opens.</summary>
    public static ItemMenuEntry Symbol(WidgetMenuLines menu, StockQuoteConfig config, Func<string, CancellationToken, Task<IReadOnlyList<SymbolHit>>> lookup)
        => WidgetMenuLines.Shared(SharedSettings.StockSymbol, WidgetMenuLines.SetsUp(SymbolLine(menu, config, lookup)));

    /// <summary>[S-2778] The period and what depends on it: Timeframe, then Extended hours where they apply.</summary>
    public static IEnumerable<ItemMenuEntry> Period(WidgetMenuLines menu, StockQuoteConfig config)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);
        yield return WidgetMenuLines.Shared(SharedSettings.StockTimeframe, menu.Choice(Glyphs.Hours, Str.Stocks_Timeframe.Text(),
            [
                (QuoteRange.Day, Str.WidgetsC_Stocks_Period_Days.Plural(1)),
                (QuoteRange.FiveDays, Str.WidgetsC_Stocks_Period_Days.Plural(5)),
                (QuoteRange.Month, Str.WidgetsC_Stocks_Period_Months.Plural(1)),
                (QuoteRange.ThreeMonths, Str.WidgetsC_Stocks_Period_Months.Plural(3)),
                (QuoteRange.SixMonths, Str.WidgetsC_Stocks_Period_Months.Plural(6)),
                (QuoteRange.Year, Str.WidgetsC_Stocks_Period_Years.Plural(1)),
                (QuoteRange.TwoYears, Str.WidgetsC_Stocks_Period_Years.Plural(2)),
                (QuoteRange.FiveYears, Str.WidgetsC_Stocks_Period_Years.Plural(5)),
                (QuoteRange.TenYears, Str.WidgetsC_Stocks_Period_Years.Plural(10)),
            ],
            config.Range, value => config.Range = value));

        // [S-6074] Only where extended hours exist and fit the period.
        if (config.OffersExtendedHours)
        {
            yield return WidgetMenuLines.Shared(SharedSettings.StockTimeframe, menu.Toggle(Glyphs.Sun, Str.Stocks_ExtendedHours.Text(), config.ShowExtendedHours, on => config.ShowExtendedHours = on));
        }
    }

    /// <summary>[S-3749] How often the markets are asked: ⚙'s, for every market widget at once.</summary>
    public static ItemMenuEntry Updates(WidgetMenuLines menu)
    {
        ArgumentNullException.ThrowIfNull(menu);
        return menu.Link(new SettingLink(OwnedSetting.OnlineUpdates, HostCadences.Markets));
    }

    /// <summary>
    /// [S-6734] No quote, said in the Status row: the symbol refused ("“X” is not a symbol"), nothing quoted
    /// for it, or how the sync went (not set up is the card's Set up row, [S-1862]).
    /// </summary>
    public static void ShowState(StatusBlock state, FetchStatus status, string symbol, Action retry)
    {
        ArgumentNullException.ThrowIfNull(state);
        switch (status)
        {
            case FetchStatus.Refused:
                state.Show(StatusKind.Problem, Glyphs.Stocks, Str.WidgetsC_Stocks_NotSymbol.Format(("symbol", symbol)));
                break;
            case FetchStatus.Empty:
                state.Show(StatusKind.Empty, Glyphs.Stocks, Str.WidgetsC_Stocks_NoQuote.Text());
                break;
            default:
                OnlineParts.ShowState(state, status, Glyphs.Stocks, retry);
                break;
        }
    }

    /// <summary>[S-6734] The state to say without a quote: nothing typed is not set up, something typed that is not a symbol refused.</summary>
    public static FetchStatus Unasked(StockQuoteConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return string.IsNullOrWhiteSpace(config.Symbol) ? FetchStatus.NotConfigured : FetchStatus.Refused;
    }

    /// <summary>
    /// [S-1307] The symbol: a search asking Yahoo and CoinGecko together once typing pauses; a pick sets the
    /// symbol, its market and its name in one go. What is typed, if it is a plausible symbol, is offered
    /// first as itself ([S-3790]); when no service answered, a note says why under it. A symbol with
    /// characters no symbol has is said to be one, never stripped.
    /// </summary>
    private static ItemMenuEntry SymbolLine(WidgetMenuLines menu, StockQuoteConfig config, Func<string, CancellationToken, Task<IReadOnlyList<SymbolHit>>> lookup)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(lookup);

        async Task<MenuSearchAnswer> Find(string typed, CancellationToken cancel)
        {
            var query = typed.Trim();
            if (!MarketSearch.IsQuery(query))
            {
                return new MenuSearchAnswer([]);
            }

            IReadOnlyList<SymbolHit> hits;
            string? unanswered = null;
            try
            {
                hits = await lookup(query, cancel).ConfigureAwait(true);
            }
            catch (SymbolSearchFailedException ex)
            {
                // [S-1307] No service answered (blocked, offline): say why, never "no matches".
                Log.Info(Scope, ex.Message);
                hits = [];
                unanswered = FetchState.Message(ex.Status);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A symbol typed in full still works without the search.
                Log.Warn(Scope, $"symbol search failed: {ex.GetType().Name}");
                hits = [];
                unanswered = FetchState.Message(FetchStatus.Unreachable);
            }

            var lines = new List<ItemMenuEntry>();
            if (WebInput.IsPlausibleSymbol(query))
            {
                lines.Add(new ItemMenuEntry(Glyphs.Edit, Str.Stocks_Menu_UseTyped.Format(("symbol", query)), () =>
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
            if (unanswered is not null)
            {
                lines.Add(WidgetMenuLines.Note(unanswered));
                return new MenuSearchAnswer(lines);
            }

            return lines.Count > 0
                ? new MenuSearchAnswer(lines)
                : new MenuSearchAnswer([], Str.WidgetsC_Stocks_Settings_SymbolRefused.Text());
        }

        var shown = config.EffectiveSymbol is { Length: > 0 } symbol ? symbol.ToUpperInvariant() : null;
        return WidgetMenuLines.Search(Glyphs.Search, Str.Stocks_Tab_Symbols.Text(), shown,
            new MenuSearch("AAPL, ^GSPC, EURUSD=X, BTC-USD", Find, [], SearchPause));
    }

    /// <summary>One answer: "Microsoft Corporation — MSFT · NASDAQ · Shares".</summary>
    public static string Describe(SymbolHit hit)
    {
        ArgumentNullException.ThrowIfNull(hit);
        var parts = new List<string> { hit.Symbol.ToUpperInvariant() };
        if (hit.Exchange.Length > 0)
        {
            parts.Add(hit.Exchange);
        }

        parts.Add(MarketSearch.Label(hit.Asset));
        return $"{hit.Name} — {string.Join(" · ", parts)}";
    }
}
