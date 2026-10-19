using Themia.App.Services;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Themia.Widget.ScreenTimeApps;

namespace Themia.App.Shell.Config;

/// <summary>
/// [S-3356] The app list's options, all on its properties menu ([S-3719]): Appearance ([S-3723]), the
/// period, how many apps, the figures beside each (an average only over a window, [S-6074]); then the
/// limits and hidden apps every screen-time widget shares ([S-3323]). It has no settings page.
/// </summary>
internal static class ScreenTimeAppsMenu
{
    public static void Lines(WidgetMenuLines menu, ScreenTimeAppsConfig config, IScreenTimeSource? source)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);

        menu.PlaceCard();
        menu.Lines.Add(ScreenTimeMenuLines.Period(menu, config.Period, value => config.Period = value));

        // The list's own: how many apps and the figures beside each, apart from the period.
        menu.Lines.Add(ItemMenuEntry.Separator);

        // [S-1211] A ceiling over what the tile has room for; 0 (the default) is as many as fit.
        var most = ScreenTimeRules.EffectiveMaxApps(config.MaxApps);
        var mostLine = menu.Number(Glyphs.Count, ScreenTimeAppsStr.WidgetsC_ScreenTime_Settings_Most_Label.Text(), most, 0, ScreenTimeRules.MaxAppsLimit, 1,
            n => LocaleFormat.Integer(n), value => config.MaxApps = value);
        menu.Lines.Add(most == 0 ? mostLine with { Value = ScreenTimeAppsStr.WidgetsC_ScreenTime_Settings_Most_Fit.Text() } : mostLine);

        if (config.Offers(ScreenTimeSetting.Average))
        {
            menu.Lines.Add(menu.Toggle(Glyphs.Hours, ScreenTimeAppsStr.Screentime_Metric_Average.Text(), config.ShowAverage, on => config.ShowAverage = on));
        }

        menu.Lines.Add(menu.Toggle(Glyphs.Count, ScreenTimeAppsStr.Screentime_Metric_Percent.Text(), config.ShowPercent, on => config.ShowPercent = on));

        ScreenTimeMenuLines.AddShared(menu, config, source);
    }
}
