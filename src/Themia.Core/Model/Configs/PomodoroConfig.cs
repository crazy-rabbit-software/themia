using System.Text.Json.Serialization;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Office;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Core.Model;

/// <summary>[S-6074] The sound rows of the pomodoro and the alarm that depend on the sound chosen.</summary>
public enum SoundSetting
{
    /// <summary>The volume: only while a sound is chosen.</summary>
    Volume,
}

/// <summary>
/// [S-1460] The phase-flip cue: off, or one of the original's twelve ringtones. Read leniently, so
/// a hand-edited or unknown name is the default (<see cref="Chime"/>, the zero value) rather than a
/// state file that refuses to load.
/// </summary>
[JsonConverter(typeof(LenientEnumConverter<AlarmSound>))]
public enum AlarmSound
{
    Chime = 0,
    Marimba = 1,
    Crystal = 2,
    Pluck = 3,
    Droplet = 4,
    Birdsong = 5,
    Zen = 6,
    Bell = 7,
    Gong = 8,
    Arcade = 9,
    Digital = 10,
    Buzzer = 11,
    Off = 12,
}

/// <summary>
/// [S-6035] How the Pomodoro draws the phase's progress: a ring where the tile holds one (the bar
/// below that), always the bar, or the digits alone.
/// </summary>
[JsonConverter(typeof(StyleJsonConverter<PomodoroStyle>))]
public enum PomodoroStyle
{
    Ring = 0,
    Bar = 1,
    Digits = 2,
}

/// <summary>A focus/break timer. [S-01] Run state is deliberately not persisted.</summary>
public sealed class PomodoroConfig : WidgetConfig, IStyled, IShownSettings<SoundSetting>
{
    /// <summary>[S-1460] The original's default level.</summary>
    public const int DefaultVolume = 70;

    private static readonly StyleOption[] PomodoroStyles =
    [
        StyleOption.Of(PomodoroStyle.Ring, Str.Styles_Pomodoro_Ring, 0xEA3A),
        StyleOption.Of(PomodoroStyle.Bar, Str.Styles_Pomodoro_Bar, 0xE7A5),
        StyleOption.Of(PomodoroStyle.Digits, Str.Styles_Pomodoro_Digits, 0xEC92),
    ];

    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.Pomodoro;

    /// <summary>[S-6902] This widget's descriptor: what the host knows of it without its UI.</summary>
    public static WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("pomodoro"),
        Kind = WidgetKind.Pomodoro,
        LabelId = Str.Widget_Pomodoro_Label,
        DescriptionId = Str.WidgetsA_Catalog_Pomodoro_Description,
        Glyph = Glyphs.Pomodoro,
        DefaultWidth = 240,
        DefaultHeight = 200,
    };

    /// <summary>[S-6035] How the phase's progress is drawn.</summary>
    public PomodoroStyle Style { get; set; } = PomodoroStyle.Ring;

    IReadOnlyList<StyleOption> IStyled.Styles => PomodoroStyles;

    int IStyled.StyleValue
    {
        get => (int)Style;
        set => Style = (PomodoroStyle)value;
    }

    /// <summary>[S-6035] Whether a tile with room for a ring draws one: the Ring style only.</summary>
    [JsonIgnore]
    public bool DrawsRing => Style == PomodoroStyle.Ring;

    /// <summary>[S-6035] Whether the progress bar is drawn where the ladder has room for it: not by the Digits style.</summary>
    [JsonIgnore]
    public bool DrawsBar => Style != PomodoroStyle.Digits;

    /// <summary>Length of a focus phase, 1 to 180 minutes.</summary>
    public int WorkMinutes { get; set; } = 25;

    /// <summary>Length of a break phase, 1 to 180 minutes.</summary>
    public int BreakMinutes { get; set; } = 5;

    /// <summary>[S-1460] The cue a phase flip sounds; <see cref="AlarmSound.Off"/> for none.</summary>
    public AlarmSound Sound { get; set; } = AlarmSound.Chime;

    /// <summary>[S-6074] The volume of a sound, offered while there is one.</summary>
    [JsonIgnore]
    public IReadOnlyList<SoundSetting> ShownSettings => Sound == AlarmSound.Off ? [] : [SoundSetting.Volume];

    /// <summary>[S-1460] The cue's level, 0 to 100.</summary>
    public int Volume { get; set; } = DefaultVolume;

    [JsonIgnore]
    public int EffectiveWorkMinutes => Math.Clamp(WorkMinutes, 1, 180);

    [JsonIgnore]
    public int EffectiveBreakMinutes => Math.Clamp(BreakMinutes, 1, 180);

    [JsonIgnore]
    public int EffectiveVolume => Math.Clamp(Volume, 0, 100);

    /// <summary>Bring the volume and the sound back inside their sets.</summary>
    public override IReadOnlyList<string> Repair()
    {
        var repairs = new List<string>();

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
        WorkMinutes = WorkMinutes,
        BreakMinutes = BreakMinutes,
        Sound = Sound,
        Volume = Volume,
    };
}
