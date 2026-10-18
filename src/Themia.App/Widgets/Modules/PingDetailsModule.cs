using Microsoft.UI.Xaml;
using Themia.App.Shell.Config;
using Themia.App.Widgets.Details;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("pingDetails", typeof(Themia.App.Widgets.Modules.PingDetailsModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The PingDetails widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class PingDetailsModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => PingDetailsConfig.Descriptor;

    public void SettingsRows(WidgetSettingsRows rows, WidgetConfig config)
        => WidgetSettings.Details(rows.Rows, (DetailConfig)config, rows.Save);

    public FrameworkElement CreateView(IWidgetHost host) => new DetailView(host, new PingDetailPart());
}
