using Themia.App.Shell;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Office;
using Themia.Core.Widgets;
using Themia.Sdk.Items;
using Themia.Sdk.Kit;

namespace Themia.Widget.Meetings;

/// <summary>
/// [S-16960]..[S-16963] The meetings on the item base (owner, 2026-11-23: "the meetings should get a join button on the right side
/// for example instead of the percent thingy"): which meetings a list shows and in what order, which one a Meeting shows,
/// and a meeting as an item: its title the subject, its subtitle when (the time range, the day where it is not today, how
/// long until it where it is soon or under way), its end Join where it has a join link and is not over (else the start, or
/// how long until it), its bar how far an under-way one has run, its note the place and the organiser. Pure.
/// </summary>
public static class MeetingItems
{
    /// <summary>[S-16963] The parts a meeting item has, in its own order.</summary>
    public static IReadOnlyList<ItemPart> Parts { get; } = [ItemPart.Lead, ItemPart.Title, ItemPart.Subtitle, ItemPart.Note, ItemPart.Bar, ItemPart.End];

    /// <summary>A meeting's fingerprint: its account, its provider's id and its start (an id may be long).</summary>
    public static string Id(CalendarEvent meeting)
    {
        ArgumentNullException.ThrowIfNull(meeting);
        return KitItems.Fingerprint($"{meeting.AccountId}|{meeting.Id}|{meeting.Start.UtcTicks}");
    }

    /// <summary>A meeting: timed, not declined.</summary>
    private static bool IsMeeting(CalendarEvent e) => !e.AllDay && !e.Declined;

    /// <summary>
    /// [S-16962] The meetings a list shows at <paramref name="now"/>: the timed ones not declined, coming up (not over: one under
    /// way too) up to the end of the last day of its reach, past (over) back to the start of the first day of its reach, or
    /// both; the next one left out where <see cref="MeetingsConfig.AfterNext"/> (After this's rule, [S-11408]: only those still
    /// to start after it); soonest or latest first, a tie broken as <see cref="NextMeetingRules.Pick"/> breaks it.
    /// </summary>
    public static IReadOnlyList<CalendarEvent> Select(IEnumerable<CalendarEvent> events, MeetingsConfig config, DateTimeOffset now, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(zone);
        var all = events.Where(IsMeeting).ToList();
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
        var days = config.EffectiveDays;
        var until = Midnight(today.AddDays(days), zone);
        var since = Midnight(today.AddDays(1 - days), zone);

        IEnumerable<CalendarEvent> chosen = config.When switch
        {
            MeetingWhen.Past => all.Where(e => e.End <= now && e.Start >= since),
            MeetingWhen.Both => all.Where(e => (e.End <= now && e.Start >= since) || (e.End > now && e.Start < until)),
            _ => all.Where(e => e.End > now && e.Start < until),
        };

        if (config.AfterNext && config.When != MeetingWhen.Past && NextMeetingRules.Pick(all, now) is { } next)
        {
            var after = NextMeetingRules.After(all, next, now, zone).Select(CalendarShare.Key).ToHashSet();
            chosen = chosen.Where(e => e.End <= now || after.Contains(CalendarShare.Key(e)));
        }

        var ordered = chosen
            .OrderBy(e => e.Start)
            .ThenBy(e => e.JoinUrl is null)
            .ThenBy(e => e.End - e.Start)
            .ThenBy(e => e.Subject, StringComparer.Ordinal)
            .ThenBy(e => e.Id, StringComparer.Ordinal)
            .ToList();
        if (config.Order == MeetingOrder.Latest)
        {
            ordered.Reverse();
        }

        return ordered;
    }

    /// <summary>
    /// [S-16960] The meeting a Meeting shows at <paramref name="now"/>: the one picked while it is not over, else the next one
    /// (<see cref="NextMeetingRules.Pick"/>); null with nothing coming.
    /// </summary>
    public static CalendarEvent? Pick(IEnumerable<CalendarEvent> events, MeetingConfig config, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(config);
        var all = events.Where(IsMeeting).ToList();
        if (config.Pick == MeetingPick.Chosen && all.FirstOrDefault(e => Id(e) == config.Chosen && e.End > now) is { } chosen)
        {
            return chosen;
        }

        return NextMeetingRules.Pick(all, now);
    }

    /// <summary>[S-16960] The meetings a Meeting's page offers to pick: those not over, soonest first.</summary>
    public static IReadOnlyList<CalendarEvent> Upcoming(IEnumerable<CalendarEvent> events, DateTimeOffset now)
        => [.. (events ?? []).Where(e => IsMeeting(e) && e.End > now).OrderBy(e => e.Start).ThenBy(e => e.Subject, StringComparer.Ordinal)];

