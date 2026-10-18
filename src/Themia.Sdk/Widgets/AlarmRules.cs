using System.Globalization;
using Themia.Core.Localization;
using Themia.Core.Model;

namespace Themia.Core.Widgets;

/// <summary>[S-4621] An alarm's time of day as minutes after midnight, and as the file writes it.</summary>
public static class AlarmTimes
{
    /// <summary>What a new alarm is set to: seven in the morning.</summary>
    public const int Default = 7 * 60;

    /// <summary>Minutes in a day.</summary>
    public const int Day = 24 * 60;

    /// <summary>"7:00", "07:00", "18:30" as minutes after midnight; null for anything else.</summary>
    public static int? Parse(string? text)
    {
        var parts = (text ?? string.Empty).Trim().Split(':');
        if (parts.Length != 2
            || parts[1].Length != 2
            || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var hours)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var minutes)
            || hours > 23 || minutes > 59)
        {
            return null;
        }

        return (hours * 60) + minutes;
    }

    /// <summary>What <paramref name="text"/> sets it to: <see cref="Default"/> where it is no time.</summary>
    public static int Minutes(string? text) => Parse(text) ?? Default;

    /// <summary>"07:00": minutes after midnight as the file writes them, wrapped into the day.</summary>
    public static string Text(int minutes)
    {
        var day = ((minutes % Day) + Day) % Day;
        return string.Create(CultureInfo.InvariantCulture, $"{day / 60:00}:{day % 60:00}");
    }
}

/// <summary>
/// [S-4621] The days an alarm repeats on, as the file writes them ("mon" … "sun") and as a mask of
/// bits, one per <see cref="DayOfWeek"/> (Sunday the lowest).
/// </summary>
public static class AlarmDays
{
    /// <summary>Each day's word, by <see cref="DayOfWeek"/>.</summary>
    public static readonly string[] Words = ["sun", "mon", "tue", "wed", "thu", "fri", "sat"];

    /// <summary>The days Monday first, the order the file writes them in.</summary>
    public static readonly DayOfWeek[] Week =
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday];

    public const int Every = 0x7F;

    /// <summary>Monday to Friday.</summary>
    public const int Weekdays = 0x3E;

    /// <summary>Saturday and Sunday.</summary>
    public const int Weekend = 0x41;

    public static int Bit(DayOfWeek day) => 1 << (int)day;

    public static bool Has(int mask, DayOfWeek day) => (mask & Bit(day)) != 0;

    /// <summary>The mask of <paramref name="days"/>; a word this build does not know is no day.</summary>
    public static int Mask(IEnumerable<string?>? days)
    {
        var mask = 0;
        foreach (var word in days ?? [])
        {
            var index = Array.FindIndex(Words, known => string.Equals(known, word?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                mask |= 1 << index;
            }
        }

        return mask;
    }

    /// <summary>The words of <paramref name="mask"/>, Monday first; null for none, which the file leaves out.</summary>
    public static string[]? Of(int mask)
    {
        var days = Week.Where(day => Has(mask, day)).Select(day => Words[(int)day]).ToArray();
        return days.Length > 0 ? days : null;
    }

    /// <summary>The days a file says, as this build writes them.</summary>
    public static string[]? Normalized(string[]? days) => Of(Mask(days));

    /// <summary>The days of a week that starts on <paramref name="first"/>, in the order a region lists them.</summary>
    public static IEnumerable<DayOfWeek> From(DayOfWeek first) => Enumerable.Range(0, 7).Select(step => (DayOfWeek)(((int)first + step) % 7));
}

/// <summary>What an alarm is set to: its time, its days (a mask; none rings once) and whether it is on.</summary>
public readonly record struct AlarmPlan(int Minutes, int Days, bool On)
{
    public static AlarmPlan Of(IAlarm? alarm) =>
        alarm is null ? new AlarmPlan(AlarmTimes.Default, 0, false) : new AlarmPlan(AlarmTimes.Minutes(alarm.Time), AlarmDays.Mask(alarm.Days), alarm.On);

    /// <summary>It rings on its days, every week; with none it rings once.</summary>
    public bool Repeats => Days != 0;
}

