using Themia.Core.Astronomy;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-6030]..[S-6061] The styles of the countdown and the pomodoro, and the shared ring and
/// dot-grid rules they draw with (year progress's, the sun's and the moon's are their modules').
/// </summary>
public class TimeStylesTests
{
    // ---- the styles themselves ---------------------------------------------------------------

    [Fact]
    public void S6035_the_pomodoro_ring_style_alone_draws_a_ring_and_digits_draws_neither()
    {
        Assert.True(new PomodoroConfig().DrawsRing);
        Assert.True(new PomodoroConfig().DrawsBar);
        Assert.False(new PomodoroConfig { Style = PomodoroStyle.Bar }.DrawsRing);
        Assert.True(new PomodoroConfig { Style = PomodoroStyle.Bar }.DrawsBar);
        Assert.False(new PomodoroConfig { Style = PomodoroStyle.Digits }.DrawsRing);
        Assert.False(new PomodoroConfig { Style = PomodoroStyle.Digits }.DrawsBar);
    }

    // ---- countdown ----------------------------------------------------------------------------

    [Fact]
    public void S6031_the_ring_is_the_share_still_to_go_and_unknown_without_a_creation_time()
    {
        var created = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
        var trip = new CountdownConfig { Label = "Trip" };
        trip.Retarget(created.AddDays(10), created);

        Assert.Equal(1, CountdownLayout.Left(trip, created));
        Assert.Equal(0.75, CountdownLayout.Left(trip, created.AddDays(2.5))!.Value, 6);
        Assert.Equal(0, CountdownLayout.Left(trip, created.AddDays(12)));
        Assert.Null(CountdownLayout.Left(new CountdownConfig { Target = created.AddDays(10) }, created));
        Assert.Null(CountdownLayout.Left(new CountdownConfig(), created));
    }

    [Fact]
    public void S6032_flip_cards_are_days_while_there_are_any_then_two_figure_hours_and_minutes()
    {
        var now = new DateTimeOffset(2026, 10, 16, 9, 0, 0, TimeSpan.Zero);

        var far = CountdownLayout.Cards(now.AddDays(12).AddHours(3).AddMinutes(7), now, seconds: false);
        Assert.Equal(["12", "03", "07"], far.Select(c => c.Digits));
        Assert.Equal([CountdownUnit.Days, CountdownUnit.Hours, CountdownUnit.Minutes], far.Select(c => c.Unit));

        var near = CountdownLayout.Cards(now.AddHours(5).AddMinutes(4).AddSeconds(9), now, seconds: true);
        Assert.Equal(["05", "04", "09"], near.Select(c => c.Digits));
        Assert.Equal(CountdownUnit.Seconds, near[^1].Unit);

        // Gone by: it counts up, as the text style's "… ago" does.
        var past = CountdownLayout.Cards(now.AddMinutes(-90), now, seconds: false);
        Assert.Equal(["01", "30"], past.Select(c => c.Digits));

        Assert.Equal("365", CountdownLayout.Cards(now.AddDays(365), now, false)[0].Digits);
    }

    // ---- the shared ring and dot grid ----------------------------------------------------------

    [Fact]
    public void S6060_a_ring_runs_clockwise_from_twelve_and_is_empty_or_whole_at_its_ends()
    {
        var quarter = RingArc.Of(100, 10, 0.25);
        Assert.Equal((50, 45), (quarter.Centre, quarter.Radius));
        Assert.Equal((50d, 5d), (quarter.StartX, quarter.StartY));
        Assert.Equal(95, quarter.EndX, 6);
        Assert.Equal(50, quarter.EndY, 6);
        Assert.False(quarter.IsLarge);

        Assert.True(RingArc.Of(100, 10, 0.75).IsLarge);
        Assert.True(RingArc.Of(100, 10, 0).Empty);
        Assert.True(RingArc.Of(100, 10, double.NaN).Empty);
        Assert.True(RingArc.Of(100, 10, 1.4).Full);
        Assert.False(RingArc.Of(100, 10, 0.5).Full);
        Assert.Equal(1, RingArc.Of(4, 10, 0.5).Radius);
        Assert.Equal(3, RingArc.Stroke(20));
        Assert.Equal(12, RingArc.Stroke(1000));
    }

    [Fact]
    public void S6061_the_dot_grid_reflows_to_the_box_and_every_dot_fits()
    {
        var wide = DotGrid.Fit(365, 448, 128);
        var tall = DotGrid.Fit(365, 168, 368);
        Assert.True(wide.Columns > wide.Rows);
        Assert.True(tall.Rows > tall.Columns);

        foreach (var (plan, w, h) in new[] { (wide, 448d, 128d), (tall, 168d, 368d) })
        {
            Assert.True(plan.Columns * plan.Rows >= 365);
            Assert.True(plan.Width <= w + 1e-6 && plan.Height <= h + 1e-6);
            Assert.True(plan.Diameter < plan.Pitch);
        }

        var month = DotGrid.Fit(31, 1000, 100, maxColumns: 7);
        Assert.Equal(7, month.Columns);
        Assert.Equal((2, 4), month.Cell(31 - 1));
        Assert.Equal(0, DotGrid.Fit(0, 100, 100).Columns);
        Assert.Equal(0, DotGrid.Fit(10, 0, 100).Pitch);
    }
}
