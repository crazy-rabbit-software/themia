using Themia.Core.Model;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-4600]..[S-4604] The stopwatch: its state machine on a monotonic clock, the record that outlives a restart, its text and keys.</summary>
public sealed class StopwatchTests
{
    private static TimeSpan S(double seconds) => TimeSpan.FromSeconds(seconds);

    [Fact]
    public void S4601_start_pause_resume_count_only_while_running()
    {
        var state = StopwatchState.None.Act(S(100));
        Assert.Equal(StopwatchPhase.Running, state.Phase);
        Assert.Equal(S(5), state.Elapsed(S(105)));

        state = state.Act(S(105));
        Assert.Equal(StopwatchPhase.Paused, state.Phase);
        Assert.Equal(S(5), state.Elapsed(S(500)));

        state = state.Act(S(600));
        Assert.Equal(StopwatchPhase.Running, state.Phase);
        Assert.Equal(S(7.5), state.Elapsed(S(602.5)));
    }

    [Fact]
    public void S4601_it_never_reads_below_nought_nor_past_its_longest()
    {
        var state = StopwatchState.Start(S(100));
        Assert.Equal(TimeSpan.Zero, state.Elapsed(S(50)));
        Assert.Equal(StopwatchState.Longest, state.Elapsed(S(100) + TimeSpan.FromHours(200)));
    }

    [Fact]
    public void S4601_laps_are_totals_and_lengths_are_their_differences()
    {
        var state = StopwatchState.Start(S(0)).Lap(S(10)).Lap(S(25));

        Assert.Equal((S(10), S(10)), state.LapAt(0));
        Assert.Equal((S(15), S(25)), state.LapAt(1));
        Assert.Null(state.LapAt(2));
        Assert.Equal(S(5), state.CurrentLap(S(30)));
    }

    [Fact]
    public void S4601_a_lap_needs_it_running_and_reset_clears_everything()
    {
        var paused = StopwatchState.Start(S(0)).Lap(S(1)).Pause(S(2));
        Assert.False(paused.CanLap);
        Assert.Equal(paused, paused.Lap(S(3)));
        Assert.True(paused.CanReset);

        Assert.False(StopwatchState.None.CanReset);
        Assert.False(StopwatchState.None.CanLap);
        Assert.Equal(StopwatchState.None, StopwatchState.None.Lap(S(1)));
    }

    [Fact]
    public void S4601_laps_stop_at_the_most_it_keeps()
    {
        var state = StopwatchState.Start(S(0));
        for (var i = 1; i <= StopwatchState.MostLaps + 5; i++)
        {
            state = state.Lap(S(i));
        }

        Assert.Equal(StopwatchState.MostLaps, state.Splits.Length);
        Assert.False(state.CanLap);
    }

    [Fact]
    public void S4602_a_running_stopwatch_keeps_running_across_a_restart()
    {
        var started = new DateTimeOffset(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);

        // Session one: started at mono 1000 s, laps at 3 s, still running when Themia closes at 10 s.
        var state = StopwatchState.Start(S(1000)).Lap(S(1003));
        var record = StopwatchRecord.Of(state, started + S(10), S(1010));
        Assert.Equal(StopwatchPhase.Running, record.Phase);
        Assert.Equal(started, record.StartedAt);

        // Session two: the PC rebooted (the monotonic clock is small again), an hour later.
        var back = record.ToState(started + TimeSpan.FromHours(1), S(20));
        Assert.Equal(StopwatchPhase.Running, back.Phase);
        Assert.Equal(TimeSpan.FromHours(1), back.Elapsed(S(20)));
        Assert.Equal(S(3), back.LapAt(0)!.Value.Total);
    }

    [Fact]
    public void S4602_a_clock_change_during_the_session_never_reaches_the_record()
    {
        var wallAtStart = new DateTimeOffset(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);
        var state = StopwatchState.Start(S(50));

        // Sixty monotonic seconds later the wall clock was set back two hours: the run is still 60 s.
        var record = StopwatchRecord.Of(state, wallAtStart - TimeSpan.FromHours(2) + S(60), S(110));
        var back = record.ToState(wallAtStart - TimeSpan.FromHours(2) + S(60), S(0));
        Assert.Equal(S(60), back.Elapsed(S(0)));
    }

    [Fact]
    public void S4602_a_clock_set_back_while_closed_reads_as_no_time_not_negative_time()
    {
        var started = new DateTimeOffset(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);
        var record = new StopwatchRecord(StopwatchPhase.Running, 5_000, started, []);

        var back = record.ToState(started - TimeSpan.FromDays(1), S(7));
        Assert.Equal(S(5), back.Elapsed(S(7)));
    }