    /// <summary>[S-16963] Whether a meeting starts within the hour or is under way: how long until it is said then.</summary>
    private static bool Soon(CalendarEvent meeting, DateTimeOffset now) => meeting.Start - now < TimeSpan.FromHours(1) && meeting.End > now;

    /// <summary>
    /// [S-16963] A meeting as an item at <paramref name="now"/>: the subject (or "No title"), its lead the meeting's glyph (an
    /// online one's, else the calendar's), its subtitle when ("11:00 – 11:45"; "Tomorrow · 9:30 – 10:00" on another day; "in 18
    /// min · 11:00 – 11:45" soon or under way with Join), its end Join where it has a join link and is not over, else the start
    /// or how long until it (none once over), its bar how far an under-way one has run, its note the place and the organiser
    /// as <paramref name="config"/> shows them; under way strong.
    /// </summary>
    public static Item Item(CalendarEvent meeting, MeetingsBaseConfig config, DateTimeOffset now, TimeZoneInfo zone, Locale locale)
    {
        ArgumentNullException.ThrowIfNull(meeting);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(zone);

        var title = string.IsNullOrWhiteSpace(meeting.Subject) ? Strings.Get(locale, Str.Calendar_NoTitle) : meeting.Subject;
        var range = NextMeetingRules.Range(locale, meeting, zone);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
        var day = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(meeting.Start, zone).DateTime);
        var dayed = day == today ? range : $"{Formatting.DayHeading(locale, day, today)} · {range}";
        var over = meeting.End <= now;
        var running = !over && meeting.Start <= now;
        var soon = Soon(meeting, now);
        var when = NextMeetingRules.When(locale, meeting, now, zone);
        var join = !over && meeting.JoinUrl is not null;

        ItemEnd? end = join
            ? ItemEnd.Action(MeetingsStr.Meeting_Join.Text(locale))
            : over ? null : ItemEnd.Text(soon ? when : NextMeetingRules.Start(locale, meeting, now, zone), running ? ColourRole.TextPrimary : ColourRole.TextSecondary);
        var subtitle = join && soon ? $"{when} · {range}" : dayed;

        var notes = new List<string>(2);
        if (config.ShowPlace && !string.IsNullOrWhiteSpace(meeting.Location))
        {
            notes.Add(meeting.Location!.Trim());
        }

        if (config.ShowOrganizer && !string.IsNullOrWhiteSpace(meeting.Organizer))
        {
            notes.Add(meeting.Organizer!.Trim());
        }

        var note = string.Join(" · ", notes);
        var length = (meeting.End - meeting.Start).TotalSeconds;
        ItemBar? bar = running && length > 0 ? new ItemBar(Math.Clamp((now - meeting.Start).TotalSeconds / length, 0, 1)) : null;
        return new Item(
            Id(meeting),
            title,
            new ItemLead(meeting.JoinUrl is null ? Glyphs.Calendar : Glyphs.Meeting, running ? ColourRole.TextPrimary : ColourRole.TextSecondary),
            subtitle,
            end,
            bar,
            note,
            Tooltip: string.Join(Environment.NewLine, new[] { title, dayed, note }.Where(t => t.Length > 0)),
            Strong: running,
            Muted: over);
    }

    /// <summary>[S-14262] No meeting keeps the face: one item, the calendar's glyph, "--" and why (WR-10).</summary>
    public static Item Nothing(string why) => new(string.Empty, EmptyRows.Placeholder, new ItemLead(Glyphs.Calendar), why, Tooltip: why, Muted: true);

    /// <summary>[S-16963] A meeting item's words for its parts: "Meeting", "When", "Join or starts", "Progress", "Where".</summary>
    public static string PartName(ItemPart part, Locale locale) => part switch
    {
        ItemPart.Title => MeetingsStr.Meetings_Part_Title.Text(locale),
        ItemPart.Subtitle => MeetingsStr.Meetings_Part_Subtitle.Text(locale),
        ItemPart.End => MeetingsStr.Meetings_Part_End.Text(locale),
        ItemPart.Bar => MeetingsStr.Meetings_Part_Bar.Text(locale),
        ItemPart.Note => MeetingsStr.Meetings_Part_Note.Text(locale),
        _ => Strings.Get(locale, Str.Items_Part_Lead),
    };

    private static DateTimeOffset Midnight(DateOnly day, TimeZoneInfo zone)
    {
        var local = day.ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(local, zone.GetUtcOffset(local));
    }
}
