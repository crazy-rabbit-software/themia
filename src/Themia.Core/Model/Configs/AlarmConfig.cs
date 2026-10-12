using System.Text.Json.Serialization;
using Themia.Core.Widgets;

namespace Themia.Core.Model;

/// <summary>
/// [S-4620] One alarm: a time of day, the days it repeats on (none: it rings once), whether it is on,
/// and a name. Kept in the file, unlike a timer: an alarm is a promise about tomorrow.
/// </summary>
public sealed class AlarmEntry
{
    /// <summary>The most a name says, in characters.</summary>
    public const int MostName = 40;

    /// <summary>Stable identity, so the alarm service keeps what an alarm is doing across edits of the others.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>When it rings, "HH:mm" on the 24-hour clock whatever the language shows.</summary>
    public string Time { get; set; } = AlarmTimes.Text(AlarmTimes.Default);

    /// <summary>The days it repeats on ("mon" … "sun"), Monday first; absent for one that rings once.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string[]? Days { get; set; }

    /// <summary>Whether it is set to ring: the switch on its row.</summary>
    public bool On { get; set; }

    /// <summary>Its own name ("Wake up"); empty for "Alarm".</summary>
    public string Label { get; set; } = string.Empty;

    [JsonIgnore]
    public AlarmPlan Plan => AlarmPlan.Of(this);

    public AlarmEntry Clone() => new()
    {
        Id = Id,
        Time = Time,
        Days = Days is null ? null : [.. Days],
        On = On,
        Label = Label,
    };

    /// <summary>A time that is one, known days once each Monday first, a name no longer than <see cref="MostName"/>, an id. True when anything changed.</summary>
    public bool Normalize()
    {
        var before = (Id, Time, Days: Days is null ? null : string.Join(",", Days), Label);

        if (string.IsNullOrWhiteSpace(Id))
        {
            Id = Guid.NewGuid().ToString("N");
        }

        Time = AlarmTimes.Text(AlarmTimes.Minutes(Time));
        Days = AlarmDays.Normalized(Days);
        var name = (Label ?? string.Empty).ReplaceLineEndings(" ").Trim();
        Label = name.Length <= MostName ? name : name[..MostName].TrimEnd();

        return before != (Id, Time, Days is null ? null : string.Join(",", Days), Label);
    }
}

/// <summary>
/// [S-4620] An alarm widget: its alarms, the sound they ring with and how long a snooze is. What an
/// alarm is <i>doing</i> (ringing, snoozed) is the alarm service's, in memory.
/// </summary>
public sealed class AlarmConfig : WidgetConfig
{
    /// <summary>The most alarms one widget keeps.</summary>
    public const int MostAlarms = 20;

    public const int DefaultVolume = 80;

    public const int MinSnooze = 1;

    public const int MaxSnooze = 30;

    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.Alarm;

    /// <summary>A new widget has one alarm, at seven, off: switching it on is the first thing anyone does.</summary>
    public List<AlarmEntry> Alarms { get; set; } = [new AlarmEntry()];

    /// <summary>[S-4626] The ringtone, one of the timer's (<see cref="PomodoroSounds"/>); Off rings silently (the notice only).</summary>
    public AlarmSound Sound { get; set; } = AlarmSound.Bell;

    /// <summary>The ring's level, 0 to 100.</summary>
    public int Volume { get; set; } = DefaultVolume;

    /// <summary>[S-4625] How long a snooze is, in minutes (Windows' Clock: 10).</summary>
    public int SnoozeMinutes { get; set; } = 10;

    [JsonIgnore]
    public int EffectiveVolume => Math.Clamp(Volume, 0, 100);

    [JsonIgnore]
    public TimeSpan Snooze => TimeSpan.FromMinutes(Math.Clamp(SnoozeMinutes, MinSnooze, MaxSnooze));

    public override IReadOnlyList<string> Repair()
    {
        var repairs = new List<string>();
        Alarms ??= [];

        if (Alarms.RemoveAll(a => a is null) > 0)
        {
            repairs.Add("Alarm widget had an empty alarm entry");
        }

        if (Alarms.Count > MostAlarms)
        {
            repairs.Add($"Alarm widget had {Alarms.Count} alarms; kept {MostAlarms}");
            Alarms.RemoveRange(MostAlarms, Alarms.Count - MostAlarms);
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var alarm in Alarms)
        {
            if (alarm.Normalize())
            {
                repairs.Add("Alarm widget had an alarm with a bad time, day or name");
            }

            if (!ids.Add(alarm.Id))
            {
                alarm.Id = Guid.NewGuid().ToString("N");
                repairs.Add("Alarm widget had two alarms with one id");
            }
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
    }

    public override WidgetConfig Clone() => new AlarmConfig
    {
        Alarms = [.. (Alarms ?? []).Select(a => a.Clone())],
        Sound = Sound,
        Volume = Volume,
        SnoozeMinutes = SnoozeMinutes,
    };
}
