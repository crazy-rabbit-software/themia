using Microsoft.UI.Xaml;
using Themia.App.Shell.Config;
using Themia.App.Widgets.Details;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("networkDetails", typeof(Themia.App.Widgets.Modules.NetworkDetailsModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The NetworkDetails widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class NetworkDetailsModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => NetworkDetailsConfig.Descriptor;

    public void SettingsRows(WidgetSettingsRows rows, WidgetConfig config)
        => WidgetSettings.Details(rows.Rows, (DetailConfig)config, rows.Save);

    public FrameworkElement CreateView(IWidgetHost host) => new DetailView(host, new NetworkDetailPart());
}
