using Themia.Core.Localization;
using Themia.Core.Office;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;

namespace Themia.Widget.NextMeeting;

/// <summary>
/// [S-2160] [S-2161] The meetings after the one shown, and how many of them a size holds: the room
/// left under the meeting's own lines goes to more of the widget's own items, never to a larger
/// title ([S-666]). They are one-line Rows (the title the name, the start the value) under a Caption
/// label, 8 under the details and over Join, whole rows only.
/// </summary>
public static class NextMeetingFollowing
{
    /// <summary>Between the details and the label over the rows.</summary>
    public const double Gap = FamilySpace.S2;

    /// <summary>What the label over the rows takes: one Caption line.</summary>
    public static double LabelCost => FamilyType.Caption.LineHeight;

    /// <summary>
    /// [S-2160] The meetings after <paramref name="shown"/> at <paramref name="now"/>, in order: the timed
    /// events not declined that have not started yet and do not start before it (an event under way is no
    /// meeting after this one), today's first as time runs, ordered as <see cref="NextMeetingRules.Pick"/>
    /// orders a tie so a redraw never swaps them. Without <paramref name="laterDays"/> only today's
    /// (in <paramref name="zone"/>): a row whose value is a time alone is never another day's.
    /// </summary>
    public static IReadOnlyList<CalendarEvent> After(IEnumerable<CalendarEvent> events, CalendarEvent shown, DateTimeOffset now, TimeZoneInfo zone, bool laterDays)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(shown);
        ArgumentNullException.ThrowIfNull(zone);
        var key = CalendarShare.Key(shown);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);

        return [.. events
            .Where(e => !e.AllDay && !e.Declined && e.Start > now && e.Start >= shown.Start && !CalendarShare.Key(e).Equals(key))
            .Where(e => laterDays || DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(e.Start, zone).DateTime) == today)
            .OrderBy(e => e.Start)
            .ThenBy(e => e.JoinUrl is null)
            .ThenBy(e => e.End - e.Start)
            .ThenBy(e => e.Subject, StringComparer.Ordinal)
            .ThenBy(e => e.Id, StringComparer.Ordinal)];
    }

    /// <summary>
    /// [S-2160] Whether the rows go on to the next days: only on an expanded width, where a row holds a
    /// title beside "Tomorrow, 9:30 AM"; narrower (Tall), a later day's value would crush the title, so
    /// the rows are today's alone and say a time.
    /// </summary>
    public static bool LaterDays(WidgetArea area) => area.WidthClass >= SizeClass.Expanded;

    /// <summary>
    /// [S-2161] How tall the meeting's own lines stand in <paramref name="plan"/>: each shown line (the when
    /// and the range, location, organiser lines in Body, the title in Title), 4 apart.
    /// </summary>
    public static double DetailsHeight(NextMeetingPlan plan)
    {
        var line = FamilyType.Body.LineHeight;
        var heights = new List<double>();
        if (plan.When)
        {
            heights.Add(line);
        }

        if (plan.Title)
        {
            heights.Add(FamilyType.Title.LineHeight);
        }

        foreach (var shown in new[] { plan.Range, plan.Location, plan.Organizer })
        {
            if (shown)
            {
                heights.Add(line);
            }
        }

        return heights.Count == 0 ? 0 : heights.Sum() + ((heights.Count - 1) * NextMeetingLayout.Gap);
    }

    /// <summary>
    /// [S-2161] How many of <paramref name="count"/> following meetings a content box of
    /// <paramref name="height"/> shows under the details of <paramref name="plan"/>: whole one-line rows
    /// in what is left once the details, 8, the label and Join (with its 8) are taken; none while the
    /// details themselves are cut short (no range line yet), and none where not one row fits.
    /// </summary>
    public static int Rows(double height, NextMeetingPlan plan, bool hasJoin, int count)
    {
        if (count <= 0 || !plan.Range || !double.IsFinite(height))
        {
            return 0;
        }

        var room = height - (hasJoin ? NextMeetingLayout.JoinCost : 0) - DetailsHeight(plan) - Gap - LabelCost;
        return Math.Min(count, RowRules.Fit(room, 1));
    }

    /// <summary>
    /// [S-2160] A following meeting's value: its start on the regional clock ([S-3773]); on another day
    /// than today the day too ("Tomorrow, 9:30 AM"), as the when line says it.
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
}
