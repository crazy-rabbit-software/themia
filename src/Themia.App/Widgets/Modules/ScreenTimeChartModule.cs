using Microsoft.UI.Xaml;
using Themia.App.Shell.Config;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("screenTimeChart", typeof(Themia.App.Widgets.Modules.ScreenTimeChartModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The ScreenTimeChart widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class ScreenTimeChartModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => ScreenTimeChartConfig.Descriptor;

    public void SettingsRows(WidgetSettingsRows rows, WidgetConfig config)
        => WidgetSettings.ScreenTimeChart(rows.Rows, (ScreenTimeChartConfig)config, rows.Save);

    public FrameworkElement CreateView(IWidgetHost host) => new ScreenTimeChartView(host);
}
