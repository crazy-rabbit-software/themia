using Themia.App.Services;
using Themia.App.Widgets;
using Themia.Core.Model;
using Themia.Sdk.WinUI;

namespace Themia.App.Shell.Config;

/// <summary>
/// [S-3345] The donut's options, all on its properties menu ([S-3719]): Appearance ([S-3723]), the
/// period, then the limits and hidden apps every screen-time widget shares ([S-3323]). It has no
/// settings page.
/// </summary>
internal static class ScreenTimeDonutMenu
{
    public static void Lines(WidgetMenuLines menu, ScreenTimeDonutConfig config, IScreenTimeSource? source)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);

        menu.PlaceCard();
        menu.Lines.Add(ScreenTimeMenuLines.Period(menu, config.Period, value => config.Period = value));
        ScreenTimeMenuLines.AddShared(menu, config, source);
    }
}
