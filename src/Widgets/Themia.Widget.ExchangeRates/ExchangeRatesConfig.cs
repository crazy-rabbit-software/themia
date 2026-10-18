using System.Text.Json.Serialization;
using Themia.Core.Localization;
using Themia.Core.Sync;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Widget.ExchangeRates;

namespace Themia.Core.Model;

/// <summary>[S-6167] The exchange-rate widget's styles: its one pair, two looks.</summary>
[JsonConverter(typeof(StyleJsonConverter<ExchangeRatesStyle>))]
public enum ExchangeRatesStyle
{
    /// <summary>[S-6168] The rate over its 90-day line: the default, as for Stocks.</summary>
    Chart = 0,

    /// <summary>[S-5146] The rate alone, as large as the tile allows.</summary>
    Number = 1,
}

/// <summary>
/// [S-5140] The ECB's reference rate of one pair: what one unit of the base currency buys of the
/// quote. Several pairs are several widgets.
/// </summary>
public sealed class ExchangeRatesConfig : WidgetConfig, IStyled, IContentAligned, IInterests
{
    /// <summary>[S-2608] What the background sync keeps fresh for it.</summary>
    public void AddInterests(InterestsBuilder interests) => interests.Of<RatesInterest>().Add(new RatesInterest(OnlineCadence.Rates));

    private static readonly StyleOption[] RateStyles =
    [
        StyleOption.Of(ExchangeRatesStyle.Chart, ExchangeRatesStr.Styles_Rates_Chart.Local(), 0xE9D2),
        StyleOption.Of(ExchangeRatesStyle.Number, ExchangeRatesStr.Styles_Rates_Number.Local(), 0xE75F),
    ];


    /// <summary>[S-6581] Only this style is one block to align.</summary>
    bool IContentAligned.AlignApplies => Style == ExchangeRatesStyle.Number;

    /// <summary>[S-5143] The base currency, an ISO 4217 code the ECB publishes; empty follows the Windows region.</summary>
    public string BaseCurrency { get; set; } = string.Empty;

    /// <summary>[S-5143] The currency priced in the base; empty is the euro, or the dollar for a euro base.</summary>
    public string QuoteCurrency { get; set; } = string.Empty;

    /// <summary>[S-6167] The widget's look: the rate alone, or over its chart.</summary>
    public ExchangeRatesStyle Style { get; set; } = ExchangeRatesStyle.Chart;

    IReadOnlyList<StyleOption> IStyled.Styles => RateStyles;

    int IStyled.StyleValue
    {
        get => (int)Style;
        set => Style = (ExchangeRatesStyle)value;
    }

    /// <summary>The base in force: the stored one, or the Windows region's currency ([S-5143]).</summary>
    [JsonIgnore]
    public string EffectiveBase => BaseCurrency.Length > 0 ? BaseCurrency : RegionCurrency.Current;

    /// <summary>
    /// [S-5143] The quote in force: the stored one, unless it is empty or the base itself (one CHF is
    /// one CHF), then <see cref="ExchangeRules.DefaultQuote"/> for the base.
    /// </summary>
    [JsonIgnore]
    public string EffectiveQuote
    {
        get
        {
            var basis = EffectiveBase;
            return QuoteCurrency.Length > 0 && !string.Equals(QuoteCurrency, basis, StringComparison.Ordinal)
                ? QuoteCurrency
                : ExchangeRules.DefaultQuote(basis);
        }
    }

    public override WidgetConfig Clone() => new ExchangeRatesConfig
    {
        BaseCurrency = BaseCurrency,
        QuoteCurrency = QuoteCurrency,
        Style = Style,
    };

    /// <summary>[S-5144] A base or quote the ECB does not publish follows the default, loudly.</summary>
    public override IReadOnlyList<string> Repair()
    {
        var repairs = new List<string>();
        BaseCurrency = Code(BaseCurrency, "base", "following the region", repairs);
        QuoteCurrency = Code(QuoteCurrency, "quote", "the default", repairs);
        return repairs;
    }

    private static string Code(string? code, string what, string instead, List<string> repairs)
    {
        if (string.IsNullOrEmpty(code))
        {
            return string.Empty;
        }

        if (ExchangeRules.Normalise(code) is { } known)
        {
            return known;
        }

        repairs.Add($"exchange rates: {what} '{Clip(code)}' is not a published currency; {instead}");
        return string.Empty;
    }

    private static string Clip(string? text) => text is null ? "null" : text.Length <= 8 ? text : text[..8];
}
