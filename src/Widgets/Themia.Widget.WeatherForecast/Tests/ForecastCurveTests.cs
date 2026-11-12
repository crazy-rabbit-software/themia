using Themia.Core.Model;
using Themia.Core.Tests;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;
using static Themia.Core.Tests.StyleTiers;

namespace Themia.Widget.WeatherForecast.Tests;

/// <summary>
/// [S-6164] The temperature curve style. Moved with the widget from Core's WeatherStylesTests,
/// unchanged in what they assert.
/// </summary>
public sealed class ForecastCurveTests
{
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
    public void S11146_the_curve_shows_the_configs_days_each_a_column_of_the_content_and_every_part()
    {
        var config = new WeatherForecastConfig { Style = ForecastStyle.Curve, Days = 5 };
        var plan = ForecastCurve.Plan(new WidgetArea(304, 160), config);

        Assert.Equal(5, plan.Days);
        Assert.Equal(280 / 5.0, plan.ColumnWidth, 6);
        Assert.True(plan.Icon && plan.Low && plan.Precipitation);
        Assert.Equal(148 - ForecastCurve.Parts(true), plan.ChartHeight, 6);
        Assert.False(ForecastCurve.Plan(new WidgetArea(304, 160), new WeatherForecastConfig { Days = 5, ShowPrecipitation = false }).Precipitation);

        // The points stand under the highs' figures and over the lows', the stroke inside the band.
        Assert.Equal(17, plan.Top);
        Assert.Equal(plan.ChartHeight - 16 - 1, plan.Bottom);
    }

    [Fact]
    public void S11146_the_curve_s_size_follows_the_days_with_a_small_range()
    {
        var sizing = WeatherForecastLayout.Sizing(new WeatherForecastConfig { Style = ForecastStyle.Curve, Days = 5 });

        Assert.Equal((24 + (5 * 48.0), 24 + (5 * 56.0), 24 + (5 * 72.0)), (sizing.Min.Width, sizing.Natural.Width, sizing.Max.Width));
        Assert.Equal(36 + ForecastCurve.Parts(true) + 96 + 12, sizing.Natural.Height);
        Assert.Equal(sizing.Natural.Height - 96 + 72, sizing.Min.Height);
    }

    [Fact]
    public void S6164_the_smooth_line_is_drawn_as_straight_steps_through_every_day()
    {
        var points = ForecastCurve.Points([8, 16, 12], 8, 16, 50, 0, 100);
        var run = Assert.Single(ForecastCurve.Smooth(points));
        var line = ForecastCurve.Sample(run, 4);

        Assert.Equal(1 + (2 * 4), line.Count);
        Assert.Equal(points[0], line[0]);
        Assert.Equal(points[1], line[4]);
        Assert.Equal(points[2], line[8]);
        for (var k = 1; k < line.Count; k++)
        {
            Assert.True(line[k].X > line[k - 1].X);
            Assert.InRange(line[k].Y, -1e-9, 100 + 1e-9);
        }
    }
}
