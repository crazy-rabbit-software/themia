using System.Text.Json.Serialization;
using Themia.Core.Localization;
using Themia.Core.Widgets;

namespace Themia.Core.Model;

/// <summary>[S-5103] Which air quality index the widget reads out.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<AirQualityScale>))]
public enum AirQualityScale
{
    /// <summary>The European index for a place in Europe, the US index everywhere else.</summary>
    Auto = 0,

    /// <summary>The European Environment Agency's index (EAQI), 0 to 100+.</summary>
    European = 1,

    /// <summary>The US EPA's index, 0 to 500.</summary>
    Us = 2,
}

/// <summary>[S-6169] The air widget's styles: the same reading, two looks.</summary>
[JsonConverter(typeof(StyleJsonConverter<AirQualityStyle>))]
public enum AirQualityStyle
{
    /// <summary>[S-5107] The index and its band as a number, on the ladder.</summary>
    Number = 0,

    /// <summary>[S-6170] The index on an arc of its six bands.</summary>
    Gauge = 1,
}

/// <summary>[S-6167] The exchange-rate widget's styles: the same rates, two looks. What was its chart switch.</summary>
[JsonConverter(typeof(StyleJsonConverter<ExchangeRatesStyle>))]
public enum ExchangeRatesStyle
{
    /// <summary>[S-5146] The first currency over the others as rows.</summary>
    List = 0,

    /// <summary>[S-6168] The first currency large, with its 90-day line.</summary>
    Chart = 1,
}

/// <summary>
/// [S-5100] Air quality, UV and pollen at a place. A weather-family widget: the same place picker and
/// the same header rule ([S-728]), so it derives from <see cref="WeatherConfig"/>; the sync tells the
/// kinds apart ([S-5101]).
/// </summary>
public sealed class AirQualityConfig : WeatherConfig, IStyled
{
    private static readonly StyleOption[] AirStyles =
    [
        StyleOption.Of(AirQualityStyle.Number, Str.Styles_Air_Number, 0xE75F),
        StyleOption.Of(AirQualityStyle.Gauge, Str.Styles_Air_Gauge, 0xEC4A),
    ];

    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.AirQuality;

    /// <summary>[S-6169] The widget's look.</summary>
    public AirQualityStyle Style { get; set; } = AirQualityStyle.Number;

    IReadOnlyList<StyleOption> IStyled.Styles => AirStyles;

    int IStyled.StyleValue
    {
        get => (int)Style;
        set => Style = (AirQualityStyle)value;
    }

    /// <summary>[S-5103] Auto picks by the place, not by the user's region: the index is about the air there.</summary>
    public AirQualityScale Scale { get; set; } = AirQualityScale.Auto;

    public override WidgetConfig Clone()
    {
        var copy = new AirQualityConfig { Scale = Scale, Style = Style };
        CopyTo(copy);
        return copy;
    }

    public override IReadOnlyList<string> Repair()
    {
        if (Enum.IsDefined(Scale))
        {
            return [];
        }

        var was = Scale;
        Scale = AirQualityScale.Auto;
        return [$"air quality: index {(int)was} is not one; Auto"];
    }
}

/// <summary>[S-5120] Rain in the next two hours at a place. A weather-family widget, like air quality.</summary>
public sealed class RainNextHourConfig : WeatherConfig
{
    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.RainNextHour;

    public override WidgetConfig Clone()
    {
        var copy = new RainNextHourConfig();
        CopyTo(copy);
        return copy;
    }
}

/// <summary>
/// [S-5140] The ECB's reference rates for one base currency against a short list of others.
/// </summary>
public sealed class ExchangeRatesConfig : WidgetConfig, IStyled
{
    private static readonly StyleOption[] RateStyles =
    [
        StyleOption.Of(ExchangeRatesStyle.List, Str.Styles_Rates_List, 0xEA37),
        StyleOption.Of(ExchangeRatesStyle.Chart, Str.Styles_Rates_Chart, 0xE9D2),
    ];

    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.ExchangeRates;

    /// <summary>[S-5143] The base currency, an ISO 4217 code the ECB publishes; empty follows the Windows region.</summary>
    public string BaseCurrency { get; set; } = string.Empty;

    /// <summary>[S-5143] The currencies priced in the base, in the order shown.</summary>
    public List<string> Currencies { get; set; } = [.. ExchangeRules.DefaultQuotes];

    /// <summary>[S-6167] The widget's look: the list, or the first currency's chart.</summary>
    public ExchangeRatesStyle Style { get; set; } = ExchangeRatesStyle.List;

    IReadOnlyList<StyleOption> IStyled.Styles => RateStyles;

    int IStyled.StyleValue
    {
        get => (int)Style;
        set => Style = (ExchangeRatesStyle)value;
    }

    /// <summary>The base in force: the stored one, or the Windows region's currency ([S-5143]).</summary>
    [JsonIgnore]
    public string EffectiveBase => BaseCurrency.Length > 0 ? BaseCurrency : RegionCurrency.Current;

    /// <summary>The currencies to show: the list without the base itself (one CHF is one CHF).</summary>
    [JsonIgnore]
    public IReadOnlyList<string> EffectiveQuotes
    {
        get
        {
            var basis = EffectiveBase;
            return [.. Currencies.Where(c => !string.Equals(c, basis, StringComparison.Ordinal))];
        }
    }

    public override WidgetConfig Clone() => new ExchangeRatesConfig
    {
        BaseCurrency = BaseCurrency,
        Currencies = [.. Currencies],
        Style = Style,
    };

    /// <summary>[S-5144] Codes the ECB does not publish are dropped, loudly; an empty list is the default.</summary>
    public override IReadOnlyList<string> Repair()
    {
        var repairs = new List<string>();

        if (BaseCurrency.Length > 0 && ExchangeRules.Normalise(BaseCurrency) is not { } basis)
        {
            repairs.Add($"exchange rates: base '{Clip(BaseCurrency)}' is not a published currency; following the region");
            BaseCurrency = string.Empty;
        }
        else if (BaseCurrency.Length > 0)
        {
            BaseCurrency = ExchangeRules.Normalise(BaseCurrency)!;
        }

        var kept = new List<string>();
        foreach (var code in Currencies ?? [])
        {
            if (ExchangeRules.Normalise(code) is not { } known)
            {
                repairs.Add($"exchange rates: '{Clip(code)}' is not a published currency; dropped");
            }
            else if (!kept.Contains(known))
            {
                kept.Add(known);
            }
        }

        if (kept.Count == 0)
        {
            repairs.Add("exchange rates: no currency left; the defaults");
            kept.AddRange(ExchangeRules.DefaultQuotes);
        }

        Currencies = kept;
        return repairs;
    }

    private static string Clip(string? text) => text is null ? "null" : text.Length <= 8 ? text : text[..8];
}
