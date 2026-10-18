using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Tests;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Widget.Stocks.Tests;

/// <summary>
/// [S-1308] [S-1310] [S-1311] [S-2770] The Stocks widget: what its config asks and repairs, its
/// ladder and the captions on its chart. Moved with the widget from Core's StockWidgetTests, unchanged
/// in what they assert.
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

    [Fact]
    public void S1310_AHandEditedColourThatIsNotAColourGoesBackToTheTheme()
    {
        var config = new StocksConfig { UpColor = "#22aa44", DownColor = "red" };

        var repairs = config.Repair();

        Assert.Equal("#22AA44", config.UpColor);
        Assert.Equal(string.Empty, config.DownColor);
        Assert.Single(repairs);
    }

    private static StockPlan Plan(double width, double height, bool chart = true)
        => StockLayout.Plan(new WidgetArea(width, height), new StockInputs(6, 10, chart, false, true));

    [Fact]
    public void S1311_TheSmallestTileKeepsThePriceAndChange_AndSheds()
    {
        var plan = Plan(120, 76);

        Assert.False(plan.Currency);
        Assert.False(plan.Chart);
    }

    [Fact]
    public void S1311_AStripPutsTheChartBesideTheReadout()
    {
        var plan = Plan(480, 116);

        Assert.True(plan.Beside);
        Assert.True(plan.Chart);
        Assert.True(plan.ChartHeight >= 76);
    }

    [Fact]
    public void S1311_ALargeTileGrowsTheChart_NotJustThePrice()
    {
        var plan = Plan(560, 376);

        Assert.True(plan.ChartHeight > 120, $"chart {plan.ChartHeight}");
        Assert.True(plan.Labels && plan.MinMax);
        Assert.True(plan.Price.FontSize <= FluentType.TitleLarge.FontSize);
    }

    /// <summary>
    /// [S-2770] No facts and no range chips at any size: what the readout does not use is the
    /// chart's, so the parts add up to the tile (less the gaps), from the tall column to the largest.
    /// [S-1311] The time labels sit inside the chart, so the chart itself reaches the bottom edge.
    /// </summary>
    [Theory]
    [InlineData(200, 356)]
    [InlineData(320, 196)]
    [InlineData(560, 376)]
    [InlineData(800, 516)]
    public void S2770_TheChartTakesEverythingTheReadoutLeaves(double width, double height)
    {
        var plan = Plan(width, height);

        var used = plan.Price.LineHeight + (plan.ExtrasUnder ? plan.Change.LineHeight : 0) + plan.ChartHeight;

        Assert.True(plan.Chart);
        Assert.True(used >= height - (4 * StockLayout.Gap) - 1, $"{width}x{height}: {used} of {height}");
    }

    [Fact]
    public void S1311_WithoutAChartThePriceTakesTheTile()
    {
        var plan = Plan(560, 376, chart: false);

        Assert.False(plan.Chart);
        Assert.True(plan.Price.FontSize >= FluentType.TitleLarge.FontSize);
    }

    [Theory]
    [InlineData(120, 76)]
    [InlineData(200, 106)]
    [InlineData(320, 196)]
    [InlineData(480, 116)]
    [InlineData(200, 356)]
    [InlineData(560, 376)]
    [InlineData(800, 516)]
    public void S1311_NothingOverflowsTheTile(double width, double height)
    {
        var plan = Plan(width, height);

        var used = plan.Beside
            ? Math.Max(plan.Price.LineHeight + plan.Change.LineHeight, plan.ChartHeight)
            : plan.Price.LineHeight + (plan.ExtrasUnder ? plan.Change.LineHeight : 0)
                + (plan.Chart ? plan.ChartHeight : 0);

        Assert.True(used <= height + 1, $"{width}x{height}: {used}");
    }

    [Fact]
    public void S1311_TheTimesSitInsideTheChart_UnderTheLowestPointAndItsPrice()
    {
        var band = StockChartLabels.Band(ChartHeight, markers: true, times: true);
        var line = FluentType.Caption.LineHeight;

        // The high's price fits above the trace, the low's under it, and the times under that.
        Assert.Equal(line, band.Top, 6);
        Assert.True(band.Top + band.Usable + line <= StockChartLabels.TimesTop(ChartHeight) + 0.001);
        Assert.True(StockChartLabels.TimesTop(ChartHeight) + line <= ChartHeight);
    }

    [Fact]
    public void S1311_WithoutCaptionsTheTraceUsesTheWholeHeightLessItsStroke()
    {
        var band = StockChartLabels.Band(ChartHeight, markers: false, times: false);

        Assert.True(band.Top > 0 && band.Top < 4);
        Assert.Equal(ChartHeight - (2 * band.Top), band.Usable, 6);
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
