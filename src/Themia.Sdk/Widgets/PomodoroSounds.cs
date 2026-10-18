using Themia.Core.Localization;
using Themia.Core.Model;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-1461] The Pomodoro's twelve ringtones, ported note for note from the original's
/// <c>alarmSound.ts</c>, as <see cref="SoundPlan"/>s.
/// <para>
/// Each alarm carries the phase it announces in its melodic direction: the focus cue rises (get
/// going), the break cue is the same notes reversed (wind down). The user is not looking at the
/// widget, so the sound alone has to say which way the timer just rolled. Every design is therefore
/// one note sequence plus a timbre, and the reversal lives in <see cref="Plan"/> once rather than in
/// twelve places.
/// </para>
/// </summary>
public static class PomodoroSounds
{
    /// <summary>
    /// [S-1463] The peak every alarm is levelled to, leaving 10% headroom at volume 100. Without it
    /// "digital" (one square voice at a time) came out the quietest and "gong" (four partials) the
    /// loudest, the reverse of how insistent they are meant to be.
    /// </summary>
    public const double Peak = 0.9;

    /// <summary>Near-instant attack, so a strike reads as struck rather than swelled.</summary>
    private const double AttackMs = 6;

    /// <summary>[S-1460] The order the settings list them in: off, then gentle to insistent.</summary>
    public static IReadOnlyList<AlarmSound> Order { get; } =
    [
        AlarmSound.Off, AlarmSound.Chime, AlarmSound.Marimba, AlarmSound.Crystal, AlarmSound.Pluck,
        AlarmSound.Droplet, AlarmSound.Birdsong, AlarmSound.Zen, AlarmSound.Bell, AlarmSound.Gong,
        AlarmSound.Arcade, AlarmSound.Digital, AlarmSound.Buzzer,
    ];

    /// <summary>The name a person reads in the settings list.</summary>
    public static string Label(AlarmSound sound) => Label(Strings.Current, sound);

    /// <summary><see cref="Label(AlarmSound)"/> in an explicit language (the original's own sound names).</summary>
    public static string Label(Locale locale, AlarmSound sound) => Strings.Get(locale, sound switch
    {
        AlarmSound.Off => Str.Pomodoro_AlarmSound_Off,
        AlarmSound.Chime => Str.Pomodoro_AlarmSound_Chime,
        AlarmSound.Marimba => Str.Pomodoro_AlarmSound_Marimba,
        AlarmSound.Crystal => Str.Pomodoro_AlarmSound_Crystal,
        AlarmSound.Pluck => Str.Pomodoro_AlarmSound_Pluck,
        AlarmSound.Droplet => Str.Pomodoro_AlarmSound_Droplet,
        AlarmSound.Birdsong => Str.Pomodoro_AlarmSound_Birdsong,
        AlarmSound.Zen => Str.Pomodoro_AlarmSound_Zen,
        AlarmSound.Bell => Str.Pomodoro_AlarmSound_Bell,
        AlarmSound.Gong => Str.Pomodoro_AlarmSound_Gong,
        AlarmSound.Arcade => Str.Pomodoro_AlarmSound_Arcade,
        AlarmSound.Digital => Str.Pomodoro_AlarmSound_Digital,
        AlarmSound.Buzzer => Str.Pomodoro_AlarmSound_Buzzer,
        _ => Str.Pomodoro_AlarmSound_Chime,
    });

    /// <summary>
    /// The pitches of a sound's notes in the order they sound for <paramref name="entering"/>:
    /// rising into focus, falling into a break. Empty for <see cref="AlarmSound.Off"/>.
    /// </summary>
    public static double[] Notes(AlarmSound sound, PomodoroPhase entering)
    {
        if (Design(sound) is not { } design)
        {
            return [];
        }

        var notes = design.Notes.ToArray();

        if (entering == PomodoroPhase.Break)
        {
            Array.Reverse(notes);
        }

        return notes;
    }

