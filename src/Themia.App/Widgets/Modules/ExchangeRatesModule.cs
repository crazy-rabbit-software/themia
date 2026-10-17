using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("exchangeRates", typeof(Themia.App.Widgets.Modules.ExchangeRatesModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The ExchangeRates widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class ExchangeRatesModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => ExchangeRatesConfig.Descriptor;
}
