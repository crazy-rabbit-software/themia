using System.Globalization;

namespace Themia.Core.Office;

/// <summary>
/// [S-578] Wall clocks to instants for one iCalendar object.
/// <para>
/// <b>Why the calendar's own <c>VTIMEZONE</c> first.</b> The app builds with
/// <c>InvariantGlobalization</c>, which leaves .NET without its IANA-to-Windows table, so a TZID
/// such as <c>America/New_York</c> usually cannot be found on the system ([S-187]). But a CalDAV
/// object carries a <c>VTIMEZONE</c> for every TZID it uses, and that definition — offsets plus the
/// yearly rule for when they change — is all that is needed. So: the calendar's own definition,
/// then the system's zone of that name (Windows ids such as <c>W. Europe Standard Time</c>, which
/// Outlook writes, always resolve), then the local clock — right for the common case of an event in
/// the user's own zone, and never an invented offset. Nothing here throws on a bad zone.
/// </para>
/// </summary>
public sealed class IcalZones
{
    private readonly Dictionary<string, List<Observance>> _defined = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<(string, int), List<(DateTime Onset, TimeSpan Offset)>> _transitions = [];
    private readonly Dictionary<string, TimeZoneInfo?> _system = new(StringComparer.OrdinalIgnoreCase);
    private readonly Func<string, TimeZoneInfo?> _findSystem;

    public IcalZones(IEnumerable<IcalComponent> vtimezones, TimeZoneInfo local, Func<string, TimeZoneInfo?>? findSystem = null)
    {
        Local = local;
        _findSystem = findSystem ?? FindSystemZone;

        foreach (var zone in vtimezones)
        {
            if (zone.First("TZID")?.Value.Trim() is not { Length: > 0 } id)
            {
                continue;
            }

            var observances = new List<Observance>();
            foreach (var part in zone.Children.Where(c => c.Name is "STANDARD" or "DAYLIGHT"))
            {
                if (IcalTime.From(part.First("DTSTART")) is not { } onset
                    || ParseOffset(part.First("TZOFFSETTO")?.Value) is not { } to)
                {
                    continue;
                }

                var from = ParseOffset(part.First("TZOFFSETFROM")?.Value) ?? to;
                var rule = RecurrenceRule.Parse(part.First("RRULE")?.Value);
                var rdates = part.All("RDATE")
                    .SelectMany(p => p.Value.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(v => IcalTime.Parse(v)))
                    .Where(t => t is not null)
                    .Select(t => t!.Value.Clock)
                    .ToList();

                observances.Add(new Observance(onset.Clock, from, to, rule is { Supported: true } ? rule : null, rdates));
            }

            if (observances.Count > 0)
            {
                _defined[id] = observances;
            }
        }
    }

    /// <summary>The zone floating times and all-day dates are read in.</summary>
    public TimeZoneInfo Local { get; }

    /// <summary>The instant a time names. Dates and floating times are read on the local clock.</summary>
    public DateTimeOffset ToInstant(IcalTime time) => time.Kind switch
    {
        IcalTimeKind.Utc => new DateTimeOffset(DateTime.SpecifyKind(time.Clock, DateTimeKind.Unspecified), TimeSpan.Zero),
        IcalTimeKind.Zoned => new DateTimeOffset(Unspecified(time.Clock), OffsetOf(time.Tzid!, time.Clock)),
        _ => new DateTimeOffset(Unspecified(time.Clock), Local.GetUtcOffset(Unspecified(time.Clock))),
    };

    /// <summary>
    /// The UTC offset of a zone at a wall clock. A wall clock inside a spring-forward gap or a
    /// fall-back overlap gets the offset in force before the change: deterministic, and the
    /// earlier of the two instants for an overlap.
    /// </summary>
    public TimeSpan OffsetOf(string tzid, DateTime clock)
    {
        if (_defined.TryGetValue(tzid, out var observances))
        {
            return DefinedOffset(tzid, observances, clock);
        }

        if (!_system.TryGetValue(tzid, out var zone))
        {
            zone = _findSystem(tzid);
            _system[tzid] = zone;
        }

        return (zone ?? Local).GetUtcOffset(Unspecified(clock));
    }

