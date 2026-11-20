using System.Text.Json.Serialization;
using Themia.Core.Localization;
using Themia.Core.Office;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Widget.Pomodoro;

namespace Themia.Core.Model;

/// <summary>
/// [S-6035] How the Pomodoro draws the cycle's progress: the phases as a sectioned ring round the time,
/// as one sectioned bar, or the digits alone.
/// </summary>
[JsonConverter(typeof(StyleJsonConverter<PomodoroStyle>))]
public enum PomodoroStyle
{
    Ring = 0,
    Bar = 1,
    Digits = 2,
}

/// <summary>
/// [S-8112] What a phase is: focus or a break. It names the phase where the person gave it no name of its
/// own, colours its section (focus the accent, a break calm, [S-8115]) and picks the cue that enters it
/// ([S-152]). Read leniently: an unknown name is Focus, never a state file that refuses to load.
/// </summary>
[JsonConverter(typeof(LenientEnumConverter<PomodoroPhaseKind>))]
public enum PomodoroPhaseKind
{
    Focus = 0,
    ShortBreak = 1,
    LongBreak = 2,
}

/// <summary>[S-8112] One phase of the cycle: its kind, a name of the person's own (none: the kind's) and its length.</summary>
public sealed class PomodoroPhaseSpec
{
    /// <summary>[S-8112] A custom name's most characters: a caption, never a sentence.</summary>
    public const int MaxName = 32;

    public PomodoroPhaseKind Kind { get; set; }

    /// <summary>[S-8112] The person's own name for the phase; null or blank: the kind's name.</summary>
    public string? Name { get; set; }

    /// <summary>[S-8101] Its length, 1 to 180 minutes.</summary>
    public int Minutes { get; set; } = PomodoroConfig.FocusMinutes;

    [JsonIgnore]
    public int EffectiveMinutes => Math.Clamp(Minutes, PomodoroConfig.MinMinutes, PomodoroConfig.MaxMinutes);

    /// <summary>[S-8112] A phase of <paramref name="kind"/> at its classic length (25, 5, 15).</summary>
    public static PomodoroPhaseSpec Of(PomodoroPhaseKind kind, int? minutes = null) => new()
    {
        Kind = kind,
        Minutes = minutes ?? kind switch
        {
            PomodoroPhaseKind.ShortBreak => PomodoroConfig.ShortBreakMinutes,
            PomodoroPhaseKind.LongBreak => PomodoroConfig.LongBreakMinutes,
            _ => PomodoroConfig.FocusMinutes,
        },
    };

    public PomodoroPhaseSpec Clone() => new() { Kind = Kind, Name = Name, Minutes = Minutes };
}

/// <summary>
/// A focus/break timer that runs through its phases in order. [S-150] Run state is deliberately not
/// persisted; the phases are the config ([S-8112]).
/// </summary>
public sealed class PomodoroConfig : WidgetConfig, IStyled, IShownSettings<SoundSetting>
{
    /// <summary>[S-1460] The original's default level.</summary>
    public const int DefaultVolume = 70;

    /// <summary>[S-8113] The classic cycle's lengths: focus 25, a short break 5, the long break 15.</summary>
    public const int FocusMinutes = 25, ShortBreakMinutes = 5, LongBreakMinutes = 15;

    /// <summary>[S-8113] The classic cycle's rounds: four focus phases, the long break after the fourth.</summary>
    public const int ClassicRounds = 4;

    /// <summary>[S-8112] The most phases a cycle holds: the add lines are greyed at it.</summary>
    public const int MaxPhases = 16;

    private static readonly StyleOption[] PomodoroStyles =
    [
        StyleOption.Of(PomodoroStyle.Ring, PomodoroStr.Styles_Pomodoro_Ring.Local(), 0xEA3A),
        StyleOption.Of(PomodoroStyle.Bar, PomodoroStr.Styles_Pomodoro_Bar.Local(), 0xE7A5),
        StyleOption.Of(PomodoroStyle.Digits, PomodoroStr.Styles_Pomodoro_Digits.Local(), 0xEC92),
    ];

    /// <summary>[S-6035] How the cycle's progress is drawn.</summary>
    public PomodoroStyle Style { get; set; } = PomodoroStyle.Ring;

    IReadOnlyList<StyleOption> IStyled.Styles => PomodoroStyles;

    int IStyled.StyleValue
    {
        get => (int)Style;
        set => Style = (PomodoroStyle)value;
    }

    /// <summary>[S-6035] Whether the phases are drawn as a sectioned ring round the time: the Ring style only.</summary>
    [JsonIgnore]
    public bool DrawsRing => Style == PomodoroStyle.Ring;

    /// <summary>[S-6035] Whether the phases are drawn as one sectioned bar: the Bar style only.</summary>
    [JsonIgnore]
    public bool DrawsBar => Style == PomodoroStyle.Bar;

