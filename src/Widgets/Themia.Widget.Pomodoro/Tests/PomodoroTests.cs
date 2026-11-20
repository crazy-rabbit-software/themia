using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Core.Tests;
using Xunit;

namespace Themia.Widget.Pomodoro.Tests;

/// <summary>
/// The pomodoro cycle. Pure, so the flip at the end of a phase is reachable without running a widget
/// for twenty-five minutes — which is the only reason the flip ([S-151], the original's S-03), its cue
/// ([S-152], the original's S-06) and the cycle's end ([S-8114]) are testable at all.
/// </summary>
public class PomodoroCycleTests
{
    private const int Work = 1500; // 25 min
    private const int Break = 300; //  5 min
    private const int Long = 900;  // 15 min

    /// <summary>The classic cycle's lengths: focus, short break, four times, the last break the long one.</summary>
    private static readonly int[] Classic = [Work, Break, Work, Break, Work, Break, Work, Long];

    private static PomodoroConfig Of(params (PomodoroPhaseKind Kind, int Minutes)[] phases)
        => new() { Phases = [.. phases.Select(p => PomodoroPhaseSpec.Of(p.Kind, p.Minutes))] };

    // -----------------------------------------------------------------------------------------
    // [S-8101] lengths, clamped (the original's S-01)
    // -----------------------------------------------------------------------------------------

    [Theory]
    [InlineData(25, 1500)]
    [InlineData(1, 60)]
    [InlineData(180, 10800)]
    [InlineData(0, 60)]         // a persisted 0 would otherwise flip once a second for ever
    [InlineData(-5, 60)]
    [InlineData(9999, 10800)]
    public void S8101_a_phase_length_is_clamped_to_one_to_a_hundred_and_eighty_minutes(int configured, int expectedSeconds)
    {
        var phase = new PomodoroPhaseSpec { Kind = PomodoroPhaseKind.ShortBreak, Minutes = configured };

        Assert.Equal(expectedSeconds, PomodoroCycle.Seconds(phase));
        Assert.Equal([expectedSeconds], PomodoroCycle.Lengths(new PomodoroConfig { Phases = [phase] }));
    }

    [Fact]
    public void S8102_a_fresh_cycle_is_its_first_phase_in_full()
    {
        var state = PomodoroCycle.Fresh(Of((PomodoroPhaseKind.ShortBreak, 7), (PomodoroPhaseKind.Focus, 30)));

        Assert.Equal(new PomodoroState(0, 420), state);
        Assert.Equal(new PomodoroState(0, Work), PomodoroCycle.Fresh(new PomodoroConfig()));
    }

    [Fact]
    public void S8112_the_lengths_are_the_phases_in_order()
        => Assert.Equal(Classic, PomodoroCycle.Lengths(new PomodoroConfig()));

    [Theory]
    [InlineData(0, 8, 0)]
    [InlineData(7, 8, 7)]
    [InlineData(8, 8, 0)]    // a phase gone after an edit: the cycle starts again
    [InlineData(-1, 8, 0)]
    [InlineData(3, 0, 0)]
    public void S8112_an_index_outside_the_cycle_is_its_first_phase(int index, int count, int expected)
        => Assert.Equal(expected, PomodoroCycle.Normalise(index, count));

    [Fact]
    public void S8112_a_config_without_phases_runs_the_classic_cycle()
        => Assert.Equal(Classic, PomodoroCycle.Lengths(new PomodoroConfig { Phases = [] }));

    // -----------------------------------------------------------------------------------------
    // [S-151] the flip (the original's S-03), through the phases in order [S-8112]
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void S151_a_tick_with_time_left_only_decrements()
    {
        Assert.Equal(new PomodoroTick(0, 1499, Flipped: false), PomodoroCycle.Advance(new PomodoroState(0, Work), Classic, repeat: true));
        Assert.Equal(new PomodoroTick(5, 1, Flipped: false), PomodoroCycle.Advance(new PomodoroState(5, 2), Classic, repeat: true));
    }

    [Fact]
    public void S8112_each_phase_flips_to_the_next_in_full_and_keeps_running()
    {
        Assert.Equal(new PomodoroTick(1, Break, Flipped: true), PomodoroCycle.Advance(new PomodoroState(0, 1), Classic, repeat: true));
        Assert.Equal(new PomodoroTick(2, Work, Flipped: true), PomodoroCycle.Advance(new PomodoroState(1, 1), Classic, repeat: true));
        Assert.Equal(new PomodoroTick(7, Long, Flipped: true), PomodoroCycle.Advance(new PomodoroState(6, 1), Classic, repeat: true));
    }

    [Fact]
    public void S151_one_remaining_is_the_last_second_not_a_spare_one()
    {
        // The off-by-one that would show "0:00" for a whole second before flipping.
        var tick = PomodoroCycle.Advance(new PomodoroState(0, 1), [60, 60], repeat: true);

        Assert.True(tick.Flipped);
        Assert.Equal(60, tick.RemainingSeconds);
    }

