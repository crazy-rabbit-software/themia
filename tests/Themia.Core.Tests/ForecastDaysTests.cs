using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-725] [S-726] [S-6171] [S-6172] The forecast's days: never slivers, grown with the room, the
/// grid's arrangement and the now mark.
/// </summary>
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

    // ---- never thinner than the floor [S-725] [S-726] ----------------------------------------

    [Theory]
    [MemberData(nameof(Sizes))]
    public void S725_the_rows_shown_fit_the_list_at_the_floor_or_more(double width, double height)
    {
        var slot = new WidgetArea(width, height);
        for (var days = 1; days <= 14; days++)
        {
            var plan = WeatherForecastLayout.Rows(slot, days);

            Assert.InRange(plan.Days, 1, days);
            Assert.InRange(plan.RowHeight, WeatherForecastLayout.RowMin, WeatherForecastLayout.RowMax);

            // Every row shown is whole (none clipped, none scrolled), unless one row is all there is.
            if (plan.Days > 1)
            {
                Assert.True(plan.Days * plan.RowHeight <= height - WeatherForecastLayout.ListPadding + 0.5, $"{width}x{height} {days}");
            }

            // A day left out is one that had no room: one more would go under the floor.
            if (plan.Days < days)
            {
                Assert.True((plan.Days + 1) * WeatherForecastLayout.RowMin > height - WeatherForecastLayout.ListPadding + 0.5);
            }
        }
    }

    [Theory]
    [MemberData(nameof(Sizes))]
    public void S726_no_column_is_thinner_than_the_floor_and_none_is_a_sliver(double width, double height)
    {
        var slot = new WidgetArea(width, height);
        var inside = width - (2 * WeatherForecastLayout.ColumnListPadding);

        for (var days = 1; days <= 14; days++)
        {
            var plan = WeatherForecastLayout.Columns(slot, days);

            Assert.InRange(plan.Days, 1, days);
            Assert.True(plan.ColumnWidth <= WeatherForecastLayout.ColumnMax);
            Assert.True(plan.Days * plan.ColumnWidth <= inside + 0.5, $"{width}x{height} {days}");
            if (plan.Days > 1)
            {
                Assert.True(plan.ColumnWidth >= WeatherForecastLayout.ColumnMin - 0.5);
            }
        }
    }

    // ---- the parts grow with the room [S-669] ------------------------------------------------

    [Fact]
    public void S669_the_day_parts_grow_by_whole_ramp_steps()
    {
        for (var k = 1; k <= DayScale.Largest; k++)
        {
            var smaller = DayScale.At(k - 1);
            var larger = DayScale.At(k);
            Assert.True(larger.High.FontSize > smaller.High.FontSize);
            Assert.True(larger.Icon > smaller.Icon);
            Assert.True(larger.Bar >= smaller.Bar);
        }

        Assert.Equal(PartMetrics.DayIcon, DayScale.At(0).Icon);
        Assert.True(DayScale.At(0).Bar >= 6, "a 4px bar read as a scratch");
    }

    [Fact]
    public void S725_a_large_tile_gets_larger_figures_not_more_facts()
    {
        var xl = WeatherForecastLayout.Rows(Tiers[6], 7);
        var m = WeatherForecastLayout.Rows(Tiers[2], 7);

        Assert.True(xl.Scale.Level > m.Scale.Level);
        Assert.Equal(7, xl.Days);
    }

    [Fact]
    public void S726_a_large_tile_gets_larger_columns_parts()
    {
        var xl = WeatherForecastLayout.Columns(Tiers[6], 7);
        Assert.True(xl.Scale.Level >= 2);
        Assert.True(xl.Icon && xl.Low && xl.Bar);
    }

    [Theory]
    [MemberData(nameof(Sizes))]
    public void S726_a_column_s_parts_fit_its_height(double width, double height)
    {
        var plan = WeatherForecastLayout.Columns(new WidgetArea(width, height), 7);
        var s = plan.Scale;
        var gap = WeatherForecastLayout.ColumnGap;
        var caption = Themia.Core.Widgets.FluentType.Caption.LineHeight;
        var used = s.Label.LineHeight + s.High.LineHeight
            + (plan.Icon ? s.Icon + gap : 0) + (plan.Low ? s.Low.LineHeight + gap : 0) + (plan.Bar ? WeatherForecastLayout.ColumnBarMin + gap : 0)
            + (plan.Precipitation ? caption + gap : 0) + (plan.Description ? caption + gap : 0)
            + (plan.Chance ? caption + gap : 0) + (plan.Wind ? caption + gap : 0) + gap;
        var room = height - (2 * WeatherForecastLayout.ColumnListPadding) - (2 * WeatherForecastLayout.ColumnPadding);

        // The base day and high are drawn whatever the height; everything past them must fit.
        if (plan.Icon || plan.Low)
        {
            Assert.True(used <= room + 0.5, $"{width}x{height}: {used} > {room}");
        }
    }

    // ---- the grid [S-6171] -------------------------------------------------------------------

    [Theory]
    [InlineData(544, 360, 7, 4, 2)]  // L: landscape
    [InlineData(184, 340, 7, 2, 4)]  // Tall: a column
    [InlineData(784, 200, 14, 7, 2)] // two weeks on a broad strip
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

            Assert.True(ForecastGrid.CellWidthFor(plan.Scale) <= plan.CellWidth + 0.5 || plan.Scale.Level == 0);
            Assert.True(ForecastGrid.CellHeightFor(plan.Scale) <= plan.CellHeight + 0.5 || plan.Scale.Level == 0);
        }
    }

    [Fact]
    public void S6171_the_smallest_tile_sheds_the_glyph_rather_than_clip_the_figures()
    {
        Assert.False(ForecastGrid.Plan(Tiers[0], 7).Icon);
        Assert.All(Tiers.Skip(1), t => Assert.True(ForecastGrid.Plan(t, 7).Icon));
    }

    [Fact]
    public void S6171_a_large_grid_has_larger_parts()
        => Assert.True(ForecastGrid.Plan(Tiers[6], 7).Scale.Level > ForecastGrid.Plan(Tiers[1], 7).Scale.Level);

    // ---- now [S-6172] ------------------------------------------------------------------------

    [Fact]
    public void S6172_now_crosses_today_s_column_with_the_day()
    {
        Assert.Equal(100, ForecastNow.CurveX(1, 0, 100));
        Assert.Equal(150, ForecastNow.CurveX(1, ForecastNow.DayFraction(new TimeOnly(12, 0)), 100));
        Assert.Equal(0.75, ForecastNow.DayFraction(new TimeOnly(18, 0)));
        Assert.Equal(200, ForecastNow.CurveX(1, 2, 100));
    }

    [Fact]
    public void S6172_the_now_dot_sits_on_the_forecast_s_scale_clamped_to_the_bar()
    {
        Assert.Equal(0.5, ForecastNow.OnBar(15, 10, 20));
        Assert.Equal(0, ForecastNow.OnBar(2, 10, 20));
        Assert.Equal(1, ForecastNow.OnBar(30, 10, 20));
        Assert.Equal(0.5, ForecastNow.OnBar(12, 12, 12));
        Assert.Equal(0.5, ForecastNow.OnBar(double.NaN, 10, 20));
    }

    [Fact]
    public void S6172_the_curve_scale_takes_in_the_temperature_now()
    {
        Assert.Equal((5.0, 22.0), ForecastCurve.Scale([10, 20, null, 5, 22]));
        Assert.Equal((0.0, 1.0), ForecastCurve.Scale([null, double.NaN]));
        Assert.Equal(4, ForecastCurve.Y(20, 10, 20, 4, 104));
        Assert.Equal(54, ForecastCurve.Y(15, 10, 20, 4, 104));
    }

    [Fact]
    public void S6172_only_a_recent_reading_is_the_temperature_now()
    {
        var now = new DateTimeOffset(2026, 10, 16, 14, 0, 0, TimeSpan.Zero);

        Assert.Equal(14.2, ForecastNow.Temperature(14.2, now.AddMinutes(-20), now));
        Assert.Null(ForecastNow.Temperature(14.2, now.AddHours(-3), now));
        Assert.Null(ForecastNow.Temperature(null, now, now));
        Assert.Null(ForecastNow.Temperature(double.NaN, now, now));
        Assert.Null(ForecastNow.Temperature(14.2, now.AddHours(1), now));
    }

    [Fact]
    public void S6172_today_is_found_among_the_days_shown_only()
    {
        var today = new DateOnly(2026, 10, 16);
        ForecastDay[] days = [new(today.AddDays(-1), 0, 1, 2, 0), new(today, 0, 1, 2, 0), new(today.AddDays(1), 0, 1, 2, 0)];

        Assert.Equal(1, ForecastNow.TodayIndex(days, today, 3));
        Assert.Equal(-1, ForecastNow.TodayIndex(days, today, 1));
        Assert.Equal(-1, ForecastNow.TodayIndex(days, today.AddDays(5), 3));
    }
}
