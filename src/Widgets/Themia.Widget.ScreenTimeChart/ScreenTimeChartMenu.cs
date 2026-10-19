using Themia.App.Services;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Themia.Widget.ScreenTimeChart;

namespace Themia.App.Shell.Config;

/// <summary>
/// [S-3335] The chart's options, all on its properties menu ([S-3719]): Appearance ([S-3723]), the
/// period (a week or a month: a day is the donut's and the list's, [S-3330]), bars or a calendar;
/// then the limits and hidden apps every screen-time widget shares ([S-3323]). It has no settings page.
/// </summary>
internal static class ScreenTimeChartMenu
{
    public static void Lines(WidgetMenuLines menu, ScreenTimeChartConfig config, IScreenTimeSource? source)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);

        menu.PlaceCard();
        menu.Lines.Add(menu.Choice(Glyphs.Calendar, Str.Screentime_Cfg_Period.Text(),
            [(ScreenTimePeriod.Week, ScreenTimeRules.PeriodLabel(ScreenTimePeriod.Week)), (ScreenTimePeriod.Month, ScreenTimeRules.PeriodLabel(ScreenTimePeriod.Month))],
            ScreenTimeRules.ChartPeriod(config.Period), value => config.Period = value));
        menu.Lines.Add(menu.Choice(Glyphs.Palette, ScreenTimeChartStr.Screentime_Cfg_Style.Text(),
            [(ScreenTimeChartStyle.Bars, ScreenTimeChartStr.Screentime_Cfg_StyleBars.Text()), (ScreenTimeChartStyle.Heatmap, ScreenTimeChartStr.WidgetsC_ScreenTime_Settings_StyleCalendar.Text())],
            ScreenTimeRules.ClampChartStyle(config.Style), value => config.Style = value));
        ScreenTimeMenuLines.AddShared(menu, config, source);
    }
}