    /// <summary>[S-8101] The shortest phase, in minutes: the menu's least length and the least a nudge leaves ([S-8111]).</summary>
    public const int MinMinutes = 1;

    /// <summary>[S-8101] The longest phase, in minutes: the menu's most and the most a nudge makes ([S-8111]).</summary>
    public const int MaxMinutes = 180;

    /// <summary>[S-8112] [S-8113] The phases the timer runs through, in order; the classic cycle by default.</summary>
    public List<PomodoroPhaseSpec> Phases { get; set; } = Classic();

    /// <summary>[S-8114] Whether the cycle starts again after its last phase (on) or stops there (off).</summary>
    public bool Repeat { get; set; } = true;

    /// <summary>[S-1460] The cue a phase flip sounds; <see cref="AlarmSound.Off"/> for none.</summary>
    public AlarmSound Sound { get; set; } = AlarmSound.Chime;

    /// <summary>[S-6074] The volume of a sound, offered while there is one.</summary>
    [JsonIgnore]
    public IReadOnlyList<SoundSetting> ShownSettings => Sound == AlarmSound.Off ? [] : [SoundSetting.Volume];

    /// <summary>[S-1460] The cue's level, 0 to 100.</summary>
    public int Volume { get; set; } = DefaultVolume;

    [JsonIgnore]
    public int EffectiveVolume => Math.Clamp(Volume, 0, 100);

    /// <summary>
    /// [S-8113] The classic cycle (owner, 2026-11-20): Focus 25 and a short break 5, four times, the fourth
    /// break the long one, 15.
    /// </summary>
    public static List<PomodoroPhaseSpec> Classic()
    {
        var phases = new List<PomodoroPhaseSpec>(ClassicRounds * 2);
        for (var round = 1; round <= ClassicRounds; round++)
        {
            phases.Add(PomodoroPhaseSpec.Of(PomodoroPhaseKind.Focus));
            phases.Add(PomodoroPhaseSpec.Of(round == ClassicRounds ? PomodoroPhaseKind.LongBreak : PomodoroPhaseKind.ShortBreak));
        }

        return phases;
    }

    /// <summary>
    /// [S-8112] Bring the phases, the volume and the sound back inside their sets: no phases (or none
    /// stored) is the classic cycle, more than <see cref="MaxPhases"/> are cut, a length is held to 1..180, an
    /// unknown kind is Focus and a blank or long name is cleared or cut.
    /// </summary>
    public override IReadOnlyList<string> Repair()
    {
        var repairs = new List<string>();

        if (Phases is null || Phases.Count == 0 || Phases.Any(p => p is null))
        {
            repairs.Add("Pomodoro had no phases or a missing one; the classic cycle");
            Phases = Phases is { Count: > 0 } stored && stored.Any(p => p is not null) ? [.. stored.Where(p => p is not null)] : Classic();
        }

        if (Phases.Count > MaxPhases)
        {
            repairs.Add($"Pomodoro had {Phases.Count} phases; the first {MaxPhases} kept");
            Phases.RemoveRange(MaxPhases, Phases.Count - MaxPhases);
        }

        foreach (var phase in Phases)
        {
            if (!Enum.IsDefined(phase.Kind))
            {
                repairs.Add($"Pomodoro phase kind {(int)phase.Kind} unknown; focus");
                phase.Kind = PomodoroPhaseKind.Focus;
            }

            if (phase.Minutes != phase.EffectiveMinutes)
            {
                repairs.Add($"Pomodoro phase length {phase.Minutes} was out of range");
                phase.Minutes = phase.EffectiveMinutes;
            }

            var name = phase.Name?.Trim();
            var kept = string.IsNullOrEmpty(name) ? null : name.Length > PomodoroPhaseSpec.MaxName ? name[..PomodoroPhaseSpec.MaxName] : name;
            if (!string.Equals(kept, phase.Name, StringComparison.Ordinal))
            {
                repairs.Add("Pomodoro phase name was blank, padded or too long");
                phase.Name = kept;
            }
        }

        if (Volume != EffectiveVolume)
        {
            repairs.Add($"Pomodoro volume {Volume} was out of range");
            Volume = EffectiveVolume;
        }

        if (!Enum.IsDefined(Sound))
        {
            repairs.Add("Pomodoro widget had an unknown sound");
            Sound = AlarmSound.Chime;
        }

        return repairs;
    }

    public override WidgetConfig Clone() => new PomodoroConfig
    {
        Style = Style,
        Phases = [.. (Phases ?? []).Where(p => p is not null).Select(p => p.Clone())],
        Repeat = Repeat,
        Sound = Sound,
        Volume = Volume,
    };
}
