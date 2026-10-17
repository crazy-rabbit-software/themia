using Microsoft.UI.Xaml;
using Themia.App.Shell.Config;
using Themia.App.Widgets.Details;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("memoryDetails", typeof(Themia.App.Widgets.Modules.MemoryDetailsModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The MemoryDetails widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class MemoryDetailsModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => MemoryDetailsConfig.Descriptor;

    public void SettingsRows(WidgetSettingsRows rows, WidgetConfig config)
        => WidgetSettings.Details(rows.Rows, (DetailConfig)config, rows.Save);

    public FrameworkElement CreateView() => new DetailView(new MemoryDetailPart());
}
