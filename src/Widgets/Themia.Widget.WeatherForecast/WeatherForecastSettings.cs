using Microsoft.UI.Xaml;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Widget.WeatherForecast;

namespace Themia.App.Shell.Config;

/// <summary>The forecast widget's settings rows.</summary>
internal static class WeatherForecastSettings
{
    public static void Rows(List<FrameworkElement> rows, WeatherForecastConfig config, Action save)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(save);

        // The same place picker the current-conditions widget uses, not a copy of it. The original
        // app carried two line-for-line duplicates of its city search and filed a note about it.
        rows.Add(FluentLayout.Header(Str.WidgetsC_Weather_Settings_Place.Text()));
        WeatherPlaceRows.AddPlacePicker(rows, config, save);

        rows.Add(FluentLayout.Header(WeatherForecastStr.Forecast_Title.Text()));
        WeatherPlaceRows.AddWeatherDisplay(rows, config, save);

        rows.Add(FluentLayout.Row(Glyphs.Droplet, WeatherForecastStr.WidgetsC_Weather_Settings_ShowPrecipitation_Label.Text(),
            WeatherForecastStr.WidgetsC_Weather_Settings_ShowPrecipitation_Desc.Text(),
            FluentLayout.Toggle(config.ShowPrecipitation, value => { config.ShowPrecipitation = value; save(); })));

        rows.Add(FluentLayout.Row(Glyphs.Calendar, WeatherForecastStr.Forecast_DaysLabel.Text(), WeatherForecastStr.Forecast_DaysDesc.Text(),
            FluentLayout.Slider(config.EffectiveDays, 1, 14, 1, string.Empty,
                value => { config.Days = (int)Math.Round(value); save(); })));
    }
}