/// <summary>[S-4622] When an alarm rings next.</summary>
public static class AlarmSchedule
{
    /// <summary>
    /// The first moment after <paramref name="after"/> (UTC) that <paramref name="plan"/> rings at in
    /// <paramref name="zone"/>, as UTC: its time on the first day after that it may ring on. Whether it
    /// is on is not asked.
    /// </summary>
    public static DateTime Next(AlarmPlan plan, DateTime after, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);
        var utc = DateTime.SpecifyKind(after, DateTimeKind.Utc);
        var today = TimeZoneInfo.ConvertTimeFromUtc(utc, zone).Date;

        // From the day before, for a zone whose change of offset puts yesterday's time after now; a
        // week and two days covers every day of every week.
        for (var step = -1; step <= 8; step++)
        {
            var day = today.AddDays(step);
            if (plan.Repeats && !AlarmDays.Has(plan.Days, day.DayOfWeek))
            {
                continue;
            }

            var at = At(day, plan.Minutes, zone);
            if (at > utc)
            {
                return at;
            }
        }

        // Not reached: some day of the next eight is one of its days and after now.
        return utc.AddDays(1);
    }

    /// <summary>
    /// <paramref name="minutes"/> after midnight on <paramref name="day"/>'s wall clock in
    /// <paramref name="zone"/>, as UTC: in the gap the change to summer time makes, the first minute
    /// after it; said twice by the change back, the first time.
    /// </summary>
    public static DateTime At(DateTime day, int minutes, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);
        var wall = DateTime.SpecifyKind(day.Date.AddMinutes(minutes), DateTimeKind.Unspecified);

        for (var guard = 0; zone.IsInvalidTime(wall) && guard < AlarmTimes.Day; guard++)
        {
            wall = wall.AddMinutes(1);
        }

        if (zone.IsAmbiguousTime(wall))
        {
            // The larger offset is summer time's, which comes first.
            var first = zone.GetAmbiguousTimeOffsets(wall).Max();
            return DateTime.SpecifyKind(wall - first, DateTimeKind.Utc);
        }

        return TimeZoneInfo.ConvertTimeToUtc(wall, zone);
    }
}

public enum AlarmPhase
{
    /// <summary>Switched off.</summary>
    Off,

    /// <summary>On, waiting for its time.</summary>
    Set,

    /// <summary>Its time came: it rings until dismissed, snoozed or it has rung for <see cref="AlarmBook.RingFor"/>.</summary>
    Ringing,

    /// <summary>Snoozed: it rings again at <see cref="AlarmStatus.Until"/>.</summary>
    Snoozed,
}

/// <summary>What an alarm is doing: its phase, when it rings next while on (UTC), and until when it is snoozed (UTC).</summary>
public readonly record struct AlarmStatus(AlarmPhase Phase, DateTime? Next = null, DateTime? Until = null);

/// <summary>One alarm the book is handed: its key (its widget's id) and the widget's alarm itself.</summary>
public readonly record struct AlarmSlot(string Key, IAlarm Alarm);

/// <summary>
/// [S-4622]..[S-4625] The alarm of every alarm widget, by key: what each is doing and when the next
/// thing happens to any of them. Handed the entries (<see cref="Sync"/>) and the time on every call,
/// so every rule is tested without a clock (after Karpo's <c>AlarmBook</c>, for many alarms). One that
/// does not repeat turns itself off in its config when its ring is over (<see cref="Switched"/>, which
/// the service writes). One thread only.
/// <list type="bullet">
/// <item>Set at a time already past, it is tomorrow's: switching on, starting or a new time never rings for a moment before.</item>
/// <item>Overdue by at most <see cref="Grace"/> (asleep through it, the clock set forward) it rings once when seen; later it is missed.</item>
/// <item>Each ring or miss is remembered and the next is reckoned after it, so the clock set back never rings it twice.</item>
/// </list>
/// </summary>
public sealed class AlarmBook
{
    /// <summary>How late a ring may still be: overdue by more (a PC asleep through it) it is missed.</summary>
    public static readonly TimeSpan Grace = TimeSpan.FromMinutes(5);

