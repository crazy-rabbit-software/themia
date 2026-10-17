using Microsoft.UI.Xaml;
using Themia.App.Shell.Config;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("weatherNow", typeof(Themia.App.Widgets.Modules.WeatherNowModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The WeatherNow widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class WeatherNowModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => WeatherNowConfig.Descriptor;

    public void SettingsRows(WidgetSettingsRows rows, WidgetConfig config)
        => WidgetSettings.WeatherNow(rows.Rows, (WeatherNowConfig)config, rows.Save);

    public FrameworkElement CreateView(IWidgetHost host) => new WeatherNowView();
}
