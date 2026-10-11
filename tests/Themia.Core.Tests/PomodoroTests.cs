using System.Buffers.Binary;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// The pomodoro cycle. Pure, so the flip at the end of a phase is reachable without running a widget
/// for twenty-five minutes — which is the only reason [S-03] and [S-06] are testable at all.
/// </summary>
public class PomodoroCycleTests
{
    private const int Work = 1500; // 25 min
    private const int Break = 300; //  5 min

    // -----------------------------------------------------------------------------------------
    // [S-01] durations, clamped
    // -----------------------------------------------------------------------------------------

    [Theory]
    [InlineData(25, 1500)]
    [InlineData(1, 60)]
    [InlineData(180, 10800)]
    [InlineData(0, 60)]         // a persisted 0 would otherwise flip once a second for ever
    [InlineData(-5, 60)]
    [InlineData(9999, 10800)]
    public void S01_a_phase_length_is_clamped_to_one_to_a_hundred_and_eighty_minutes(int configured, int expectedSeconds)
    {
        var config = new PomodoroConfig { WorkMinutes = configured, BreakMinutes = configured };

        Assert.Equal(expectedSeconds, PomodoroCycle.PhaseSeconds(PomodoroPhase.Work, config));
        Assert.Equal(expectedSeconds, PomodoroCycle.PhaseSeconds(PomodoroPhase.Break, config));
    }

    [Fact]
    public void S02_a_fresh_cycle_is_a_full_work_phase()
    {
        var state = PomodoroCycle.Fresh(new PomodoroConfig { WorkMinutes = 30, BreakMinutes = 7 });

        Assert.Equal(PomodoroPhase.Work, state.Phase);
        Assert.Equal(1800, state.RemainingSeconds);
    }

    // -----------------------------------------------------------------------------------------
    // [S-03] the flip
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void S03_a_tick_with_time_left_only_decrements()
    {
        Assert.Equal(
            new PomodoroTick(PomodoroPhase.Work, 1499, Flipped: false),
            PomodoroCycle.Advance(new PomodoroState(PomodoroPhase.Work, Work), Work, Break));

        Assert.Equal(
            new PomodoroTick(PomodoroPhase.Break, 1, Flipped: false),
            PomodoroCycle.Advance(new PomodoroState(PomodoroPhase.Break, 2), Work, Break));
    }

    [Fact]
    public void S03_work_flips_to_a_full_break_and_keeps_running()
        => Assert.Equal(
            new PomodoroTick(PomodoroPhase.Break, Break, Flipped: true),
            PomodoroCycle.Advance(new PomodoroState(PomodoroPhase.Work, 1), Work, Break));

    [Fact]
    public void S03_break_flips_to_a_full_work_phase_and_keeps_running()
        => Assert.Equal(
            new PomodoroTick(PomodoroPhase.Work, Work, Flipped: true),
            PomodoroCycle.Advance(new PomodoroState(PomodoroPhase.Break, 1), Work, Break));

    [Fact]
    public void S03_one_remaining_is_the_last_second_not_a_spare_one()
    {
        // The off-by-one that would show "0:00" for a whole second before flipping.
        var tick = PomodoroCycle.Advance(new PomodoroState(PomodoroPhase.Work, 1), 60, 60);

        Assert.True(tick.Flipped);
        Assert.Equal(60, tick.RemainingSeconds);
    }

    [Fact]
    public void S03_it_flips_exactly_once_per_phase_across_a_whole_cycle()
    {
        var state = new PomodoroState(PomodoroPhase.Work, 3);
        var flips = new List<PomodoroPhase>();

        // 3 → 2 → 1 → flip(break, 2) → 1 → flip(work, 2) → 1
        for (var i = 0; i < 6; i++)
        {
            var tick = PomodoroCycle.Advance(state, 2, 2);

            if (tick.Flipped)
            {
                flips.Add(tick.Phase);
            }

            state = new PomodoroState(tick.Phase, tick.RemainingSeconds);
        }

        Assert.Equal(new[] { PomodoroPhase.Break, PomodoroPhase.Work }, flips);
    }

