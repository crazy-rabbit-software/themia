using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.WeatherForecast.Tests;

/// <summary>[S-6171] [S-11146] [S-14815] The forecast's grid: its cells of one size from the card's edges, its lines and its size.</summary>
public class ForecastDaysTests
{
    [Fact]
    public void S724_without_a_forecast_the_days_stay_from_today_with_nothing_read()
    {
        var today = new DateOnly(2026, 11, 19);

        var days = ForecastDefaults.Unread(today, 5);

        Assert.Equal(Enumerable.Range(0, 5).Select(i => today.AddDays(i)), days.Select(d => d.Day));
        Assert.All(days, d =>
        {
            Assert.Null(d.Code);
            Assert.Null(d.Low);
            Assert.Null(d.High);
            Assert.Equal("--", WeatherReport.Degrees(d.High));
        });
    }

    [Theory]
    [InlineData(5, 3, "3,2")]
    [InlineData(7, 4, "4,3")]
    [InlineData(8, 4, "4,4")]
    [InlineData(14, 5, "5,5,4")]
    [InlineData(1, 3, "1")]
    public void S12403_each_line_holds_its_columns_and_the_last_the_days_left(int days, int columns, string lines)
        => Assert.Equal(lines, string.Join(',', ForecastGrid.Lines(days, columns)));

    [Fact]
    public void S12403_every_day_is_on_a_line_and_none_twice()
    {
        for (var days = 1; days <= 14; days++)
        {
            for (var columns = 1; columns <= 5; columns++)
            {
                var lines = ForecastGrid.Lines(days, columns);
                Assert.Equal(days, lines.Sum());
                Assert.Equal((days + columns - 1) / columns, lines.Count);
            }
        }
    }

    [Fact]
    public void S12403_no_days_no_lines()
        => Assert.Empty(ForecastGrid.Lines(0, 3));

    /// <summary>
    /// [S-14815] Owner, 2026-11-21: "the weather forecast widget grid view items do not go from edge to edge, they have
    /// space everywhere." The grid is its cells alone, touching, from the card's edges: no inset round them, no gap
    /// between them (a new card three across and two down).
    /// </summary>
    [Fact]
    public void S14815_the_grid_is_its_cells_from_the_card_edges_with_no_inset_or_gap()
    {
        var sizing = WeatherForecastLayout.Sizing(new WeatherForecastConfig { Style = ForecastStyle.Grid });
        var grid = ItemGrid.Default();
        var (w, h) = (ForecastGrid.CellWidth(grid), ForecastGrid.CellHeight(grid));

        Assert.Equal((80.0, 100.0), (w, h));
        Assert.Equal((3 * w, 2 * h), (sizing.Natural.Width, sizing.Natural.Height));
        Assert.Equal((w, h), (sizing.Min.Width, sizing.Min.Height));
        Assert.Equal((5 * w, 3 * h), (sizing.Max.Width, sizing.Max.Height));
    }

    /// <summary>
    /// [S-14815] [S-14810] [S-16438] A cell keeps its one height at every card size and its optimal width at least: the card
    /// shows more of them, never larger ones; across, the whole columns share the card's width edge to edge (less than a
    /// column's share over the optimal).
    /// </summary>
    [Theory]
    [InlineData(80, 100, 80)]
    [InlineData(240, 200, 80)]
    [InlineData(257, 233, 257.0 / 3)]
    [InlineData(400, 300, 80)]
    [InlineData(613, 517, 613.0 / 7)]
    public void S14815_S16438_a_cell_keeps_its_height_and_the_columns_share_the_width(double width, double height, double cell)
    {
        var plan = ForecastGrid.Plan(new WidgetArea(width, height - FamilySpace.ContentTop), new WeatherForecastConfig { Style = ForecastStyle.Grid });

        Assert.Equal(cell, plan.CellWidth, 6);
        Assert.Equal(100.0, plan.CellHeight);
        Assert.InRange(plan.CellWidth, 80, 80 + (80.0 / plan.Columns));
        Assert.Equal(FamilyShape.IconLarge, plan.Icon);
    }

    /// <summary>
    /// [S-14815] [S-16438] The days wrapped line after line (WR-4: "the last item will be cutoff to some extent then"): across
    /// the whole columns the width holds (no day in a line's middle cut by the right edge), down every line the height
    /// reaches, the last cut by the foot; the days every cell, at most 14.
    /// </summary>
    [Theory]
    [InlineData(240, 200, 3, 2, 6)]
    [InlineData(250, 200, 3, 2, 6)]
    [InlineData(320, 200, 4, 2, 8)]
    [InlineData(240, 210, 3, 3, 9)]
    [InlineData(80, 100, 1, 1, 1)]
    [InlineData(400, 300, 5, 3, 14)]
    [InlineData(330, 130, 4, 2, 8)]
    [InlineData(520, 600, 6, 3, 14)]
    public void S14815_S16438_whole_columns_across_and_every_line_down_the_last_cut(double width, double height, int across, int lines, int days)
    {
        var area = new WidgetArea(width, height - FamilySpace.ContentTop);
        var plan = ForecastGrid.Plan(area, new WeatherForecastConfig { Style = ForecastStyle.Grid });

        Assert.Equal((across, lines, days), (plan.Columns, plan.Rows, plan.Days));
    }
}
