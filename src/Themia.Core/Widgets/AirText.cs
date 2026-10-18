using Themia.Core.Localization;
using Themia.Core.Model;

namespace Themia.Core.Widgets;

/// <summary>[S-5104]..[S-5106] The words and figures the air widget prints.</summary>
public static class AirText
{
    /// <summary>[S-5104] A band's name on its scale.</summary>
    public static string Band(AirQualityScale scale, AirBand band) => (scale == AirQualityScale.Us
        ? band switch
        {
            AirBand.Best => Str.Online_Air_Us_Band0,
            AirBand.Second => Str.Online_Air_Us_Band1,
            AirBand.Third => Str.Online_Air_Us_Band2,
            AirBand.Fourth => Str.Online_Air_Us_Band3,
            AirBand.Fifth => Str.Online_Air_Us_Band4,
            _ => Str.Online_Air_Us_Band5,
        }
        : band switch
        {
            AirBand.Best => Str.Online_Air_Eu_Band0,
            AirBand.Second => Str.Online_Air_Eu_Band1,
            AirBand.Third => Str.Online_Air_Eu_Band2,
            AirBand.Fourth => Str.Online_Air_Eu_Band3,
            AirBand.Fifth => Str.Online_Air_Eu_Band4,
            _ => Str.Online_Air_Eu_Band5,
        }).Text();

    /// <summary>The short name of a scale, written as the index's unit: "EAQI", "US AQI".</summary>
    public static string ScaleUnit(AirQualityScale scale)
        => (scale == AirQualityScale.Us ? Str.Online_Air_Unit_Us : Str.Online_Air_Unit_Eu).Text();

    /// <summary>An index as a whole number, as both agencies publish it.</summary>
    public static string Index(double value) => LocaleFormat.Integer((long)Math.Round(value));

    public static string Uv(UvLevel level) => (level switch
    {
        UvLevel.Low => Str.Online_Air_UvLevel_Low,
        UvLevel.Moderate => Str.Online_Air_UvLevel_Moderate,
        UvLevel.High => Str.Online_Air_UvLevel_High,
        UvLevel.VeryHigh => Str.Online_Air_UvLevel_VeryHigh,
        _ => Str.Online_Air_UvLevel_Extreme,
    }).Text();

    /// <summary>[S-5105] "2.5 · Low": one decimal, as the services publish it, and the category.</summary>
    public static string UvReading(double index)
        => $"{LocaleFormat.Number(index, 1)} · {Uv(AirQualityRules.Uv(index))}";

    /// <summary>A concentration in µg/m³, a whole number from 10 up, one decimal below.</summary>
    public static string Concentration(double value)
        => $"{LocaleFormat.Number(value, value < 10 ? 1 : 0)} µg/m³";

    public static string Pollen(PollenType type) => (type switch
    {
        PollenType.Alder => Str.Online_Air_PollenType_Alder,
        PollenType.Birch => Str.Online_Air_PollenType_Birch,
        PollenType.Grass => Str.Online_Air_PollenType_Grass,
        PollenType.Mugwort => Str.Online_Air_PollenType_Mugwort,
        PollenType.Olive => Str.Online_Air_PollenType_Olive,
        _ => Str.Online_Air_PollenType_Ragweed,
    }).Text();

    public static string Level(PollenLevel level) => (level switch
    {
        PollenLevel.High => Str.Online_Air_PollenLevel_High,
        PollenLevel.Moderate => Str.Online_Air_PollenLevel_Moderate,
        _ => Str.Online_Air_PollenLevel_Low,
    }).Text();

    /// <summary>[S-5106] The worst pollen and its level, "Grass · Moderate", or "None in the air".</summary>
    public static string PollenSummary(IReadOnlyList<(PollenType Type, PollenLevel Level)> pollen)
    {
        ArgumentNullException.ThrowIfNull(pollen);
        return pollen.Count == 0
            ? Str.Online_Air_PollenNone.Text()
            : $"{Pollen(pollen[0].Type)} · {Level(pollen[0].Level)}";
    }
}
