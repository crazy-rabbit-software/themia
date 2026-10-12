using Themia.Core.Localization;
using Themia.Core.Model;

namespace Themia.Core.Widgets;

/// <summary>[S-5104]..[S-5145] The words and figures the three online widgets print, in one place.</summary>
public static class OnlineText
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

    /// <summary>[S-5124] The rain headline in words.</summary>
    public static string Headline(RainOutlook outlook)
    {
        ArgumentNullException.ThrowIfNull(outlook);
        var minutes = ("minutes", LocaleFormat.Integer(outlook.Minutes));
        return outlook.Headline switch
        {
            RainHeadline.StartsIn => Str.Online_Rain_StartsIn.Format(minutes),
            RainHeadline.StopsIn => Str.Online_Rain_StopsIn.Format(minutes),
            RainHeadline.Continues => Str.Online_Rain_Continues.Text(),
            RainHeadline.Dry => Str.Online_Rain_Dry.Text(),
            _ => Str.Online_Rain_Outdated.Text(),
        };
    }

    public static string Intensity(RainIntensity intensity) => (intensity switch
    {
        RainIntensity.Heavy => Str.Online_Rain_Intensity_Heavy,
        RainIntensity.Moderate => Str.Online_Rain_Intensity_Moderate,
        _ => Str.Online_Rain_Intensity_Light,
    }).Text();

    /// <summary>
    /// [S-5124] The caption under the headline: the heaviest rain in the window and the total, or the
    /// hour's chance when it stays dry; parts the window has nothing for are left out.
    /// </summary>
    public static string Caption(RainOutlook outlook)
    {
        ArgumentNullException.ThrowIfNull(outlook);

        var parts = new List<string>(3);
        var heaviest = outlook.Steps.Count == 0 ? RainIntensity.None : outlook.Steps.Max(s => RainRules.Intensity(s.Millimetres));

        if (outlook.Headline != RainHeadline.Outdated && heaviest > RainIntensity.None)
        {
            parts.Add(Intensity(heaviest));
            parts.Add(Str.Online_Rain_Total.Format(("amount", LocaleFormat.Number(outlook.Total, 1))));
        }

        if (outlook.Chance is { } chance && (heaviest > RainIntensity.None || chance > 0))
        {
            parts.Add(Str.Online_Rain_Chance.Format(("percent", LocaleFormat.Percent(Strings.Current, chance))));
        }

        return string.Join(" · ", parts);
    }

    /// <summary>[S-5145] A day change, "+0.21%" / "−0.08%", or empty when there is no earlier day.</summary>
    public static string Change(double? percent) => percent is { } p ? MarketQuote.ChangePercent(p) : string.Empty;
}
