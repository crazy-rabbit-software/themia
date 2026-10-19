using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Themia.Widget.WeatherNow;

namespace Themia.App.Shell.Config;

/// <summary>
/// [S-2830] The current-conditions widget's options, all on its properties menu ([S-3719]): under
/// Style the readings line (the standard style only, [S-6074]); then Appearance ([S-3723]) and the
/// location's search ([S-3722]) with the location-name switch under it. It has no settings page.
/// </summary>
internal static class WeatherNowMenu
{
    public static void Lines(WidgetMenuLines menu, WeatherNowConfig config)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);

        if (config.Offers(WeatherNowSetting.Details))
        {
            menu.StyleLines.Add(menu.Toggle(null, WeatherNowStr.WidgetsC_Weather_Settings_ShowDetail_Label.Text(), config.ShowDetails, on => config.ShowDetails = on));
        }

        // The look (Style, Appearance), a divider, then the place.
        menu.PlaceCard();
        menu.Lines.Add(ItemMenuEntry.Separator);
        menu.Lines.Add(WeatherPlaceRows.LocationLine(menu, config));
    }
}