    [Fact]
    public void S4602_paused_and_idle_round_trip_and_a_damaged_record_is_read_safely()
    {
        var paused = StopwatchState.Start(S(0)).Lap(S(2)).Pause(S(4.5));
        var record = StopwatchRecord.Of(paused, DateTimeOffset.UnixEpoch, S(9));
        Assert.Null(record.StartedAt);
        Assert.Equal(paused, record.ToState(DateTimeOffset.UnixEpoch, S(99)));

        Assert.Equal(StopwatchState.None, StopwatchRecord.Of(StopwatchState.None, DateTimeOffset.UnixEpoch, S(1)).ToState(DateTimeOffset.UnixEpoch, S(1)));

        // Running with no start: paused where it was. Laps out of order: the rest dropped.
        var damaged = new StopwatchRecord(StopwatchPhase.Running, 4_000, null, [1_000, 3_000, 2_000]);
        var read = damaged.ToState(DateTimeOffset.UnixEpoch, S(1));
        Assert.Equal(StopwatchPhase.Paused, read.Phase);
        Assert.Equal(S(4), read.Elapsed(S(100)));
        Assert.Equal(2, read.Splits.Length);
    }

    [Fact]
    public void S4602_the_config_round_trips_through_the_state_file()
    {
        var config = (StopwatchConfig)ConfigJson.Read("stopwatch",
            """{ "kind": "stopwatch", "phase": "Running", "bankedMs": 2000, "startedAt": "2026-10-12T09:00:00+00:00", "lapsMs": [500, 1500] }""");

        Assert.Equal(StopwatchPhase.Running, config.Phase);
        Assert.Equal(2_000, config.BankedMs);
        Assert.Equal([500L, 1500L], config.LapsMs);

        var clone = (StopwatchConfig)config.Clone();
        clone.LapsMs.Add(9);
        Assert.Equal(2, config.LapsMs.Count);
    }

    [Fact]
    public void S4602_repair_pauses_a_running_stopwatch_without_a_start()
    {
        var config = new StopwatchConfig { Phase = StopwatchPhase.Running, BankedMs = 3_000, LapsMs = [1_000] };

        Assert.NotEmpty(config.Repair());
        Assert.Equal(StopwatchPhase.Paused, config.Phase);
        Assert.Equal(3_000, config.BankedMs);
        Assert.Empty(config.Repair());
    }

    [Theory]
    [InlineData(0, false, "0:00")]
    [InlineData(5.99, false, "0:05")]
    [InlineData(754.56, true, "12:34.5")]
    [InlineData(3723.4, true, "1:02:03.4")]
    [InlineData(59.99, true, "0:59.9")]
    public void S4603_time_is_written_in_whole_seconds_begun_not_rounded(double seconds, bool tenths, string expected)
    {
        Assert.Equal(expected, StopwatchText.Time(S(seconds), tenths));
    }

    [Fact]
    public void S4603_the_tenth_takes_the_languages_decimal_sign_and_stops_at_the_longest()
    {
        Assert.Equal("1:00,5", StopwatchText.Time(S(60.5), true, ','));
        Assert.Equal("99:59:59.9", StopwatchText.Time(TimeSpan.FromDays(30), true));
        Assert.Equal(StopwatchText.Widest.Length, StopwatchText.Time(TimeSpan.FromDays(30), true).Length);
    }

    [Theory]
    [InlineData(WidgetKey.Space, ControlKey.Toggle)]
    [InlineData(WidgetKey.Enter, ControlKey.Toggle)]
    [InlineData(WidgetKey.Right, ControlKey.Lap)]
    [InlineData(WidgetKey.Home, ControlKey.Reset)]
    [InlineData(WidgetKey.Escape, ControlKey.None)]
    [InlineData(WidgetKey.Tab, ControlKey.None)]
    public void S4604_keys(WidgetKey key, ControlKey expected) => Assert.Equal(expected, ControlKeys.Stopwatch(key));

    [Theory]
    [InlineData('l', ControlKey.Lap)]
    [InlineData('L', ControlKey.Lap)]
    [InlineData('r', ControlKey.Reset)]
    [InlineData('x', ControlKey.None)]
    public void S4604_letters(char letter, ControlKey expected) => Assert.Equal(expected, ControlKeys.StopwatchLetter(letter));
}
