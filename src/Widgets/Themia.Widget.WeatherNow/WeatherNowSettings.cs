using Microsoft.UI.Xaml;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Widget.WeatherNow;

namespace Themia.App.Shell.Config;

/// <summary>The current-conditions widget's settings rows.</summary>
internal static class WeatherNowSettings
{
    public static void Rows(List<FrameworkElement> rows, WeatherNowConfig config, Action save)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(save);

        rows.Add(FluentLayout.Header(Str.WidgetsC_Weather_Settings_Place.Text()));
        WeatherPlaceRows.AddPlacePicker(rows, config, save);

        rows.Add(FluentLayout.Header(WeatherNowStr.Weather_DefaultLabel.Text()));
        WeatherPlaceRows.AddWeatherDisplay(rows, config, save);

        // [S-6074] Only the standard style draws the readings line.
        if (config.Offers(WeatherNowSetting.Details))
        {
            rows.Add(FluentLayout.Row(Glyphs.Info, WeatherNowStr.WidgetsC_Weather_Settings_ShowDetail_Label.Text(),
                WeatherNowStr.WidgetsC_Weather_Settings_ShowDetail_Desc.Text(),
                FluentLayout.Toggle(config.ShowDetails, value => { config.ShowDetails = value; save(); })));
        }
    }
}
