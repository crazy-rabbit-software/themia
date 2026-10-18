using Themia.Core.Localization;
using Themia.Core.Office;

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

/// <summary>[S-4824] What the Next meeting widget shows at one size.</summary>
/// <param name="When">The when line ("in 12 min.").</param>
/// <param name="Title">How many lines the title takes (0: none, on the smallest tile with Join).</param>
/// <param name="Range">The time range line ("10:00 – 10:30").</param>
/// <param name="Location">The location line, when the event has one.</param>
/// <param name="Organizer">The organiser line, when the event has one.</param>
/// <param name="JoinBeside">Join stands beside the text (a wide strip) rather than under it.</param>
/// <param name="Large">The title in Subtitle and the when line in Body: a tile with height to spare.</param>
public readonly record struct NextMeetingPlan(bool When, int Title, bool Range, bool Location, bool Organizer, bool JoinBeside, bool Large);

/// <summary>
/// [S-4824] The Next meeting ladder, on the area less its inset. Join, when there is a link, is
/// always there (it is the reason for the widget); then the when line; then the title, which is
/// always there without Join; then the title's further lines, the time range, the location and the
/// organiser. From 300 DIPs of height the title is set larger. On a strip Join stands at the right.
/// </summary>
public static class NextMeetingLayout
{
    /// <summary>The Join button's height (a standard Windows button).</summary>
    public const double JoinHeight = 32;

    /// <summary>Join's width on a strip.</summary>
    public const double JoinWidth = 88;

    /// <summary>Between two lines, and above Join.</summary>
    public const double Gap = 4;

    /// <summary>The height from which the title is set in Subtitle.</summary>
    public const double LargeFrom = 300;

    public static TypeStep TitleStep(bool large) => large ? FluentType.Subtitle : FluentType.BodyStrong;

    public static TypeStep WhenStep(bool large) => large ? FluentType.Body : FluentType.Caption;

    public static NextMeetingPlan Plan(WidgetArea area, bool hasJoin, bool hasLocation, bool hasOrganizer)
    {
        if (!area.IsMeasured)
        {
            return new NextMeetingPlan(false, 1, false, false, false, false, false);
        }

        var large = area.Height >= LargeFrom;
        var title = TitleStep(large).LineHeight;
        var beside = hasJoin && area.Shape == AreaShape.Wide && area.Width >= 280;
        var room = new Disclosure(beside ? area.Width - JoinWidth - 8 : area.Width, area.Height, Gap);

        var lines = 0;
        if (hasJoin && !beside)
        {
            room.Reserve(JoinHeight + Gap);
        }
        else
        {
            room.Reserve(title);
            lines = 1;
        }

        var when = room.Take(WhenStep(large).LineHeight);
        if (lines == 0 && room.Take(title))
        {
            lines = 1;
        }

        var most = large ? 3 : 2;
        while (lines > 0 && lines < most && room.Take(title))
        {
            lines++;
        }

        var range = room.Take(FluentType.Caption.LineHeight);
        var location = hasLocation && room.Take(FluentType.Caption.LineHeight);
        var organizer = hasOrganizer && room.Take(FluentType.Caption.LineHeight);
        return new NextMeetingPlan(when, lines, range, location, organizer, beside, large);
    }
}
