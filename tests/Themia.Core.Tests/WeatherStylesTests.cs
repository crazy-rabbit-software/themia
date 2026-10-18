using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;
using static Themia.Core.Tests.StyleTiers;

namespace Themia.Core.Tests;

/// <summary>
/// [S-6161]..[S-6166] The styles of the weather, forecast and stock widgets:
/// the rules each style draws by, at the seven size tiers (each card less its
/// ~44px header).
/// </summary>
public sealed class WeatherStylesTests
{
    // ---- weather now [S-6161] [S-6162] -------------------------------------------------------

    // ---- forecast curve [S-6164] -------------------------------------------------------------

    [Fact]
    public void S6164_the_warmest_day_is_at_the_top_and_the_coldest_at_the_bottom_on_its_column_s_middle()
    {
        var points = ForecastCurve.Points([10, 20, 15], 10, 20, 50, 4, 104);

        Assert.Equal(new CurvePoint(25, 104), points[0]);
        Assert.Equal(new CurvePoint(75, 4), points[1]);
        Assert.Equal(new CurvePoint(125, 54), points[2]);
    }

    [Fact]
    public void S6164_one_temperature_all_week_is_a_flat_line_in_the_middle()
    {
        var points = ForecastCurve.Points([12, 12], 12, 12, 40, 0, 100);

        Assert.All(points, p => Assert.Equal(50, p!.Value.Y));
    }

    [Fact]
    public void S6164_a_day_without_a_value_breaks_the_curve_and_a_lone_day_is_a_dot()
    {
        var points = ForecastCurve.Points([10, 12, null, 14, double.NaN, 9], 9, 14, 40, 0, 100);
        var runs = ForecastCurve.Smooth(points);

        Assert.Null(points[2]);
        Assert.Null(points[4]);
        Assert.Equal(3, runs.Count);
        Assert.Equal([0, 3, 5], runs.Select(r => r.FirstDay));
        Assert.Single(runs[0].Segments);
        Assert.Empty(runs[1].Segments);
        Assert.Empty(runs[2].Segments);
    }

    [Theory]
    [InlineData(new double[] { 8, 16, 13, 12, 4, 17, 11 })]
    [InlineData(new double[] { 1, 2, 30, 31, 32, -5, -5 })]
    [InlineData(new double[] { 0, 0, 10, 10 })]
    public void S6164_the_curve_passes_through_every_day_and_never_swings_past_its_neighbours(double[] values)
    {
        var points = ForecastCurve.Points([.. values.Select(v => (double?)v)], values.Min(), values.Max(), 60, 10, 110);
        var run = Assert.Single(ForecastCurve.Smooth(points));

        Assert.Equal(points[0], run.Start);

        var previous = run.Start;
        for (var i = 0; i < run.Segments.Count; i++)
        {
            var segment = run.Segments[i];
            Assert.Equal(points[i + 1], segment.End);

            // Monotone: both controls inside the band the piece's two ends span, so the curve
            // between two days rises or falls without a bump past either.
            var low = Math.Min(previous.Y, segment.End.Y) - 1e-9;
            var high = Math.Max(previous.Y, segment.End.Y) + 1e-9;
            Assert.InRange(segment.Control1.Y, low, high);
            Assert.InRange(segment.Control2.Y, low, high);
            Assert.True(segment.Control1.X > previous.X && segment.Control2.X < segment.End.X);
            previous = segment.End;
        }
    }

    [Fact]
    public void S6164_the_curve_shows_the_days_that_have_a_column_and_never_invents_one()
    {
        Assert.Equal(2, ForecastCurve.Plan(Xs, 7, true).Days);
        Assert.Equal(7, ForecastCurve.Plan(Xl, 7, true).Days);
        Assert.Equal(3, ForecastCurve.Plan(Xl, 3, true).Days);
        Assert.Equal(8, ForecastCurve.Plan(new WidgetArea(400, 300), 14, true).Days);
        Assert.All(Tiers, t => Assert.True(ForecastCurve.Plan(t, 7, true).ColumnWidth >= ForecastCurve.ColumnMin - 1e-9));
    }

    [Fact]
    public void S6164_the_glyph_the_lows_and_the_rain_come_with_the_height()
    {
        var small = ForecastCurve.Plan(Xs, 7, true);
        var large = ForecastCurve.Plan(L, 7, true);
        var dry = ForecastCurve.Plan(L, 7, false);

        Assert.False(small.Low);
        Assert.False(small.Precipitation);
        Assert.True(large.Icon && large.Low && large.Precipitation);
        Assert.False(dry.Precipitation);

        // The chart takes what the rungs leave: the parts add up to the column's height.
        var column = L.Height - (2 * ForecastCurve.Padding);
        var used = FluentType.Caption.LineHeight + ForecastCurve.IconSize + large.ChartHeight + FluentType.Caption.LineHeight + (3 * ForecastCurve.Gap);
        Assert.Equal(column, used, 1);
    }

    // ---- stocks number [S-6166] --------------------------------------------------------------

    private static StockPlan Number(WidgetArea area)
        => StockLayout.Plan(area, new StockInputs(6, 10, Chart: false, Extended: false, MinMax: true, Number: true));

    [Fact]
    public void S6166_the_number_style_has_no_chart_and_its_change_on_a_line_of_its_own()
    {
        foreach (var tier in Tiers)
        {
            var plan = Number(tier);
            Assert.False(plan.Chart);
            Assert.True(plan.ExtrasUnder);
            Assert.True(plan.Price.LineHeight + StockLayout.Gap + plan.Change.LineHeight <= tier.Height, $"{tier}");
        }
    }

    [Fact]
    public void S6166_the_price_and_the_change_grow_with_the_tile()
    {
        var large = Number(Xl);

        Assert.True(large.Price.FontSize > FluentType.Display.FontSize, $"price {large.Price.FontSize}");
        Assert.True(large.Change.FontSize >= FluentType.Title.FontSize, $"change {large.Change.FontSize}");
        Assert.True(Number(M).Price.FontSize > StockLayout.Plan(M, new StockInputs(6, 10, true, false, true)).Price.FontSize);
    }

    [Fact]
    public void S6166_the_change_is_the_largest_step_within_half_the_price_and_never_under_body()
    {
        Assert.Equal(FluentType.Title, StockLayout.NumberChangeStep(FluentType.Display));
        Assert.Equal(FluentType.Body, StockLayout.NumberChangeStep(FluentType.Subtitle));
        Assert.Equal(FluentType.TitleLarge, StockLayout.NumberChangeStep(FluentType.Scaled(FluentType.Display, 1.5)));
    }
}
