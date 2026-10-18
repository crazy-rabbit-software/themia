using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;

namespace Themia.App.Shell.Config;

/// <summary>
/// Settings rows for the three screen-time widgets (the GPU meter's and the mixer's are their modules').
/// <para>
/// Same shape as the rest of <see cref="WidgetSettings"/>: each control writes straight to the live
/// config and saves, because the widget is on the desktop behind the window and watching it change
/// is the point. Each page has its own widget's rows, then the limits and hidden apps every
/// screen-time widget shares ([S-3322]), which write through the tracker rather than the config.
/// </para>
/// </summary>
internal static partial class WidgetSettings
{
    /// <summary>[S-3330] The chart: a week or a month, as bars or a calendar; then the shared rows.</summary>
    public static void ScreenTimeChart(List<FrameworkElement> rows, ScreenTimeChartConfig config, Action save)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(save);

        rows.Add(FluentLayout.Header(Str.WidgetsC_ScreenTime_Chart_Label.Text()));

        rows.Add(FluentLayout.Row(Glyphs.Calendar, Str.Screentime_Cfg_Period.Text(), Str.WidgetsC_ScreenTime_Settings_ChartPeriod_Desc.Text(),
            FluentLayout.Choice(
                [(ScreenTimePeriod.Week, ScreenTimeRules.PeriodLabel(ScreenTimePeriod.Week)), (ScreenTimePeriod.Month, ScreenTimeRules.PeriodLabel(ScreenTimePeriod.Month))],
                ScreenTimeRules.ChartPeriod(config.Period),
                value => { config.Period = value; save(); })));

        rows.Add(FluentLayout.Row(Glyphs.Palette, Str.Screentime_Cfg_Style.Text(), Str.WidgetsC_ScreenTime_Settings_ChartStyle_Desc.Text(),
            FluentLayout.Choice(
                [(ScreenTimeChartStyle.Bars, Str.Screentime_Cfg_StyleBars.Text()), (ScreenTimeChartStyle.Heatmap, Str.WidgetsC_ScreenTime_Settings_StyleCalendar.Text())],
                ScreenTimeRules.ClampChartStyle(config.Style),
                value => { config.Style = value; save(); })));

        ScreenTimeSettingsRows.AddShared(rows, config);
    }

    /// <summary>[S-3340] The donut: the period; then the shared rows.</summary>
    public static void ScreenTimeDonut(List<FrameworkElement> rows, ScreenTimeDonutConfig config, Action save)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(save);

        rows.Add(FluentLayout.Header(Str.WidgetsC_ScreenTime_Donut_Label.Text()));
        rows.Add(ScreenTimeSettingsRows.PeriodRow(config.Period, value => { config.Period = value; save(); }));
        ScreenTimeSettingsRows.AddShared(rows, config);
    }

    /// <summary>[S-3350] The app list: the period, how many apps, the figures beside each; then the shared rows.</summary>
    public static void ScreenTimeApps(List<FrameworkElement> rows, ScreenTimeAppsConfig config, Action save)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(save);

        rows.Add(FluentLayout.Header(Str.WidgetsC_ScreenTime_Apps_Label.Text()));
        rows.Add(ScreenTimeSettingsRows.PeriodRow(config.Period, value => { config.Period = value; save(); }));

        // [S-1211] A ceiling over what the tile has room for.
        rows.Add(FluentLayout.Row(Glyphs.Count, Str.WidgetsC_ScreenTime_Settings_Most_Label.Text(),
            Str.WidgetsC_ScreenTime_Settings_Most_Desc.Format(("zero", 0)),
            FluentLayout.Slider(ScreenTimeRules.EffectiveMaxApps(config.MaxApps), 0, ScreenTimeRules.MaxAppsLimit, 1, string.Empty,
                value => { config.MaxApps = (int)Math.Round(value); save(); }),
            "apps listed", "max apps"));

        rows.Add(FluentLayout.Header(Str.WidgetsC_ScreenTime_Settings_Figures.Text()));

        // [S-6074] An average per day needs more than one day.
        if (config.Offers(ScreenTimeSetting.Average))
        {
            rows.Add(FluentLayout.Row(Glyphs.Hours, Str.Screentime_Metric_Average.Text(), Str.WidgetsC_ScreenTime_Settings_Average_Desc.Text(),
                FluentLayout.Toggle(config.ShowAverage, value => { config.ShowAverage = value; save(); })));
        }

        rows.Add(FluentLayout.Row(Glyphs.Count, Str.Screentime_Metric_Percent.Text(), Str.WidgetsC_ScreenTime_Settings_Share_Desc.Text(),
            FluentLayout.Toggle(config.ShowPercent, value => { config.ShowPercent = value; save(); })));

        ScreenTimeSettingsRows.AddShared(rows, config);
    }
}
