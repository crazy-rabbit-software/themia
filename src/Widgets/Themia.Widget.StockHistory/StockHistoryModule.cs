using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Themia.App.Shell;
using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Links;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Widget.StockHistory;

[assembly: ThemiaWidget("stockHistory", typeof(StockHistoryModule))]

namespace Themia.Widget.StockHistory;

/// <summary>[S-11165] One instrument's price over its period as a chart, headerless by default. SPEC.md beside this file.</summary>
public sealed class StockHistoryModule : IWidgetModule
{
    /// <summary>[S-11166] The chart's band under the inset: its least, a new card's and its most.</summary>
    public const double BandMin = 64;

    public const double BandStart = 96;

    public const double BandMax = 240;

    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("stockHistory"),
        ConfigType = typeof(StockHistoryConfig),
        CreateDefault = () => new StockHistoryConfig(),
        Json = StockHistoryJsonContext.Default,
        Keywords = ["chart", "shares", "ticker", "market", "crypto", "bitcoin", "history", "price"],
        Label = StockHistoryStr.StockHistory_Label.Local(),
        Description = StockHistoryStr.StockHistory_Desc.Local(),
        Glyph = Glyphs.ScreenTimeChart,
        Sizing = _ => Sizing(),
        Traits = WidgetTraits.CanHaveHeader | WidgetTraits.StartsHeaderless,
        Links = LinkUse.Of([LinkTopicId.HoveredInstant], [LinkTopicId.HoveredInstant]),
        Add = new(AddFolderId.Markets, 0, 1),
    };

    /// <summary>[S-11166] The chart edge to edge under the inset (headerless): 200..480 wide, its band 64..240 tall.</summary>
    internal static WidgetSizing Sizing()
        => WidgetSizing.Range(new(200, FamilySpace.Inset + BandMin), new(280, FamilySpace.Inset + BandStart), new(480, FamilySpace.Inset + BandMax));

    /// <summary>[S-11167] Every option is on the properties menu: the instrument and its period as the price's, and the markers.</summary>
    public void MenuLines(WidgetMenuLines menu, WidgetConfig config) => Lines(menu, (StockHistoryConfig)config, SymbolLookup.FindAsync);

    /// <summary>The lines, with the symbol search's lookup given (a test's in place of the host's two services).</summary>
    internal static void Lines(WidgetMenuLines menu, StockHistoryConfig config, Func<string, CancellationToken, Task<IReadOnlyList<SymbolHit>>> lookup)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);
        menu.Lines.Add(StockQuoteLines.Symbol(menu, config, lookup));
        menu.Lines.Add(ItemMenuEntry.Separator);
        menu.Lines.AddRange(StockQuoteLines.Period(menu, config));
        menu.Lines.Add(menu.Toggle(null, StockHistoryStr.Stocks_ShowMinMax.Text(), config.ShowMinMax, on => config.ShowMinMax = on));
        menu.Lines.Add(ItemMenuEntry.Separator);
        menu.Lines.Add(StockQuoteLines.Updates(menu));
    }

    public FrameworkElement CreateView(IWidgetHost host) => new StockHistoryView(host);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(StockHistoryConfig))]
internal sealed partial class StockHistoryJsonContext : JsonSerializerContext;
