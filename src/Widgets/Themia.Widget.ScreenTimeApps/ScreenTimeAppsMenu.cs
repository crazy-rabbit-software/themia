using Themia.App.Services;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Themia.Widget.ScreenTimeApps;

namespace Themia.App.Shell.Config;

/// <summary>
/// [S-3356] The app list's options, all on its properties menu ([S-3719]): Appearance ([S-3723]), the
/// period, its rows and lines, the figures on the second line (an average only over a window, [S-6074]); then the
/// limits and hidden apps every screen-time widget shares ([S-3323]). It has no settings page.
/// </summary>
internal static class ScreenTimeAppsMenu
{
    public static void Lines(WidgetMenuLines menu, ScreenTimeAppsConfig config, IScreenTimeSource? source)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);

        menu.Lines.Add(ScreenTimeMenuLines.Period(menu, config.Period, value => config.Period = value));

        // The list's own: how many apps and the figures beside each, apart from the period.
        menu.Lines.Add(ItemMenuEntry.Separator);

        // [S-11510] Rows (the height follows), and Lines where a row has figures for a second one.
        menu.Lines.AddRange(KitMenuLines.Rows(menu, config));

        if (config.Offers(ScreenTimeSetting.Average))
        {
            menu.Lines.Add(menu.Toggle(Glyphs.Hours, ScreenTimeAppsStr.Screentime_Metric_Average.Text(), config.ShowAverage, on => config.ShowAverage = on));
        }

        menu.Lines.Add(menu.Toggle(Glyphs.ScreenTimeDonut, ScreenTimeAppsStr.Screentime_Metric_Percent.Text(), config.ShowPercent, on => config.ShowPercent = on));

        ScreenTimeMenuLines.AddShared(menu, config, source);
    }
}
