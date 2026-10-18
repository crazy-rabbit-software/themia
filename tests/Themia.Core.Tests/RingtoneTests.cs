using System.Buffers.Binary;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-1461]..[S-1463] The twelve ringtones the pomodoro and the alarm share (the SDK's
/// <see cref="PomodoroSounds"/>): their direction, their level and the wave they render. Split out of
/// the pomodoro's tests when the widget moved to its module, unchanged in what they assert.
/// </summary>
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
