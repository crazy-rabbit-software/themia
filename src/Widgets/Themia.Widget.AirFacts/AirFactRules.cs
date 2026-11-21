using System.Text.Json.Serialization;
using Themia.App.Services.Web;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.App.Shell;

namespace Themia.Widget.AirFacts;

/// <summary>[S-11135] One row the air facts can show; the order is the kind's own (what a taller card adds next).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<AirFactKind>))]
public enum AirFactKind
{
    /// <summary>[S-5105] The UV index and its WHO category.</summary>
    Uv,
    Pm25,

    /// <summary>[S-5106] The worst pollen in the air and its level.</summary>
    Pollen,
    Pm10,
    Ozone,
    Alder,
    Birch,
    Grass,
    Mugwort,
    Olive,
    Ragweed,

    /// <summary>[S-14714] The air quality index and its band, "25 · Fair", on the place's scale ([S-5103]: European or US).</summary>
    Index,
}

/// <summary>
/// [S-5105] [S-5106] [S-11135] [S-14680] The air's readings from the air answer, through the SDK's
/// <see cref="AirQualityRules"/> (the index's own): UV "2.5 · Low", a concentration in µg/m³ (a whole
/// number from 10, one decimal under it), the worst pollen "Grass · Moderate" or "None in the air", one
/// pollen's level. A reading the service did not send (no pollen forecast outside Europe) is the em dash.
/// </summary>
public static class AirFactRules
{
    public static IReadOnlyList<AirFactKind> All { get; } = Enum.GetValues<AirFactKind>();

    public const string Missing = "—";

    /// <summary>The air's glyph (U+EC0A, a leaf): the index's, the pollen's.</summary>
    public static readonly string Leaf = Glyphs.EnergySaver;

    public static string Name(AirFactKind kind) => kind switch
    {
        AirFactKind.Uv => AirFactsStr.Online_Air_Uv.Text(),
        AirFactKind.Pm25 => AirFactsStr.Online_Air_Pm25.Text(),
        AirFactKind.Pollen => AirFactsStr.Online_Air_Pollen.Text(),
        AirFactKind.Pm10 => AirFactsStr.Online_Air_Pm10.Text(),
        AirFactKind.Ozone => AirFactsStr.Online_Air_Ozone.Text(),
        AirFactKind.Index => AirFactsStr.AirFacts_AirIndex_Label.Text(),
        _ => PollenName(Type(kind)),
    };

    /// <summary>
    /// [S-14603] What history a reading has: the pollutants the service sends hour by hour (PM2.5, PM10, ozone) a series,
    /// an arrow and a line over the hours; UV and each pollen the readings the card saw; the worst pollen none (a name).
    /// </summary>
    public static MetricHistory History(AirFactKind kind) => kind switch
    {
        AirFactKind.Pm25 or AirFactKind.Pm10 or AirFactKind.Ozone or AirFactKind.Index => MetricHistory.Series,
        AirFactKind.Pollen => MetricHistory.None,
        _ => MetricHistory.Readings,
    };

    /// <summary>[S-14603] A reading's figure in one of the service's hours, null where the hours carry none; the index on <paramref name="scale"/>.</summary>
    public static double? Hourly(AirFactKind kind, AirHour hour, AirQualityScale scale = AirQualityScale.European)
    {
        ArgumentNullException.ThrowIfNull(hour);
        return kind switch
        {
            AirFactKind.Index => scale == AirQualityScale.Us ? hour.UsAqi : hour.EuropeanAqi,
            AirFactKind.Pm25 => hour.Pm25,
            AirFactKind.Pm10 => hour.Pm10,
            AirFactKind.Ozone => hour.Ozone,
            _ => null,
        };
    }

    /// <summary>[S-14603] [S-11135] A reading as a metric: its value and the number a trend compares; the index on <paramref name="scale"/>.</summary>
    public static MetricValue Metric(AirFactKind kind, AirReading air, AirQualityScale scale = AirQualityScale.European)
    {
        ArgumentNullException.ThrowIfNull(air);
        var text = Value(kind, air, scale);
        double? number = kind switch
        {
            AirFactKind.Index => AirQualityRules.Index(air, scale),
            AirFactKind.Uv => air.Uv,
            AirFactKind.Pm25 => air.Pm25,
            AirFactKind.Pm10 => air.Pm10,
            AirFactKind.Ozone => air.Ozone,
            AirFactKind.Pollen => null,
            _ => air.Pollen.TryGetValue(Type(kind), out var grains) ? grains : null,
        };
        return new MetricValue(text, text == Missing ? null : number);
    }

    public static string Value(AirFactKind kind, AirReading air, AirQualityScale scale = AirQualityScale.European)
    {
        ArgumentNullException.ThrowIfNull(air);
        return kind switch
        {
            AirFactKind.Index => AirQualityRules.Index(air, scale) is { } index ? IndexReading(scale, index) : Missing,
            AirFactKind.Uv => air.Uv is { } uv ? UvReading(uv) : Missing,
            AirFactKind.Pm25 => air.Pm25 is { } pm25 ? Concentration(pm25) : Missing,
            AirFactKind.Pm10 => air.Pm10 is { } pm10 ? Concentration(pm10) : Missing,
            AirFactKind.Ozone => air.Ozone is { } ozone ? Concentration(ozone) : Missing,
            AirFactKind.Pollen => AirQualityRules.HasPollen(air) ? PollenSummary(AirQualityRules.Pollen(air)) : Missing,
            _ => air.Pollen.TryGetValue(Type(kind), out var grains) ? Level(AirQualityRules.Level(Type(kind), grains)) : Missing,
        };
    }