    /// <summary>How long it rings when nobody answers.</summary>
    public static readonly TimeSpan RingFor = TimeSpan.FromMinutes(5);

    private sealed class Entry(AlarmSlot slot, DateTime armed)
    {
        public AlarmSlot Slot = slot;
        public AlarmPlan Plan = slot.Alarm.Plan;

        /// <summary>When it was set as it is now: nothing before it rings.</summary>
        public DateTime Armed = armed;

        /// <summary>The ring it last had, or missed: the next is after it.</summary>
        public DateTime? Done;

        public DateTime? Ringing;
        public DateTime? Snoozed;
    }

    private readonly Dictionary<string, Entry> _set = new(StringComparer.Ordinal);
    private readonly Func<TimeZoneInfo> _zone;

    /// <param name="zone">The zone its wall clock is in; Windows' own when null.</param>
    public AlarmBook(Func<TimeZoneInfo>? zone = null)
    {
        _zone = zone ?? (() => TimeZoneInfo.Local);
    }

    /// <summary>An alarm has just begun to ring (its state already the new one): its key.</summary>
    public event Action<string>? Rang;

    /// <summary>An alarm stopped ringing — dismissed, snoozed, run out, switched off or gone: its key.</summary>
    public event Action<string>? Silenced;

    /// <summary>An alarm was switched off here (a ring that does not repeat is over): its key. The service writes the file.</summary>
    public event Action<string>? Switched;

    public TimeZoneInfo Zone => _zone();

    /// <summary>Whether any of them rings now.</summary>
    public bool AnyRinging => _set.Values.Any(entry => entry.Ringing is not null);

    /// <summary>The keys ringing now.</summary>
    public IReadOnlyList<string> RingingKeys => [.. _set.Where(pair => pair.Value.Ringing is not null).Select(pair => pair.Key)];

    public IReadOnlyCollection<string> Keys => _set.Keys;

    /// <summary>The alarm the book holds for <paramref name="key"/>, or null.</summary>
    public AlarmSlot? Slot(string key) => _set.TryGetValue(key, out var entry) ? entry.Slot : null;

    /// <summary>
    /// The alarms there are now, at <paramref name="utc"/>: one new, or set to another time, other days
    /// or switched, is set from now; one that is gone goes (and stops ringing).
    /// </summary>
    public void Sync(IEnumerable<AlarmSlot> alarms, DateTime utc)
    {
        ArgumentNullException.ThrowIfNull(alarms);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var slot in alarms)
        {
            if (!seen.Add(slot.Key))
            {
                continue;
            }

            var plan = slot.Alarm.Plan;
            if (_set.TryGetValue(slot.Key, out var entry) && entry.Plan == plan)
            {
                entry.Slot = slot;
                continue;
            }

            var wasRinging = entry is { Ringing: not null };
            _set[slot.Key] = new Entry(slot, utc);

            if (wasRinging)
            {
                Silenced?.Invoke(slot.Key);
            }
        }

