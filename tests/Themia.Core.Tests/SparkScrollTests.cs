using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-1826]..[S-1829] The scrolling history chart: drawn a sample behind and slid one step per period,
/// so the geometry rebuilt on the next point lies exactly where the slid one ended.
/// </summary>
public sealed class SparkScrollTests
{
    private const double Width = 300;
    private const double Stroke = 2;
    private const int Window = 60;

    [Fact]
    public void S1826_settled_unfolded_matches_the_old_axis()
    {
        // No lag and no folding is today's chart: Sparkline.PlotX for every slot of a full window.
        var (skip, x) = SparkScroll.Layout(Window, 1000, 1, Window, Width, Stroke, 0);

        Assert.Equal(0, skip);
        for (var i = 0; i < Window; i++)
        {
            Assert.Equal(Sparkline.PlotX(i, Window, Window, Width, Stroke), x[i], 9);
        }
    }

    [Fact]
    public void S1826_a_sample_behind_puts_the_newest_one_step_beyond_the_right_edge()
    {
        var step = SparkScroll.Step(1, Window, Width, Stroke);
        var newest = SparkScroll.PlotX(999, 999, 1, Window, Width, Stroke, 1);
        var previous = SparkScroll.PlotX(998, 999, 1, Window, Width, Stroke, 1);

        Assert.Equal(Width - (Stroke / 2) + step, newest, 9);
        Assert.Equal(Width - (Stroke / 2), previous, 9);
        Assert.Equal((Width - Stroke) / (Window - 1), step, 9);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(12)]
    public void S1826_sliding_one_step_is_the_settled_drawing(int size)
    {
        // The animation only translates: lag 1 shifted left by one step is lag 0, for every slot.
        var step = SparkScroll.Step(size, Window, Width, Stroke);

        for (long bucket = 0; bucket < 100; bucket++)
        {
            var behind = SparkScroll.PlotX(bucket, 1234, size, Window, Width, Stroke, 1);
            var settled = SparkScroll.PlotX(bucket, 1234, size, Window, Width, Stroke, 0);
            var half = SparkScroll.PlotX(bucket, 1234, size, Window, Width, Stroke, 0.5);

            Assert.Equal(settled, behind - step, 9);
            Assert.Equal(behind - (step / 2), half, 9);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    public void S1826_the_next_point_rebuilds_where_the_slide_ended(int size)
    {
        // A slide that played out (lag 0, newest n) and the rebuild on point n+1 (lag 1) agree, so
        // swapping geometry and resetting the translation in one frame shows no jump.
        for (long bucket = 0; bucket < 400; bucket++)
        {
            var ended = SparkScroll.PlotX(bucket, 500, size, Window, Width, Stroke, 0);
            var rebuilt = SparkScroll.PlotX(bucket, 501, size, Window, Width, Stroke, 1);

            Assert.Equal(ended, rebuilt, 9);
        }
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void S1829_the_slot_still_filling_is_never_inside_the_chart(double lag)
    {
        // A folded slot stands at its last member, so the one still taking points is at or beyond
        // the right edge and nothing on screen changes value as points arrive.
        const int size = 5;
        var right = Width - (Stroke / 2);

        for (long newest = 100; newest < 110; newest++)
        {
            var filling = newest / size;
            Assert.True(SparkScroll.PlotX(filling, newest, size, Window, Width, Stroke, lag) >= right - 1e-9);

            if ((newest + 1) % size != 0)
            {
                // Still filling: strictly beyond the edge.
                Assert.True(SparkScroll.PlotX(filling, newest, size, Window, Width, Stroke, lag) > right);
            }
        }
    }

    [Theory]
    [InlineData(1, 60, 120.0)]   // a minute unfolded
    [InlineData(1, 60, 300.0)]
    [InlineData(2, 60, 60.0)]    // a minute folded into a narrow chart
    [InlineData(24, 3600, 300.0)] // an hour folded into a tile
    [InlineData(5, 300, 120.0)]
    public void S1829_the_slide_shows_only_finished_slots_and_moves_by_their_spacing(int size, int span, double width)
    {
        // Point after point through several slots: what slides into view must already be final
        // (a slot still filling changes value with every point), the slid frame must be exactly the
        // next point's rebuilt one, and whatever enters does so from beyond the right edge.
        var window = MeterTiming.Slots(span, size);
        var lead = SparkScroll.Lead(size);
        var step = SparkScroll.Step(size, window, width, Stroke);

        for (long added = 4 * span; added < (4 * span) + (3 * size) + 2; added++)
        {
            var before = Drawn(added, size, span + lead, window, width, lag: 0);
            var after = Drawn(added + 1, size, span + lead, window, width, lag: 1);
            var newest = added; // 0-based index of the point just added

            foreach (var (bucket, x) in after)
            {
                // Finished: its last member has arrived.
                Assert.True(((bucket + 1) * size) - 1 <= newest, $"slot {bucket} is still filling at point {newest}");

                if (before.TryGetValue(bucket, out var was))
                {
                    Assert.Equal(was, x, 9);
                }
                else if (x > 0)
                {
                    // New on the right: it starts wholly beyond the chart, not inside it.
                    Assert.True(x >= width + 0.5, $"slot {bucket} enters at {x}, inside a {width} chart");
                }

                // The slide is one sample step; settled is that far left of the rebuilt frame.
                var settled = SparkScroll.PlotX(bucket, newest, size, window, width, Stroke, 0);
                Assert.Equal(x - step, settled, 9);
            }

            // The line still reaches the right edge (half a stroke in, [S-1824]) and the left one.
            Assert.True(after.Values.Max() >= width - (Stroke / 2) - 1e-9);
            Assert.True(after.Values.Min() <= 0);

            // The leftmost slot drawn is a whole one, not a part the held history cut.
            var oldestHeld = added + 1 - (span + lead);
            Assert.True(after.Keys.Min() * size >= oldestHeld, $"slot {after.Keys.Min()} is cut at the left");
        }
    }

    /// <summary>The slots a chart of the newest <paramref name="held"/> of <paramref name="added"/> points draws, by absolute id.</summary>
    private static Dictionary<long, double> Drawn(long added, int size, int held, int window, double width, double lag)
    {
        var count = (int)Math.Min(added, held);
        var first = added - count;
        var buckets = (int)(((added - 1) / size) - (first / size) + 1);
        var (skip, x) = SparkScroll.Layout(buckets, added, size, window, width, Stroke, lag);
        var oldest = first / size;
        var drawn = new Dictionary<long, double>();

        for (var i = 0; i < x.Length; i++)
        {
            drawn[oldest + skip + i] = x[i];
        }

        return drawn;
    }

    [Fact]
    public void S1826_keeps_one_slot_past_the_left_edge_and_drops_older()
    {
        var held = Window + SparkScroll.Lead(1);
        var (skip, x) = SparkScroll.Layout(held, 5000, 1, Window, Width, Stroke, 1);

        Assert.True(x[0] <= 0);
        Assert.True(x[1] > 0);
        Assert.Equal(held - skip, x.Length);
        Assert.True(x[^1] > Width - (Stroke / 2));
    }

    [Fact]
    public void S1826_a_filling_chart_keeps_everything_and_grows_from_the_right()
    {
        var (skip, x) = SparkScroll.Layout(5, 5, 1, Window, Width, Stroke, 0);

        Assert.Equal(0, skip);
        Assert.Equal(5, x.Length);
        Assert.True(x[0] > Width / 2);
        Assert.Equal(Width - (Stroke / 2), x[^1], 9);
    }

    [Fact]
    public void S1826_an_unmeasured_chart_draws_nothing()
    {
        Assert.Empty(SparkScroll.Layout(10, 10, 1, Window, 0, Stroke, 1).X);
        Assert.Empty(SparkScroll.Layout(10, 10, 1, Window, double.NaN, Stroke, 1).X);
        Assert.Empty(SparkScroll.Layout(0, 10, 1, Window, Width, Stroke, 1).X);
        Assert.Equal(0, SparkScroll.Step(1, Window, 1, Stroke));
    }

    [Fact]
    public void S1826_holds_the_newest_and_the_folded_slots_beyond_the_edges()
    {
        Assert.Equal(2, SparkScroll.Lead(1));
        Assert.Equal(35, SparkScroll.Lead(12));
        Assert.Equal(2, SparkScroll.Lead(0));
    }

    [Fact]
    public void S1827_only_a_new_point_slides()
    {
        Assert.Equal(new SparkFrame(1, true), SparkScroll.Plan(SparkChange.Point, animates: true));
        Assert.Equal(new SparkFrame(0, false), SparkScroll.Plan(SparkChange.Settle, animates: true));
    }

    [Fact]
    public void S1828_with_animation_effects_off_the_chart_steps_with_no_lag()
    {
        Assert.Equal(new SparkFrame(0, false), SparkScroll.Plan(SparkChange.Point, animates: false));
        Assert.Equal(new SparkFrame(0, false), SparkScroll.Plan(SparkChange.Settle, animates: false));
    }
}
