using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Sdk.WinUI;
using Themia.Widget.WeatherForecast;

namespace Themia.App.Shell.Config;

/// <summary>
/// [S-2860] The forecast widget's options, all on its properties menu ([S-3719]): Appearance
/// ([S-3723]), the location's search ([S-3722]) with the location-name switch after it, how many
/// days (1 to 14) and the precipitation switch. It has no settings page.
/// </summary>
internal static class WeatherForecastMenu
{
    public static void Lines(WidgetMenuLines menu, WeatherForecastConfig config)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);

        // The look (Style, Appearance), then behind a divider each: the place; what each day shows.
        menu.PlaceCard();
        menu.Lines.Add(ItemMenuEntry.Separator);
        menu.Lines.AddRange(WeatherPlaceRows.Lines(menu, config));
        menu.Lines.Add(ItemMenuEntry.Separator);

        (int, string)[] days = [.. Enumerable.Range(1, 14).Select(n => (n, LocaleFormat.Integer(Strings.Current, n)))];
        menu.Lines.Add(menu.Choice(Glyphs.Calendar, WeatherForecastStr.Forecast_DaysLabel.Text(), days, config.EffectiveDays, value => config.Days = value));
        menu.Lines.Add(menu.Toggle(Glyphs.Droplet, WeatherForecastStr.WidgetsC_Weather_Settings_ShowPrecipitation_Label.Text(), config.ShowPrecipitation, on => config.ShowPrecipitation = on));
    }
}
