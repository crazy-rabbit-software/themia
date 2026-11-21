using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.StockHistory.Tests;

/// <summary>[S-11165]..[S-11167] The stock chart: its size, its captions and its menu.</summary>
public sealed class StockHistoryTests
{
    private const double ChartWidth = 560;
    private const double ChartHeight = 220;

    private static readonly WidgetDescriptor Kind = new StockHistoryModule().Descriptor;

    [Fact]
    public void S11166_S6755_the_chart_starts_headerless_200_wide_80_tall_at_least_and_104_new_without_its_markers()
    {
        var sizing = Kind.Sizing(new StockHistoryConfig());

        // The one band (48 least, 72 new) and what the Simple chart keeps: the ends' line and the inset, 32.
        Assert.Equal(new SizeDip(280, 32 + 72), sizing.Natural);
        Assert.Equal((200.0, Themia.Sdk.SizeStretch.Ceiling), (sizing.Min.Width, sizing.Max.Width)); // relaxed, [S-12411]: no practical Max
        Assert.Equal((32 + 48.0, Themia.Sdk.SizeStretch.Ceiling), (sizing.Min.Height, sizing.Max.Height)); // relaxed, [S-12411]: no practical Max
    }

    [Fact]
    public void S11165_the_times_sit_inside_the_chart_under_the_lowest_point_and_its_price()
    {
        var config = new StockHistoryConfig { ShowMinMax = true };
        var model = StockHistoryModule.Frame(config) with { Ends = new ChartEnds("9:30 AM", "4:00 PM") };
        var frame = ChartPlot.Frame(model, ChartWidth, ChartHeight);
        var line = FamilyType.Caption.LineHeight;

        // [S-6754] The plot is the card's whole box; the high's price fits above the trace, the low's under it, and the
        // times under that, at the inset off the foot.
        Assert.Equal(new ChartBox(0, 0, ChartWidth, ChartHeight), frame.Plot);
        Assert.True(frame.LineTop - line >= frame.Text.Top - 0.001);
        Assert.True(frame.LineBottom + line <= frame.FootLine + 0.001);
        Assert.Equal(ChartHeight - FamilySpace.Inset, frame.FootLine + line, 6);
    }

    [Fact]
    public void S11165_marker_prices_stay_inside_the_chart_at_both_edges()
    {
        var frame = ChartPlot.Frame(StockHistoryModule.Frame(new StockHistoryConfig { ShowMinMax = true }), ChartWidth, ChartHeight);
        const double width = 48;
        foreach (var x in (double[])[0, ChartWidth])
        {
            var left = ChartPlot.Across(frame, x, width);
            Assert.True(left >= FamilySpace.Inset && left + width <= ChartWidth - FamilySpace.Inset);
        }
    }

    [Fact]
    public void S11166_the_markers_and_detailed_are_taller_by_what_they_write_so_the_trace_keeps_its_band()
    {
        // The trace's band is the same whatever is written over and under it: the markers' lines and Detailed's room
        // make the card taller, never the trace shorter.
        var plain = Kind.Sizing(new StockHistoryConfig());
        var extra = FamilySpace.Inset + FamilyType.CaptionLine + FamilyType.CaptionLine;
        Assert.Equal(plain.Natural.Height + extra, Kind.Sizing(new StockHistoryConfig { ShowMinMax = true }).Natural.Height);
        Assert.Equal(plain.Min.Height + extra, Kind.Sizing(new StockHistoryConfig { ShowMinMax = true }).Min.Height);
        Assert.Equal(plain.Natural.Height + extra, Kind.Sizing(new StockHistoryConfig { ShowMinMax = true, Detail = ChartDetail.Detailed }).Natural.Height);
        Assert.Equal(plain.Natural.Height + extra, Kind.Sizing(new StockHistoryConfig { Detail = ChartDetail.Detailed }).Natural.Height);

        foreach (var config in (StockHistoryConfig[])[new(), new() { ShowMinMax = true }, new() { Detail = ChartDetail.Detailed }])
        {
            var min = Kind.Sizing(config).Min;
            var frame = ChartPlot.Frame(StockHistoryModule.Frame(config) with { Ends = new ChartEnds("9:30 AM", "4:00 PM") }, min.Width, min.Height);
            Assert.Equal(ChartSizing.BandLeast, frame.LineBottom - frame.LineTop, 6);
        }
    }

    [Fact]
    public void S11167_the_menu_is_the_symbol_then_the_period_and_the_markers_then_the_link()
    {
        var menu = new WidgetMenuLines(() => { });
        var config = new StockHistoryConfig { Symbol = "MSFT" };
        StockHistoryModule.Lines(menu, config, (_, _) => Task.FromResult<IReadOnlyList<SymbolHit>>([]));

        Assert.NotNull(menu.Lines[0].Search);
        var markers = menu.Lines.Single(l => l.Text == StockHistoryStr.Stocks_ShowMinMax.Text());
        markers.Action!();
        Assert.True(config.ShowMinMax);
        Assert.True(((StockHistoryConfig)config.Clone()).ShowMinMax);
    }
}
