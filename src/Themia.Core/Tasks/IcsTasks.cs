using System.Globalization;
using Themia.Core.Office;

namespace Themia.Core.Tasks;

/// <summary>
/// [S-619] [S-620] CalDAV VTODOs (RFC 5545 §3.6.2) reduced to the one task shape, as jtx Board /
/// DAVx⁵, Nextcloud Tasks and iCloud Reminders write them.
/// <para>
/// Lenient like <see cref="IcsEvents"/>: whatever a server sends is read through <see cref="Ical"/>,
/// and one bad task never costs the rest. A VTODO without a UID has no id that survives a refresh, so
/// it is skipped rather than given a position that would move under the user; a UID seen twice keeps
/// the first.
/// </para>
/// <para>
/// The model is flat and has no categories: RELATED-TO (subtasks) and CATEGORIES are not read, a
/// subtask is a row like any other. Pure, with the viewer's zone and "today" passed in.
/// </para>
/// </summary>
public static class IcsTasks
{
    /// <summary>How far a recurring task's rule is walked looking for its current instance.</summary>
    public const int MaxInstances = 1000;

    /// <param name="ics">One or more iCalendar objects (a REPORT's calendar-data), or bare VTODOs.</param>
    /// <param name="listId">The collection the tasks are in.</param>
    /// <param name="today">The viewer's day, which picks a recurring task's current instance ([S-620]).</param>
    /// <param name="local">The zone floating times are read in.</param>
    /// <param name="findSystemZone">Looks up a TZID the data does not define; the system by default.</param>
    public static IReadOnlyList<TaskItem> Read(
        string? ics,
        string listId,
        DateOnly today,
        TimeZoneInfo local,
        Func<string, TimeZoneInfo?>? findSystemZone = null)
    {
        ArgumentNullException.ThrowIfNull(listId);
        ArgumentNullException.ThrowIfNull(local);

        var roots = Ical.Parse(ics);
        var tasks = new List<TaskItem>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        void Scope(IEnumerable<IcalComponent> vtodos, IcalZones zones)
        {
            foreach (var series in vtodos.Select(RawTodo.From).OfType<RawTodo>().GroupBy(t => t.Uid, StringComparer.Ordinal))
            {
                if (!seen.Add(series.Key) || Current(series.ToList(), zones, today) is not { } task)
                {
                    continue;
                }

                tasks.Add(task.ToItem(listId, zones));
            }
        }

        foreach (var calendar in roots.Where(r => r.Name == "VCALENDAR"))
        {
            Scope(Ical.Descendants(calendar.Children, "VTODO"), new IcalZones(calendar.Children.Where(c => c.Name == "VTIMEZONE"), local, findSystemZone));
        }

        if (roots.Where(r => r.Name == "VTODO").ToList() is { Count: > 0 } bare)
        {
            Scope(bare, new IcalZones(roots.Where(r => r.Name == "VTIMEZONE"), local, findSystemZone));
        }

        return tasks;
    }

    /// <summary>
    /// [S-620] The one row a task's series shows: a plain task as itself; a recurring one as its
    /// current instance — the first occurrence due today or later that an override has not finished
    /// (earlier occurrences count as done: no server says otherwise for them), its override's fields
    /// when it has one; once the rule has run out, its last occurrence. A cancelled task or instance
    /// is no row ([S-619]). Null when nothing is left to show.
    /// </summary>
    private static RawTodo? Current(IReadOnlyList<RawTodo> series, IcalZones zones, DateOnly today)
    {
        var master = series.FirstOrDefault(t => t.RecurrenceId is null);

        if (master is null)
        {
            // Only instances came (the server expanded): the earliest open one, else the last.
            var instances = series.Where(t => !t.Cancelled).OrderBy(t => t.Anchor is { } a ? zones.ToInstant(a) : DateTimeOffset.MaxValue).ToList();
            return instances.FirstOrDefault(t => !t.Completed) ?? instances.LastOrDefault();
        }

        if (master.Cancelled)
        {
            return null;
        }

        if (master.Rule is not { Supported: true } rule || master.Anchor is not { } anchor || master.Completed)
        {
            return master;
        }

        var overrides = new Dictionary<DateTimeOffset, RawTodo>();
        foreach (var instance in series.Where(t => t.RecurrenceId is not null))
        {
            overrides[zones.ToInstant(instance.RecurrenceId!.Value)] = instance;
        }

        var excluded = master.ExDates.Select(zones.ToInstant).ToHashSet();
        RawTodo? last = null;
        var walked = 0;

        foreach (var clock in rule.Occurrences(anchor.Clock))
        {
            if (++walked > MaxInstances)
            {
                break;
            }

            var time = anchor with { Clock = clock };
            var at = zones.ToInstant(time);

            if (rule.Until is { } until
                && (until.IsDate ? DateOnly.FromDateTime(clock) > DateOnly.FromDateTime(until.Clock) : at > zones.ToInstant(until)))
            {
                break;
            }

            if (excluded.Contains(at))
            {
                continue;
            }

            var occurrence = overrides.TryGetValue(at, out var moved) ? moved : master.At(time);
            if (occurrence.Cancelled)
            {
                continue;
            }

            last = occurrence;
            if (!occurrence.Completed && occurrence.Anchor is { } due && DayOf(due, zones) >= today)
            {
                return occurrence;
            }
        }

        return last ?? master;
    }

