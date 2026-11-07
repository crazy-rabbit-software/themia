using Themia.Core.Model;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Widget.Pomodoro.Tests;

/// <summary>[S-8111] The − and + nudge (issue 186): 5 minutes on the current phase, inside the menu's bounds.</summary>
public sealed class PomodoroNudgeTests
{
    [Fact]
    public void S8111_an_idle_phase_starts_five_minutes_longer_or_shorter()
    {
        var fresh = new PomodoroState(PomodoroPhase.Work, 1500);

        Assert.Equal(new PomodoroNudged(new PomodoroState(PomodoroPhase.Work, 1800), 1800), PomodoroNudge.Apply(fresh, 1500, +1));
        Assert.Equal(new PomodoroNudged(new PomodoroState(PomodoroPhase.Work, 1200), 1200), PomodoroNudge.Apply(fresh, 1500, -1));
    }

    [Fact]
    public void S8111_a_running_phase_moves_the_time_left_and_keeps_the_time_gone()
    {
        // 15 of 25 minutes gone, 10 left.
        var running = new PomodoroState(PomodoroPhase.Break, 600);

        var longer = PomodoroNudge.Apply(running, 1500, +1)!.Value;
        Assert.Equal(900, longer.State.RemainingSeconds);
        Assert.Equal(900, longer.LengthSeconds - longer.State.RemainingSeconds);
        Assert.Equal(PomodoroPhase.Break, longer.State.Phase);

        var shorter = PomodoroNudge.Apply(running, 1500, -1)!.Value;
        Assert.Equal(300, shorter.State.RemainingSeconds);
        Assert.Equal(900, shorter.LengthSeconds - shorter.State.RemainingSeconds);
    }

    [Theory]
    [InlineData(360, 1500, true)]    // 6:00 left: 1:00 after the press, the least
    [InlineData(359, 1500, false)]   // 5:59 left: would leave less than a minute
    [InlineData(300, 300, false)]    // a fresh 5-minute break: not a 0-minute one
    [InlineData(61, 1500, false)]
    public void S8111_shorter_never_leaves_less_than_a_minute(int remaining, int length, bool can)
    {
        var state = new PomodoroState(PomodoroPhase.Work, remaining);

        Assert.Equal(can, PomodoroNudge.Can(state, length, -1));
        Assert.Equal(can, PomodoroNudge.Apply(state, length, -1) is not null);
    }

    [Theory]
    [InlineData(10500, 10500, true)]  // 175 min: 180 after the press, the menu's most
    [InlineData(10800, 10800, false)] // 180 min: the most already
    [InlineData(600, 10600, false)]   // a 176-minute phase with 10 left: the phase may not pass 180
    [InlineData(10501, 10501, false)]
    public void S8111_longer_never_passes_the_menus_most(int remaining, int length, bool can)
    {
        var state = new PomodoroState(PomodoroPhase.Work, remaining);

        Assert.Equal(can, PomodoroNudge.Can(state, length, +1));
        Assert.Equal(can, PomodoroNudge.Apply(state, length, +1) is not null);
    }

    [Fact]
    public void S8111_the_bounds_are_the_menus()
    {
        Assert.Equal(PomodoroNudge.LeastSeconds, PomodoroCycle.PhaseSeconds(PomodoroPhase.Work, new PomodoroConfig { WorkMinutes = 0 }));
        Assert.Equal(PomodoroNudge.MostSeconds, PomodoroCycle.PhaseSeconds(PomodoroPhase.Work, new PomodoroConfig { WorkMinutes = 9999 }));
    }

    [Fact]
    public void S8111_no_direction_does_nothing()
        => Assert.Null(PomodoroNudge.Apply(new PomodoroState(PomodoroPhase.Work, 1500), 1500, 0));

    [Theory]
    [InlineData(WidgetKey.Plus, 1)]
    [InlineData(WidgetKey.Up, 1)]
    [InlineData(WidgetKey.Minus, -1)]
    [InlineData(WidgetKey.Down, -1)]
    [InlineData(WidgetKey.Right, 0)]
    [InlineData(WidgetKey.Space, 0)]
    public void S1987_plus_and_up_nudge_longer_minus_and_down_shorter(WidgetKey key, int expected)
    {
        Assert.Equal(expected, PomodoroKeys.Nudge(key));
        if (expected != 0)
        {
            Assert.Equal(ControlKey.None, PomodoroKeys.Of(key));
        }
    }
}
