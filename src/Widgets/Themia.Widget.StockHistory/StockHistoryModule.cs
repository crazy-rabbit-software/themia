using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Themia.App.Shell;
using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Links;
using Themia.Core.Model;
using Themia.Core.Settings;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Widget.StockHistory;

[assembly: ThemiaWidget("stockHistory", typeof(StockHistoryModule))]

namespace Themia.Widget.StockHistory;

/// <summary>[S-11165] One instrument's price over its period as a chart. SPEC.md beside this file.</summary>
public sealed class StockHistoryModule : IWidgetModule
{
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
        Sizing = c => Sized((StockHistoryConfig)c),
        Traits = WidgetTraits.None,
        Links = LinkUse.Of([LinkTopicId.HoveredInstant], [LinkTopicId.HoveredInstant]),
        Add = new(AddFolderId.Markets, 0, 1),
    };

    /// <summary>
    /// [S-11166] [S-6754] [S-6755] The chart the card's edge to edge, the one band sizing: as tall as what its Simple chart
    /// writes (the ends' line, the markers' where they are on) and the trace's band (<see cref="ChartSizing.Band"/>).
    /// </summary>
    internal static WidgetSizing Sizing(StockHistoryConfig config)
    {
        var (top, bottom) = Keeps(config);
        return ChartSizing.Band(top + bottom);
    }

    /// <summary>[S-6754] What its Simple chart keeps over the trace and under it: the markers' lines where they are on, the ends' line.</summary>
    private static (double Top, double Bottom) Keeps(StockHistoryConfig config)
        => ChartPlot.Keeps(Frame(config) with { Detail = ChartDetail.Simple, PadTop = config.ShowMinMax ? FamilyType.CaptionLine : 0, PadBottom = config.ShowMinMax ? FamilyType.CaptionLine : 0, Ends = new ChartEnds(string.Empty, string.Empty) });

    /// <summary>
    /// [S-6751] Its sizing at its detail: Detailed keeps a line over the trace and one under it as the markers do, and
    /// the times it always has, so it grows only by what its Simple chart (the markers, the ends) does not keep already.
    /// </summary>
    internal static WidgetSizing Sized(StockHistoryConfig config)
    {
        var (top, bottom) = Keeps(config);
        return ChartPlot.Sized(Sizing(config), ChartLooks.EffectiveDetail(config), top, bottom);
    }

    /// <summary>
    /// [S-1310] [S-1311] [S-6754] The stock chart before its data: its look, and a Caption line kept over the trace and
    /// one under it for the high's and the low's prices where the markers are on (Simple; Detailed writes its own).
    /// </summary>
    internal static ChartModel Frame(StockHistoryConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var detail = ChartLooks.EffectiveDetail(config);
        var markers = config.ShowMinMax && detail == ChartDetail.Simple ? FamilyType.CaptionLine : 0;
        return new ChartModel { Type = ChartLooks.Effective(config), Detail = detail, PadTop = markers, PadBottom = markers };
    }

    /// <summary>[S-11167] Every option is on the selection's page: the instrument and its period as the price's, and the markers.</summary>
    public void MenuLines(WidgetMenuLines menu, WidgetConfig config) => Lines(menu, (StockHistoryConfig)config, SymbolLookup.FindAsync);

    /// <summary>The lines, with the symbol search's lookup given (a test's in place of the host's two services).</summary>
    internal static void Lines(WidgetMenuLines menu, StockHistoryConfig config, Func<string, CancellationToken, Task<IReadOnlyList<SymbolHit>>> lookup)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);
        menu.Lines.Add(StockQuoteLines.Symbol(menu, config, lookup));
        menu.Lines.Add(SettingsLine.Separator);
        menu.Lines.AddRange(StockQuoteLines.Period(menu, config));
        menu.Lines.Add(menu.Toggle(null, StockHistoryStr.Stocks_ShowMinMax.Text(), config.ShowMinMax, on => config.ShowMinMax = on));
        ChartMenuLines.Add(menu, config);
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
