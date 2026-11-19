using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.WeatherForecast.Tests;

/// <summary>[S-6171] [S-11146] The forecast's grid: its fixed arrangement by the days, its cells and its size.</summary>
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
    [InlineData(1, 1, 1)]
    [InlineData(4, 4, 1)]
    [InlineData(5, 3, 2)]
    [InlineData(7, 4, 2)]
    [InlineData(8, 4, 2)]
    [InlineData(9, 3, 3)]
    [InlineData(14, 5, 3)]
    public void S11146_the_days_take_one_row_up_to_four_two_up_to_eight_three_past(int days, int columns, int rows)
        => Assert.Equal((columns, rows), ForecastGrid.Arrangement(days));

    [Theory]
    [InlineData(5, "3,2")]
    [InlineData(7, "4,3")]
    [InlineData(8, "4,4")]
    [InlineData(14, "5,5,4")]
    [InlineData(1, "1")]
    public void S12403_each_line_holds_its_columns_and_the_last_the_days_left(int days, string lines)
        => Assert.Equal(lines, string.Join(',', ForecastGrid.Lines(days, ForecastGrid.Arrangement(days).Columns)));

    [Fact]
    public void S12403_every_day_is_on_a_line_and_none_twice()
    {
        for (var days = 1; days <= 14; days++)
        {
            var (columns, rows) = ForecastGrid.Arrangement(days);
            var lines = ForecastGrid.Lines(days, columns);
            Assert.Equal(days, lines.Sum());
            Assert.Equal(rows, lines.Count);
        }
    }

    [Fact]
    public void S12403_no_days_no_lines()
        => Assert.Empty(ForecastGrid.Lines(0, 3));

    [Fact]
    public void S11146_an_arrangement_never_leaves_a_whole_row_empty()
    {
        for (var days = 1; days <= 14; days++)
        {
            var (columns, rows) = ForecastGrid.Arrangement(days);
            Assert.True(columns * rows >= days);
            Assert.True((rows - 1) * columns < days, $"{days}");
        }
    }

    [Fact]
    public void S11146_the_grid_is_one_height_its_cells_64_wide_at_first_and_80_at_most()
    {
        var sizing = WeatherForecastLayout.Sizing(new WeatherForecastConfig { Style = ForecastStyle.Grid, Days = 7 });

        Assert.True(sizing.FixedHeight);
        Assert.Equal(12 + (2 * ForecastGrid.CellMinHeight) + 12 + 12, sizing.Natural.Height);
        Assert.Equal(24 + (4 * 64) + (3 * 12), sizing.Natural.Width);
        Assert.Equal(24 + (4 * 80) + (3 * 12), sizing.Max.Width);
        Assert.Equal(24 + (4 * 56) + (3 * 12), sizing.Min.Width);
    }

    [Fact]
    public void S6171_the_plan_shows_every_day_with_the_32_glyph_and_the_cells_share_the_content()
    {
        var plan = ForecastGrid.Plan(new WidgetArea(340, 184), new WeatherForecastConfig { Style = ForecastStyle.Grid, Days = 7 });

        Assert.Equal((7, 4, 2), (plan.Days, plan.Columns, plan.Rows));
        Assert.Equal(FamilyShape.IconLarge, plan.Icon);
        Assert.Equal((340 - 24 - (3 * 12)) / 4.0, plan.CellWidth, 6);
    }
}
