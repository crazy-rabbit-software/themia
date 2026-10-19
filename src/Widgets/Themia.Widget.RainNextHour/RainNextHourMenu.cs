using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Sdk.WinUI;

namespace Themia.App.Shell.Config;

/// <summary>
/// [S-5127] The rain widget's options, all on its properties menu ([S-3719]): Appearance ([S-3723]),
/// the weather family's location search ([S-2840], [S-3722]) with the location-name switch under it,
/// and the source's credit. Rain has nothing else to choose. It has no settings page.
/// </summary>
internal static class RainNextHourMenu
{
    public static void Lines(WidgetMenuLines menu, RainNextHourConfig config)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);

        menu.PlaceCard();
        menu.Lines.Add(WeatherPlaceRows.LocationLine(menu, config));
        menu.Lines.Add(WidgetMenuLines.Note(Str.Online_Source.Format(("name", "Open-Meteo"))));
    }
}
