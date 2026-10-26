using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-6330] [S-6331] A live history chart steps once per finished slot, settled, and never moves between points.</summary>
public sealed class SparkSlotsTests
{
    private const double Width = 300;
    private const double Stroke = 2;
    private const int Window = 60;

    [Fact]
    public void S6331_an_unfolded_chart_puts_its_newest_point_on_the_right_edge_and_its_oldest_on_the_left()
    {
        var (skip, x) = SparkSlots.Layout(Window, 1000, 1, Window, Width, Stroke);

        Assert.Equal(0, skip);
        Assert.Equal(Window, x.Length);
        Assert.Equal(Width - (Stroke / 2), x[^1], 9);
        Assert.Equal(Stroke / 2, x[0], 9);

        // [S-1824] The same places Sparkline gives a full window, half a stroke inside either edge.
        for (var i = 0; i < x.Length; i++)
        {
            Assert.Equal(Sparkline.PlotX(i, Window, Window, Width, Stroke), x[i], 9);
        }
    }

    [Fact]
    public void S6331_a_point_moves_the_chart_by_exactly_one_slot_and_nothing_lies_beyond_the_edges()
    {
        var before = SparkSlots.Layout(Window, 500, 1, Window, Width, Stroke).X;
        var after = SparkSlots.Layout(Window, 501, 1, Window, Width, Stroke).X;
        var step = SparkSlots.Step(Window, Width, Stroke);

        // The point that was newest stands one slot left of the edge once the next has arrived.
        Assert.Equal(before[^1] - step, after[^2], 9);
        Assert.All(after, x => Assert.InRange(x, Stroke / 2 - 1e-9, Width - (Stroke / 2) + 1e-9));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(5)]
    [InlineData(24)]
    public void S1829_a_folded_slot_still_filling_is_not_drawn_and_the_newest_finished_one_is_on_the_edge(int size)
    {
        const int buckets = Window + 2;

        for (var added = (long)(size * 100); added < (size * 100) + (2 * size); added++)
        {
            var filling = added % size != 0;
            var (skip, x) = SparkSlots.Layout(buckets, added, size, Window, Width, Stroke);

            // Every finished slot up to the window, the filling one never; the oldest beyond the window skipped.
            Assert.Equal(Window, x.Length);
            Assert.Equal((filling ? buckets - 1 : buckets) - Window, skip);
            Assert.Equal(Width - (Stroke / 2), x[^1], 9);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(12)]
    public void S6331_only_a_point_that_finishes_a_slot_draws(int size)
    {
        var draws = Enumerable.Range(1, size * 10).Count(added => SparkSlots.Moves(added, size));

        // One drawing per slot: every point unfolded, every size-th point folded, never in between.
        Assert.Equal(10, draws);
        Assert.True(SparkSlots.Moves(size * 7, size));
        Assert.Equal(size == 1, SparkSlots.Moves((size * 7) + 1, size));
        Assert.False(SparkSlots.Moves(0, size));
    }

    [Fact]
    public void S1825_a_young_chart_stays_right_aligned()
    {
        var (skip, x) = SparkSlots.Layout(5, 5, 1, Window, Width, Stroke);
        var step = SparkSlots.Step(Window, Width, Stroke);

        Assert.Equal(0, skip);
        Assert.Equal(5, x.Length);
        Assert.Equal(Width - (Stroke / 2), x[^1], 9);
        Assert.Equal(Width - (Stroke / 2) - (4 * step), x[0], 9);
    }

    [Fact]
    public void S6331_the_lead_is_the_filling_slot_and_the_cut_oldest_one_and_nothing_unfolded()
    {
        Assert.Equal(0, SparkSlots.Lead(1));
        Assert.Equal(0, SparkSlots.Lead(0));
        Assert.Equal(22, SparkSlots.Lead(12));

        // Holding the span and the lead always leaves a whole window of finished slots, however the
        // newest point falls in its slot.
        foreach (var size in new[] { 2, 5, 7, 24 })
        {
            const int capacity = 3600 / 10;
            var window = MeterTiming.Slots(capacity, size);
            var held = capacity + SparkSlots.Lead(size);

            for (var filled = 0; filled < size; filled++)
            {
                Assert.True(held - filled >= window * size, $"size {size}, {filled} filling");
            }
        }
    }

    [Fact]
    public void S6331_an_unmeasured_or_empty_chart_draws_nothing()
    {
        Assert.Empty(SparkSlots.Layout(10, 10, 1, Window, 0, Stroke).X);
        Assert.Empty(SparkSlots.Layout(10, 10, 1, Window, double.NaN, Stroke).X);
        Assert.Empty(SparkSlots.Layout(0, 10, 1, Window, Width, Stroke).X);
        Assert.Empty(SparkSlots.Layout(10, 0, 1, Window, Width, Stroke).X);
        Assert.Equal(0, SparkSlots.Step(Window, 1, Stroke));
    }
}
