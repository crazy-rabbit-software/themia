using Microsoft.UI.Xaml;
using Themia.App.Shell.Config;
using Themia.App.Widgets.Meters;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("battery", typeof(Themia.App.Widgets.Modules.BatteryModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The Battery widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class BatteryModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => BatteryConfig.Descriptor;

    public void SettingsRows(WidgetSettingsRows rows, WidgetConfig config)
        => WidgetSettings.Battery(rows.Rows, (BatteryConfig)config, rows.Save);

    public FrameworkElement CreateView(IWidgetHost host) => new MeterView(host, new BatteryMeter());
}
