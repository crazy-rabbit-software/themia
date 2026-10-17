using Themia.Core.Model;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-3212]..[S-3215] The sixth visualizer design: the waveform (issue #136).</summary>
public class VisualizerScopeTests
{
    public static TheoryData<string> Tiers() => [.. WidgetTiers.All.Select(t => t.Name)];

    private static WidgetArea Tier(string name)
    {
        var tier = WidgetTiers.All.Single(t => t.Name == name);
        return new WidgetArea(tier.Width, tier.Height);
    }

    private static float[] Sine(int length, double period, double shift, double amplitude = 0.5)
    {
        var samples = new float[length];
        for (var i = 0; i < length; i++)
        {
            samples[i] = (float)(amplitude * Math.Sin(2 * Math.PI * (i + shift) / period));
        }

        return samples;
    }

    [Fact]
    public void S3212_the_waveform_is_a_visualizer_style_that_never_has_a_header()
    {
        Assert.Contains(WidgetStyles.For(AudioVisualizerConfig.Descriptor.Id), s => s.Value == (int)AudioVisualizerStyle.Waveform && s.Name == "waveform");
        Assert.False(WidgetInstance.CanHaveHeader(AudioVisualizerConfig.Descriptor.Id));
        Assert.Equal(HeaderPlacement.Hidden, WidgetInstance.DefaultHeader(AudioVisualizerConfig.Descriptor.Id));
    }

    [Theory]
    [MemberData(nameof(Tiers))]
    public void S3213_the_line_runs_the_long_side_with_a_point_every_few_dips(string tier)
    {
        var area = Tier(tier);
        var plan = VisualizerLayout.Plan(area, AudioVisualizerStyle.Waveform);
        var along = plan.Horizontal ? area.Height : area.Width;
        var across = plan.Horizontal ? area.Width : area.Height;

        Assert.Equal(area.Shape == AreaShape.Tall, plan.Horizontal);
        Assert.Equal(along, plan.Run);
        Assert.Equal(across, plan.Length);
        Assert.InRange(plan.Bars, VisualizerLayout.MinScopePoints, Oscilloscope.Points);
        Assert.True(plan.Bars == Oscilloscope.Points || plan.Run / (plan.Bars - 1) >= VisualizerLayout.ScopeStep - 0.01, tier);
        Assert.InRange(plan.Thickness, 2, 4);
    }

    [Fact]
    public void S3213_a_larger_tile_draws_more_points_and_a_thicker_line_never_past_the_trace()
    {
        var xs = VisualizerLayout.Plan(Tier("XS"), AudioVisualizerStyle.Waveform);
        var xl = VisualizerLayout.Plan(Tier("XL"), AudioVisualizerStyle.Waveform);
        Assert.True(xl.Bars > xs.Bars);
        Assert.True(xl.Thickness > xs.Thickness);
        Assert.Equal(Oscilloscope.Points, xl.Bars);
    }

    [Fact]
    public void S3214_a_steady_tone_stands_still_whatever_the_phase_the_window_caught_it_at()
    {
        var first = new float[Oscilloscope.Points];
        var later = new float[Oscilloscope.Points];
        Oscilloscope.Trace(Sine(1024, 100, 0), first);
        Oscilloscope.Trace(Sine(1024, 100, 37), later);

        for (var i = 0; i < first.Length; i++)
        {
            Assert.True(Math.Abs(first[i] - later[i]) < 0.15f, $"point {i}: {first[i]} vs {later[i]}");
        }

        // It starts on the rising edge.
        Assert.True(first[1] >= 0 && first[3] > first[1]);
    }

    [Fact]
    public void S3214_a_point_keeps_the_peak_of_its_stretch_and_is_compressed_like_the_spectrum()
    {
        var window = new float[1024];
        window[^100] = -0.25f;
        var trace = new float[Oscilloscope.Points];
        Oscilloscope.Trace(window, trace);

        Assert.Contains(trace, p => Math.Abs(p - (-0.5f)) < 1e-4);
        Assert.All(trace, p => Assert.InRange(p, -1, 1));

        var resampled = new float[8];
        Oscilloscope.Resample([0, 0.2f, -0.9f, 0.1f, 0, 0, 0.4f, float.NaN], resampled.AsSpan(0, 4));
        Assert.Equal(new float[] { 0.2f, -0.9f, 0, 0.4f }, resampled[..4]);
    }

    [Fact]
    public void S3214_no_samples_and_bad_samples_are_silence()
    {
        var trace = Enumerable.Repeat(0.5f, Oscilloscope.Points).ToArray();
        Oscilloscope.Trace([], trace);
        Assert.All(trace, p => Assert.Equal(0, p));

        Oscilloscope.Trace(Enumerable.Repeat(float.PositiveInfinity, 1024).ToArray(), trace);
        Assert.All(trace, p => Assert.Equal(0, p));
    }

    [Fact]
    public void S3215_the_line_follows_the_sound_and_falls_flat_in_silence()
    {
        var shown = new float[4];
        Assert.True(Oscilloscope.Settle(shown, [0.5f, -0.5f, 1, -1], arrived: true));
        Assert.Equal(new float[] { 0.5f, -0.5f, 1, -1 }, shown);

        var frames = 0;
        while (Oscilloscope.Settle(shown, [], arrived: false))
        {
            frames++;
            Assert.True(frames < 40, "the line never settled");
        }

        Assert.All(shown, p => Assert.Equal(0, p));
        Assert.InRange(frames, 10, 25);
    }
}