    [Fact]
    public void S03_a_zero_length_phase_still_loads_at_least_one_second()
    {
        // Defence against the 1 Hz alarm loop: a phase of no seconds would flip on every tick.
        var tick = PomodoroCycle.Advance(new PomodoroState(PomodoroPhase.Work, 1), 0, 0);

        Assert.True(tick.Flipped);
        Assert.Equal(1, tick.RemainingSeconds);
    }

    // -----------------------------------------------------------------------------------------
    // [S-04] progress
    // -----------------------------------------------------------------------------------------

    [Theory]
    [InlineData(1500, 1500, 0.0)]   // nothing elapsed at the start of a phase
    [InlineData(750, 1500, 0.5)]
    [InlineData(1, 1500, 0.999333)]
    [InlineData(0, 1500, 1.0)]      // full at the flip
    public void S04_progress_is_the_elapsed_fraction_of_the_phase(int remaining, int total, double expected)
        => Assert.Equal(expected, PomodoroCycle.Progress(new PomodoroState(PomodoroPhase.Work, remaining), total), 5);

    [Fact]
    public void S04_progress_stays_within_range_when_the_durations_change_mid_phase()
    {
        // Shortening the phase in settings while it runs leaves `remaining` above the new total for
        // one tick. An unclamped fraction would drive the bar backwards past its own start.
        Assert.Equal(0.0, PomodoroCycle.Progress(new PomodoroState(PomodoroPhase.Work, 5000), 1500));
        Assert.Equal(1.0, PomodoroCycle.Progress(new PomodoroState(PomodoroPhase.Work, -20), 1500));
        Assert.Equal(0.0, PomodoroCycle.Progress(new PomodoroState(PomodoroPhase.Work, 10), 0));
    }

    [Theory]
    [InlineData(1500, "25:00")]
    [InlineData(59, "0:59")]
    [InlineData(60, "1:00")]
    [InlineData(5, "0:05")]
    [InlineData(0, "0:00")]
    [InlineData(-3, "0:00")]        // never renders a negative clock
    [InlineData(10800, "180:00")]
    public void S02_the_readout_is_minutes_and_padded_seconds(int remaining, string expected)
        => Assert.Equal(expected, PomodoroCycle.Clock(remaining));
}

/// <summary>[S-1460]..[S-1463] the twelve ringtones, their volume, and the phase-flip direction.</summary>
public class PomodoroSoundTests
{
    public static TheoryData<AlarmSound> Sounding()
    {
        var data = new TheoryData<AlarmSound>();
        foreach (var sound in PomodoroSounds.Order.Where(s => s != AlarmSound.Off))
        {
            data.Add(sound);
        }

        return data;
    }

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

    [Fact]
    public void S1461_off_is_the_empty_plan_and_renders_nothing()
    {
        Assert.True(PomodoroSounds.Plan(AlarmSound.Off, PomodoroPhase.Work).IsEmpty);
        Assert.Empty(PomodoroSounds.Wave(AlarmSound.Off, PomodoroPhase.Break, 100));
    }

    [Theory]
    [MemberData(nameof(Sounding))]
    public void S1461_every_sound_rises_into_focus_and_falls_into_a_break(AlarmSound sound)
    {
        var focus = PomodoroSounds.Notes(sound, PomodoroPhase.Work);
        var rest = PomodoroSounds.Notes(sound, PomodoroPhase.Break);

        Assert.True(focus.Length >= 2);
        Assert.True(focus[^1] > focus[0], $"{sound} must rise into focus");
        Assert.Equal(focus.Reverse(), rest);
    }

    [Theory]
    [MemberData(nameof(Sounding))]
    public void S1463_every_sound_is_levelled_to_the_same_peak_bound(AlarmSound sound)
    {
        var plan = PomodoroSounds.Plan(sound, PomodoroPhase.Work);
        Assert.Equal(PomodoroSounds.Peak, plan.PeakBound(), 6);
    }

    [Theory]
    [MemberData(nameof(Sounding))]
    public void S1463_no_sound_clips_at_volume_a_hundred(AlarmSound sound)
    {
        var samples = PomodoroSounds.Plan(sound, PomodoroPhase.Break).Render(1.0);
        Assert.All(samples, s => Assert.True(Math.Abs(s) < 1.0, $"{sound} clips: {s}"));
    }

