using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("stocks", typeof(Themia.App.Widgets.Modules.StocksModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The Stocks widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class StocksModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => StocksConfig.Descriptor;
}
