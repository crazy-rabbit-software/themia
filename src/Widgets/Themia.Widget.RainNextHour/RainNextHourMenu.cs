using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Sdk.WinUI;
using Themia.Core.Sync;
using Themia.Sdk;

namespace Themia.App.Shell.Config;

/// <summary>
/// [S-5127] [S-14940] The rain widgets' options, all on their page ([S-3719]): Appearance ([S-3723]),
/// the weather family's location search ([S-2840], [S-3722]) and the source's credit. Rain has nothing else to choose. It has no settings page.
/// </summary>
internal static class RainNextHourMenu
{
    public static void Lines(WidgetMenuLines menu, RainConfig config)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);

        // Appearance, then behind a divider each: the place; the data's credit.
        menu.Lines.Add(ItemMenuEntry.Separator);
        menu.Lines.AddRange(WeatherPlaceRows.Lines(menu, config));
        menu.Lines.Add(ItemMenuEntry.Separator);
        menu.Lines.Add(WidgetMenuLines.Note(Str.Online_Source.Format(("name", "Open-Meteo"))));
    }
}