    [Theory]
    [MemberData(nameof(Sounding))]
    public void S1462_every_sound_is_a_well_formed_wave_that_starts_and_ends_silent(AlarmSound sound)
    {
        var wave = PomodoroSounds.Wave(sound, PomodoroPhase.Work, 70);
        var span = wave.AsSpan();

        Assert.True("RIFF"u8.SequenceEqual(span[..4]));
        Assert.True("WAVE"u8.SequenceEqual(span[8..12]));
        Assert.Equal(SoundPlan.SampleRate, BinaryPrimitives.ReadInt32LittleEndian(span[24..]));
        Assert.Equal(16, BinaryPrimitives.ReadInt16LittleEndian(span[34..]));
        Assert.Equal(wave.Length - 44, BinaryPrimitives.ReadInt32LittleEndian(span[40..]));

        Assert.Equal(0, BinaryPrimitives.ReadInt16LittleEndian(span[44..]));
        Assert.Equal(0, BinaryPrimitives.ReadInt16LittleEndian(span[^2..]));

        // A notification, not a song: the longest (the zen bowl) is under three seconds.
        Assert.InRange((wave.Length - 44) / 2.0 / SoundPlan.SampleRate, 0.3, 3.0);
    }

    [Fact]
    public void S1462_volume_scales_the_cue_and_zero_is_silence()
    {
        var loud = PomodoroSounds.Plan(AlarmSound.Chime, PomodoroPhase.Work).Render(1.0).Max(Math.Abs);
        var soft = PomodoroSounds.Plan(AlarmSound.Chime, PomodoroPhase.Work).Render(0.5).Max(Math.Abs);

        Assert.Equal(loud / 2, soft, 6);
        Assert.Empty(PomodoroSounds.Wave(AlarmSound.Chime, PomodoroPhase.Work, 0));
    }

    [Fact]
    public void S1462_rendering_is_deterministic_noise_included()
        => Assert.Equal(PomodoroSounds.Wave(AlarmSound.Gong, PomodoroPhase.Work, 80), PomodoroSounds.Wave(AlarmSound.Gong, PomodoroPhase.Work, 80));

    [Fact]
    public void S1462_an_envelope_ramps_between_points_and_holds_outside_them()
    {
        EnvPoint[] envelope = [new(0, 0), new(10, 1), new(20, 0)];

        Assert.Equal(0, SoundPlan.Envelope(envelope, -5));
        Assert.Equal(0.5, SoundPlan.Envelope(envelope, 5), 9);
        Assert.Equal(1, SoundPlan.Envelope(envelope, 10), 9);
        Assert.Equal(0.25, SoundPlan.Envelope(envelope, 17.5), 9);
        Assert.Equal(0, SoundPlan.Envelope(envelope, 99));
    }
}

/// <summary>[S-1466]/[S-1467] skip, the next-phase line, and the session count.</summary>
public class PomodoroLadderRulesTests
{
    [Fact]
    public void S1466_skip_loads_the_other_phase_in_full()
    {
        var config = new PomodoroConfig { WorkMinutes = 25, BreakMinutes = 5 };
        var skipped = PomodoroCycle.Skip(new PomodoroState(PomodoroPhase.Work, 61), config);
        Assert.Equal(new PomodoroState(PomodoroPhase.Break, 300), skipped);
        Assert.Equal(new PomodoroState(PomodoroPhase.Work, 1500), PomodoroCycle.Skip(skipped, config));
    }

    [Fact]
    public void S1466_the_next_line_names_the_phase_the_flip_loads()
    {
        var config = new PomodoroConfig { WorkMinutes = 50, BreakMinutes = 10 };
        Assert.Equal("Next: Break 10:00", PomodoroCycle.NextLine(Locale.En, PomodoroPhase.Work, config));
        Assert.Equal("Next: Focus 50:00", PomodoroCycle.NextLine(Locale.En, PomodoroPhase.Break, config));
        Assert.Equal("Danach: Pause 10:00", PomodoroCycle.NextLine(Locale.De, PomodoroPhase.Work, config));
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
