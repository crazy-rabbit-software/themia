using Microsoft.UI.Xaml;
using Themia.App.Shell.Config;
using Themia.App.Widgets.Details;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("batteryDetails", typeof(Themia.App.Widgets.Modules.BatteryDetailsModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The BatteryDetails widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class BatteryDetailsModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => BatteryDetailsConfig.Descriptor;

    public void SettingsRows(WidgetSettingsRows rows, WidgetConfig config)
        => WidgetSettings.Details(rows.Rows, (DetailConfig)config, rows.Save);

    public FrameworkElement CreateView() => new DetailView(new BatteryDetailPart());
}
