using Themia.App.Widgets;
using Themia.Core.Model;
using Themia.Core.Settings;
using Themia.Sdk.WinUI;

namespace Themia.App.Shell.Config;

/// <summary>
/// [S-6174] The weather summary's parts' options, all on their page ([S-3719]): Appearance, then behind a divider the
/// location's search ([S-3722]); the temperature unit and how often the weather is asked are the settings', followed, never
/// the widget's own ([S-3590]). What each shows is its kind.
/// </summary>
internal static class WeatherNowMenu
{
    public static void Lines(WidgetMenuLines menu, WeatherNowPartConfig config)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);

        // The look (Appearance), a divider, then the place.
        menu.Lines.Add(SettingsLine.Separator);
        menu.Lines.AddRange(WeatherPlaceRows.Lines(menu, config));
    }
}
