using Microsoft.UI.Xaml;
using Themia.App.Shell.Config;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("exchangeRates", typeof(Themia.App.Widgets.Modules.ExchangeRatesModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The ExchangeRates widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class ExchangeRatesModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => ExchangeRatesConfig.Descriptor;

    public void SettingsRows(WidgetSettingsRows rows, WidgetConfig config)
        => WidgetSettings.ExchangeRates(rows.Rows, (ExchangeRatesConfig)config, rows.Save);

    public FrameworkElement CreateView(IWidgetHost host) => new ExchangeRatesView(host);
}
