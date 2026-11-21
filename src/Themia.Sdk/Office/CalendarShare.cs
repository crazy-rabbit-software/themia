using Themia.Core.Links;
using Themia.Core.Model;

namespace Themia.Core.Office;

/// <summary>
/// [S-3620]..[S-3629] What the calendar widgets on one view share through its links ([S-3300]): the
/// selected day, the period the view's calendars show (a header or a body paged to it, [S-3626]), and the event
/// under the pointer. The rules of who follows what live here; the views only publish and draw.
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
    /// [S-11943] The person an event is on the view's links: its organiser, by the address where the provider
    /// gave one (so the same person as a mail's sender or a contact), else by the name it shows; none without one.
    /// </summary>
    public static PersonLink? Person(CalendarEvent item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return LinkKeys.PersonLink(item.OrganizerAddress, item.Organizer);
    }

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
    /// <param name="pages">The first row of each page, from <see cref="Themia.Sdk.Kit.KitPages.Pages"/>.</param>
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

    /// <summary>
    /// [S-3627] The period a calendar says it shows when it pages (<see cref="LinkTopics.VisibleRange"/>): a month its own
    /// days, never the neighbours' that pad its weeks; a week its columns; a day; a year. The agenda's is its page's
    /// (<see cref="AgendaPeriod"/>).
    /// </summary>
    public static RangeLink Period(CalendarStyle layout, DateOnly anchor, DateOnly today, CalendarPeriod span)
    {
        if (layout == CalendarStyle.Month)
        {
            return new RangeLink(new DateOnly(anchor.Year, anchor.Month, 1), new DateOnly(anchor.Year, anchor.Month, DateTime.DaysInMonth(anchor.Year, anchor.Month)));
        }

        var (first, last) = CalendarRules.ViewRange(layout, anchor, today, span);
        return new RangeLink(first, last);
    }

    /// <summary>
    /// [S-3627] The agenda's period: the days of the rows on <paramref name="page"/>, from its first row's day to its last's.
    /// Null while it lists nothing.
    /// </summary>
    /// <param name="days">Each row's day, in order.</param>
    /// <param name="pages">The first row of each page.</param>
    public static RangeLink? AgendaPeriod(IReadOnlyList<DateOnly> days, IReadOnlyList<int> pages, int page)
    {
        ArgumentNullException.ThrowIfNull(days);
        ArgumentNullException.ThrowIfNull(pages);

        if (days.Count == 0 || pages.Count == 0)
        {
            return null;
        }

        page = Math.Clamp(page, 0, pages.Count - 1);
        var first = Math.Clamp(pages[page], 0, days.Count - 1);
        var last = page + 1 < pages.Count ? Math.Clamp(pages[page + 1] - 1, first, days.Count - 1) : days.Count - 1;
        return new RangeLink(days[first], days[last]);
    }

    /// <summary>
    /// [S-3628] The day a calendar anchors on for a period another widget of the view paged to: the selected day while the
    /// period holds it, else today while it holds today, else the period's first day. A month, a week, a day or a year
    /// shows the period of its own that holds that day; the agenda turns to the page holding it ([S-3622]).
    /// </summary>
    public static DateOnly AnchorIn(RangeLink period, DateOnly today, DateOnly? selected)
    {
        ArgumentNullException.ThrowIfNull(period);

        if (selected is { } day && period.Contains(day))
        {
            return day;
        }

        return period.Contains(today) ? today : period.First;
    }

    /// <summary>One notch of a mouse wheel, as Windows reports it (<c>WHEEL_DELTA</c>).</summary>
    public const int WheelNotch = 120;

    /// <summary>
    /// [S-3629] What a wheel delta pages: one period a notch, turned away from the user back, towards the user forward. A
    /// precision wheel or a touchpad reports a notch in parts, so the rest is carried to the next event; turning back drops
    /// what was carried the other way.
    /// </summary>
    /// <returns>The periods to step (negative: back) and the carry for the next call.</returns>
    public static (int Steps, int Carry) Wheel(int carry, int delta)
    {
        if (carry != 0 && Math.Sign(carry) != Math.Sign(delta))
        {
            carry = 0;
        }

        var total = carry + delta;
        return (-(total / WheelNotch), total % WheelNotch);
    }
}