    private TimeSpan DefinedOffset(string tzid, List<Observance> observances, DateTime clock)
    {
        var year = clock.Year;
        if (!_transitions.TryGetValue((tzid, year), out var transitions))
        {
            transitions = [];
            var horizon = new DateTime(Math.Min(year, 9998), 12, 31, 23, 59, 59);
            var floor = new DateTime(Math.Max(1, year - 1), 1, 1);

            foreach (var o in observances)
            {
                foreach (var onset in OnsetsUpTo(o, horizon))
                {
                    if (onset >= floor)
                    {
                        transitions.Add((onset, o.To));
                    }
                }
            }

            // Before the first transition there is still an offset: the oldest observance's "from".
            var oldest = observances.OrderBy(o => o.Onset).First();
            transitions.Add((DateTime.MinValue, transitions.Count == 0 ? oldest.To : oldest.From));
            transitions.Sort((a, b) => a.Onset.CompareTo(b.Onset));
            _transitions[(tzid, year)] = transitions;
        }

        var offset = transitions[0].Offset;
        foreach (var (onset, to) in transitions)
        {
            if (onset > clock)
            {
                break;
            }

            offset = to;
        }

        return offset;
    }

    private static IEnumerable<DateTime> OnsetsUpTo(Observance o, DateTime horizon)
    {
        if (o.Onset <= horizon)
        {
            yield return o.Onset;
        }

        foreach (var r in o.RDates.Where(r => r <= horizon))
        {
            yield return r;
        }

        if (o.Rule is not { } rule)
        {
            yield break;
        }

        foreach (var occurrence in rule.Occurrences(o.Onset).Skip(1))
        {
            if (occurrence > horizon || (rule.Until is { } until && occurrence > until.Clock.Add(o.From)))
            {
                yield break;
            }

            yield return occurrence;
        }
    }

