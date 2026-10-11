using Microsoft.UI.Xaml;
using Themia.Core.Localization;
using Themia.Core.Model;

namespace Themia.App.Shell.Config;

/// <summary>The forecast widget's settings rows.</summary>
internal static partial class WidgetSettings
{
    public static void WeatherForecast(List<FrameworkElement> rows, WeatherForecastConfig config, Action save)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(save);

        // The same place picker the current-conditions widget uses, not a copy of it. The original
        // app carried two line-for-line duplicates of its city search and filed a note about it.
        rows.Add(FluentLayout.Header(Str.WidgetsC_Weather_Settings_Place.Text()));
        AddPlacePicker(rows, config, save);

        rows.Add(FluentLayout.Header(Str.Forecast_Title.Text()));
        AddWeatherDisplay(rows, config, save);

        // [S-724] Auto follows the tile: columns on a strip, rows otherwise.
        rows.Add(FluentLayout.Row(Glyphs.ListLayout, Str.Forecast_LayoutLabel.Text(), Str.Forecast_LayoutDesc.Text(),
            FluentLayout.Choice(
                [
                    (ForecastLayout.Auto, Str.WidgetsC_Weather_Settings_LayoutAuto.Text()),
                    (ForecastLayout.Rows, Str.Forecast_LayoutRows.Text()),
                    (ForecastLayout.Columns, Str.Forecast_LayoutColumns.Text()),
                    (ForecastLayout.Grid, Str.Forecast_LayoutGrid.Text()),
                ],
                config.Layout,
                value => { config.Layout = value; save(); })));

        rows.Add(FluentLayout.Row(Glyphs.Droplet, Str.WidgetsC_Weather_Settings_ShowPrecipitation_Label.Text(),
            Str.WidgetsC_Weather_Settings_ShowPrecipitation_Desc.Text(),
            FluentLayout.Toggle(config.ShowPrecipitation, value => { config.ShowPrecipitation = value; save(); })));

        rows.Add(FluentLayout.Row(Glyphs.Calendar, Str.Forecast_DaysLabel.Text(), Str.Forecast_DaysDesc.Text(),
            FluentLayout.Slider(config.EffectiveDays, 1, 14, 1, string.Empty,
                value => { config.Days = (int)Math.Round(value); save(); })));
    }
}
