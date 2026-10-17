using Microsoft.UI.Xaml;
using Themia.App.Shell.Config;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("rainNextHour", typeof(Themia.App.Widgets.Modules.RainNextHourModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The RainNextHour widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class RainNextHourModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => RainNextHourConfig.Descriptor;

    public void SettingsRows(WidgetSettingsRows rows, WidgetConfig config)
        => WidgetSettings.RainNextHour(rows.Rows, (RainNextHourConfig)config, rows.Save);

    public FrameworkElement CreateView() => new RainNextHourView();
}
