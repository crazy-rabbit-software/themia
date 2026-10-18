using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Themia.Widget.ScreenTimeApps;

namespace Themia.App.Shell.Config;

/// <summary>The ScreenTimeApps widget's settings page.</summary>
internal static class ScreenTimeAppsSettings
{
    /// <summary>[S-3350] The app list: the period, how many apps, the figures beside each; then the shared rows.</summary>
    public static void Rows(List<FrameworkElement> rows, ScreenTimeAppsConfig config, Action save)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(save);

        rows.Add(FluentLayout.Header(ScreenTimeAppsStr.WidgetsC_ScreenTime_Apps_Label.Text()));
        rows.Add(ScreenTimeSettingsRows.PeriodRow(config.Period, value => { config.Period = value; save(); }));

        // [S-1211] A ceiling over what the tile has room for.
        rows.Add(FluentLayout.Row(Glyphs.Count, ScreenTimeAppsStr.WidgetsC_ScreenTime_Settings_Most_Label.Text(),
            ScreenTimeAppsStr.WidgetsC_ScreenTime_Settings_Most_Desc.Format(("zero", 0)),
            FluentLayout.Slider(ScreenTimeRules.EffectiveMaxApps(config.MaxApps), 0, ScreenTimeRules.MaxAppsLimit, 1, string.Empty,
                value => { config.MaxApps = (int)Math.Round(value); save(); }),
            "apps listed", "max apps"));

        rows.Add(FluentLayout.Header(ScreenTimeAppsStr.WidgetsC_ScreenTime_Settings_Figures.Text()));

        // [S-6074] An average per day needs more than one day.
        if (config.Offers(ScreenTimeSetting.Average))
        {
            rows.Add(FluentLayout.Row(Glyphs.Hours, ScreenTimeAppsStr.Screentime_Metric_Average.Text(), ScreenTimeAppsStr.WidgetsC_ScreenTime_Settings_Average_Desc.Text(),
                FluentLayout.Toggle(config.ShowAverage, value => { config.ShowAverage = value; save(); })));
        }

        rows.Add(FluentLayout.Row(Glyphs.Count, ScreenTimeAppsStr.Screentime_Metric_Percent.Text(), ScreenTimeAppsStr.WidgetsC_ScreenTime_Settings_Share_Desc.Text(),
            FluentLayout.Toggle(config.ShowPercent, value => { config.ShowPercent = value; save(); })));

        ScreenTimeSettingsRows.AddShared(rows, config);
    }
}
