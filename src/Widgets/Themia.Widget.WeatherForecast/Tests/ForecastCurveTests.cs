using Themia.Core.Model;
using Themia.Core.Tests;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;
using static Themia.Core.Tests.StyleTiers;

namespace Themia.Widget.WeatherForecast.Tests;

/// <summary>
/// [S-6164] The forecast curve, the forecast's one look ([S-15540]): its geometry, its days and its sizes.
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
    public void S11146_S14810_the_curve_shows_the_days_its_width_reaches_each_a_column_of_its_one_width_and_every_part()
    {
        var config = new WeatherForecastConfig();
        var plan = ForecastCurve.Plan(new WidgetArea(300, 160), config);

        Assert.Equal(5, plan.Days);
        Assert.Equal(60, plan.ColumnWidth, 6);
        Assert.True(plan.Icon && plan.Low && plan.Precipitation);
        Assert.Equal(148 - ForecastCurve.Parts(true), plan.ChartHeight, 6);
        Assert.False(ForecastCurve.Plan(new WidgetArea(304, 160), new WeatherForecastConfig { ShowPrecipitation = false }).Precipitation);

        // The points stand under the highs' figures and over the lows', the stroke inside the band.
        Assert.Equal(17, plan.Top);
        Assert.Equal(plan.ChartHeight - 16 - 1, plan.Bottom);
    }

    [Fact]
    public void S11146_S14410_the_curve_is_a_60_day_a_new_one_five_and_its_days_what_its_width_reaches()
    {
        var config = new WeatherForecastConfig();
        var sizing = ForecastCurve.Sizing(config);

        Assert.Equal((60.0, 5 * 60.0, 14 * 60.0), (sizing.Min.Width, sizing.Natural.Width, sizing.Max.Width));
        // [S-6755] The chart is the one band (72 new, 48 least) and the figures' two Caption lines.
        Assert.Equal(12 + ForecastCurve.Parts(true) + 104 + 12, sizing.Natural.Height);
        Assert.Equal(sizing.Natural.Height - 104 + 80, sizing.Min.Height);

        // [S-14502] A card two days wider shows seven days; its height is the chart's, never a count.
        Assert.Equal(7, ForecastCurve.Plan(new WidgetArea(7 * 60, 300), config).Days);
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

    [Fact]
    public void S11145_S14502_S14810_the_days_are_every_column_the_width_reaches_never_stored()
    {
        // Owner, 2026-11-20: "they should just keep their size and the last item will be cutoff to some extent then":
        // three columns wide three days, a sliver more a cut fourth; a box not yet measured a new one's five.
        Assert.Equal(WeatherForecastConfig.DefaultDays, ForecastCurve.DaysIn(default));
        Assert.Equal(3, ForecastCurve.DaysIn(new WidgetArea(3 * 60, 200)));
        Assert.Equal(4, ForecastCurve.DaysIn(new WidgetArea((3 * 60) + 10, 200)));
        Assert.Equal(WeatherForecastConfig.MostDays, ForecastCurve.DaysIn(new WidgetArea(4000, 200)));
        Assert.Equal(WeatherForecastConfig.MostDays, new WeatherForecastConfig().ForecastDays);
    }

    [Fact]
    public void S11146_the_rain_line_is_counted_in_the_height_and_a_copy_keeps_the_switch()
    {
        var wet = ForecastCurve.Sizing(new WeatherForecastConfig());
        var dry = ForecastCurve.Sizing(new WeatherForecastConfig { ShowPrecipitation = false });

        Assert.Equal(ForecastCurve.Parts(true) - ForecastCurve.Parts(false), wet.Natural.Height - dry.Natural.Height, 6);
        Assert.Equal(wet.Natural.Width, dry.Natural.Width);
        Assert.False(((WeatherForecastConfig)new WeatherForecastConfig { ShowPrecipitation = false }.Clone()).ShowPrecipitation);
    }

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
}
