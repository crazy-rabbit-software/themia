using Themia.Sdk;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-6750]..[S-6753] The one chart's geometry: the outline, the candles, the value axis, the times, the room and the baseline.</summary>
public sealed class ChartPlotTests
{
    private static readonly ChartPoint[] Three = [new(0, 10), new(50, 4), new(100, 7)];

    [Fact]
    public void S6750_an_area_closes_its_line_down_to_the_foot_and_a_line_has_no_wash()
    {
        var area = ChartPlot.Outline(Three, ChartType.Area, 20);
        Assert.Equal(Three, area.Line);
        Assert.Equal([.. Three, new ChartPoint(100, 20), new ChartPoint(0, 20)], area.Area);

        var line = ChartPlot.Outline(Three, ChartType.Line, 20);
        Assert.Equal(Three, line.Line);
        Assert.Empty(line.Area);
    }

    [Fact]
    public void S6750_columns_candles_and_a_single_point_are_no_outline()
    {
        Assert.Same(ChartOutline.None, ChartPlot.Outline(Three, ChartType.Bars, 20));
        Assert.Same(ChartOutline.None, ChartPlot.Outline(Three, ChartType.Candles, 20));
        Assert.Same(ChartOutline.None, ChartPlot.Outline([new ChartPoint(0, 1)], ChartType.Area, 20));
    }

    [Fact]
    public void S6750_points_are_bucketed_into_periods_of_their_first_last_lowest_and_highest()
    {
        Candle[] points = [.. new[] { 1.0, 3, 2, 5, 4, 6 }.Select(Candle.Point)];
        Assert.Equal([new Candle(1, 3, 1, 3), new Candle(2, 5, 2, 5), new Candle(4, 6, 4, 6)], ChartPlot.Bucket(points, 3));

        // Uneven runs: seven samples in three periods split 2, 2, 3.
        Candle[] seven = [.. new[] { 5.0, 4, 3, 6, 2, 8, 7 }.Select(Candle.Point)];
        Assert.Equal([new Candle(5, 5, 4, 4), new Candle(3, 6, 3, 6), new Candle(2, 8, 2, 7)], ChartPlot.Bucket(seven, 3));
    }

    [Fact]
    public void S6750_real_periods_keep_their_open_high_low_and_close()
    {
        Candle[] bars = [new(10, 12, 9, 11), new(11, 15, 10, 14), new(14, 14, 8, 9)];
        Assert.Equal([new Candle(10, 12, 9, 11), new Candle(11, 15, 8, 9)], ChartPlot.Bucket(bars, 2));
        Assert.Equal([new Candle(10, 15, 8, 9)], ChartPlot.Bucket(bars, 1));
    }

    [Fact]
    public void S6750_bucketing_leaves_out_what_is_not_finite_and_never_makes_more_periods_than_samples()
    {
        Candle[] mixed = [Candle.Point(1), Candle.Point(double.NaN), Candle.Point(3)];
        Assert.Equal([Candle.Point(1), Candle.Point(3)], ChartPlot.Bucket(mixed, 10));
        Assert.Empty(ChartPlot.Bucket([], 4));
        Assert.Empty(ChartPlot.Bucket(mixed, 0));
        Assert.Equal(16, ChartPlot.CandleCount(400, 200));
        Assert.Equal(3, ChartPlot.CandleCount(3, 200));
        Assert.Equal(20, ChartPlot.CandleCount(80, 400, real: false)); // four points a candle at least
        Assert.Equal(1, ChartPlot.CandleCount(3, 400, real: false));
        Assert.Equal(0, ChartPlot.CandleCount(10, 0));
    }

    [Fact]
    public void S6750_a_candle_is_its_body_from_open_to_close_and_its_wick_from_low_to_high()
    {
        static double Y(double v) => 100 - v;

        var rise = ChartPlot.Shape(new Candle(10, 30, 5, 20), 50, 10, Y);
        Assert.Equal(new CandleShape(47, 80, 6, 10, 50, 70, 95, Rises: true), rise);

        var fall = ChartPlot.Shape(new Candle(20, 25, 10, 12), 50, 10, Y);
        Assert.False(fall.Rises);
        Assert.Equal((80.0, 8.0), (fall.BodyTop, fall.BodyHeight));
    }

