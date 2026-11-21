using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Themia.Widget.Stock;
using Themia.Core.Settings;

namespace Themia.App.Shell.Config;

/// <summary>
/// [S-2778] The price's options, all on its page ([S-3719]): Appearance, the symbol as a
/// search ([S-1307], [S-3722]) and the holding (its row, [S-2777]); after a divider the period, the
/// extended hours where they apply ([S-1308]: their row) and the closed market's line. How often the markets are
/// asked is the settings' and the price just follows it ([S-3590]). It has no settings page.
/// </summary>
internal static class StockMenu
{
    public static void Lines(WidgetMenuLines menu, StockConfig config) => Lines(menu, config, SymbolLookup.FindAsync);

    /// <summary>The lines, with the symbol search's lookup given (a test's in place of the host's two services).</summary>
    internal static void Lines(WidgetMenuLines menu, StockConfig config, Func<string, CancellationToken, Task<IReadOnlyList<SymbolHit>>> lookup)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);
        var lines = menu.Lines;
        lines.Add(StockQuoteLines.Symbol(menu, config, lookup));

        // [S-2776] How many the user owns: the holding's row under the price.
        lines.Add(HoldingLine(menu, config));
        lines.Add(SettingsLine.Separator);
        lines.AddRange(StockQuoteLines.Period(menu, config));

        // [S-6074] Only where the market closes: not a coin.
        if (config.Market != QuoteMarket.Crypto)
        {
            lines.Add(menu.Toggle(Glyphs.Moon, StockStr.Stocks_MarketHours.Text(), config.ShowMarketState, on => config.ShowMarketState = on));
        }

    }

    /// <summary>
    /// [S-2776] "Shares owned", typed in place in its box ([S-2332]): an amount in the language's own
    /// decimals, refused rather than coerced (<see cref="StockHolding.TryParse"/>), the setting keeps
    /// its last good value. Empty or 0 is no holding.
    /// </summary>
    private static SettingsLine HoldingLine(WidgetMenuLines menu, StockConfig config)
    {
        var locale = Strings.Current;
        var owned = config.SharesOwned is { } amount ? StockHolding.Amount(locale, amount) : null;

        return new SettingsAsk(StockStr.WidgetsC_Stocks_Settings_Holding_Label.Text(), StockStr.WidgetsC_Stocks_Settings_Holding_Label.Text(), Glyphs.Calculator, StockHolding.Amount(locale, 0), owned ?? string.Empty, HoldingRefusal, typed =>
            {
                if (StockHolding.TryParse(Strings.Current, typed, out var parsed) && parsed != config.SharesOwned)
                {
                    config.SharesOwned = parsed;
                    menu.Save();
                }
            })
        {
            AllowEmpty = true,
            Value = owned,
        };
    }

    /// <summary>[S-2776] Why a typed holding cannot be used, or null when it can.</summary>
    internal static string? HoldingRefusal(string typed)
        => StockHolding.TryParse(Strings.Current, typed, out _)
            ? null
            : StockStr.WidgetsC_Stocks_Settings_Holding_Refused.Format(("zero", 0), ("example", StockHolding.Amount(Strings.Current, 12.5)));
}
