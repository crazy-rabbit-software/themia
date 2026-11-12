using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Themia.App.Shell;
using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Themia.Widget.Stock;

[assembly: ThemiaWidget("stock", typeof(StockModule))]

namespace Themia.Widget.Stock;

/// <summary>[S-11160] One instrument's price as a reading: the price, its change, the session, and the holding and the extended print where set. SPEC.md beside this file.</summary>
public sealed class StockModule : IWidgetModule
{
    /// <summary>[S-2039] A click on the card opens the symbol's own page: Yahoo Finance for a listed symbol, CoinGecko for a coin.</summary>
    internal static CardOpen? Opens(StockConfig config) => config.Page();

    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("stock"),
        ConfigType = typeof(StockConfig),
        CreateDefault = () => new StockConfig(),
        Json = StockJsonContext.Default,
        Keywords = ["shares", "ticker", "market", "finance", "crypto", "bitcoin", "forex", "currency", "portfolio", "price"],
        Label = StockStr.Widget_Stocks_Label.Local(),
        Description = StockStr.WidgetsA_Catalog_Stocks_Description.Local(),
        Glyph = Glyphs.Stocks,
        Sizing = c => StockLayout.Sizing((StockConfig)c),
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.Markets, 0, 0),
    };

    /// <summary>[S-3719] Every option is on the properties menu: the widget has no settings page.</summary>
    public void MenuLines(WidgetMenuLines menu, WidgetConfig config)
        => StockMenu.Lines(menu, (StockConfig)config);

    public FrameworkElement CreateView(IWidgetHost host) => new StockView(host);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(StockConfig))]
internal sealed partial class StockJsonContext : JsonSerializerContext;
