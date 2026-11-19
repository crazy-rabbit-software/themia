using Themia.App.Shell;
using Themia.App.Shell.Config;
using Themia.Sdk.WinUI;

namespace Themia.Widget.WeatherFacts;

/// <summary>
/// [S-13903] A weather fact's options, all on its properties menu ([S-3719]): Appearance and the place's search
/// ([S-3722]); its fact is its kind, never a setting; the temperature unit and the weather's cadence are ⚙'s,
/// followed ([S-3590]).
/// </summary>
internal static class WeatherFactMenu
{
    public static void Lines(WidgetMenuLines menu, WeatherFactConfig config)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);
        menu.Lines.AddRange(WeatherPlaceRows.Lines(menu, config));
    }
}
