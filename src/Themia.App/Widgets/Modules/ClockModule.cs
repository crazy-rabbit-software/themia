using Microsoft.UI.Xaml;
using Themia.App.Shell.Config;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("clock", typeof(Themia.App.Widgets.Modules.ClockModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The Clock widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class ClockModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => ClockConfig.Descriptor;

    public void SettingsRows(WidgetSettingsRows rows, WidgetConfig config)
        => WidgetSettings.Clock(rows.Rows, (ClockConfig)config, rows.Save);

    public FrameworkElement CreateView(IWidgetHost host) => new ClockView();
}