    [Fact]
    public void S8112_it_runs_through_every_phase_once_and_flips_exactly_once_per_phase()
    {
        var lengths = new[] { 2, 3, 2 };
        var state = new PomodoroState(0, 2);
        var flips = new List<int>();

        // 2 → 1 → flip(1, 3) → 2 → 1 → flip(2, 2) → 1 → flip(0, 2)
        for (var i = 0; i < 7; i++)
        {
            var tick = PomodoroCycle.Advance(state, lengths, repeat: true);
            if (tick.Flipped)
            {
                flips.Add(tick.Index);
            }

            state = new PomodoroState(tick.Index, tick.RemainingSeconds);
        }

        Assert.Equal([1, 2, 0], flips);
    }

    [Fact]
    public void S151_a_zero_length_phase_still_loads_at_least_one_second()
    {
        // Defence against the 1 Hz alarm loop: a phase of no seconds would flip on every tick.
        var tick = PomodoroCycle.Advance(new PomodoroState(0, 1), [0, 0], repeat: true);

        Assert.True(tick.Flipped);
        Assert.Equal(1, tick.RemainingSeconds);
    }

    // -----------------------------------------------------------------------------------------
    // [S-8114] the end of the cycle: repeat or stop
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void S8114_after_the_last_phase_a_repeating_cycle_starts_again_and_keeps_running()
        => Assert.Equal(new PomodoroTick(0, Work, Flipped: true, Stopped: false), PomodoroCycle.Advance(new PomodoroState(7, 1), Classic, repeat: true));

    [Fact]
    public void S8114_without_repeat_the_last_phase_loads_the_first_and_stops()
        => Assert.Equal(new PomodoroTick(0, Work, Flipped: true, Stopped: true), PomodoroCycle.Advance(new PomodoroState(7, 1), Classic, repeat: false));

    [Fact]
    public void S8114_without_repeat_only_the_last_flip_stops()
        => Assert.False(PomodoroCycle.Advance(new PomodoroState(6, 1), Classic, repeat: false).Stopped);

    [Fact]
    public void S8112_a_state_past_a_shortened_cycle_ticks_on_from_its_first_phase()
        => Assert.Equal(new PomodoroTick(0, 99, Flipped: false), PomodoroCycle.Advance(new PomodoroState(12, 100), Classic, repeat: true));

    // -----------------------------------------------------------------------------------------
    // [S-8104] progress (the original's S-04)
    // -----------------------------------------------------------------------------------------

    [Theory]
    [InlineData(1500, 1500, 0.0)]   // nothing elapsed at the start of a phase
    [InlineData(750, 1500, 0.5)]
    [InlineData(1, 1500, 0.999333)]
    [InlineData(0, 1500, 1.0)]      // full at the flip
    public void S8104_progress_is_the_elapsed_fraction_of_the_phase(int remaining, int total, double expected)
        => Assert.Equal(expected, PomodoroCycle.Progress(new PomodoroState(0, remaining), total), 5);

    [Fact]
    public void S8104_progress_stays_within_range_when_the_durations_change_mid_phase()
    {
        // Shortening the phase in settings while it runs leaves `remaining` above the new total for
        // one tick. An unclamped fraction would drive the section backwards past its own start.
        Assert.Equal(0.0, PomodoroCycle.Progress(new PomodoroState(0, 5000), 1500));
        Assert.Equal(1.0, PomodoroCycle.Progress(new PomodoroState(0, -20), 1500));
        Assert.Equal(0.0, PomodoroCycle.Progress(new PomodoroState(0, 10), 0));
    }

    [Theory]
    [InlineData(1500, "25:00")]
    [InlineData(59, "0:59")]
    [InlineData(60, "1:00")]
    [InlineData(5, "0:05")]
    [InlineData(0, "0:00")]
    [InlineData(-3, "0:00")]        // never renders a negative clock
    [InlineData(10800, "180:00")]
    public void S8102_the_readout_is_minutes_and_padded_seconds(int remaining, string expected)
        => Assert.Equal(expected, PomodoroCycle.Clock(remaining));
}

/// <summary>[S-1460] the pomodoro's sound: off or one of twelve ringtones, and its volume (the ringtones themselves, [S-1461]..[S-1463], are the SDK's, tested in Core).</summary>
public class PomodoroSoundTests
{
    [Fact]
    public void S1460_off_and_twelve_ringtones_listed_gentle_to_insistent()
    {
        Assert.Equal(13, PomodoroSounds.Order.Count);
        Assert.Equal(AlarmSound.Off, PomodoroSounds.Order[0]);
        Assert.Equal(AlarmSound.Chime, PomodoroSounds.Order[1]);
        Assert.Equal(AlarmSound.Buzzer, PomodoroSounds.Order[^1]);
        Assert.Equal(Enum.GetValues<AlarmSound>().Length, PomodoroSounds.Order.Distinct().Count());
    }

    [Fact]
    public void S1460_defaults_are_chime_at_seventy()
    {
        var config = new PomodoroConfig();
        Assert.Equal(AlarmSound.Chime, config.Sound);
        Assert.Equal(70, config.Volume);
    }

