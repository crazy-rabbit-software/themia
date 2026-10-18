using Microsoft.UI.Xaml;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Widget.RainNextHour;

namespace Themia.App.Shell.Config;

/// <summary>[S-5127] The rain widget's settings page: only what changes what it shows.</summary>
internal static class RainNextHourSettings
{
    /// <summary>[S-5127] The place and the location name: rain has nothing else to choose.</summary>
    public static void Rows(List<FrameworkElement> rows, RainNextHourConfig config, Action save)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(save);

        rows.Add(FluentLayout.Header(Str.WidgetsC_Weather_Settings_Place.Text()));
        WeatherPlaceRows.AddPlacePicker(rows, config, save);

        rows.Add(FluentLayout.Header(RainNextHourStr.Online_Rain_Label.Text()));
        WeatherPlaceRows.AddWeatherDisplay(rows, config, save);
        rows.Add(FluentLayout.Note(Str.Online_Source.Format(("name", "Open-Meteo"))));
    }
}
