using Themia.Core.Localization;
using Themia.Core.Office;
using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>[S-4821] Where the chosen event stands: still to come, or under way.</summary>
public enum MeetingPhase
{
    Upcoming = 0,
    Running = 1,
}

/// <summary>
/// [S-4821] Which one event the Next meeting widget shows, and how it says when.
/// <para>
/// Candidates are the timed events the user has not declined that have not ended (an all-day event
/// is a label on a day, not a meeting). An event under way is shown until it ends, unless the next
/// one starts within <see cref="LeadIn"/>: then that one is what the user needs, Join and all. Of
/// several under way, the one that began last (the meeting inside a long block). Of several
/// starting together, the one with a join link, then the shorter, then by title, so a redraw never
/// swaps them.
/// </para>
/// </summary>
public static class NextMeetingRules
{
    /// <summary>How soon the next meeting takes over from one still running.</summary>
    public static readonly TimeSpan LeadIn = TimeSpan.FromMinutes(5);

    /// <summary>How far ahead the widget looks: two weeks, from yesterday (an overnight event still running).</summary>
    public const int DaysAhead = 14;

    /// <summary>The local days to ask the calendar for: yesterday to <see cref="DaysAhead"/> days on.</summary>
    public static (DateOnly First, DateOnly Last) Window(DateOnly today) => (today.AddDays(-1), today.AddDays(DaysAhead));

    /// <summary>[S-4821] The event to show at <paramref name="now"/>, or null when nothing is coming.</summary>
    public static CalendarEvent? Pick(IEnumerable<CalendarEvent> events, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(events);

        var candidates = events.Where(e => !e.AllDay && !e.Declined && (e.End > now || (e.End <= e.Start && e.Start > now))).ToList();

        var next = candidates
            .Where(e => e.Start > now)
            .OrderBy(e => e.Start)
            .ThenBy(e => e.JoinUrl is null)
            .ThenBy(e => e.End - e.Start)
            .ThenBy(e => e.Subject, StringComparer.Ordinal)
            .ThenBy(e => e.Id, StringComparer.Ordinal)
            .FirstOrDefault();

        if (next is not null && next.Start - now <= LeadIn)
        {
            return next;
        }

        var running = candidates
            .Where(e => e.Start <= now && e.End > now)
            .OrderByDescending(e => e.Start)
            .ThenBy(e => e.End)
            .ThenBy(e => e.JoinUrl is null)
            .ThenBy(e => e.Subject, StringComparer.Ordinal)
            .ThenBy(e => e.Id, StringComparer.Ordinal)
            .FirstOrDefault();

        return running ?? next;
    }

    /// <summary>Whether <paramref name="item"/> is under way at <paramref name="now"/>.</summary>
    public static MeetingPhase Phase(CalendarEvent item, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.Start <= now && item.End > now ? MeetingPhase.Running : MeetingPhase.Upcoming;
    }

    /// <summary>
    /// [S-4823] When, in words: "Now · ends 10:30" under way; "in 12 min" within the hour; else the
    /// day ("Today", "Tomorrow", "Mon, Oct 5") and the time. Times on the regional clock ([S-3773]).
    /// </summary>
    public static string When(Locale locale, CalendarEvent item, DateTimeOffset now, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(zone);

        var start = TimeZoneInfo.ConvertTime(item.Start, zone).DateTime;
        var end = TimeZoneInfo.ConvertTime(item.End, zone).DateTime;
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);

        if (Phase(item, now) == MeetingPhase.Running)
        {
            var endText = DateOnly.FromDateTime(end) == today
                ? LocaleFormat.Time(locale, end)
                : $"{Formatting.DayHeading(locale, DateOnly.FromDateTime(end), today)} {LocaleFormat.Time(locale, end)}";
            return Strings.Format(locale, Str.Meeting_NowEnds, ("time", endText));
        }

        var until = item.Start - now;
        if (until < TimeSpan.FromHours(1))
        {
            return LocaleFormat.Relative(locale, until, RelativeStyle.Short);
        }

        var day = Formatting.DayHeading(locale, DateOnly.FromDateTime(start), today);
        return Strings.Format(locale, Str.Meeting_DayTime, ("day", day), ("time", LocaleFormat.Time(locale, start)));
    }

    /// <summary>
    /// [S-2160] The meetings after <paramref name="shown"/> at <paramref name="now"/>, in order: the timed
    /// events not declined that have not started yet and do not start before it (an event under way is no
    /// meeting after this one), today's first as time runs, then the next days', ordered as
    /// <see cref="Pick"/> orders a tie so a redraw never swaps them.
    /// </summary>
    public static IReadOnlyList<CalendarEvent> After(IEnumerable<CalendarEvent> events, CalendarEvent shown, DateTimeOffset now, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(shown);
        ArgumentNullException.ThrowIfNull(zone);
        var key = CalendarShare.Key(shown);

        return [.. events
            .Where(e => !e.AllDay && !e.Declined && e.Start > now && e.Start >= shown.Start && !CalendarShare.Key(e).Equals(key))
            .OrderBy(e => e.Start)
            .ThenBy(e => e.JoinUrl is null)
            .ThenBy(e => e.End - e.Start)
            .ThenBy(e => e.Subject, StringComparer.Ordinal)
            .ThenBy(e => e.Id, StringComparer.Ordinal)];
    }

    /// <summary>
    /// [S-2160] A following meeting's start: on the regional clock ([S-3773]); on another day than today
    /// the day too ("Tomorrow, 9:30 AM"), as the countdown says it.
    /// </summary>
    public static string Start(Locale locale, CalendarEvent item, DateTimeOffset now, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(zone);

        var start = TimeZoneInfo.ConvertTime(item.Start, zone).DateTime;
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
        var day = DateOnly.FromDateTime(start);
        var time = LocaleFormat.Time(locale, start);
        return day == today
            ? time
            : Strings.Format(locale, Str.Meeting_DayTime, ("day", Formatting.DayHeading(locale, day, today)), ("time", time));
    }

    /// <summary>[S-4823] The time range, "10:00 – 10:30", on the regional clock.</summary>
    public static string Range(Locale locale, CalendarEvent item, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(zone);

        var start = LocaleFormat.Time(locale, TimeZoneInfo.ConvertTime(item.Start, zone).DateTime);
        var end = LocaleFormat.Time(locale, TimeZoneInfo.ConvertTime(item.End, zone).DateTime);
        return item.End > item.Start ? $"{start} – {end}" : start;
    }
}
