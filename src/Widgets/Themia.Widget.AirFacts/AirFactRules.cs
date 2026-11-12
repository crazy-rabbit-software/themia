using System.Text.Json.Serialization;
using Themia.App.Services.Web;
using Themia.Core.Localization;
using Themia.Core.Widgets;

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
}

/// <summary>One labelled reading: "PM2.5" / "7.7 µg/m³".</summary>
public readonly record struct AirFact(AirFactKind Kind, string Label, string Value);

/// <summary>
/// [S-5105] [S-5106] [S-11135] The air facts' readings from the air answer, through the SDK's
/// <see cref="AirQualityRules"/> (the index's own): UV "2.5 · Low", a concentration in µg/m³ (a whole
/// number from 10, one decimal under it), the worst pollen "Grass · Moderate" or "None in the air", one
/// pollen's level. A reading the service did not send (no pollen forecast outside Europe) is the em dash.
/// </summary>
public static class AirFactRules
{
    public static IReadOnlyList<AirFactKind> All { get; } = Enum.GetValues<AirFactKind>();

    /// <summary>[S-11135] A new card's rows: UV, PM2.5, pollen, PM10.</summary>
    public static IReadOnlyList<AirFactKind> Defaults { get; } = [AirFactKind.Uv, AirFactKind.Pm25, AirFactKind.Pollen, AirFactKind.Pm10];

    public const string Missing = "—";

    public static string Name(AirFactKind kind) => kind switch
    {
        AirFactKind.Uv => AirFactsStr.Online_Air_Uv.Text(),
        AirFactKind.Pm25 => AirFactsStr.Online_Air_Pm25.Text(),
        AirFactKind.Pollen => AirFactsStr.Online_Air_Pollen.Text(),
        AirFactKind.Pm10 => AirFactsStr.Online_Air_Pm10.Text(),
        AirFactKind.Ozone => AirFactsStr.Online_Air_Ozone.Text(),
        _ => PollenName(Type(kind)),
    };

    /// <summary>[S-11135] The picked readings, one per pick, in its order.</summary>
    public static IReadOnlyList<AirFact> For(IReadOnlyList<AirFactKind> picked, AirReading air)
    {
        ArgumentNullException.ThrowIfNull(picked);
        ArgumentNullException.ThrowIfNull(air);
        return [.. picked.Select(kind => new AirFact(kind, Name(kind), Value(kind, air)))];
    }

    public static string Value(AirFactKind kind, AirReading air)
    {
        ArgumentNullException.ThrowIfNull(air);
        return kind switch
        {
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