    private static DateOnly DayOf(IcalTime time, IcalZones zones)
        => time.IsDate ? DateOnly.FromDateTime(time.Clock) : DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(zones.ToInstant(time), zones.Local).DateTime);

    /// <summary>
    /// [S-619] A DUE as the task model's due string ([S-593]): a DATE, or a wall-clock midnight
    /// (floating or zoned: how jtx Board and Nextcloud write a day in some versions), is the day for
    /// every viewer; any other time is the instant it names in its zone, in UTC.
    /// </summary>
    internal static string? DueRaw(IcalTime? due, IcalZones zones)
    {
        if (due is not { } time)
        {
            return null;
        }

        if (time.IsDate || (time.Kind != IcalTimeKind.Utc && time.Clock.TimeOfDay == TimeSpan.Zero))
        {
            return TaskStamp.OfDay(DateOnly.FromDateTime(time.Clock)).Wire();
        }

        return TaskStamp.At(zones.ToInstant(time)).Wire();
    }

    /// <summary>One VTODO as written, before its series is resolved.</summary>
    private sealed record RawTodo(
        string Uid,
        string Title,
        IcalTime? Due,
        IcalTime? Start,
        bool Completed,
        IcalTime? CompletedAt,
        bool Cancelled,
        bool Important,
        RecurrenceRule? Rule,
        IReadOnlyList<IcalTime> ExDates,
        IcalTime? RecurrenceId)
    {
        /// <summary>What the rule steps: DUE when there is one, else DTSTART.</summary>
        public IcalTime? Anchor => Due ?? Start;

        /// <summary>
        /// The master at one of its occurrences: the anchor moved there (DUE keeps its distance from
        /// DTSTART when both are set and the rule steps DUE). Open: the master itself is not finished.
        /// </summary>
        public RawTodo At(IcalTime occurrence) => Due is not null
            ? this with { Due = occurrence, Rule = null, RecurrenceId = occurrence }
            : this with { Start = occurrence, Rule = null, RecurrenceId = occurrence };

        public TaskItem ToItem(string listId, IcalZones zones)
        {
            // The series is one row under its UID, whichever instance is current: a refresh that moves
            // it on to the next occurrence keeps its place in the widget's order ([S-598]).
            return new TaskItem(
                Uid,
                listId,
                Title,
                Completed,
                Important,
                DueRaw(Due, zones),
                Completed && CompletedAt is { } done ? TaskStamp.At(zones.ToInstant(done)) : null);
        }

        public static RawTodo? From(IcalComponent vtodo)
        {
            if (vtodo.First("UID")?.Value.Trim() is not { Length: > 0 } uid)
            {
                return null;
            }

            var status = vtodo.First("STATUS")?.Value.Trim();
            var completedAt = IcalTime.From(vtodo.First("COMPLETED"));
            var percent = int.TryParse(vtodo.First("PERCENT-COMPLETE")?.Value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var p) ? p : 0;

            // RFC 5545: STATUS says it; Reminders and older clients leave STATUS out and write
            // COMPLETED or 100 %. NEEDS-ACTION and IN-PROCESS are open work.
            var completed = string.Equals(status, "COMPLETED", StringComparison.OrdinalIgnoreCase)
                || (status is null && (completedAt is not null || percent >= 100));

            // PRIORITY 1..4 is "high" (RFC 5545 §3.8.1.9; Reminders' "!!!" is 1, jtx Board's high 1..3,
            // Nextcloud's star 1); 5 medium, 6..9 low and 0 undefined are not important.
            var priority = int.TryParse(vtodo.First("PRIORITY")?.Value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var level) ? level : 0;

            return new RawTodo(
                uid,
                vtodo.Text("SUMMARY") ?? string.Empty,
                IcalTime.From(vtodo.First("DUE")),
                IcalTime.From(vtodo.First("DTSTART")),
                completed,
                completedAt,
                string.Equals(status, "CANCELLED", StringComparison.OrdinalIgnoreCase),
                priority is >= 1 and <= 4,
                RecurrenceRule.Parse(vtodo.First("RRULE")?.Value),
                Times(vtodo, "EXDATE"),
                IcalTime.From(vtodo.First("RECURRENCE-ID")));
        }

        private static IReadOnlyList<IcalTime> Times(IcalComponent vtodo, string name)
        {
            var times = new List<IcalTime>();
            foreach (var property in vtodo.All(name))
            {
                foreach (var value in property.Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    var single = property with { Value = value };
                    if (IcalTime.From(single) is { } time)
                    {
                        times.Add(time);
                    }
                }
            }

            return times;
        }
    }
}
