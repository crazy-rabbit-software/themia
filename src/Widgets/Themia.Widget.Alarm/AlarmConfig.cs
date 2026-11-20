using System.Text.Json.Serialization;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Core.Model;

/// <summary>
/// [S-4620] An alarm widget is one alarm: a time of day, the days it repeats on (none: it rings once),
/// whether it is on, a name, and the sound it rings with, its level and how long a snooze is. Several
/// alarms are several widgets. Kept in the file, unlike a timer: an alarm is a promise about tomorrow.
/// What it is <i>doing</i> (ringing, snoozed) is the alarm service's, in memory.
/// </summary>
public sealed class AlarmConfig : WidgetConfig, IShownSettings<SoundSetting>, IAlarm
{
    /// <summary>The most a name says, in characters.</summary>
    public const int MostName = 40;

    public const int DefaultVolume = 80;

    public const int MinSnooze = 1;

    public const int MaxSnooze = 30;

    /// <summary>When it rings, "HH:mm" on the 24-hour clock whatever the language shows; a new widget's is seven.</summary>
    public string Time { get; set; } = AlarmTimes.Text(AlarmTimes.Default);

    /// <summary>The days it repeats on ("mon" … "sun"), Monday first; absent for one that rings once.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string[]? Days { get; set; }

    /// <summary>Whether it is set to ring: the widget's switch. A new widget's is off: switching it on is the first thing anyone does.</summary>
    public bool On { get; set; }

    /// <summary>Its own name ("Wake up"); empty for "Alarm".</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>[S-4626] The ringtone, one of the timer's (<see cref="PomodoroSounds"/>); Off rings silently (the notice only).</summary>
    public AlarmSound Sound { get; set; } = AlarmSound.Bell;

    /// <summary>[S-6074] The volume of a sound, offered while there is one.</summary>
    [JsonIgnore]
    public IReadOnlyList<SoundSetting> ShownSettings => Sound == AlarmSound.Off ? [] : [SoundSetting.Volume];

    /// <summary>The ring's level, 0 to 100.</summary>
    public int Volume { get; set; } = DefaultVolume;

    /// <summary>[S-4625] How long a snooze is, in minutes (Windows' Clock: 10).</summary>
    public int SnoozeMinutes { get; set; } = 10;

    [JsonIgnore]
    public int EffectiveVolume => Math.Clamp(Volume, 0, 100);

    [JsonIgnore]
    public TimeSpan Snooze => TimeSpan.FromMinutes(Math.Clamp(SnoozeMinutes, MinSnooze, MaxSnooze));

    [JsonIgnore]
    public AlarmPlan Plan => AlarmPlan.Of(this);

    /// <summary>[S-4621] A time that is one, known days once each Monday first, a name no longer than <see cref="MostName"/>, a known sound, a level and a snooze in range.</summary>
    public override IReadOnlyList<string> Repair()
    {
        var repairs = new List<string>();

        var time = AlarmTimes.Text(AlarmTimes.Minutes(Time));
        var days = AlarmDays.Normalized(Days);
        var name = (Label ?? string.Empty).ReplaceLineEndings(" ").Trim();
        name = name.Length <= MostName ? name : name[..MostName].TrimEnd();

        if (time != Time || !SameDays(days, Days) || name != Label)
        {
            repairs.Add("Alarm widget had a bad time, day or name");
            (Time, Days, Label) = (time, days, name);
        }

        if (!Enum.IsDefined(Sound))
        {
            repairs.Add("Alarm widget had an unknown sound");
            Sound = AlarmSound.Bell;
        }

        if (Volume != EffectiveVolume)
        {
            repairs.Add($"Alarm volume {Volume} was out of range");
            Volume = EffectiveVolume;
        }

        if (SnoozeMinutes is < MinSnooze or > MaxSnooze)
        {
            repairs.Add($"Alarm snooze {SnoozeMinutes} min was out of range");
            SnoozeMinutes = Math.Clamp(SnoozeMinutes, MinSnooze, MaxSnooze);
        }

        return repairs;

        static bool SameDays(string[]? a, string[]? b) => (a ?? []).SequenceEqual(b ?? [], StringComparer.Ordinal) && (a is null) == (b is null);
    }

    public override WidgetConfig Clone() => new AlarmConfig
    {
        Time = Time,
        Days = Days is null ? null : [.. Days],
        On = On,
        Label = Label,
        Sound = Sound,
        Volume = Volume,
        SnoozeMinutes = SnoozeMinutes,
    };
}
