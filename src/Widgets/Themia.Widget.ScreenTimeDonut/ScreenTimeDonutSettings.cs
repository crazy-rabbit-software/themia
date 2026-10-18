using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Themia.Widget.ScreenTimeDonut;

namespace Themia.App.Shell.Config;

/// <summary>The ScreenTimeDonut widget's settings page.</summary>
internal static class ScreenTimeDonutSettings
{
    /// <summary>[S-3340] The donut: the period; then the shared rows.</summary>
    public static void Rows(List<FrameworkElement> rows, ScreenTimeDonutConfig config, Action save)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(save);

        rows.Add(FluentLayout.Header(ScreenTimeDonutStr.WidgetsC_ScreenTime_Donut_Label.Text()));
        rows.Add(ScreenTimeSettingsRows.PeriodRow(config.Period, value => { config.Period = value; save(); }));
        ScreenTimeSettingsRows.AddShared(rows, config);
    }
}
