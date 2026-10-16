using Themia.Core.Links;
using Themia.Core.Model;

namespace Themia.Core.Office;

/// <summary>
/// [S-3620]..[S-3625] What the calendar widgets on one view share through its links ([S-3300]): the
/// selected day, the range a widget was paged to, and the event under the pointer. The rules of who
/// follows what live here; the views only publish and draw.
/// </summary>
public static class CalendarShare
{
    /// <summary>[S-3623] An event's key across widgets: the same event in two widgets is the same key.</summary>
    public static EventLink Key(CalendarEvent item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return new EventLink(item.AccountId, item.CalendarId, item.Id);
    }

    /// <summary>
    /// [S-3623] Whether an event is drawn dimmed: another widget's pointer is on an event, and it is
    /// not this one. Nothing is dimmed while nothing is hovered.
    /// </summary>
    public static bool Dimmed(EventLink? hovered, CalendarEvent item)
        => hovered is not null && !hovered.Equals(Key(item));

    /// <summary>[S-3620] A click on a day: that day becomes the selection, or, when it is the selection, none.</summary>
    public static DateOnly? Toggle(DateOnly? selected, DateOnly day) => selected == day ? null : day;

    /// <summary>
    /// [S-3621] The day a widget shows once <paramref name="selected"/> is the selection: the month,
    /// week, day and year bring it into view (their anchor becomes it, so a month pages only when the
    /// day is in another month); the agenda keeps its own range and moves its page instead ([S-3622]).
    /// </summary>
    public static DateOnly AnchorFor(CalendarStyle layout, DateOnly anchor, DateOnly selected)
        => layout == CalendarStyle.Agenda ? anchor : selected;

    /// <summary>
    /// [S-3622] The agenda page that holds <paramref name="date"/>: the one whose rows include the first
    /// row on or after it. Null when every row is before it (nothing of that day or later is listed).
    /// </summary>
    /// <param name="days">Each row's day, in order.</param>
    /// <param name="pages">The first row of each page, from <see cref="CalendarFit.AgendaPages"/>.</param>
    public static int? AgendaPageOf(IReadOnlyList<DateOnly> days, IReadOnlyList<int> pages, DateOnly date)
    {
        ArgumentNullException.ThrowIfNull(days);
        ArgumentNullException.ThrowIfNull(pages);

        var row = -1;
        for (var i = 0; i < days.Count; i++)
        {
            if (days[i] >= date)
            {
                row = i;
                break;
            }
        }

        if (row < 0 || pages.Count == 0)
        {
            return null;
        }

        var page = 0;
        for (var p = 0; p < pages.Count; p++)
        {
            if (pages[p] <= row)
            {
                page = p;
            }
        }

        return page;
    }

    /// <summary>
    /// [S-3624] Where an arrow moves the keyboard's day: from the selection when the widget shows it,
    /// else from the day it is anchored on, by <paramref name="days"/>.
    /// </summary>
    public static DateOnly Step(DateOnly? selected, bool selectedInView, DateOnly anchor, int days)
        => (selected is { } day && selectedInView ? day : anchor).AddDays(days);
}
