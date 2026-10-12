using Themia.Core.Model;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>The shared meter framework: timing, pressure colours, chart scale, the gauge config and memory figures.</summary>
public sealed class MeterFrameworkTests
{
    private const ulong GiB = 1024UL * 1024 * 1024;

    // ---- S-1120 / S-1121: refresh and span ------------------------------------------------------

    [Theory]
    [InlineData(60, 1, 60)]
    [InlineData(900, 5, 180)]
    [InlineData(3600, 1, 3600)]
    [InlineData(3600, 5, 720)]
    [InlineData(99999, 1, MeterTiming.MaxSpanSeconds)]
    [InlineData(1, 5, 2)]
    [InlineData(0, 1, 60)]
    [InlineData(-5, 0, 60)]
    public void S1121_points_are_the_span_at_the_sources_spacing_bounded(int span, int period, int expected)
        => Assert.Equal(expected, MeterTiming.Points(span, period));

    [Theory]
    [InlineData(60, 0, 1)]
    [InlineData(60, double.NaN, 1)]
    [InlineData(60, 500, 1)]
    [InlineData(3600, 300, 24)]
    [InlineData(900, 300, 6)]
    [InlineData(600, 3, 300)]
    public void S1121_a_narrow_chart_folds_neighbouring_points_into_one_slot(int points, double width, int expected)
        => Assert.Equal(expected, MeterTiming.BucketSize(points, width));

    [Fact]
    public void S1121_a_folded_hour_still_spans_the_whole_chart()
    {
        // 3600 points on a 300 DIP tile: 150 slots of 24, the whole hour, never its newest 150 points.
        var size = MeterTiming.BucketSize(3600, 300);
        Assert.Equal(150, MeterTiming.Slots(3600, size));
        Assert.Equal(150, MeterTiming.Fold(new double[3600], 3600, size, peak: false).Length);
    }

    [Fact]
    public void S1121_a_share_folds_to_the_mean_and_a_rate_to_its_peak()
    {
        double[] samples = [0.2, 0.4, 0.6, 1.0];

        Assert.Equal([0.30000000000000004, 0.8], MeterTiming.Fold(samples, 4, 2, peak: false));
        Assert.Equal([0.4, 1.0], MeterTiming.Fold(samples, 4, 2, peak: true));
    }

    [Fact]
    public void S1121_buckets_are_fixed_by_position_so_only_the_newest_moves()
    {
        // Five points ever added, the ring holding the newest four (2..5): buckets of two are
        // {1,2} (only 2 held), {3,4} and {5}. The next point joins {5}; the rest keep their members.
        double[] held = [2, 3, 4, 5];
        Assert.Equal([2, 3.5, 5], MeterTiming.Fold(held, 5, 2, peak: false));

        double[] next = [3, 4, 5, 6];
        Assert.Equal([3.5, 5.5], MeterTiming.Fold(next, 6, 2, peak: false));
    }

    [Fact]
    public void S1121_a_bucket_of_gaps_is_a_gap()
    {
        var folded = MeterTiming.Fold([double.NaN, double.NaN, 1, double.NaN], 4, 2, peak: true);

        Assert.True(double.IsNaN(folded[0]));
        Assert.Equal(1, folded[1]);
    }

    [Theory]
    [InlineData(0, 60)]
    [InlineData(45, 60)]
    [InlineData(200, 300)]
    [InlineData(1200, 900)]
    [InlineData(99999, 3600)]
    public void S1121_a_stored_span_lands_on_the_nearest_offered_one(int stored, int expected)
        => Assert.Equal(expected, MeterTiming.NearestSpan(stored));

    [Fact]
    public void S1121_the_offered_spans_are_one_minute_to_one_hour()
        => Assert.Equal([60, 300, 900, 1800, 3600], MeterTiming.SpanChoices);

    [Theory]
    [InlineData(60, "1 minute")]
    [InlineData(300, "5 minutes")]
    [InlineData(900, "15 minutes")]
    [InlineData(3600, "1 hour")]
    public void S1122_spans_read_as_words(int seconds, string expected)
        => Assert.Equal(expected, MeterTiming.SpanLabel(seconds));

    [Fact]
    public void S1120_each_meter_has_its_default_span()
    {
        Assert.Equal(60, new CpuConfig().GraphSpanSeconds);
        Assert.Equal(60, new MemoryConfig().GraphSpanSeconds);
        Assert.Equal(60, new NetworkConfig().GraphSpanSeconds);
        Assert.Equal(60, new GpuConfig().GraphSpanSeconds);
        Assert.Equal(300, new DiskConfig().GraphSpanSeconds);
        Assert.Equal(900, new BatteryConfig().GraphSpanSeconds);
        Assert.Equal(900, new PingConfig().GraphSpanSeconds);

        foreach (var config in new GaugeConfig[] { new CpuConfig(), new MemoryConfig(), new NetworkConfig(), new GpuConfig(), new DiskConfig(), new BatteryConfig(), new PingConfig() })
        {
            Assert.Contains(config.GraphSpanSeconds, MeterTiming.SpanChoices);
        }
    }