    [Fact]
    public void S1460_an_unknown_sound_name_reads_as_the_default_chime()
    {
        var config = Assert.IsType<PomodoroConfig>(ConfigJson.Read("Pomodoro", """{"kind":"pomodoro","sound":"kazoo","volume":40}"""));
        Assert.Equal(AlarmSound.Chime, config.Sound);
        Assert.Equal(40, config.Volume);
    }

    [Fact]
    public void S1460_a_sound_reads_by_name_in_any_case()
        => Assert.Equal(AlarmSound.Gong, Assert.IsType<PomodoroConfig>(ConfigJson.Read("Pomodoro", """{"kind":"pomodoro","sound":"Gong"}""")).Sound);

    [Theory]
    [InlineData(-5, 0)]
    [InlineData(250, 100)]
    [InlineData(55, 55)]
    public void S1460_volume_is_repaired_into_zero_to_a_hundred(int stored, int expected)
    {
        var config = new PomodoroConfig { Volume = stored };
        var repairs = config.Repair();
        Assert.Equal(expected, config.Volume);
        Assert.Equal(stored != expected, repairs.Count > 0);
    }

    [Theory]
    [InlineData(PomodoroPhaseKind.Focus, PomodoroPhase.Work)]
    [InlineData(PomodoroPhaseKind.ShortBreak, PomodoroPhase.Break)]
    [InlineData(PomodoroPhaseKind.LongBreak, PomodoroPhase.Break)]
    public void S152_the_cue_rises_into_focus_and_falls_into_either_break(PomodoroPhaseKind kind, PomodoroPhase cue)
        => Assert.Equal(cue, PomodoroCycle.Entering(kind));
}

/// <summary>[S-1466] [S-8116] skip, the next-phase line and the caption.</summary>
public class PomodoroLadderRulesTests
{
    private static readonly int[] Classic = PomodoroCycle.Lengths(new PomodoroConfig());

    [Fact]
    public void S1466_skip_loads_the_next_phase_in_full_and_never_stops_a_repeating_cycle()
    {
        Assert.Equal(new PomodoroTick(1, 300, Flipped: true), PomodoroCycle.Skip(new PomodoroState(0, 61), Classic, repeat: true));
        Assert.Equal(new PomodoroTick(0, 1500, Flipped: true), PomodoroCycle.Skip(new PomodoroState(7, 61), Classic, repeat: true));
    }

    [Fact]
    public void S8114_skipping_the_last_phase_of_a_cycle_without_repeat_stops_it_on_the_first()
        => Assert.Equal(new PomodoroTick(0, 1500, Flipped: true, Stopped: true), PomodoroCycle.Skip(new PomodoroState(7, 61), Classic, repeat: false));

    [Fact]
    public void S1466_the_next_line_names_the_phase_the_flip_loads()
    {
        var config = new PomodoroConfig();
        Assert.Equal("Next: Short break 5:00", PomodoroCycle.NextLine(Locale.En, config, 0));
        Assert.Equal("Next: Focus 25:00", PomodoroCycle.NextLine(Locale.En, config, 1));
        Assert.Equal("Next: Long break 15:00", PomodoroCycle.NextLine(Locale.En, config, 6));
        Assert.Equal("Next: Focus 25:00", PomodoroCycle.NextLine(Locale.En, config, 7));
        Assert.StartsWith("Danach: Fokus", PomodoroCycle.NextLine(Locale.De, config, 1));
    }

    [Fact]
    public void S8116_the_caption_is_the_phase_and_its_place_in_the_cycle()
    {
        var phases = PomodoroCycle.Phases(new PomodoroConfig());
        Assert.Equal("Focus · 1 of 8", PomodoroCycle.Caption(Locale.En, phases, 0));
        Assert.Equal("Short break · 4 of 8", PomodoroCycle.Caption(Locale.En, phases, 3));
        Assert.Equal("Long break · 8 of 8", PomodoroCycle.Caption(Locale.En, phases, 7));
    }

    [Fact]
    public void S8116_a_named_phase_says_its_own_name_and_a_cycle_of_one_its_name_alone()
    {
        var reading = new PomodoroPhaseSpec { Kind = PomodoroPhaseKind.Focus, Name = "  Reading ", Minutes = 40 };
        Assert.Equal("Reading · 2 of 2", PomodoroCycle.Caption(Locale.En, [PomodoroPhaseSpec.Of(PomodoroPhaseKind.ShortBreak), reading], 1));
        Assert.Equal("Reading", PomodoroCycle.Caption(Locale.En, [reading], 0));
        Assert.Equal("Short break", PomodoroCycle.Label(Locale.En, new PomodoroPhaseSpec { Kind = PomodoroPhaseKind.ShortBreak, Name = " " }));
    }

    [Fact]
    public void S1460_every_sound_has_a_name_in_every_language()
    {
        foreach (var locale in Enum.GetValues<Locale>())
        {
            var names = PomodoroSounds.Order.Select(sound => PomodoroSounds.Label(locale, sound)).ToList();
            Assert.All(names, name => Assert.False(string.IsNullOrWhiteSpace(name)));
            Assert.Equal(names.Count, names.Distinct().Count());
        }

        Assert.Equal("Singing bowl", PomodoroSounds.Label(Locale.En, AlarmSound.Zen));
    }
}
