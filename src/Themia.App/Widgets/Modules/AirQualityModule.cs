using Microsoft.UI.Xaml;
using Themia.App.Shell.Config;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("airQuality", typeof(Themia.App.Widgets.Modules.AirQualityModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The AirQuality widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class AirQualityModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => AirQualityConfig.Descriptor;

    public void SettingsRows(WidgetSettingsRows rows, WidgetConfig config)
        => WidgetSettings.AirQuality(rows.Rows, (AirQualityConfig)config, rows.Save);

    public FrameworkElement CreateView(IWidgetHost host) => new AirQualityView();
}