    /// <summary><c>+0100</c>, <c>-0500</c>, <c>+053000</c> as an offset, or null.</summary>
    public static TimeSpan? ParseOffset(string? value)
    {
        var s = value?.Trim();
        if (s is not ({ Length: 5 } or { Length: 7 }) || s[0] is not ('+' or '-'))
        {
            return null;
        }

        if (!int.TryParse(s.AsSpan(1, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var h)
            || !int.TryParse(s.AsSpan(3, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var m)
            || h > 14 || m > 59)
        {
            return null;
        }

        var sec = 0;
        if (s.Length == 7 && (!int.TryParse(s.AsSpan(5, 2), NumberStyles.None, CultureInfo.InvariantCulture, out sec) || sec > 59))
        {
            return null;
        }

        var span = new TimeSpan(h, m, sec);
        return s[0] == '-' ? -span : span;
    }

    /// <summary>
    /// The system's zone with this id, or null. Windows ids always resolve; IANA ids only when the
    /// runtime has a table for them, which an invariant-globalization build usually does not.
    /// </summary>
    public static TimeZoneInfo? FindSystemZone(string tzid)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(tzid);
        }
        catch (TimeZoneNotFoundException)
        {
            return null;
        }
        catch (InvalidTimeZoneException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static DateTime Unspecified(DateTime clock) => DateTime.SpecifyKind(clock, DateTimeKind.Unspecified);

    private sealed record Observance(DateTime Onset, TimeSpan From, TimeSpan To, RecurrenceRule? Rule, IReadOnlyList<DateTime> RDates);
}

/// <summary>
/// [S-576] The events in CalDAV calendar data, as the widget draws them, for one window of time.
/// <para>
/// Ported from the classic app's <c>parse_ics_events</c> with what it left out: zoned times resolve
/// through the object's own <c>VTIMEZONE</c> ([S-578]); a recurring master the server did not
/// expand is expanded here ([S-577]) with its <c>EXDATE</c>s and moved or cancelled instances
/// (<c>RECURRENCE-ID</c>); cancelled events are not listed. A server that did expand sends each
/// instance with a <c>RECURRENCE-ID</c> and no master, and each is then simply an event.
/// </para>
/// </summary>
public static class IcsEvents
{
    /// <summary>A ceiling on instances from one recurring event, whatever its rule says.</summary>
    public const int MaxInstances = 1000;

    /// <summary>
    /// Every event, or instance of one, that overlaps [<paramref name="from"/>, <paramref name="to"/>).
    /// </summary>
    /// <param name="ics">One or more iCalendar objects, or bare VEVENTs.</param>
    /// <param name="local">The zone for floating times and all-day dates.</param>
    /// <param name="findSystemZone">Looks up a TZID the data does not define; the system by default.</param>
    public static IReadOnlyList<CalendarEvent> Read(
        string? ics,
        DateTimeOffset from,
        DateTimeOffset to,
        TimeZoneInfo local,
        Func<string, TimeZoneInfo?>? findSystemZone = null)
    {
        var roots = Ical.Parse(ics);
        var events = new List<CalendarEvent>();

        // Each VCALENDAR is a scope for its VTIMEZONEs; bare VEVENTs share one with none.
        var calendars = roots.Where(r => r.Name == "VCALENDAR").ToList();
        var bare = roots.Where(r => r.Name == "VEVENT").ToList();

        foreach (var calendar in calendars)
        {
            var zones = new IcalZones(calendar.Children.Where(c => c.Name == "VTIMEZONE"), local, findSystemZone);
            ReadScope(Ical.Descendants(calendar.Children, "VEVENT"), zones, from, to, events);
        }

        if (bare.Count > 0)
        {
            var zones = new IcalZones(roots.Where(r => r.Name == "VTIMEZONE"), local, findSystemZone);
            ReadScope(bare, zones, from, to, events);
        }

        return events;
    }

    private static void ReadScope(
        IEnumerable<IcalComponent> vevents,
        IcalZones zones,
        DateTimeOffset from,
        DateTimeOffset to,
        List<CalendarEvent> into)
    {
        var raws = new List<RawEvent>();
        foreach (var vevent in vevents)
        {
            if (RawEvent.From(vevent, raws.Count) is { } raw)
            {
                raws.Add(raw);
            }
        }

        foreach (var series in raws.GroupBy(r => r.Uid, StringComparer.Ordinal))
        {
            var master = series.FirstOrDefault(r => r.RecurrenceId is null);
            var overrides = new Dictionary<DateTimeOffset, RawEvent>();

            foreach (var instance in series.Where(r => r.RecurrenceId is not null))
            {
                overrides[zones.ToInstant(instance.RecurrenceId!.Value)] = instance;
            }

            if (master is not null)
            {
                if (master.Cancelled)
                {
                    continue;
                }

                if (master.Rule is { Supported: true } rule)
                {
                    Expand(master, rule, overrides, zones, from, to, into);
                }
                else
                {
                    Emit(master, master.Start.Clock, zones, from, to, into, single: true);
                }
            }

            // Instances with no master here (the server expanded), and moved instances whose
            // original date was not reached.
            foreach (var instance in overrides.Values.Where(o => !o.Cancelled))
            {
                Emit(instance, instance.Start.Clock, zones, from, to, into, single: master is null && series.Count() == 1);
            }
        }
    }

    private static void Expand(
        RawEvent master,
        RecurrenceRule rule,
        Dictionary<DateTimeOffset, RawEvent> overrides,
        IcalZones zones,
        DateTimeOffset from,
        DateTimeOffset to,
        List<CalendarEvent> into)
    {
        var excluded = master.ExDates.Select(zones.ToInstant).ToHashSet();
        var emitted = 0;

        // One occurrence: skipped when excluded, replaced when moved or cancelled, else the
        // master's. False once the instance ceiling is reached.
        bool Take(DateTime clock, DateTimeOffset at)
        {
            if (excluded.Contains(at))
            {
                return true;
            }

            if (overrides.Remove(at, out var moved))
            {
                if (!moved.Cancelled)
                {
                    Emit(moved, moved.Start.Clock, zones, from, to, into, single: false);
                }

                return true;
            }

            return !Emit(master, clock, zones, from, to, into, single: false) || ++emitted < MaxInstances;
        }

        // The rule's occurrences come in order, so the first past UNTIL or past the window ends it.
        foreach (var clock in rule.Occurrences(master.Start.Clock))
        {
            var at = zones.ToInstant(master.Start with { Clock = clock });

            if (rule.Until is { } until
                && (until.IsDate ? DateOnly.FromDateTime(clock) > DateOnly.FromDateTime(until.Clock) : at > zones.ToInstant(until)))
            {
                break;
            }

            if (at >= to || !Take(clock, at))
            {
                break;
            }
        }

        // RDATEs add instances the rule does not produce; UNTIL does not bind them.
        foreach (var extra in master.RDates)
        {
            var at = zones.ToInstant(master.Start with { Clock = extra.Clock });

            if (at < to && extra.Clock != master.Start.Clock && !Take(extra.Clock, at))
            {
                break;
            }
        }
    }

    /// <summary>Add one occurrence if it overlaps the window. True when it was added.</summary>
    private static bool Emit(
        RawEvent raw,
        DateTime clock,
        IcalZones zones,
        DateTimeOffset from,
        DateTimeOffset to,
        List<CalendarEvent> into,
        bool single)
    {
        var id = single ? raw.Uid : $"{raw.Uid}#{clock.ToString("yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture)}";
        CalendarEvent item;

        if (raw.Start.IsDate)
        {
            var first = DateOnly.FromDateTime(clock);
            var days = raw.LengthInDays();
            item = CalendarEvent.AllDayEvent(id, raw.Subject, first, first.AddDays(days));

            var startsAt = CalendarRules.LocalMidnight(first, zones.Local);
            var endsAt = CalendarRules.LocalMidnight(first.AddDays(days), zones.Local);
            if (!(startsAt < to && endsAt > from))
            {
                return false;
            }
        }
        else
        {
            var start = zones.ToInstant(raw.Start with { Clock = clock });
            var end = start + raw.Length(zones);

            var overlaps = end > start ? start < to && end > from : start >= from && start < to;
            if (!overlaps)
            {
                return false;
            }

            item = new CalendarEvent { Id = id, Subject = raw.Subject, Start = start, End = end };
        }

        into.Add(item with { Location = raw.Location, Organizer = raw.Organizer, JoinUrl = raw.JoinUrl });
        return true;
    }