    [Fact]
    public void S1120_repair_snaps_the_span()
    {
        var cpu = new CpuConfig { GraphSpanSeconds = 7 };
        var notes = cpu.Repair();

        Assert.Single(notes);
        Assert.Equal(60, cpu.GraphSpanSeconds);
        Assert.Empty(cpu.Repair());
    }

    [Fact]
    public void S2503_a_stored_cadence_is_not_a_setting()
    {
        var cpu = (CpuConfig)ConfigJson.Read("Cpu", """{ "kind": "cpu", "intervalSeconds": 300 }""");

        Assert.Empty(cpu.Repair());
        Assert.Null(typeof(GaugeConfig).GetProperty("IntervalSeconds"));
    }

    [Fact]
    public void S1120_clone_carries_the_shared_settings()
    {
        var memory = new MemoryConfig { GraphSpanSeconds = 1800, ShowGraph = false };
        var copy = (MemoryConfig)memory.Clone();

        Assert.Equal((1800, false), (copy.GraphSpanSeconds, copy.ShowGraph));
    }

    // ---- S-1123: pressure ------------------------------------------------------------------------

    [Theory]
    [InlineData(0.69, MeterPressure.Normal)]
    [InlineData(0.696, MeterPressure.Elevated)]
    [InlineData(0.70, MeterPressure.Elevated)]
    [InlineData(0.89, MeterPressure.Elevated)]
    [InlineData(0.90, MeterPressure.Critical)]
    [InlineData(1.0, MeterPressure.Critical)]
    public void S1123_usage_turns_amber_at_70_and_red_at_90(double fraction, MeterPressure expected)
        => Assert.Equal(expected, MeterPressures.For(PressureScale.Usage, fraction));

    [Theory]
    [InlineData(0.51, false, MeterPressure.Healthy)]
    [InlineData(0.50, false, MeterPressure.Elevated)]
    [InlineData(0.21, false, MeterPressure.Elevated)]
    [InlineData(0.20, false, MeterPressure.Critical)]
    [InlineData(0.05, true, MeterPressure.Normal)]
    public void S1123_a_battery_is_inverted_and_charging_is_the_accent(double fraction, bool charging, MeterPressure expected)
        => Assert.Equal(expected, MeterPressures.For(PressureScale.Charge, fraction, charging));

    [Fact]
    public void S1123_an_unknown_reading_is_never_a_warning()
    {
        Assert.Equal(MeterPressure.Normal, MeterPressures.For(PressureScale.Usage, null));
        Assert.Equal(MeterPressure.Normal, MeterPressures.For(PressureScale.Charge, double.NaN));
        Assert.Equal(MeterPressure.Normal, MeterPressures.For(PressureScale.None, 1));
    }

    // ---- S-1124: chart scale ---------------------------------------------------------------------

    [Fact]
    public void S1124_a_share_chart_tops_out_at_100_percent()
        => Assert.Equal(1, ChartScale.Share.Ceiling([0.2, 0.4]));

    [Fact]
    public void S1124_a_rate_chart_tops_out_at_the_windows_peak_not_the_sessions()
    {
        var scale = ChartScale.Peak(1024);

        // Only what is visible counts: a burst that has scrolled off no longer flattens the trace.
        Assert.Equal(5000, scale.Ceiling([100, 5000, 300]));
        Assert.Equal(1024, scale.Ceiling([10, 20]));
        Assert.Equal(9000, scale.Ceiling([100, 200], [9000, double.NaN]));
    }

    [Theory]
    [InlineData(new double[] { 10, 20 }, 1024)]
    [InlineData(new double[] { 1024 }, 1024)]
    [InlineData(new double[] { 1025 }, 2048)]
    [InlineData(new double[] { 100, 5000, 300 }, 8192)]
    [InlineData(new double[] { double.NaN, double.PositiveInfinity }, 1024)]
    public void S1821_a_doubling_chart_tops_out_at_the_floor_doubled_past_the_windows_peak(double[] samples, double expected)
        => Assert.Equal(expected, ChartScale.Doubling(1024).Ceiling(samples));

    [Fact]
    public void S1821_a_doubling_chart_counts_the_second_series_too()
        => Assert.Equal(4096, ChartScale.Doubling(1024).Ceiling([100], [3000]));

    [Fact]
    public void S1124_fractions_are_clamped_and_gaps_stay_gaps()
    {
        Assert.Equal(0.5, ChartScale.Fraction(512, 1024));
        Assert.Equal(1, ChartScale.Fraction(4096, 1024));
        Assert.True(double.IsNaN(ChartScale.Fraction(double.NaN, 1024)));
        Assert.Equal(0, ChartScale.Fraction(5, 0));
    }

    // ---- S-1130: memory figures ----------------------------------------------------------

    [Fact]
    public void S1130_used_is_the_total_less_what_is_available()
    {
        var s = new MemorySnapshot(16 * GiB, 6 * GiB);
        Assert.Equal(10 * GiB, MemoryFigures.Used(s));
        Assert.Equal(0.625, MemoryFigures.UsedFraction(s));
        Assert.Null(MemoryFigures.UsedFraction(s with { Total = 0 }));
    }
}
