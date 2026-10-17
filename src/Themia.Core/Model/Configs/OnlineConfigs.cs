using System.Text.Json.Serialization;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Themia.Sdk;

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

    /// <summary>[S-6902] This widget's descriptor: what the host knows of it without its UI.</summary>
    public static WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("airQuality"),
        Kind = WidgetKind.AirQuality,
        ConfigType = typeof(AirQualityConfig),
        CreateDefault = () => new AirQualityConfig(),
        Json = WidgetConfigJsonContext.Default,
        Keywords = ["aqi", "pollution", "smog", "pm2.5", "particulates", "ozone", "uv", "sunburn", "pollen", "allergy", "hay fever"],
        LabelId = Str.Online_Air_Label,
        DescriptionId = Str.Online_Air_Desc,
        Glyph = Glyphs.AirQuality,
        DefaultWidth = 260,
        DefaultHeight = 200,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader,
    };

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

    /// <summary>[S-6902] This widget's descriptor: what the host knows of it without its UI.</summary>
    public static WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("rainNextHour"),
        Kind = WidgetKind.RainNextHour,
        ConfigType = typeof(RainNextHourConfig),
        CreateDefault = () => new RainNextHourConfig(),
        Json = WidgetConfigJsonContext.Default,
        Keywords = ["rain", "precipitation", "shower", "umbrella", "radar", "nowcast", "drizzle", "weather"],
        LabelId = Str.Online_Rain_Label,
        DescriptionId = Str.Online_Rain_Desc,
        Glyph = Glyphs.Rain,
        DefaultWidth = 300,
        DefaultHeight = 180,
        DescriptionFacts = [("minutes", (long)RainRules.Step.TotalMinutes)],
        Traits = WidgetTraits.CanHaveHeader,
    };

    public override WidgetConfig Clone()
    {
        var copy = new RainNextHourConfig();
        CopyTo(copy);
        return copy;
    }
}

/// <summary>
/// [S-5140] The ECB's reference rate of one pair: what one unit of the base currency buys of the
/// quote. Several pairs are several widgets.
/// </summary>
public sealed class ExchangeRatesConfig : WidgetConfig, IStyled
{
    private static readonly StyleOption[] RateStyles =
    [
        StyleOption.Of(ExchangeRatesStyle.Chart, Str.Styles_Rates_Chart, 0xE9D2),
        StyleOption.Of(ExchangeRatesStyle.Number, Str.Styles_Rates_Number, 0xE75F),
    ];

    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.ExchangeRates;

    /// <summary>[S-6902] This widget's descriptor: what the host knows of it without its UI.</summary>
    public static WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("exchangeRates"),
        Kind = WidgetKind.ExchangeRates,
        ConfigType = typeof(ExchangeRatesConfig),
        CreateDefault = () => new ExchangeRatesConfig(),
        Json = WidgetConfigJsonContext.Default,
        Keywords = ["currency", "forex", "fx", "exchange", "money", "euro", "dollar", "franc", "pound", "ecb", "conversion"],
        LabelId = Str.Online_Rates_Label,
        DescriptionId = Str.Online_Rates_Desc,
        Glyph = Glyphs.ExchangeRates,
        DefaultWidth = 300,
        DefaultHeight = 220,
        Traits = WidgetTraits.CanHaveHeader,
    };

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
