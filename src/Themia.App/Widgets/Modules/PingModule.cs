using Microsoft.UI.Xaml;
using Themia.App.Shell.Config;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("ping", typeof(Themia.App.Widgets.Modules.PingModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The Ping widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class PingModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => PingConfig.Descriptor;

    public void SettingsRows(WidgetSettingsRows rows, WidgetConfig config)
        => WidgetSettings.Ping(rows.Rows, (PingConfig)config, rows.Save);

    public FrameworkElement CreateView() => new MeterView(WidgetKind.Ping);
}
