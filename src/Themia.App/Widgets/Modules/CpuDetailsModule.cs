using Microsoft.UI.Xaml;
using Themia.App.Shell.Config;
using Themia.App.Widgets.Details;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("cpuDetails", typeof(Themia.App.Widgets.Modules.CpuDetailsModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The CpuDetails widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class CpuDetailsModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => CpuDetailsConfig.Descriptor;

    public void SettingsRows(WidgetSettingsRows rows, WidgetConfig config)
        => WidgetSettings.Details(rows.Rows, (DetailConfig)config, rows.Save);

    public FrameworkElement CreateView(IWidgetHost host) => new DetailView(new CpuDetailPart());
}
