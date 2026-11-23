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
        // [S-6754] Its words keep the family's inset off the plot's edges: the high's line over the line, the low's and
        // the times' (4 apart) under it.
        const double line = FamilyType.CaptionLine;
        Assert.Equal((0.0, 0.0), ChartPlot.Room(ChartDetail.Simple));
        Assert.Equal((FamilySpace.Inset + line, FamilySpace.Inset + line + FamilySpace.S1 + line), ChartPlot.Room(ChartDetail.Detailed));

        var sizing = WidgetSizing.Range(new(160, 48), new(160, 64), new(640, 200));
        Assert.Same(sizing, ChartPlot.Sized(sizing, ChartDetail.Simple, 0, 0));
        var taller = ChartPlot.Sized(sizing, ChartDetail.Detailed, 0, 0);
        Assert.Equal(48 + 28 + 48, taller.Min.Height);
        Assert.Equal(160, taller.Min.Width);

        // A kind that keeps the room already (a weather graph: its figures' lines and its time line) grows by nothing.
        Assert.Same(sizing, ChartPlot.Sized(sizing, ChartDetail.Detailed, 28, 48));
    }

    /// <summary>Every chart a graph kind draws: Simple and Detailed, each type, an owner's top line and arrow line, ends, no data.</summary>
    public static TheoryData<string> Looks => ["history", "detailed", "bars", "candles", "weather", "wind", "weather-detailed", "stock", "empty"];

    private static ChartModel Look(string look) => look switch
    {
        "history" => new ChartModel { Series = [new(0, 0.2), new(80, 0.9), new(160, 0.4)] },
        "detailed" => new ChartModel { Detail = ChartDetail.Detailed, Series = [new(0, 0.2), new(160, 0.4)] },
        "bars" => new ChartModel { Type = ChartType.Bars, Series = [new(20, 0.2), new(60, 1)] },
        "candles" => new ChartModel { Type = ChartType.Candles, Detail = ChartDetail.Detailed, Series = [new(0, 3), new(160, 4)], Low = 3, High = 4 },
        "weather" => new ChartModel { Over = 40, PadTop = FamilyType.CaptionLine, PadBottom = FamilyType.CaptionLine, Ends = new("Now", "17:00") },
        "wind" => new ChartModel { Over = 40, Under = FamilyShape.Icon + FamilySpace.S1, PadTop = FamilyType.CaptionLine, PadBottom = FamilyType.CaptionLine, Ends = new("Now", "17:00") },
        "weather-detailed" => new ChartModel { Detail = ChartDetail.Detailed, Over = 40, PadTop = FamilyType.CaptionLine, PadBottom = FamilyType.CaptionLine },
        "stock" => new ChartModel { PadTop = FamilyType.CaptionLine, PadBottom = FamilyType.CaptionLine, Ends = new("9:30 AM", "4:00 PM") },
        _ => new ChartModel { Caption = "No data yet" },
    };

    [Theory]
    [MemberData(nameof(Looks))]
    public void S6754_the_plot_is_the_card_edge_to_edge_and_what_is_written_stands_inside_it(string look)
    {
        // Owner, 2026-11-19: "Plot edge to edge": the plot and its fill reach the card's edges, the labels inside it.
        var model = Look(look);
        foreach (var (w, h) in ((double, double)[])[(160, 124), (320, 168), (600, 200), (900, 600)])
        {
            var frame = ChartPlot.Frame(model, w, h);
            Assert.Equal(new ChartBox(0, 0, w, h), frame.Plot);
            Assert.Equal(new ChartBox(FamilySpace.Inset, FamilySpace.Inset, w - (2 * FamilySpace.Inset), h - (2 * FamilySpace.Inset)), frame.Text);
            Assert.True(frame.Plot.Holds(frame.Text));

            // The series' band inside the plot, under the owner's parts over its top, over what is written at its foot.
            Assert.InRange(frame.LineTop, model.Over, frame.LineBottom);
            Assert.InRange(frame.LineBottom, frame.LineTop, h);
            Assert.True(frame.CaptionTop >= Math.Max(model.Over, FamilySpace.Inset));

            // The foot's words (times or ends) at the inset off the foot, the owner's parts over them, both under the band.
            Assert.Equal(h - FamilySpace.Inset, frame.FootLine + FamilyType.CaptionLine, 6);
            var writesFoot = model.Detail == ChartDetail.Detailed || model.Ends is not null;
            if (writesFoot)
            {
                Assert.True(frame.Text.Holds(new ChartBox(frame.Text.Left, frame.FootLine, 10, FamilyType.CaptionLine)));
                Assert.True(frame.LineBottom <= frame.UnderTop - FamilySpace.S1 + 1e-9);
                Assert.Equal(frame.FootLine - model.Under, frame.UnderTop, 6);
                Assert.True(frame.RulesBottom <= frame.UnderTop);
            }

            // A label anywhere is kept inside the text box and under the owner's parts.
            foreach (var at in (double[])[-50, 0, w / 2, w, w + 50])
            {
                var left = ChartPlot.Across(frame, at, 40);
                var top = ChartPlot.Within(frame, at - 100, FamilyType.CaptionLine);
                Assert.True(frame.Text.Holds(new ChartBox(left, top, 40, FamilyType.CaptionLine)), $"{look} {w}x{h} at {at}");
                Assert.True(top >= frame.CaptionTop);
            }
        }
    }

    [Fact]
    public void S6754_a_simple_chart_with_nothing_written_runs_its_line_and_hairlines_edge_to_edge()
    {
        var frame = ChartPlot.Frame(new ChartModel(), 160, 48);
        Assert.Equal((0.0, 48.0), (frame.LineTop, frame.LineBottom));
        Assert.Equal((0.0, 48.0), (frame.RulesTop, frame.RulesBottom));
        Assert.Equal((0.0, 0.0), ChartPlot.Keeps(new ChartModel()));

        // An owner's top line keeps the line under it; its hairlines start under it too.
        var owned = ChartPlot.Frame(new ChartModel { Over = 40 }, 320, 168);
        Assert.Equal((40.0, 168.0), (owned.LineTop, owned.LineBottom));
        Assert.Equal(40, owned.RulesTop);
    }

    [Fact]
    public void S6754_with_no_data_the_frame_is_the_same_and_its_baseline_runs_the_plot_edge_to_edge()
    {
        var data = new ChartModel { Detail = ChartDetail.Detailed, Over = 40, Series = [new(0, 0.3), new(320, 0.6)] };
        var none = data with { Series = [], Caption = "No data yet" };
        Assert.Equal(ChartPlot.Frame(data, 320, 168), ChartPlot.Frame(none, 320, 168));

        var baseline = ChartPlot.Drawn(none, 320);
        Assert.Equal([new ChartSample(0, 0), new ChartSample(320, 0)], baseline);
        var frame = ChartPlot.Frame(none, 320, 168);
        Assert.True(frame.Text.Holds(new ChartBox(frame.Text.Left, frame.CaptionTop, 80, FamilyType.CaptionLine)));
    }

    [Fact]
    public void S6754_a_line_reaches_the_plot_sides_from_its_cells_but_a_history_filling_in_does_not()
    {
        // A sample on its cell's middle (a weather graph's hours): held out to both sides.
        ChartSample[] cells = [new(10, 1, ColourRole.Caution), new(30, 2), new(50, 3)];
        var edges = ChartPlot.ToEdges(cells, 60);
        Assert.Equal([new ChartSample(0, 1, ColourRole.Caution), .. cells, new ChartSample(60, 3)], edges);

        // A history still filling in from the right edge: its oldest sample stays where it is.
        ChartSample[] filling = [new(100, 1), new(110, 2), new(120, 3)];
        Assert.Equal([.. filling], ChartPlot.ToEdges(filling, 120));

        // A gap at a side stays a gap; a single sample is no line to hold out.
        ChartSample[] gap = [new(10, double.NaN), new(30, 2), new(50, double.NaN)];
        Assert.Same(gap, ChartPlot.ToEdges(gap, 60));
        ChartSample[] one = [new(10, 1)];
        Assert.Same(one, ChartPlot.ToEdges(one, 60));
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

    /// <summary>[S-16450] Columns spread across the plot edge to edge: the first on its left edge, the last on its right, alike, 4 apart.</summary>
    [Theory]
    [InlineData(8, 900)]
    [InlineData(8, 220)]
    [InlineData(1, 300)]
    public void S16450_columns_spread_edge_to_edge(int count, double width)
    {
        var first = ChartPlot.Spread(0, count, width);
        var last = ChartPlot.Spread(count - 1, count, width);

        Assert.Equal(0, first.Left);
        Assert.Equal(width, last.Left + last.Width, 6);
        Assert.Equal(first.Width, last.Width, 6);
        if (count > 1)
        {
            Assert.Equal(FamilySpace.S1, ChartPlot.Spread(1, count, width).Left - first.Width, 6);
        }

        Assert.Equal((0.0, 0.0), ChartPlot.Spread(0, 0, width));
    }
}
