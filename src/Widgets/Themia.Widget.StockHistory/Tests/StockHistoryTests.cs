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
    public void S11166_the_chart_starts_headerless_200_to_480_wide_its_band_64_to_240_under_the_inset()
    {
        var sizing = Kind.Sizing(new StockHistoryConfig());

        Assert.True(Kind.Traits.HasFlag(WidgetTraits.StartsHeaderless));
        Assert.Equal(new SizeDip(280, 12 + 96), sizing.Natural);
        Assert.Equal((200.0, 480.0), (sizing.Min.Width, sizing.Max.Width));
        Assert.Equal((12 + 64.0, 12 + 240.0), (sizing.Min.Height, sizing.Max.Height));
    }

    [Fact]
    public void S11165_the_times_sit_inside_the_chart_under_the_lowest_point_and_its_price()
    {
        var band = StockChartLabels.Band(ChartHeight, markers: true, times: true);
        var line = FamilyType.Caption.LineHeight;

        // The high's price fits above the trace, the low's under it, and the times under that.
        Assert.Equal(line, band.Top, 6);
        Assert.True(band.Top + band.Usable + line <= StockChartLabels.TimesTop(ChartHeight) + 0.001);
        Assert.True(StockChartLabels.TimesTop(ChartHeight) + line <= ChartHeight);
    }

    [Fact]
    public void S11165_marker_prices_stay_inside_the_chart_at_both_edges()
    {
        var placed = StockChartLabels.Place(ChartWidth,
            [new ChartMarker(ChartWidth, 20, "412.30", Above: true), new ChartMarker(0, 200, "408.00", Above: false)]);

        Assert.All(placed, box => Assert.True(box.Left >= 0 && box.Right <= ChartWidth));
        Assert.True(placed[0].Bottom <= 20);
        Assert.True(placed[1].Top >= 200);
    }

    [Fact]
    public void S11167_the_menu_is_the_symbol_then_the_period_and_the_markers_then_the_link()
    {
        var menu = new WidgetMenuLines(() => { }, _ => { });
        var config = new StockHistoryConfig { Symbol = "MSFT" };
        StockHistoryModule.Lines(menu, config, (_, _) => Task.FromResult<IReadOnlyList<SymbolHit>>([]));

        Assert.NotNull(menu.Lines[0].Search);
        var markers = menu.Lines.Single(l => l.Text == StockHistoryStr.Stocks_ShowMinMax.Text());
        markers.Action!();
        Assert.True(config.ShowMinMax);
        Assert.True(((StockHistoryConfig)config.Clone()).ShowMinMax);
    }
}