    [Fact]
    public void S6750_a_period_that_closed_where_it_opened_still_shows_a_body_at_the_stroke()
    {
        var flat = ChartPlot.Shape(new Candle(10, 10, 10, 10), 20, 100, v => 100 - v);
        Assert.Equal(FamilyShape.Stroke, flat.BodyHeight);
        Assert.Equal(90 - (FamilyShape.Stroke / 2), flat.BodyTop);
        Assert.Equal(FamilyShape.IconLarge / 2, flat.BodyWidth);
    }

    [Theory]
    [InlineData(0, 1, 5, new[] { 0, 0.5, 1 })]
    [InlineData(0, 100, 5, new[] { 0.0, 50, 100 })]
    [InlineData(0, 100, 6, new[] { 0.0, 20, 40, 60, 80, 100 })]
    [InlineData(-3.7, 12.2, 5, new[] { 0.0, 5, 10 })]
    [InlineData(17.2, 18.9, 4, new[] { 18.0 })]
    [InlineData(0, 1_250_000, 4, new[] { 0.0, 500_000, 1_000_000 })]
    [InlineData(0.1, 0.35, 4, new[] { 0.1, 0.2, 0.3 })]
    public void S6751_value_ticks_stand_on_nice_round_steps(double low, double high, int most, double[] ticks)
        => Assert.Equal(ticks, ChartPlot.Ticks(low, high, most));

    [Fact]
    public void S6751_ticks_are_never_more_than_asked_and_a_range_that_is_none_has_none()
    {
        var random = new Random(6741);
        for (var i = 0; i < 2000; i++)
        {
            var low = (random.NextDouble() - 0.5) * Math.Pow(10, random.Next(-3, 7));
            var high = low + (random.NextDouble() * Math.Pow(10, random.Next(-3, 7)));
            var most = random.Next(1, 7);
            var ticks = ChartPlot.Ticks(low, high, most);
            Assert.True(ticks.Count <= most, $"{low}..{high} at most {most}: {ticks.Count}");
            var slack = ((high - low) * 1e-6) + 1e-9;
            Assert.All(ticks, t => Assert.InRange(t, low - slack, high + slack));
        }

        Assert.Empty(ChartPlot.Ticks(1, 1, 5));
        Assert.Empty(ChartPlot.Ticks(2, 1, 5));
        Assert.Empty(ChartPlot.Ticks(double.NaN, 1, 5));
    }

    [Fact]
    public void S6751_a_byte_rate_steps_in_binary_units()
    {
        const double MiB = 1024 * 1024;
        Assert.Equal(MiB, ChartPlot.BinaryUnit(8 * MiB));
        Assert.Equal(1, ChartPlot.BinaryUnit(900));
        Assert.Equal(1024, ChartPlot.BinaryUnit(1024));
        Assert.Equal([0, 5 * MiB], ChartPlot.Ticks(0, 8.4 * MiB, 3, MiB));
        Assert.Equal(ChartPlot.Ticks(0, 100, 5), ChartPlot.Ticks(0, 100, 5, 1));
    }

    [Fact]
    public void S6751_times_stand_on_whole_steps_of_the_clock()
    {
        var zone = TimeZoneInfo.Utc;
        var end = new DateTimeOffset(2026, 11, 19, 10, 42, 30, TimeSpan.Zero);
        var (minute, step) = ChartPlot.Times(end.AddSeconds(-60), end, 4, zone);
        Assert.Equal(TimeSpan.FromSeconds(30), step);
        Assert.Equal([end.AddSeconds(-60), end.AddSeconds(-30), end], minute);

        var (hours, hourStep) = ChartPlot.Times(end, end.AddHours(24), 5, zone);
        Assert.Equal(TimeSpan.FromHours(6), hourStep);
        Assert.Equal(new DateTimeOffset(2026, 11, 19, 12, 0, 0, TimeSpan.Zero), hours[0]);
        Assert.All(hours, h => Assert.Equal(0, h.Hour % 6));
        Assert.Equal(0.5, ChartPlot.Share(end.AddHours(12), end, end.AddHours(24)));
    }

