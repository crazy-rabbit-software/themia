using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.Sdk;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;

namespace Themia.App.Shell.Config;

/// <summary>
/// The Stocks widget's settings rows.
/// <para>
/// The symbol box is the trust boundary on this page: it is composed into a request to a quote
/// service, so it is checked before it is written and the reason it was refused is shown under the
/// box rather than swallowed. It is never repaired into something that works. [S-1307] It is also a
/// search, as the original's: typing asks Yahoo and CoinGecko together and a pick sets the symbol,
/// its market and its name in one go; a symbol typed in full and committed with Enter still works.
/// [S-3790] There is no market choice: the pick knows it, and a typed symbol is a listed one.
/// </para>
/// </summary>
internal static partial class WidgetSettings
{
    /// <summary>How long typing has to pause before the search asks. Rate limiting for two free services, not timing.</summary>
    private static readonly TimeSpan SearchPause = TimeSpan.FromMilliseconds(300);

    public static void Stocks(List<FrameworkElement> rows, StocksConfig config, Action save)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(save);

        rows.Add(FluentLayout.Header(Str.WidgetsC_Stocks_Settings_Instrument.Text()));

        // [S-3790] The symbol is the whole choice: its service and its name come with the pick.
        var verdict = FluentLayout.Verdict();
        var search = SymbolSearch(config, save, verdict);

        rows.Add(FluentLayout.FieldRow(Glyphs.Search, Str.Stocks_Tab_Symbols.Text(),
            Str.WidgetsC_Stocks_Settings_Symbol_Desc.Text(), search, verdict));

        // [S-6074] Only where the market closes, and where extended hours exist and fit the period.
        if (config.Offers(StocksSetting.MarketState))
        {
            rows.Add(FluentLayout.Row(Glyphs.Moon, Str.Stocks_DimWhenClosed.Text(),
                Str.WidgetsC_Stocks_Settings_MarketState_Desc.Text(),
                FluentLayout.Toggle(config.ShowMarketState, value => { config.ShowMarketState = value; save(); })));
        }

        if (config.Offers(StocksSetting.ExtendedHours))
        {
            rows.Add(FluentLayout.Row(Glyphs.Sun, Str.Stocks_ExtendedHours.Text(),
                Str.Stocks_ExtendedHoursDesc.Text(),
                FluentLayout.Toggle(config.ShowExtendedHours, value => { config.ShowExtendedHours = value; save(); })));
        }

        // [S-2776] How many the user owns: the holding's worth under the price.
        rows.Add(HoldingRow(config, save));

        // [S-3795] The period, the chart's markers and the colours fold under one row; whether the
        // chart is drawn at all is the style ([S-6165]), and the markers are the chart's ([S-6074]).
        var options = new List<Border>
        {
            FluentLayout.Row(Glyphs.Hours, Str.Stocks_Timeframe.Text(), null,
                FluentLayout.Choice(
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
                    config.Range,
                    value => { config.Range = value; save(); })),
        };

        if (config.Offers(StocksSetting.MinMax))
        {
            options.Add(FluentLayout.Row(Glyphs.Count, Str.Stocks_ShowMinMax.Text(), null,
                FluentLayout.Toggle(config.ShowMinMax, value => { config.ShowMinMax = value; save(); })));
        }

        options.Add(
            FluentLayout.ColorPickerRow(Glyphs.Palette, Str.Stocks_UpColor.Text(), null,
                config.UpColor,
                value => { config.UpColor = value; save(); },
                new ColorChoiceOptions { DefaultLabel = Str.WidgetsC_Stocks_Settings_WindowsGreen.Text(), DefaultColor = () => ThemeColor("SystemFillColorSuccessBrush") }));
        options.Add(
            FluentLayout.ColorPickerRow(Glyphs.Palette, Str.Stocks_DownColor.Text(), null,
                config.DownColor,
                value => { config.DownColor = value; save(); },
                new ColorChoiceOptions { DefaultLabel = Str.WidgetsC_Stocks_Settings_WindowsRed.Text(), DefaultColor = () => ThemeColor("SystemFillColorCriticalBrush") }));

