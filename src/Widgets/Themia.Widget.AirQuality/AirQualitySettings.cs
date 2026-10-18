using Microsoft.UI.Xaml;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Widget.AirQuality;

namespace Themia.App.Shell.Config;

/// <summary>[S-5108] The air quality widget's settings page: only what changes what it shows.</summary>
internal static class AirQualitySettings
{
    /// <summary>[S-5108] The place (the weather widgets' picker), the location name, the index.</summary>
    public static void Rows(List<FrameworkElement> rows, AirQualityConfig config, Action save)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(save);

        rows.Add(FluentLayout.Header(Str.WidgetsC_Weather_Settings_Place.Text()));
        WeatherPlaceRows.AddPlacePicker(rows, config, save);

        rows.Add(FluentLayout.Header(AirQualityStr.Online_Air_Label.Text()));
        WeatherPlaceRows.AddWeatherDisplay(rows, config, save);

        (AirQualityScale, string)[] scales =
        [
            (AirQualityScale.Auto, AirQualityStr.Online_Air_Settings_Index_Auto.Text()),
            (AirQualityScale.European, AirQualityStr.Online_Air_Settings_Index_European.Text()),
            (AirQualityScale.Us, AirQualityStr.Online_Air_Settings_Index_Us.Text()),
        ];

        rows.Add(FluentLayout.Row(AirQualityModule.Glyph, AirQualityStr.Online_Air_Settings_Index_Label.Text(), AirQualityStr.Online_Air_Settings_Index_Desc.Text(),
            FluentLayout.Choice<AirQualityScale>(scales, config.Scale, value => { config.Scale = value; save(); }),
            "aqi", "eaqi", "european", "us"));

        // CAMS and Open-Meteo ask to be credited.
        rows.Add(FluentLayout.Note(Str.Online_Source.Format(("name", "Copernicus Atmosphere Monitoring Service (CAMS) · Open-Meteo"))));
    }
}