    /// <summary>The pollen a row names.</summary>
    private static PollenType Type(AirFactKind kind) => kind switch
    {
        AirFactKind.Alder => PollenType.Alder,
        AirFactKind.Birch => PollenType.Birch,
        AirFactKind.Grass => PollenType.Grass,
        AirFactKind.Mugwort => PollenType.Mugwort,
        AirFactKind.Olive => PollenType.Olive,
        _ => PollenType.Ragweed,
    };

    public static string Uv(UvLevel level) => (level switch
    {
        UvLevel.Low => AirFactsStr.Online_Air_UvLevel_Low,
        UvLevel.Moderate => AirFactsStr.Online_Air_UvLevel_Moderate,
        UvLevel.High => AirFactsStr.Online_Air_UvLevel_High,
        UvLevel.VeryHigh => AirFactsStr.Online_Air_UvLevel_VeryHigh,
        _ => AirFactsStr.Online_Air_UvLevel_Extreme,
    }).Text();

    /// <summary>[S-5105] "2.5 · Low": one decimal, as the services publish it, and the category.</summary>
    public static string UvReading(double index)
        => $"{LocaleFormat.Number(index, 1)} · {Uv(AirQualityRules.Uv(index))}";

    /// <summary>[S-14714] The index and its band on <paramref name="scale"/>: "25 · Fair" (the air widget's words, [S-5104]).</summary>
    public static string IndexReading(AirQualityScale scale, double index)
        => $"{LocaleFormat.Number(index, 0)} · {Band(scale, AirQualityRules.Band(scale, index))}";

    /// <summary>[S-5104] A band's name on <paramref name="scale"/>: the European words, or the US index's.</summary>
    public static string Band(AirQualityScale scale, AirBand band) => (scale == AirQualityScale.Us
        ? band switch
        {
            AirBand.Best => AirFactsStr.Online_Air_Us_Band0,
            AirBand.Second => AirFactsStr.Online_Air_Us_Band1,
            AirBand.Third => AirFactsStr.Online_Air_Us_Band2,
            AirBand.Fourth => AirFactsStr.Online_Air_Us_Band3,
            AirBand.Fifth => AirFactsStr.Online_Air_Us_Band4,
            _ => AirFactsStr.Online_Air_Us_Band5,
        }
        : band switch
        {
            AirBand.Best => AirFactsStr.Online_Air_Eu_Band0,
            AirBand.Second => AirFactsStr.Online_Air_Eu_Band1,
            AirBand.Third => AirFactsStr.Online_Air_Eu_Band2,
            AirBand.Fourth => AirFactsStr.Online_Air_Eu_Band3,
            AirBand.Fifth => AirFactsStr.Online_Air_Eu_Band4,
            _ => AirFactsStr.Online_Air_Eu_Band5,
        }).Text();

    /// <summary>A concentration in µg/m³, a whole number from 10 up, one decimal below.</summary>
    public static string Concentration(double value)
        => $"{LocaleFormat.Number(value, value < 10 ? 1 : 0)} µg/m³";

    public static string PollenName(PollenType type) => (type switch
    {
        PollenType.Alder => AirFactsStr.Online_Air_PollenType_Alder,
        PollenType.Birch => AirFactsStr.Online_Air_PollenType_Birch,
        PollenType.Grass => AirFactsStr.Online_Air_PollenType_Grass,
        PollenType.Mugwort => AirFactsStr.Online_Air_PollenType_Mugwort,
        PollenType.Olive => AirFactsStr.Online_Air_PollenType_Olive,
        _ => AirFactsStr.Online_Air_PollenType_Ragweed,
    }).Text();

    /// <summary>A pollen's level; none in the air is "None in the air".</summary>
    public static string Level(PollenLevel level) => (level switch
    {
        PollenLevel.High => AirFactsStr.Online_Air_PollenLevel_High,
        PollenLevel.Moderate => AirFactsStr.Online_Air_PollenLevel_Moderate,
        PollenLevel.Low => AirFactsStr.Online_Air_PollenLevel_Low,
        _ => AirFactsStr.Online_Air_PollenNone,
    }).Text();

    /// <summary>[S-5106] The worst pollen and its level, "Grass · Moderate", or "None in the air".</summary>
    public static string PollenSummary(IReadOnlyList<(PollenType Type, PollenLevel Level)> pollen)
    {
        ArgumentNullException.ThrowIfNull(pollen);
        return pollen.Count == 0
            ? AirFactsStr.Online_Air_PollenNone.Text()
            : $"{PollenName(pollen[0].Type)} · {Level(pollen[0].Level)}";
    }
}
