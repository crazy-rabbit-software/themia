using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;

namespace Themia.App.Shell.Config;

/// <summary>
/// [S-5148] [S-11150] What the exchange-rate atoms share on their properties menus ([S-3719]): the pair,
/// the base and the currency priced in it, each a search over the published codes ([S-3722]), and the
/// source's credit behind a divider (the ECB's terms: free reuse, with the source named).
/// </summary>
public static class ExchangePairLines
{
    public static IReadOnlyList<ItemMenuEntry> Lines(WidgetMenuLines menu, ExchangePairConfig config, string glyph)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);

        var region = Str.Online_Rates_Settings_Base_Region.Format(("code", RegionCurrency.Current));
        var quoteDefault = Str.Online_Rates_Settings_Quote_Default.Format(("code", ExchangeRules.DefaultQuote(config.EffectiveBase)));
        return
        [
            Currency(menu, glyph, Str.Online_Rates_Settings_Base_Label.Text(), region, config.BaseCurrency, code => config.BaseCurrency = code),
            Currency(menu, glyph, Str.Online_Rates_Settings_Quote_Label.Text(), quoteDefault, config.QuoteCurrency, code => config.QuoteCurrency = code),
            ItemMenuEntry.Separator,
            WidgetMenuLines.Note(Str.Online_Source.Format(("name", "European Central Bank"))),
        ];
    }

    /// <summary>
    /// [S-5148] One side of the pair: what an empty code means (the region's currency, the default
    /// quote, naming it) first whatever is typed, then the published codes that start with what is
    /// typed; the one that holds has the dot and picking it stores nothing.
    /// </summary>
    private static ItemMenuEntry Currency(WidgetMenuLines menu, string glyph, string text, string unset, string current, Action<string> set)
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

        return WidgetMenuLines.Search(glyph, text, current.Length > 0 ? current : unset,
            new MenuSearch(Str.Online_Rates_Menu_Find.Text(), Find, []));
    }
}
