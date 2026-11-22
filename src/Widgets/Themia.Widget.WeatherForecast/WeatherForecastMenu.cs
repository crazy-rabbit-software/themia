using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Settings;
using Themia.Core.Sync;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Themia.Widget.WeatherForecast;

namespace Themia.App.Shell.Config;

/// <summary>
/// [S-2860] The forecast curve's options, all on its page ([S-3719]): Appearance
/// ([S-3723]), the location's search ([S-3722]) with the location-name switch after it and the precipitation
/// switch. How many days is the card's ([S-14410]): no line sets it. It has no settings page.
/// </summary>
internal static class WeatherForecastMenu
{
    public static void Lines(WidgetMenuLines menu, WeatherForecastConfig config)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);

        // The look (Appearance), then behind a divider each: the place; what each day shows. [S-14410] How many
        // days is the card's: what its box holds, never stored ([S-14502]).
        menu.Lines.Add(SettingsLine.Separator);
        menu.Lines.AddRange(WeatherPlaceRows.Lines(menu, config));
        menu.Lines.Add(SettingsLine.Separator);
        menu.Lines.Add(menu.Toggle(Glyphs.Droplet, WeatherForecastStr.WidgetsC_Weather_Settings_ShowPrecipitation_Label.Text(), config.ShowPrecipitation, on => config.ShowPrecipitation = on));
    }
}