        foreach (var key in _set.Keys.Where(key => !seen.Contains(key)).ToList())
        {
            var wasRinging = _set[key].Ringing is not null;
            _set.Remove(key);

            if (wasRinging)
            {
                Silenced?.Invoke(key);
            }
        }
    }

    /// <summary>What <paramref name="key"/> is doing at <paramref name="utc"/>; Off for a key the book does not hold.</summary>
    public AlarmStatus Of(string key, DateTime utc)
    {
        if (!_set.TryGetValue(key, out var entry) || !entry.Plan.On)
        {
            return new AlarmStatus(AlarmPhase.Off);
        }

        var next = Next(entry, utc);
        return entry.Ringing is not null ? new AlarmStatus(AlarmPhase.Ringing, next)
            : entry.Snoozed is { } until ? new AlarmStatus(AlarmPhase.Snoozed, next, until)
            : new AlarmStatus(AlarmPhase.Set, next);
    }

    /// <summary>
    /// The time passed: each alarm at <paramref name="utc"/>. One whose time came rings
    /// (<see cref="Rang"/>), once; one that has rung long enough stops; a snooze over rings again.
    /// True when anything changed.
    /// </summary>
    public bool Tick(DateTime utc)
    {
        var changed = false;
        List<string>? rang = null;

        foreach (var (key, entry) in _set.ToList())
        {
            if (!entry.Plan.On)
            {
                continue;
            }

            // Windows' clock set back before it was set: set from there, so its time still comes.
            // Not after a ring, which is never rung again.
            if (entry.Done is null && entry.Armed > utc)
            {
                entry.Armed = utc;
            }

            if (entry.Ringing is { } since)
            {
                if (utc - since >= RingFor || utc < since)
                {
                    End(key, entry);
                    changed = true;
                }

                continue;
            }

            if (entry.Snoozed is { } until)
            {
                if (utc < until)
                {
                    continue;
                }

                entry.Snoozed = null;
                changed = true;

                if (utc - until <= Grace)
                {
                    entry.Ringing = utc;
                    (rang ??= []).Add(key);
                }
                else
                {
                    End(key, entry);
                }

                continue;
            }

            // Every time that came since the last: the newest rings where it is not too late, the
            // rest are missed. A PC asleep for days steps over each of them here, once.
            DateTime? due = null;
            for (var next = Next(entry, utc); next <= utc; next = Next(entry, utc))
            {
                entry.Done = due = next;
            }

            if (due is not { } came)
            {
                continue;
            }

            changed = true;

            if (utc - came <= Grace)
            {
                entry.Ringing = utc;
                (rang ??= []).Add(key);
            }
            else if (!entry.Plan.Repeats)
            {
                End(key, entry);
            }
        }

        foreach (var key in rang ?? [])
        {
            Rang?.Invoke(key);
        }

        return changed;
    }

    /// <summary>[S-4625] A ringing alarm snoozed: it stops and rings again its widget's snooze from <paramref name="utc"/>.</summary>
    public bool Snooze(string key, DateTime utc)
    {
        if (!_set.TryGetValue(key, out var entry) || entry.Ringing is null)
        {
            return false;
        }

        entry.Ringing = null;
        entry.Snoozed = utc + entry.Slot.Alarm.Snooze;
        Silenced?.Invoke(key);
        return true;
    }

    /// <summary>[S-4625] A ringing or snoozed alarm dismissed: its ring is over, and one that does not repeat is off.</summary>
    public bool Dismiss(string key)
    {
        if (!_set.TryGetValue(key, out var entry) || (entry.Ringing is null && entry.Snoozed is null))
        {
            return false;
        }

        End(key, entry);
        return true;
    }

    /// <summary>
    /// [S-4623] When the next thing happens to any of them, as UTC — a time coming, a snooze over, a
    /// ring that has run long enough — for the one timer to be set to; null while none is on.
    /// </summary>
    public DateTime? Due(DateTime utc)
    {
        DateTime? first = null;

        foreach (var entry in _set.Values.Where(entry => entry.Plan.On))
        {
            var at = entry.Ringing is { } since ? since + RingFor : entry.Snoozed ?? Next(entry, utc);
            first = first is { } earlier && earlier <= at ? earlier : at;
        }

        return first;
    }

    /// <summary>The next ring of <paramref name="entry"/>: after its last, or after it was set — or after now, for a clock set back before that.</summary>
    private DateTime Next(Entry entry, DateTime utc) =>
        AlarmSchedule.Next(entry.Plan, entry.Done ?? (entry.Armed <= utc ? entry.Armed : utc), Zone);

    /// <summary>Its ring is over: one that repeats waits for its next, one that does not turns itself off.</summary>
    private void End(string key, Entry entry)
    {
        var wasRinging = entry.Ringing is not null;
        entry.Ringing = null;
        entry.Snoozed = null;

        if (!entry.Plan.Repeats && entry.Slot.Alarm.On)
        {
            entry.Slot.Alarm.On = false;
            entry.Plan = entry.Slot.Alarm.Plan;
            Switched?.Invoke(key);
        }

        if (wasRinging)
        {
            Silenced?.Invoke(key);
        }
    }
}

