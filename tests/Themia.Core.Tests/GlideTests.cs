using Themia.Core.Motion;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-6323]..[S-6328] Readouts glide between readings: the progress, the steps, the colours, the switch.</summary>
public sealed class GlideTests
{
    private static readonly TimeSpan Second = TimeSpan.FromSeconds(1);

    [Fact]
    public void S6323_a_glide_is_linear_over_its_period_and_ends_on_the_target()
    {
        Assert.Equal(0, Glide.Progress(TimeSpan.Zero, Second));
        Assert.Equal(0.25, Glide.Progress(TimeSpan.FromMilliseconds(250), Second), 9);
        Assert.Equal(1, Glide.Progress(TimeSpan.FromSeconds(3), Second));
        Assert.Equal(0, Glide.Progress(TimeSpan.FromSeconds(-1), Second));

        // A period of nothing is done at once.
        Assert.Equal(1, Glide.Progress(TimeSpan.FromMilliseconds(5), TimeSpan.Zero));

        Assert.Equal(42, Glide.At(42, 45, 0));
        Assert.Equal(43.5, Glide.At(42, 45, 0.5), 9);
        Assert.Equal(45, Glide.At(42, 45, 1));
        Assert.Equal(45, Glide.At(42, 45, double.NaN));
    }

    [Fact]
    public void S6324_only_two_numbers_glide_and_only_with_animation_effects_on()
    {
        Assert.Equal(GlideMove.Glide, Glide.Plan(true, 0.42, 0.45));
        Assert.Equal(GlideMove.Still, Glide.Plan(true, 0.45, 0.45));

        // [S-6326] Animation effects off: every reading is shown at once, as before.
        Assert.Equal(GlideMove.Jump, Glide.Plan(false, 0.42, 0.45));

        // A reading appearing, vanishing or failing is a state, not a value between two others.
        Assert.Equal(GlideMove.Jump, Glide.Plan(true, null, 0.45));
        Assert.Equal(GlideMove.Jump, Glide.Plan(true, 0.42, null));
        Assert.Equal(GlideMove.Jump, Glide.Plan(true, double.NaN, 0.45));
        Assert.Equal(GlideMove.Jump, Glide.Plan(true, 0.42, double.PositiveInfinity));
    }

    [Fact]
    public void S6325_a_percentage_counts_in_whole_percents_landing_on_the_reading()
    {
        var quantum = Glide.Quantum(0.42, 0.45, 0.01, Second);
        Assert.Equal(0.01, quantum, 9);

        var shown = Enumerable.Range(0, 101).Select(i => Math.Round(Glide.Shown(0.42, 0.45, i / 100.0, quantum), 6)).Distinct().ToArray();
        Assert.Equal([0.42, 0.43, 0.44, 0.45], shown);

        // Each step is a value the line has reached: never ahead of it by more than half a step.
        for (var i = 0; i <= 100; i++)
        {
            var p = i / 100.0;
            Assert.InRange(Glide.Shown(0.42, 0.45, p, quantum) - Glide.At(0.42, 0.45, p), -0.005 - 1e-9, 0.005 + 1e-9);
        }

        // Downwards too.
        Assert.Equal(0.45, Glide.Shown(0.45, 0.42, 0, quantum), 9);
        Assert.Equal(0.42, Glide.Shown(0.45, 0.42, 1, quantum), 9);
    }

    [Fact]
    public void S6325_a_long_way_takes_at_most_twenty_steps_a_second_and_a_rate_has_no_step_of_its_own()
    {
        // 0 % to 100 % in a second: five percent a step, not a hundred digit changes.
        Assert.Equal(0.05, Glide.Quantum(0, 1, 0.01, Second), 9);

        // A ping's five seconds may take a hundred.
        Assert.Equal(1, Glide.Quantum(0, 100, 1, TimeSpan.FromSeconds(5)), 9);

        // A rate glides in twentieths of its distance, whatever its unit.
        Assert.Equal(1_000_000 / 20.0, Glide.Quantum(0, 1_000_000, 0, Second), 6);

        var shown = Enumerable.Range(0, 1001).Select(i => Glide.Shown(0, 1, i / 1000.0, 0.05)).Distinct().Count();
        Assert.Equal(21, shown);
    }

    [Fact]
    public void S6325_the_readout_is_touched_only_when_the_next_step_is_due()
    {
        const double q = 0.01;
        var times = new List<double>();
        var progress = 0.0;

        // Walk the schedule as the clock does: each wake shows a new value, the last the reading.
        for (var guard = 0; guard < 10; guard++)
        {
            var next = Glide.NextChange(0.42, 0.45, progress, q);
            if (!double.IsFinite(next))
            {
                break;
            }

            Assert.True(next > progress);
            var before = Glide.Shown(0.42, 0.45, progress, q);
            var after = Glide.Shown(0.42, 0.45, next, q);
            Assert.NotEqual(Math.Round(before, 6), Math.Round(after, 6));
            times.Add(next);
            progress = next;
        }

        // 42 -> 43 at a sixth, 44 at a half, 45 at five sixths; then nothing wakes for it again.
        Assert.Equal([1 / 6.0, 0.5, 5 / 6.0], [.. times.Select(t => Math.Round(t, 9))], new ToleranceComparer(1e-6));
        Assert.Equal(double.PositiveInfinity, Glide.NextChange(0.42, 0.45, 5 / 6.0, q));
        Assert.Equal(double.PositiveInfinity, Glide.NextChange(0.42, 0.45, 1, q));
        Assert.Equal(double.PositiveInfinity, Glide.NextChange(0.45, 0.45, 0, q));
    }

    [Fact]
    public void S6327_colours_mix_in_oklab_ends_exact_and_the_middle_even()
    {
        const uint black = 0xFF000000;
        const uint white = 0xFFFFFFFF;

        Assert.Equal(black, OkLab.Mix(black, white, 0));
        Assert.Equal(white, OkLab.Mix(black, white, 1));
        Assert.Equal(0xFF0078D4u, OkLab.Mix(0xFF0078D4, 0xFF0078D4, 0.5));

        // OKLab's half lightness is a mid grey of about 99, not sRGB's 128 nor linear light's 188.
        var mid = OkLab.Mix(black, white, 0.5);
        var grey = (mid >> 16) & 0xFF;
        Assert.InRange(grey, 96u, 102u);
        Assert.Equal(grey, (mid >> 8) & 0xFF);
        Assert.Equal(grey, mid & 0xFF);

        // Alpha mixes straight.
        Assert.Equal(0x80u, OkLab.Mix(0x00FF0000, 0xFFFF0000, 0.5) >> 24, new UintTolerance(1));

        // A key-framed glide's colours: n steps along the way, the last the target.
        var ramp = OkLab.Ramp(0xFF0078D4, 0xFFC42B1C, 4);
        Assert.Equal(4, ramp.Length);
        Assert.Equal(0xFFC42B1Cu, ramp[^1]);
    }

    private sealed class ToleranceComparer(double tolerance) : IEqualityComparer<double>
    {
        public bool Equals(double x, double y) => Math.Abs(x - y) <= tolerance;

        public int GetHashCode(double obj) => 0;
    }

    private sealed class UintTolerance(uint tolerance) : IEqualityComparer<uint>
    {
        public bool Equals(uint x, uint y) => (x > y ? x - y : y - x) <= tolerance;

        public int GetHashCode(uint obj) => 0;
    }
}
