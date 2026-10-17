using Microsoft.UI.Xaml;
using Themia.App.Shell.Config;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("cpu", typeof(Themia.App.Widgets.Modules.CpuModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The Cpu widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class CpuModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => CpuConfig.Descriptor;

    public void SettingsRows(WidgetSettingsRows rows, WidgetConfig config)
        => WidgetSettings.Cpu(rows.Rows, (CpuConfig)config, rows.Save);

    public FrameworkElement CreateView() => new MeterView(WidgetKind.Cpu);
}
