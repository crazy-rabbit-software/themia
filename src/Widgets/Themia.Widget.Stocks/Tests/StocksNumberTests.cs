using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Core.Tests;
using Xunit;
using static Themia.Core.Tests.StyleTiers;

namespace Themia.Widget.Stocks.Tests;

/// <summary>
/// [S-6166] The stock widget's number style at the seven size tiers (each card less its 36 header).
/// </summary>
public sealed class StocksNumberTests
{
    // ---- stocks number [S-6166] --------------------------------------------------------------

    private static StockPlan Number(WidgetArea area, int rows = 0)
        => StockLayout.Plan(area, new StockInputs(new ReadoutText("420.27", " USD"), "+88.88%", rows, Chart: false, MinMax: true));

    [Fact]
    public void S6166_the_number_style_has_no_chart_at_any_size()
        => Assert.All(Tiers, tier => Assert.False(Number(tier).Chart));

    [Fact]
    public void S6166_the_price_is_the_charts_step_never_bigger()
    {
        foreach (var tier in Tiers)
        {
            var chart = StockLayout.Plan(tier, new StockInputs(new ReadoutText("420.27", " USD"), "+88.88%", 0, Chart: true, MinMax: true));
            Assert.Equal(chart.Price, Number(tier).Price);
        }
    }

    [Fact]
    public void S6166_its_rows_take_the_room_under_the_price_in_whole_rows()
    {
        Assert.Equal(0, Number(Xs, rows: 2).Rows);
        Assert.Equal(2, Number(Xl, rows: 2).Rows);
        foreach (var tier in Tiers)
        {
            var plan = Number(tier, rows: 2);
            Assert.True(plan.RowsTop + (plan.Rows * FamilyShape.Row1) <= tier.Height - FamilySpace.Inset + 0.01, $"{tier}");
        }
    }
}
