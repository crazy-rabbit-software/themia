using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.WeatherForecast.Tests;

/// <summary>[S-6171] The forecast's grid: its arrangement, its floor and its glyph.</summary>
public class ForecastDaysTests
{
    /// <summary>The seven tiers less the card's header.</summary>
    private static readonly WidgetArea[] Tiers =
        [.. WidgetTiers.All.Select(t => new WidgetArea(t.Width, t.Height - 44))];

    /// <summary>The tiers and a few sizes between them.</summary>
    public static TheoryData<double, double> Sizes()
    {
        var data = new TheoryData<double, double>();
        foreach (var t in Tiers)
        {
            data.Add(t.Width, t.Height);
        }

        data.Add(260, 156);
        data.Add(400, 256);
        data.Add(640, 436);
        data.Add(300, 516);
        data.Add(1200, 116);
        return data;
    }

    [Theory]
    [InlineData(528, 360, 7, 4, 2)]  // L: landscape
    [InlineData(168, 340, 6, 2, 3)]  // Tall: a column (seven would be under the floor)
    [InlineData(768, 200, 14, 7, 2)] // two weeks on a broad strip
    [InlineData(400, 400, 4, 2, 2)]
    public void S6171_the_grid_takes_the_arrangement_with_the_largest_cells(double width, double height, int days, int columns, int rows)
    {
        var best = ForecastGrid.Arrange(days, width, height, ForecastGrid.CellMinWidth, ForecastGrid.CellMinHeight);
        Assert.Equal((columns, rows), best);
    }

    [Fact]
    public void S6171_a_strip_lays_the_week_in_one_row()
    {
        var plan = ForecastGrid.Plan(Tiers[3], 7); // Wide 480 x 116
        Assert.Equal(1, plan.Rows);
        Assert.Equal(plan.Days, plan.Columns);
    }

    [Fact]
    public void S6171_an_arrangement_never_leaves_a_whole_row_or_column_empty()
    {
        for (var days = 1; days <= 14; days++)
        {
            var best = ForecastGrid.Arrange(days, 2000, 2000, 10, 10)!.Value;
            Assert.True(best.Columns * best.Rows >= days);
            Assert.True((best.Rows - 1) * best.Columns < days);
            Assert.True((best.Columns - 1) * best.Rows < days);
        }
    }

    [Theory]
    [MemberData(nameof(Sizes))]
    public void S6171_no_cell_is_under_the_floor_fewer_days_are_shown(double width, double height)
    {
        var slot = new WidgetArea(width, height);
        for (var days = 1; days <= 14; days++)
        {
            var plan = ForecastGrid.Plan(slot, days);

            Assert.InRange(plan.Days, 1, days);
            Assert.True(plan.Columns * plan.Rows >= plan.Days);
            if (plan.Days > 1)
            {
                Assert.True(plan.CellWidth >= ForecastGrid.CellMinWidth - 0.5, $"{width}x{height} {days}");
                Assert.True(plan.CellHeight >= ForecastGrid.CellMinHeight - 0.5, $"{width}x{height} {days}");
            }

            // The glyph the cell shows is one its height holds.
            Assert.True(plan.Icon == 0 || ForecastGrid.CellHeight(plan.Icon) <= plan.CellHeight + 0.5 || plan.Days == 1);
        }
    }

    [Fact]
    public void S6171_the_smallest_tile_sheds_the_glyph_rather_than_clip_the_figures()
    {
        Assert.Equal(0, ForecastGrid.Plan(Tiers[0], 7).Icon);
        Assert.All(Tiers.Skip(1), t => Assert.True(ForecastGrid.Plan(t, 7).Icon > 0));
    }

    [Fact]
    public void S6171_the_glyph_is_32_and_48_from_an_expanded_width_where_the_cell_holds_it()
    {
        Assert.Equal(FamilyShape.IconLarge, ForecastGrid.Plan(Tiers[2], 7).Icon);  // M
        Assert.Equal(FamilyShape.IconHuge, ForecastGrid.Plan(Tiers[3], 7).Icon);   // Wide
        Assert.Equal(FamilyShape.IconHuge, ForecastGrid.Plan(Tiers[6], 7).Icon);   // XL
    }
}
