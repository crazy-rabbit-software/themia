using Microsoft.UI.Xaml;
using Themia.App.Shell.Config;
using Themia.App.Widgets.Meters;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("memory", typeof(Themia.App.Widgets.Modules.MemoryModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The Memory widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class MemoryModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => MemoryConfig.Descriptor;

    public void SettingsRows(WidgetSettingsRows rows, WidgetConfig config)
        => WidgetSettings.Memory(rows.Rows, (MemoryConfig)config, rows.Save);

    public FrameworkElement CreateView(IWidgetHost host) => new MeterView(new MemoryMeter());
}
