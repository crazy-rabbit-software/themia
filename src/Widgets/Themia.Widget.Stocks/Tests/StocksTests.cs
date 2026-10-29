using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Tests;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;
using static Themia.Core.Tests.StyleTiers;

namespace Themia.Widget.Stocks.Tests;

/// <summary>
/// [S-1308] [S-1310] [S-1311] [S-2770] The Stocks widget: what its config asks, its layout on the
/// family's blocks and the captions on its chart.
/// </summary>
public sealed class StocksTests
{
    private const double ChartWidth = 560;
    private const double ChartHeight = 220;

    [Fact]
    public void S1308_ExtendedBarsAreAskedForOnlyWhereTheyExist()
    {
        Assert.True(new StocksConfig { ShowExtendedHours = true, Range = QuoteRange.FiveDays }.WantsPrePost);
        Assert.False(new StocksConfig { ShowExtendedHours = true, Range = QuoteRange.Month }.WantsPrePost);
        Assert.False(new StocksConfig { ShowExtendedHours = true, Market = QuoteMarket.Crypto }.WantsPrePost);
        Assert.False(new StocksConfig { Range = QuoteRange.Day }.WantsPrePost);
    }

    private static readonly ReadoutText Price = new("420.27", " USD");

    private static StockPlan Plan(WidgetArea area, int rows = 0, bool chart = true)
        => StockLayout.Plan(area, new StockInputs(Price, "+88.88%", rows, chart, MinMax: true));

    [Fact]
    public void S1311_the_price_climbs_28_40_68_with_the_area_and_stops()
    {
        Assert.Equal(FamilyType.Big28, Plan(Xs).Price);
        Assert.Equal(FamilyType.Big40, Plan(M).Price);
        Assert.Equal(FamilyType.Big68, Plan(L).Price);
        Assert.Equal(FamilyType.Big68, Plan(new WidgetArea(1600, 1200)).Price);
    }

    [Fact]
    public void S1311_the_smallest_tile_folds_the_change_under_the_price_and_has_no_chart()
    {
        var plan = Plan(Xs);

        Assert.False(plan.Fold.DeltaBeside);
        Assert.True(plan.Under);
        Assert.False(plan.Chart);
    }

    /// <summary>[S-1311] [S-2770] The chart runs 8 under the price (or its rows), edge to edge, down to the card's foot.</summary>
    [Theory]
    [InlineData(200, 106)]
    [InlineData(320, 196)]
    [InlineData(480, 116)]
    [InlineData(200, 356)]
    [InlineData(560, 376)]
    [InlineData(800, 516)]
    public void S2770_the_chart_takes_everything_under_the_price_to_the_foot(double width, double height)
    {
        var plan = Plan(new WidgetArea(width, height));
        var under = plan.Under ? FamilyType.Under(plan.Price).LineHeight : 0;

        Assert.True(plan.Chart, $"{width}x{height}");
        Assert.Equal(plan.Price.LineHeight + under + FamilySpace.S2, plan.ChartTop);
        Assert.Equal(height, plan.ChartTop + plan.ChartHeight, 6);
    }

    [Fact]
    public void S1311_the_times_need_a_regular_height_and_the_markers_a_chart_of_48()
    {
        Assert.False(Plan(Wide).Labels);
        Assert.True(Plan(M).Labels);
        Assert.True(Plan(L).MinMax);
        Assert.Equal(Plan(Wide).ChartHeight >= StockLayout.MarkersFrom, Plan(Wide).MinMax);
    }

    [Fact]
    public void S1311_without_a_chart_the_number_is_never_bigger()
    {
        var plan = Plan(L, chart: false);

        Assert.False(plan.Chart);
        Assert.Equal(Plan(L).Price, plan.Price);
    }

    [Fact]
    public void S1311_TheTimesSitInsideTheChart_UnderTheLowestPointAndItsPrice()
    {
        var band = StockChartLabels.Band(ChartHeight, markers: true, times: true);
        var line = FamilyType.Caption.LineHeight;

        // The high's price fits above the trace, the low's under it, and the times under that.
        Assert.Equal(line, band.Top, 6);
        Assert.True(band.Top + band.Usable + line <= StockChartLabels.TimesTop(ChartHeight) + 0.001);
        Assert.True(StockChartLabels.TimesTop(ChartHeight) + line <= ChartHeight);
    }

    [Fact]
    public void S1311_WithoutCaptionsTheTraceUsesTheWholeHeight()
    {
        var band = StockChartLabels.Band(ChartHeight, markers: false, times: false);

        Assert.Equal(0, band.Top);
        Assert.Equal(ChartHeight, band.Usable, 6);
    }

    [Fact]
    public void S1310_MarkerPricesStayInsideTheChart_AtBothEdges()
    {
        var placed = StockChartLabels.Place(ChartWidth,
            [new ChartMarker(ChartWidth, 20, "412.30", Above: true), new ChartMarker(0, 200, "408.00", Above: false)]);

        Assert.All(placed, box => Assert.True(box.Left >= 0 && box.Right <= ChartWidth));
        Assert.True(placed[0].Bottom <= 20);
        Assert.True(placed[1].Top >= 200);
    }
}