    [Fact]
    public void S6751_detailed_keeps_room_for_what_it_writes_and_simple_none()
    {
        Assert.Equal((0.0, 0.0), ChartPlot.Room(ChartDetail.Simple, times: true));
        Assert.Equal((FamilyType.CaptionLine, FamilyType.CaptionLine + FamilySpace.S1 + FamilyType.CaptionLine), ChartPlot.Room(ChartDetail.Detailed, times: true));

        var sizing = WidgetSizing.Range(new(160, 48), new(160, 64), new(640, 200));
        Assert.Same(sizing, ChartPlot.Sized(sizing, ChartDetail.Simple, 0, 0));
        var taller = ChartPlot.Sized(sizing, ChartDetail.Detailed, 0, 0);
        Assert.Equal(48 + 52, taller.Min.Height);
        Assert.Equal(160, taller.Min.Width);

        // A kind that keeps the room already (a weather graph: its figures' lines and its time line) grows by nothing.
        Assert.Same(sizing, ChartPlot.Sized(sizing, ChartDetail.Detailed, FamilyType.CaptionLine, 52));
    }

    [Fact]
    public void S6753_an_empty_series_is_drawn_as_its_baseline_across_the_plot()
    {
        var model = new ChartModel { Baseline = 0, Low = 0, High = 1 };
        Assert.Equal([new ChartSample(0, 0), new ChartSample(200, 0)], ChartPlot.Drawn(model, 200));

        // One sample, or none finite, is no line either.
        Assert.Equal(2, ChartPlot.Drawn(model with { Series = [new ChartSample(5, 3)] }, 200).Count);
        Assert.Equal(200, ChartPlot.Drawn(model with { Series = [new(0, double.NaN), new(100, double.NaN)], Baseline = 7 }, 200)[^1].X);
        Assert.Equal(7, ChartPlot.Drawn(model with { Baseline = 7 }, 200)[0].Value);
    }

    [Fact]
    public void S6753_an_all_zero_series_is_drawn_as_it_is_and_keeps_its_axis()
    {
        ChartSample[] zeros = [new(0, 0), new(50, 0), new(100, 0)];
        var model = new ChartModel { Series = zeros, Low = 0, High = 2 };
        Assert.Same(zeros, ChartPlot.Drawn(model, 100));
        Assert.Equal(0, model.Fraction(0));
        Assert.Equal([0.0, 1, 2], ChartPlot.Ticks(model.Low, model.High, ChartPlot.ValueRoom(100)));
    }

    [Fact]
    public void S6750_runs_break_at_gaps_and_a_piece_takes_the_worse_role_of_its_ends()
    {
        ChartSample[] samples = [new(0, 1), new(1, 2), new(2, double.NaN), new(3, 4), new(4, double.PositiveInfinity), new(5, 6), new(6, 7)];
        Assert.Equal([(0, 1), (3, 3), (5, 6)], ChartPlot.Runs(samples));
        Assert.True(ChartPlot.HasLine(samples));
        Assert.False(ChartPlot.HasLine([new(0, 1), new(1, double.NaN), new(2, 1)]));
        Assert.Equal(ColourRole.Critical, ChartPlot.Worse(ColourRole.Caution, ColourRole.Critical));
    }

    [Fact]
    public void S6750_a_flat_scale_puts_every_value_in_the_middle()
    {
        var flat = new ChartModel { Low = 3, High = 3 };
        Assert.Equal(0.5, flat.Fraction(3));
        Assert.True(double.IsNaN(flat.Fraction(double.NaN)));
        Assert.Equal(1, new ChartModel { Low = 0, High = 10 }.Fraction(15));
    }
}
