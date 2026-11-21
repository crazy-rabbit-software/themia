using Themia.Core.Localization;
using Themia.Core.Office;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;

namespace Themia.Widget.UpNext;

/// <summary>
/// [S-11408] After this on the kit ([S-7074]): one-line rows, the meeting's title the name, its start the value (a
/// reading in the time's place, [S-7072]: "14:00", "Tomorrow 09:30"); a click opens it in its calendar ([S-7075]).
/// </summary>
public static class UpNextSpec
{
    /// <summary>The Open action's id: the view opens the meeting in its calendar.</summary>
    public const string OpenId = "open";

    public static KitSpec Spec { get; } = new(
        KitRowLook.Line,
        KitShow.When,
        static () => Str.Calendar_NoTitle.Text(),
        static () => UpNextStr.UpNext_None.Text(),
        KitTime.None)
    {
        ClickRuns = true,
        Readings = true,
    };

    /// <summary>A meeting's id: its account, its provider's id and its start, as one fingerprint (an id may be long).</summary>
    public static string Id(CalendarEvent meeting)
    {
        ArgumentNullException.ThrowIfNull(meeting);
        return KitItems.Fingerprint($"{meeting.AccountId}|{meeting.Id}|{meeting.Start.UtcTicks}");
    }

    /// <summary>[S-11408] A meeting as a kit item: its title, its start as the value, its title and its time range as the tooltip.</summary>
    public static KitItem Item(CalendarEvent meeting, DateTimeOffset now, TimeZoneInfo zone, Locale locale)
    {
        ArgumentNullException.ThrowIfNull(meeting);

        var title = string.IsNullOrWhiteSpace(meeting.Subject) ? Strings.Get(locale, Str.Calendar_NoTitle) : meeting.Subject;
        return new KitItem(
            Id(meeting),
            string.IsNullOrWhiteSpace(meeting.Subject) ? string.Empty : meeting.Subject,
            Tooltip: $"{title}{Environment.NewLine}{NextMeetingRules.Range(locale, meeting, zone)}",
            Value: NextMeetingRules.Start(locale, meeting, now, zone));
    }

    /// <summary>[S-1268] A meeting's menu: Open, in its calendar.</summary>
    public static IReadOnlyList<KitAction> Actions(KitItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return [new KitAction(KitActionKind.Open, Str.Common_Open.Text(), KitIcon.Event, Id: OpenId)];
    }
}
