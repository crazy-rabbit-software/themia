using Microsoft.UI.Xaml;
using Themia.App.Shell.Config;
using Themia.App.Widgets.Meters;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("network", typeof(Themia.App.Widgets.Modules.NetworkModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The Network widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class NetworkModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => NetworkConfig.Descriptor;

    public void SettingsRows(WidgetSettingsRows rows, WidgetConfig config)
        => WidgetSettings.Network(rows.Rows, (NetworkConfig)config, rows.Save);

    public FrameworkElement CreateView(IWidgetHost host) => new MeterView(host, new NetworkMeter());
}
