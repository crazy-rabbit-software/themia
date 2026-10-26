using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Themia.Widget.ExchangeRates;

namespace Themia.App.Shell.Config;

/// <summary>
/// [S-5148] The exchange-rate widget's options, all on its properties menu ([S-3719]): Appearance,
/// then the pair: the base and the currency priced in it, each a search over the published codes
/// ([S-3722]), and the source's credit. The cadence is fixed ([S-5147]). It has no settings page.
/// </summary>
internal static class ExchangeRatesMenu
{
    public static void Lines(WidgetMenuLines menu, ExchangeRatesConfig config)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);

        var region = ExchangeRatesStr.Online_Rates_Settings_Base_Region.Format(("code", RegionCurrency.Current));
        menu.Lines.Add(Currency(menu, ExchangeRatesStr.Online_Rates_Settings_Base_Label.Text(), region, config.BaseCurrency, code => config.BaseCurrency = code));

        var quoteDefault = ExchangeRatesStr.Online_Rates_Settings_Quote_Default.Format(("code", ExchangeRules.DefaultQuote(config.EffectiveBase)));
        menu.Lines.Add(Currency(menu, ExchangeRatesStr.Online_Rates_Settings_Quote_Label.Text(), quoteDefault, config.QuoteCurrency, code => config.QuoteCurrency = code));

        // The ECB's terms: free reuse, with the source named.
        menu.Lines.Add(ItemMenuEntry.Separator);
        menu.Lines.Add(WidgetMenuLines.Note(Str.Online_Source.Format(("name", "European Central Bank"))));
    }

    /// <summary>
    /// [S-5148] One side of the pair: what an empty code means (the region's currency, the default
    /// quote, naming it) first whatever is typed, then the published codes that start with what is
    /// typed; the one that holds has the dot and picking it stores nothing.
    /// </summary>
    private static ItemMenuEntry Currency(WidgetMenuLines menu, string text, string unset, string current, Action<string> set)
    {
        ItemMenuEntry Pick(string code, string words) => new(null, words, () =>
        {
            if (!string.Equals(code, current, StringComparison.Ordinal))
            {
                set(code);
                menu.Save();
            }
        }, Checked: string.Equals(code, current, StringComparison.Ordinal), Group: text);

        Task<MenuSearchAnswer> Find(string typed, CancellationToken cancel)
        {
            var query = typed.Trim();
            List<ItemMenuEntry> lines = [Pick(string.Empty, unset)];
            lines.AddRange(ExchangeRules.Published
                .Where(code => code.StartsWith(query, StringComparison.OrdinalIgnoreCase))
                .Select(code => Pick(code, code)));
            return Task.FromResult(new MenuSearchAnswer(lines));
        }

        return WidgetMenuLines.Search(ExchangeRatesModule.Glyph, text, current.Length > 0 ? current : unset,
            new MenuSearch(ExchangeRatesStr.Online_Rates_Menu_Find.Text(), Find, []));
    }
}
