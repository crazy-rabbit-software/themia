using Microsoft.UI.Xaml;
using Themia.App.Shell.Config;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("weatherForecast", typeof(Themia.App.Widgets.Modules.WeatherForecastModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The WeatherForecast widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class WeatherForecastModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => WeatherForecastConfig.Descriptor;

    public void SettingsRows(WidgetSettingsRows rows, WidgetConfig config)
        => WidgetSettings.WeatherForecast(rows.Rows, (WeatherForecastConfig)config, rows.Save);

    public FrameworkElement CreateView() => new WeatherForecastView();
}
