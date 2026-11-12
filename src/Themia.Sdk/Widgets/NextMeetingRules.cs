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

/// <summary>[S-4824] What the Next meeting widget's lines show at one size (the countdown is not one of them: it stands over Join, [S-5710]).</summary>
/// <param name="Title">The title's one line: the first line, always there.</param>
/// <param name="Range">The time range line ("10:00 – 10:30").</param>
/// <param name="Location">The location line, when the event has one.</param>
/// <param name="Organizer">The organiser line, when the event has one.</param>
public readonly record struct NextMeetingPlan(bool Title, bool Range, bool Location, bool Organizer);

/// <summary>
/// [S-4824] The Next meeting ladder on the family's tokens, in the lines' box (the view's area less the
/// card's insets, and less the end column where that stands beside them): the title (Title, one line)
/// first and always, then the time range, the location and the organiser (Body lines with their 16
/// glyphs), 4 apart, over the end column's row where that stands under them (<paramref name="foot"/>).
/// A larger tile shows more of the meeting's own lines, never a larger title (concise first, [S-666]).
/// </summary>
public static class NextMeetingLayout
{
    /// <summary>Between two lines.</summary>
    public const double Gap = FamilySpace.S1;

    /// <param name="box">The lines' box.</param>
    /// <param name="foot">What the end column costs under the lines (its row and the gap over it), 0 beside them.</param>
    /// <param name="hasLocation">The event has a place.</param>
    /// <param name="hasOrganizer">The event has an organiser.</param>
    public static NextMeetingPlan Plan(WidgetArea box, double foot, bool hasLocation, bool hasOrganizer)
    {
        if (!box.IsMeasured)
        {
            return new NextMeetingPlan(true, false, false, false);
        }

        var room = new Disclosure(box.Width, box.Height - Math.Max(0, foot), Gap);
        var line = FamilyType.Body.LineHeight;

        room.Reserve(FamilyType.Title.LineHeight);
        var range = room.Take(line);
        var location = hasLocation && room.Take(line);
        var organizer = hasOrganizer && room.Take(line);
        return new NextMeetingPlan(true, range, location, organizer);
    }
}