/// <summary>[S-4624] What an alarm says beside its time, in the current language.</summary>
public static class AlarmWords
{
    /// <summary>
    /// The days of <paramref name="mask"/>: "Every day", "Weekdays", "Weekends", else each day's short
    /// name in the language's week (from its first day): "Mon, Wed".
    /// </summary>
    public static string Days(Locale locale, int mask) => mask switch
    {
        AlarmDays.Every => Strings.Get(locale, Str.Alarm_EveryDay),
        AlarmDays.Weekdays => Strings.Get(locale, Str.Alarm_Weekdays),
        AlarmDays.Weekend => Strings.Get(locale, Str.Alarm_Weekends),
        _ => string.Join(", ", AlarmDays.From(LocaleFormat.FirstDayOfWeek(locale))
            .Where(day => AlarmDays.Has(mask, day))
            .Select(day => LocaleFormat.WeekdayName(locale, day, NameWidth.Short))),
    };

    /// <summary>
    /// What the widget says of its alarm at <paramref name="utc"/>: "Off"; "Ringing"; "Snoozed until 7:10";
    /// one that repeats, its days; one that does not, when it rings — "Today", "Tomorrow" or the day.
    /// </summary>
    public static string Hint(Locale locale, AlarmStatus status, AlarmPlan plan, DateTime utc, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);

        switch (status.Phase)
        {
            case AlarmPhase.Off:
                return Strings.Get(locale, Str.Alarm_Off);
            case AlarmPhase.Ringing:
                return Strings.Get(locale, Str.Alarm_Ringing);
            case AlarmPhase.Snoozed when status.Until is { } until:
                var wall = TimeZoneInfo.ConvertTimeFromUtc(until, zone);
                return Strings.Format(locale, Str.Alarm_SnoozedUntil, ("time", LocaleFormat.Time(locale, TimeOnly.FromDateTime(wall))));
        }

        if (plan.Repeats)
        {
            return Days(locale, plan.Days);
        }

        if (status.Next is not { } next)
        {
            return string.Empty;
        }

        var today = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone).Date;
        var day = TimeZoneInfo.ConvertTimeFromUtc(next, zone).Date;

        return (day - today).Days switch
        {
            <= 0 => Strings.Get(locale, Str.Common_Today),
            1 => Strings.Get(locale, Str.Common_Tomorrow),
            _ => LocaleFormat.WeekdayName(locale, day.DayOfWeek, NameWidth.Short),
        };
    }

    /// <summary>An alarm's time of day in the language's clock ("7:00 AM", "07:00").</summary>
    public static string Time(Locale locale, AlarmPlan plan) =>
        LocaleFormat.Time(locale, new TimeOnly(plan.Minutes / 60, plan.Minutes % 60));

    /// <summary>Its name, or "Alarm".</summary>
    public static string Name(Locale locale, IAlarm alarm) =>
        string.IsNullOrWhiteSpace(alarm.Label) ? Strings.Get(locale, Str.Alarm_Untitled) : alarm.Label;
}