    /// <summary>One VEVENT as written, before it is placed in time.</summary>
    private sealed record RawEvent(
        string Uid,
        string Subject,
        IcalTime Start,
        IcalTime? End,
        TimeSpan? Duration,
        string? Location,
        string? Organizer,
        string? JoinUrl,
        bool Cancelled,
        RecurrenceRule? Rule,
        IReadOnlyList<IcalTime> ExDates,
        IReadOnlyList<IcalTime> RDates,
        IcalTime? RecurrenceId)
    {
        public static RawEvent? From(IcalComponent vevent, int ordinal)
        {
            if (IcalTime.From(vevent.First("DTSTART")) is not { } start)
            {
                return null;
            }

            var end = IcalTime.From(vevent.First("DTEND"));

            // [S-3612] Outlook and Exchange write an all-day event as zoned midnights with a flag
            // beside them. It is a date all the same: read as one, never through the zone.
            if (!start.IsDate && IsFlaggedAllDay(vevent, start, end))
            {
                start = new IcalTime(start.Clock.Date, IcalTimeKind.Date, null);
                end = end is { } e ? new IcalTime(e.Clock.Date, IcalTimeKind.Date, null) : null;
            }

            return new RawEvent(
                vevent.First("UID")?.Value.Trim() is { Length: > 0 } uid ? uid : $"caldav:{ordinal}",

                // [S-3612] No SUMMARY is an untitled event: the widget words that in the user's language.
                vevent.Text("SUMMARY") ?? string.Empty,
                start,
                end,
                Ical.ParseDuration(vevent.First("DURATION")?.Value),
                vevent.Text("LOCATION"),
                OrganizerOf(vevent.First("ORGANIZER")),

                // [S-4822] Exchange and Google write the meeting's link in their own properties;
                // failing those, a meeting service's link in the location or the description.
                MeetingLinks.Find(
                    [vevent.Text("X-MICROSOFT-SKYPETEAMSMEETINGURL"), vevent.Text("X-GOOGLE-CONFERENCE"), vevent.Text("X-MICROSOFT-ONLINEMEETINGCONFLINK")],
                    vevent.Text("LOCATION"),
                    vevent.Text("DESCRIPTION")),
                string.Equals(vevent.First("STATUS")?.Value.Trim(), "CANCELLED", StringComparison.OrdinalIgnoreCase),
                RecurrenceRule.Parse(vevent.First("RRULE")?.Value),
                Times(vevent, "EXDATE"),
                Times(vevent, "RDATE"),
                IcalTime.From(vevent.First("RECURRENCE-ID")));
        }

        /// <summary>
        /// <c>X-MICROSOFT-CDO-ALLDAYEVENT:TRUE</c> (or <c>X-MICROSOFT-MSNCALENDAR-ALLDAYEVENT</c>) on
        /// wall-clock midnights — never on UTC ones, whose date depends on the zone they were made in.
        /// </summary>
        private static bool IsFlaggedAllDay(IcalComponent vevent, IcalTime start, IcalTime? end)
        {
            var flagged = new[] { "X-MICROSOFT-CDO-ALLDAYEVENT", "X-MICROSOFT-MSNCALENDAR-ALLDAYEVENT" }
                .Any(name => string.Equals(vevent.First(name)?.Value.Trim(), "TRUE", StringComparison.OrdinalIgnoreCase));

            return flagged
                && start.Kind != IcalTimeKind.Utc && start.Clock.TimeOfDay == TimeSpan.Zero
                && (end is not { } e || (e.Kind != IcalTimeKind.Utc && e.Clock.TimeOfDay == TimeSpan.Zero));
        }

        /// <summary>A timed event's length: DTEND − DTSTART as instants, else DURATION, else zero.</summary>

        public TimeSpan Length(IcalZones zones)
        {
            if (End is { IsDate: false } end)
            {
                var span = zones.ToInstant(end) - zones.ToInstant(Start);
                return span > TimeSpan.Zero ? span : TimeSpan.Zero;
            }

            return Duration is { } d && d > TimeSpan.Zero ? d : TimeSpan.Zero;
        }

        /// <summary>An all-day event's length in days: at least one (RFC 5545's default).</summary>
        public int LengthInDays()
        {
            if (End is { } end)
            {
                var days = (DateOnly.FromDateTime(end.Clock).DayNumber - DateOnly.FromDateTime(Start.Clock).DayNumber);
                return Math.Clamp(days, 1, 3660);
            }

            return Duration is { } d ? Math.Clamp((int)Math.Round(d.TotalDays), 1, 3660) : 1;
        }

        private static IReadOnlyList<IcalTime> Times(IcalComponent vevent, string name)
        {
            var times = new List<IcalTime>();
            foreach (var p in vevent.All(name))
            {
                var isDate = string.Equals(p.Parameter("VALUE"), "DATE", StringComparison.OrdinalIgnoreCase);
                foreach (var v in p.Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (IcalTime.Parse(isDate && v.Length >= 8 ? v[..8] : v, p.Parameter("TZID")) is { } t)
                    {
                        times.Add(t);
                    }
                }
            }

            return times;
        }

        /// <summary>
        /// The organizer's CN when there is one, else the address after <c>mailto:</c>, else the
        /// value as written. Non-ASCII names come through whole (the classic parser once split one
        /// mid-character).
        /// </summary>
        private static string? OrganizerOf(IcalProperty? organizer)
        {
            if (organizer is null)
            {
                return null;
            }

            if (organizer.Parameter("CN") is { } cn && Ical.UnescapeText(cn).Trim() is { Length: > 0 } name)
            {
                return name;
            }

            var value = organizer.Value.Trim();
            return value.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ? value[7..].Trim() : value.Length > 0 ? value : null;
        }
    }
}
