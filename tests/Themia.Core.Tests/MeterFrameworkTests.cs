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
    [InlineData(60, 2, 30)]
    [InlineData(300, 15, 20)]
    [InlineData(1200, 10, 120)]
    [InlineData(3600, 1, MeterTiming.MaxCapacity)]
    [InlineData(30, 60, 2)]
    [InlineData(0, 1, 60)]
    [InlineData(-5, 0, 60)]
    public void S1121_capacity_is_the_span_over_the_interval_bounded(int span, int interval, int expected)
        => Assert.Equal(expected, MeterTiming.Capacity(span, interval));

    [Theory]
    [InlineData(60, 0, 60)]
    [InlineData(60, double.NaN, 60)]
    [InlineData(60, 500, 60)]
    [InlineData(600, 500, 250)]
    [InlineData(600, 3, 2)]
    public void S1121_a_narrow_chart_draws_only_the_newest_samples_it_has_slots_for(int capacity, double width, int expected)
        => Assert.Equal(expected, MeterTiming.Window(capacity, width));

    [Theory]
    [InlineData(0, 30)]
    [InlineData(45, 30)]
    [InlineData(100, 120)]
    [InlineData(1500, 1200)]
    [InlineData(99999, 3600)]
    public void S1121_a_stored_span_lands_on_the_nearest_offered_one(int stored, int expected)
        => Assert.Equal(expected, MeterTiming.NearestSpan(stored));

    [Theory]
    [InlineData(30, "30 seconds")]
    [InlineData(60, "1 minute")]
    [InlineData(180, "3 minutes")]
    [InlineData(1200, "20 minutes")]
    [InlineData(3600, "1 hour")]
    public void S1122_spans_read_as_words(int seconds, string expected)
        => Assert.Equal(expected, MeterTiming.SpanLabel(seconds));

    [Fact]
    public void S1120_each_meter_has_the_originals_defaults()
    {
        Assert.Equal((1, 60), Timing(new CpuConfig()));
        Assert.Equal((1, 60), Timing(new MemoryConfig()));
        Assert.Equal((1, 60), Timing(new NetworkConfig()));
        Assert.Equal((5, 180), Timing(new DiskConfig()));
        Assert.Equal((15, 300), Timing(new BatteryConfig()));
        Assert.Equal((10, 1200), Timing(new PingConfig()));
        Assert.Equal((2, 60), Timing(new GpuConfig()));

        static (int, int) Timing(GaugeConfig config) => (config.IntervalSeconds, config.GraphSpanSeconds);
    }

    [Fact]
    public void S1120_repair_clamps_the_refresh_and_snaps_the_span()
    {
        var cpu = new CpuConfig { GraphSpanSeconds = 7 };
        var notes = cpu.Repair();

        Assert.Single(notes);
        Assert.Equal(30, cpu.GraphSpanSeconds);
        Assert.Empty(cpu.Repair());
    }

    [Fact]
    public void S3793_a_stored_cadence_is_not_read_the_kind_keeps_its_own()
    {
        var cpu = (CpuConfig)ConfigJson.Read("Cpu", """{ "kind": "cpu", "intervalSeconds": 300 }""");

        Assert.Equal(new CpuConfig().IntervalSeconds, cpu.IntervalSeconds);
        Assert.Empty(cpu.Repair());
    }

    [Fact]
    public void S1120_clone_carries_the_shared_settings()
    {
        var memory = new MemoryConfig { GraphSpanSeconds = 600, ShowGraph = false };
        var copy = (MemoryConfig)memory.Clone();

        Assert.Equal((600, false), (copy.GraphSpanSeconds, copy.ShowGraph));
        Assert.Equal(MeterTiming.Capacity(600, memory.IntervalSeconds), copy.GraphCapacity);
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