    /// <summary>
    /// [S-1461] The levelled plan for <paramref name="sound"/> announcing entry into
    /// <paramref name="entering"/>. <see cref="AlarmSound.Off"/> — or any value outside the set — is
    /// the empty plan, which plays nothing.
    /// </summary>
    public static SoundPlan Plan(AlarmSound sound, PomodoroPhase entering)
    {
        if (Design(sound) is not { } design)
        {
            return SoundPlan.Empty;
        }

        var notes = Notes(sound, entering);
        var voices = new List<SoundVoice>();

        for (var i = 0; i < notes.Length; i++)
        {
            voices.AddRange(design.Timbre(design.Offsets[i], notes[i], i));
        }

        return new SoundPlan(voices).Normalised(Peak);
    }

    /// <summary>The cue as a WAVE file image at <paramref name="volume"/> percent; empty when there is nothing to play.</summary>
    public static byte[] Wave(AlarmSound sound, PomodoroPhase entering, int volume)
        => Plan(sound, entering).RenderWave(Math.Clamp(volume, 0, 100) / 100.0);

    // ---- voices ---------------------------------------------------------------------------------

    private static SoundVoice Tone(VoiceShape shape, double at, double length, EnvPoint[] pitch, EnvPoint[] gain)
        => new(shape, at, at + length, pitch, gain);

    /// <summary>A struck, decaying partial: fast attack, then a long fall to silence.</summary>
    private static SoundVoice Struck(double at, double hz, double decayMs, double peak, VoiceShape shape = VoiceShape.Sine)
        => Tone(shape, at, decayMs, [new(0, hz)], [new(0, 0), new(AttackMs, peak), new(decayMs, 0)]);

    /// <summary>A flat-topped square blip: the digital alarm's building block.</summary>
    private static SoundVoice Beep(double at, double hz, double lengthMs, double peak)
        => Tone(VoiceShape.Square, at, lengthMs, [new(0, hz)], [new(0, 0), new(4, peak), new(lengthMs - 8, peak), new(lengthMs, 0)]);

    /// <summary>The voices one note is made of; <c>index</c> for sounds whose notes differ (a gong's answer).</summary>
    private delegate SoundVoice[] Timbre(double at, double hz, int index);

    private sealed record AlarmDesign(double[] Offsets, double[] Notes, Timbre Timbre);

    /// <summary>Inharmonic partials of a struck bell; 2.76 and 5.40 are what make it a bell rather than a beep.</summary>
    private static readonly (double Ratio, double Peak, double DecayMs)[] BellPartials =
    [
        (1, 0.55, 1600),
        (2.76, 0.18, 900),
        (5.4, 0.08, 520),
    ];

