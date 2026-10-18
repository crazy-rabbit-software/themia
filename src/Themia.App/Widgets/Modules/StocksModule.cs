using Microsoft.UI.Xaml;
using Themia.App.Shell.Config;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("stocks", typeof(Themia.App.Widgets.Modules.StocksModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The Stocks widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class StocksModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => StocksConfig.Descriptor;

    public void SettingsRows(WidgetSettingsRows rows, WidgetConfig config)
        => WidgetSettings.Stocks(rows.Rows, (StocksConfig)config, rows.Save);

    public FrameworkElement CreateView(IWidgetHost host) => new StocksView(host);
}
