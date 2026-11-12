using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Widget.AirQuality;

namespace Themia.Core.Widgets;

/// <summary>[S-5104] The words and figures the air index prints; the pollutants' and pollen's are the air facts'.</summary>
public static class AirText
{
    /// <summary>[S-5104] A band's name on its scale.</summary>
    public static string Band(AirQualityScale scale, AirBand band) => (scale == AirQualityScale.Us
        ? band switch
        {
            AirBand.Best => AirQualityStr.Online_Air_Us_Band0,
            AirBand.Second => AirQualityStr.Online_Air_Us_Band1,
            AirBand.Third => AirQualityStr.Online_Air_Us_Band2,
            AirBand.Fourth => AirQualityStr.Online_Air_Us_Band3,
            AirBand.Fifth => AirQualityStr.Online_Air_Us_Band4,
            _ => AirQualityStr.Online_Air_Us_Band5,
        }
        : band switch
        {
            AirBand.Best => AirQualityStr.Online_Air_Eu_Band0,
            AirBand.Second => AirQualityStr.Online_Air_Eu_Band1,
            AirBand.Third => AirQualityStr.Online_Air_Eu_Band2,
            AirBand.Fourth => AirQualityStr.Online_Air_Eu_Band3,
            AirBand.Fifth => AirQualityStr.Online_Air_Eu_Band4,
            _ => AirQualityStr.Online_Air_Eu_Band5,
        }).Text();

    /// <summary>The short name of a scale, written as the index's unit: "EAQI", "US AQI".</summary>
    public static string ScaleUnit(AirQualityScale scale)
        => (scale == AirQualityScale.Us ? AirQualityStr.Online_Air_Unit_Us : AirQualityStr.Online_Air_Unit_Eu).Text();

    /// <summary>An index as a whole number, as both agencies publish it.</summary>
    public static string Index(double value) => LocaleFormat.Integer((long)Math.Round(value));
}
