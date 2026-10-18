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
using Themia.Widget.Stocks;

[assembly: ThemiaWidget("stocks", typeof(StocksModule))]

namespace Themia.Widget.Stocks;

/// <summary>[S-1311] One instrument's price, its move and the shape of the period: the hero readout over a full-bleed chart, or the number alone. SPEC.md beside this file.</summary>
public sealed class StocksModule : IWidgetModule
{
    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("stocks"),
        ConfigType = typeof(StocksConfig),
        CreateDefault = () => new StocksConfig(),
        Json = StocksJsonContext.Default,
        Keywords = ["shares", "ticker", "market", "finance", "crypto", "bitcoin", "forex", "currency", "portfolio", "price"],
        Label = StocksStr.Widget_Stocks_Label.Local(),
        Description = StocksStr.WidgetsA_Catalog_Stocks_Description.Local(),
        Glyph = Glyphs.Stocks,
        DefaultWidth = 320,
        DefaultHeight = 240,
        Traits = WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.Markets, 0, 0),
    };

    public void SettingsRows(WidgetSettingsRows rows, WidgetConfig config)
        => StocksSettings.Rows(rows.Rows, (StocksConfig)config, rows.Save);

    public FrameworkElement CreateView(IWidgetHost host) => new StocksView(host);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(StocksConfig))]
internal sealed partial class StocksJsonContext : JsonSerializerContext;