    private static AlarmDesign? Design(AlarmSound sound) => sound switch
    {
        // Soft three-note major triad with an octave shimmer: the gentle default. C5 E5 G5.
        AlarmSound.Chime => new([0, 170, 340], [523.25, 659.25, 783.99], (at, hz, _) =>
        [
            Struck(at, hz, 900, 0.5, VoiceShape.Triangle),
            Struck(at, hz * 2, 480, 0.11),
        ]),

        // Two strikes of an inharmonic bell a fifth apart: the longest-ringing option.
        AlarmSound.Bell => new([0, 620], [523.25, 523.25 * 1.5], (at, hz, _) =>
            [.. BellPartials.Select(p => Struck(at, hz * p.Ratio, p.DecayMs, p.Peak))]),

        // Three double-beeps: the insistent alarm-clock pattern.
        AlarmSound.Digital => new([0, 130, 340, 470, 680, 810], [698.46, 880, 698.46, 880, 698.46, 880], (at, hz, _) =>
            [Beep(at, hz, 85, 0.4)]),

        // A deep struck gong, a short noise transient for the mallet, and a quieter answering strike.
        AlarmSound.Gong => new([0, 700], [110, 164.81], (at, hz, i) => i == 0
            ?
            [
                Tone(VoiceShape.Noise, at, 110, [new(0, 1400), new(110, 600)], [new(0, 0), new(6, 0.3), new(110, 0)]),
                Struck(at, hz, 2000, 0.9),
                Struck(at, hz * 2, 1300, 0.24),
                Struck(at, hz * 3, 800, 0.1),
            ]
            : [Struck(at, hz, 1600, 0.4), Struck(at, hz * 2, 900, 0.12)]),

        // Wooden mallet bars: short, warm, dry. The tuned 4th partial is what makes it a marimba. C5 F5 A5.
        AlarmSound.Marimba => new([0, 110, 220], [523.25, 698.46, 880], (at, hz, _) =>
        [
            Struck(at, hz, 380, 0.55),
            Struck(at, hz * 4, 150, 0.2),
            Struck(at, hz * 10, 60, 0.06),
        ]),

        // High glass: a slow-beating detuned pair that shimmers as it rings out. C6 E6 G6.
        AlarmSound.Crystal => new([0, 150, 300], [1046.5, 1318.51, 1567.98], (at, hz, _) =>
        [
            Struck(at, hz, 1200, 0.4),
            Struck(at, hz * 1.003, 1100, 0.3),
            Struck(at, hz * 2, 600, 0.12, VoiceShape.Triangle),
        ]),

        // Plucked string: a fast four-note run, like brushing a harp. G4 C5 E5 G5.
        AlarmSound.Pluck => new([0, 80, 160, 240], [392, 523.25, 659.25, 783.99], (at, hz, _) =>
        [
            Struck(at, hz, 450, 0.5, VoiceShape.Triangle),
            Struck(at, hz * 3, 120, 0.1, VoiceShape.Sawtooth),
        ]),

        // Two water drops: a rising bloop, nothing else. The bloop is the pitch rise.
        AlarmSound.Droplet => new([0, 260], [880, 1174.66], (at, hz, _) =>
        [
            Tone(VoiceShape.Sine, at, 240, [new(0, hz * 0.55), new(70, hz * 1.25), new(240, hz * 1.35)], [new(0, 0), new(8, 0.6), new(240, 0)]),
            Struck(at, hz * 3, 60, 0.08),
        ]),

        // Three chirps: a nature cue for a desk that should stay quiet.
        AlarmSound.Birdsong => new([0, 190, 380], [1567.98, 2093, 2637.02], (at, hz, _) =>
        [
            Tone(VoiceShape.Sine, at, 95, [new(0, hz * 0.8), new(45, hz * 1.35), new(95, hz * 1.1)], [new(0, 0), new(10, 0.55), new(80, 0.4), new(95, 0)]),
            Tone(VoiceShape.Sine, at, 95, [new(0, hz * 1.6), new(45, hz * 2.7), new(95, hz * 2.2)], [new(0, 0), new(10, 0.14), new(95, 0)]),
        ]),

        // Singing bowl: a slow swell with a long beating tail. C4 G4.
        AlarmSound.Zen => new([0, 900], [261.63, 392], (at, hz, _) =>
        [
            Tone(VoiceShape.Sine, at, 1600, [new(0, hz)], [new(0, 0), new(40, 0.6), new(1600, 0)]),
            Tone(VoiceShape.Sine, at, 1600, [new(0, hz * 1.004)], [new(0, 0), new(45, 0.45), new(1600, 0)]),
            Struck(at, hz * 2.4, 900, 0.18),
            Struck(at, hz * 4.6, 500, 0.07),
        ]),

        // Chiptune fanfare: four square notes landing on a held final one.
        AlarmSound.Arcade => new([0, 90, 180, 270], [523.25, 659.25, 783.99, 1046.5], (at, hz, i) => i == 3
            ? [Beep(at, hz, 320, 0.45), Beep(at, hz * 1.5, 320, 0.2)]
            : [Beep(at, hz, 80, 0.4)]),

        // Harsh two-tone buzz, chopped rather than sustained: the gaps make it a buzzer, not a drone.
        AlarmSound.Buzzer => new([0, 420], [220, 293.66], (at, hz, _) =>
        [
            Tone(VoiceShape.Sawtooth, at, 340, [new(0, hz)],
            [
                new(0, 0), new(6, 0.5), new(70, 0.5), new(80, 0.05), new(95, 0.05),
                new(105, 0.5), new(175, 0.5), new(185, 0.05), new(200, 0.05),
                new(210, 0.5), new(300, 0.5), new(340, 0),
            ]),
            Tone(VoiceShape.Square, at, 340, [new(0, hz * 2)], [new(0, 0), new(6, 0.16), new(300, 0.16), new(340, 0)]),
        ]),

        _ => null,
    };
}