        rows.Add(FluentLayout.Expander(Glyphs.Motion, Str.Styles_Stocks_Options.Text(), Str.Styles_Stocks_OptionsDesc.Text(), null, options));
    }

    /// <summary>
    /// [S-2776] "Shares owned": an amount in the language's own decimals, refused rather than
    /// coerced (<see cref="StockHolding.TryParse"/>), as every field that can be wrong reports: the
    /// typed text stays, the reason is said under it, the setting keeps its last good value. Empty
    /// or 0 is no holding.
    /// </summary>
    private static Border HoldingRow(StocksConfig config, Action save)
    {
        var locale = Strings.Current;
        var box = new TextBox
        {
            Text = config.SharesOwned is { } owned ? StockHolding.Amount(locale, owned) : string.Empty,
            MinWidth = 160,
            PlaceholderText = StockHolding.Amount(locale, 0),
            InputScope = new Microsoft.UI.Xaml.Input.InputScope { Names = { new Microsoft.UI.Xaml.Input.InputScopeName(Microsoft.UI.Xaml.Input.InputScopeNameValue.Number) } },
        };

        var verdict = FluentLayout.Verdict();

        void Commit()
        {
            if (!StockHolding.TryParse(Strings.Current, box.Text, out var amount))
            {
                FluentLayout.Refuse(verdict, Str.WidgetsC_Stocks_Settings_Holding_Refused.Format(("zero", 0), ("example", StockHolding.Amount(Strings.Current, 12.5))));
                return;
            }

            FluentLayout.Confirm(verdict, string.Empty);
            box.Text = amount is { } kept ? StockHolding.Amount(Strings.Current, kept) : string.Empty;

            if (amount != config.SharesOwned)
            {
                config.SharesOwned = amount;
                save();
            }
        }

        box.LostFocus += (_, _) => Commit();
        box.KeyDown += (_, args) =>
        {
            if (args.Key == Windows.System.VirtualKey.Enter)
            {
                Commit();
                args.Handled = true;
            }
        };

        return FluentLayout.FieldRow(Glyphs.Calculator, Str.WidgetsC_Stocks_Settings_Holding_Label.Text(),
            Str.WidgetsC_Stocks_Settings_Holding_Desc.Text(), box, verdict);
    }

    /// <summary>
    /// [S-1307] The symbol box with search: suggestions as the user types (after a short pause, the
    /// previous search abandoned), a pick sets symbol, market and name; Enter on typed text commits
    /// it as a listed symbol ([S-3790]), through the same check as before.
    /// </summary>
    private static AutoSuggestBox SymbolSearch(StocksConfig config, Action save, TextBlock verdict)
    {
        var box = new AutoSuggestBox
        {
            Text = config.Symbol,
            MinWidth = 260,
            PlaceholderText = "AAPL, ^GSPC, EURUSD=X, BTC-USD",
            QueryIcon = new SymbolIcon(Symbol.Find),
        };

        ShowClass(verdict, config);

        var hits = new List<SymbolHit>();
        CancellationTokenSource? asking = null;
        var pause = DispatcherQueue.GetForCurrentThread()?.CreateTimer();

        async Task AskAsync(string query)
        {
            asking?.Cancel();
            var cts = new CancellationTokenSource();
            asking = cts;

            try
            {
                var found = await SymbolLookup.FindAsync(query, cts.Token).ConfigureAwait(true);

                if (!ReferenceEquals(asking, cts) || box.Text.Trim() != query)
                {
                    return;
                }

                hits = [.. found];
                box.ItemsSource = hits.Select(Describe).ToList();
            }
            catch (OperationCanceledException)
            {
                // ignore: a newer keystroke asked again.
            }
            finally
            {
                if (ReferenceEquals(asking, cts))
                {
                    asking = null;
                }

                cts.Dispose();
            }
        }

        if (pause is not null)
        {
            pause.Interval = SearchPause;
            pause.IsRepeating = false;
            pause.Tick += (_, _) => _ = AskAsync(box.Text.Trim());
        }

        box.TextChanged += (_, args) =>
        {
            if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput)
            {
                return;
            }

            pause?.Stop();

            if (!MarketSearch.IsQuery(box.Text))
            {
                hits = [];
                box.ItemsSource = null;
                return;
            }

            pause?.Start();
        };

        void Pick(SymbolHit hit)
        {
            pause?.Stop();
            asking?.Cancel();

            config.Choose(hit);
            box.Text = hit.Symbol;
            box.ItemsSource = null;
            FluentLayout.Confirm(verdict, string.Empty);
            ShowClass(verdict, config, hit);
            save();
        }

        box.SuggestionChosen += (_, args) =>
        {
            var index = box.ItemsSource is List<string> shown && args.SelectedItem is string text ? shown.IndexOf(text) : -1;

            if (index >= 0 && index < hits.Count)
            {
                Pick(hits[index]);
            }
        };

        box.QuerySubmitted += (_, args) =>
        {
            if (args.ChosenSuggestion is not null)
            {
                return; // ignore: SuggestionChosen already applied the pick.
            }

            var typed = box.Text.Trim();

            if (typed == config.Symbol)
            {
                return;
            }

            // Refused, not repaired: stripping the characters that make a value unusable would
            // leave the widget quoting something other than what is written in its own settings.
            if (typed.Length > 0 && !WebInput.IsPlausibleSymbol(typed))
            {
                FluentLayout.Refuse(verdict, Str.WidgetsC_Stocks_Settings_SymbolRefused.Text());
                return;
            }

            config.ChooseTyped(typed);
            FluentLayout.Confirm(verdict, string.Empty);
            ShowClass(verdict, config);
            save();
        };

        box.Unloaded += (_, _) =>
        {
            pause?.Stop();
            asking?.Cancel();
        };

        return box;
    }

    /// <summary>What a theme brush currently paints, for a default swatch.</summary>
    private static Windows.UI.Color? ThemeColor(string key)
        => ThemeBrush.Of(key, Microsoft.UI.Colors.Gray) is Microsoft.UI.Xaml.Media.SolidColorBrush brush ? brush.Color : null;

    /// <summary>One suggestion line: "Microsoft Corporation — MSFT · NASDAQ · Shares".</summary>
    private static string Describe(SymbolHit hit)
    {
        var parts = new List<string> { hit.Symbol.ToUpperInvariant() };

        if (hit.Exchange.Length > 0)
        {
            parts.Add(hit.Exchange);
        }

        parts.Add(MarketSearch.Label(hit.Asset));
        return $"{hit.Name} — {string.Join(" · ", parts)}";
    }

    /// <summary>[S-1307] What the chosen symbol is, under the box: "Index", "Currency"…</summary>
    private static void ShowClass(TextBlock verdict, StocksConfig config, SymbolHit? hit = null)
    {
        if (config.EffectiveSymbol.Length == 0)
        {
            return;
        }

        var asset = hit?.Asset ?? MarketQuote.Classify(config.EffectiveSymbol, config.Market);
        var label = MarketSearch.Label(asset);
        FluentLayout.Confirm(verdict, hit is { Exchange.Length: > 0 }
            ? Str.WidgetsC_Stocks_Settings_ClassOnExchange.Format(("class", label), ("exchange", hit.Exchange))
            : label);
    }
}
