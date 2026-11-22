using Themia.App.Widgets;
using Themia.Core.Model;
using Themia.Core.Settings;
using Themia.Core.Sync;
using Themia.Sdk;
using Themia.Sdk.WinUI;

namespace Themia.App.Shell.Config;

/// <summary>
/// [S-6174] The reading's options, all on its page ([S-3719]): Style and Appearance, then
/// the location's search ([S-3722]); the temperature unit and how often the weather is asked are the settings',
/// followed, never the widget's own ([S-3590]). What it shows is its style alone: nothing under Style.
/// </summary>
internal static class WeatherNowMenu
{
    public static void Lines(WidgetMenuLines menu, WeatherNowConfig config)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);

        // The look (Style, Appearance), a divider, then the place.
        menu.Lines.Add(SettingsLine.Separator);
        menu.Lines.AddRange(WeatherPlaceRows.Lines(menu, config));
    }
}
