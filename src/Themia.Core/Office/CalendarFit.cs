using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.Core.Office;

/// <summary>
/// [S-1260] What the calendar draws in the room it has. The configured view is what the user
/// wants to look at; the layout is how much of it a tile of this size can show without slivers.
/// </summary>
public enum CalendarLayout
{
    /// <summary>[S-1261] The one next thing: its time and title, and how many more follow that day.</summary>
    Next,

    /// <summary>[S-571] The upcoming list, grouped by day.</summary>
    Agenda,

    /// <summary>[S-1262] A row of day columns, each with its events as one-line entries.</summary>
    Strip,

    /// <summary>[S-572] The month grid alone.</summary>
    Month,

    /// <summary>[S-1263] A month of near-square cells over the upcoming list (a tall tile).</summary>
    MonthAgendaBelow,

    /// <summary>[S-1263] The month grid with the upcoming list beside it (a large tile).</summary>
    MonthAgendaBeside,

    /// <summary>[S-561] Seven day columns.</summary>
    Week,

    /// <summary>[S-573] The day timeline.</summary>
    Day,
}

/// <summary>What a key does to the calendar ([S-1269]).</summary>
public enum CalendarKeyCommand
{
    None,

    /// <summary>One period back: a month, a week, a day or a strip's width.</summary>
    Back,

    Forward,

    /// <summary>The cursor day a week back (month and week layouts).</summary>
    WeekBack,

    WeekForward,

    Today,

    /// <summary>Open the first event of the cursor day, or the next event where there is no cursor.</summary>
    OpenFirst,
}

/// <summary>[S-1261] The next thing on the calendar, the day it falls on, and how many more that day holds.</summary>
public sealed record NextUp(DayItem Item, DateOnly Day, int MoreThatDay);

/// <summary>
/// [S-1260] The calendar's disclosure ladder: which layout a tile gets, what that layout fetches,
/// how it pages and titles itself, and the per-tier thresholds of its parts. Pure, so the
/// thresholds are pinned by tests rather than by eye.
/// <para>
/// Sizes are the view's own slot (the card less its header), in DIPs. The ladder, from XS up:
/// the next event (XS), the agenda (S), the month with dots (M), a day strip (Wide), the month over
/// the agenda (Tall), the month with chips and weekday names (L), the month beside the agenda
/// (XL). Week and day keep their own views from M up, gaining lines at L/XL.
/// </para>
/// </summary>
public static class CalendarFit
{
    /// <summary>Below this height a wide tile is a strip of days rather than a squashed grid.</summary>
    public const double StripBelow = 150;

    /// <summary>A strip needs this much width to hold three readable day columns.</summary>
    public const double StripFrom = 360;

    /// <summary>One strip column's minimum width: "Tomorrow" over "09:00 Standup".</summary>
    public const double StripColumn = 112;

    /// <summary>A strip shorter than this has no room for an event under its day headings.</summary>
    public const double StripMinHeight = 56;

    /// <summary>The upcoming list beside the month: wide enough for the time column and a title.</summary>
    public const double BesideAgendaWidth = 280;

    /// <summary>
    /// [S-1260] The layout for a configured view at a slot size. An unmeasured slot (0 or less) gets
    /// the configured view as it stands, so the first frame never flashes a fallback.
    /// </summary>
    public static CalendarLayout Layout(CalendarViewMode wanted, double width, double height)
    {
        if (!(width > 0) || !(height > 0))
        {
            return Plain(wanted);
        }

        // A wide, short strip: every view becomes a row of days, which is what the room is for.
        if (height < StripBelow && width >= StripFrom && height >= StripMinHeight)
        {
            return CalendarLayout.Strip;
        }

        // Too small for a list worth scrolling: the one next thing.
        if (width < 160 || height < 88)
        {
            return CalendarLayout.Next;
        }

        switch (wanted)
        {
            // [S-574] A month narrower than 154 or shorter than 112 is the agenda.
            case CalendarViewMode.Month when width < 7 * 22 || height < 112:
                return CalendarLayout.Agenda;

            case CalendarViewMode.Month when height >= 300 && height >= 1.6 * width:
                return CalendarLayout.MonthAgendaBelow;

            case CalendarViewMode.Month when width >= 720 && height >= 400:
                return CalendarLayout.MonthAgendaBeside;

            case CalendarViewMode.Week when width < 7 * 40 || height < 96:
            case CalendarViewMode.Day when width < 140 || height < 112:
                return CalendarLayout.Agenda;

            default:
                return Plain(wanted);
        }
    }

    private static CalendarLayout Plain(CalendarViewMode wanted) => wanted switch
    {
        CalendarViewMode.Month => CalendarLayout.Month,
        CalendarViewMode.Week => CalendarLayout.Week,
        CalendarViewMode.Day => CalendarLayout.Day,
        _ => CalendarLayout.Agenda,
    };

    /// <summary>Whether a layout pages through periods (back, forward, today and a title in the header).</summary>
    public static bool Pages(CalendarLayout layout) => layout is not (CalendarLayout.Next or CalendarLayout.Agenda);

    /// <summary>
    /// [S-1262] How many days a strip shows: the configured week's seven, else as many columns of
    /// <see cref="StripColumn"/> as fit, three to five.
    /// </summary>
    public static int StripDays(CalendarViewMode wanted, double width)
        => wanted == CalendarViewMode.Week ? 7 : Math.Clamp((int)Math.Floor(Math.Max(0, width) / StripColumn), 3, 5);

    /// <summary>[S-1262] The first day of a strip: the week's first day for a week, else the anchor.</summary>
    public static DateOnly StripStart(CalendarViewMode wanted, DateOnly anchor, WeekStartDay start)
        => wanted == CalendarViewMode.Week ? CalendarRules.StartOfWeek(anchor, start) : anchor;

    /// <summary>
    /// [S-568] The days a layout covers, both inclusive: the month layouts the month's whole weeks
    /// (the list beside or below reads from the same range), a strip its days, the next tile and
    /// the agenda today and the next <see cref="CalendarRules.AgendaDays"/> days.
    /// </summary>
    public static (DateOnly First, DateOnly Last) Range(
        CalendarLayout layout,
        CalendarViewMode wanted,
        DateOnly anchor,
        DateOnly today,
        WeekStartDay start,
        int stripDays)
    {
        switch (layout)
        {
            case CalendarLayout.Strip:
                var first = StripStart(wanted, anchor, start);
                return (first, first.AddDays(Math.Max(1, stripDays) - 1));

            case CalendarLayout.Month or CalendarLayout.MonthAgendaBelow or CalendarLayout.MonthAgendaBeside:
                return CalendarRules.ViewRange(CalendarViewMode.Month, anchor, today, start);

            case CalendarLayout.Week:
                return CalendarRules.ViewRange(CalendarViewMode.Week, anchor, today, start);

            case CalendarLayout.Day:
                return CalendarRules.ViewRange(CalendarViewMode.Day, anchor, today, start);

            default:
                return CalendarRules.ViewRange(CalendarViewMode.Agenda, anchor, today, start);
        }
    }

    /// <summary>[S-569] The anchor one period back or forward; a strip steps by its own width.</summary>
    public static DateOnly Step(CalendarLayout layout, DateOnly anchor, int direction, int stripDays) => layout switch
    {
        CalendarLayout.Strip => anchor.AddDays(Math.Sign(direction) * Math.Max(1, stripDays)),
        CalendarLayout.Month or CalendarLayout.MonthAgendaBelow or CalendarLayout.MonthAgendaBeside
            => CalendarRules.Step(CalendarViewMode.Month, anchor, direction),
        CalendarLayout.Week => CalendarRules.Step(CalendarViewMode.Week, anchor, direction),
        CalendarLayout.Day => CalendarRules.Step(CalendarViewMode.Day, anchor, direction),
        _ => anchor,
    };

    /// <summary>[S-568] The header title for a paging layout ("October 2026", "4 Oct – 8 Oct"); none for the others.</summary>
    public static string Title(CalendarLayout layout, CalendarViewMode wanted, DateOnly anchor, WeekStartDay start, int stripDays, Locale locale)
    {
        switch (layout)
        {
            case CalendarLayout.Strip:
                var first = StripStart(wanted, anchor, start);
                return CalendarRules.Range(first, first.AddDays(Math.Max(1, stripDays) - 1), locale);

            case CalendarLayout.Month or CalendarLayout.MonthAgendaBelow or CalendarLayout.MonthAgendaBeside:
                return CalendarRules.Title(CalendarViewMode.Month, anchor, start, locale);

            case CalendarLayout.Week:
                return CalendarRules.Title(CalendarViewMode.Week, anchor, start, locale);

            case CalendarLayout.Day:
                return CalendarRules.Title(CalendarViewMode.Day, anchor, start, locale);

            default:
                return string.Empty;
        }
    }

    /// <summary>
    /// [S-569] Whether a day is in the period on screen, so the today button hides when it would do
    /// nothing. A month counts only its own days, not the neighbours' that pad its weeks.
    /// </summary>
    public static bool InView(CalendarLayout layout, CalendarViewMode wanted, DateOnly anchor, DateOnly today, WeekStartDay start, int stripDays, DateOnly day)
    {
        if (layout is CalendarLayout.Month or CalendarLayout.MonthAgendaBelow or CalendarLayout.MonthAgendaBeside)
        {
            return day.Year == anchor.Year && day.Month == anchor.Month;
        }

        var (first, last) = Range(layout, wanted, anchor, today, start, stripDays);
        return day >= first && day <= last;
    }

    // ---- the next thing ([S-1261]) ---------------------------------------------------------------

    /// <summary>
    /// [S-1261] The next thing for the XS tile: on the first day of the agenda that has anything,
    /// the event under way or starting next (a meeting is what one glances at a tile for), else
    /// that day's first item (an all-day event or a birthday). Null when the agenda is empty.
    /// </summary>
    public static NextUp? Next(
        IEnumerable<CalendarEvent> events,
        IEnumerable<Birthday> birthdays,
        DateOnly today,
        TimeZoneInfo zone,
        DateTimeOffset now)
    {
        var groups = CalendarRules.Agenda(events, birthdays, today, today.AddDays(CalendarRules.AgendaDays), zone, int.MaxValue, now);
        if (groups.Count == 0)
        {
            return null;
        }

        var (day, items) = groups[0];
        var timed = -1;
        for (var i = 0; i < items.Count && timed < 0; i++)
        {
            timed = items[i].Event is { AllDay: false } ? i : -1;
        }

        return new NextUp(items[Math.Max(0, timed)], day, items.Count - 1);
    }

    // ---- month ([S-1264]) ------------------------------------------------------------------------

    /// <summary>[S-1264] Weekday names ("Mon") over the month and week columns once a column is this wide.</summary>
    public const double WeekdayNamesFrom = 64;

    /// <summary>[S-1264] A month chip leads with its start time once a cell is this wide (the classic S-44).</summary>
    public const double ChipTimeFrom = 100;

    /// <summary>
    /// [S-1264] How many week rows a month needs: the weeks holding one of its days (four to six).
    /// The fetch still covers the six-week range ([S-568]); a large tile simply does not spend a
    /// row on a week wholly in the next month.
    /// </summary>
    public static int MonthRows(DateOnly anchor, WeekStartDay start)
    {
        var first = new DateOnly(anchor.Year, anchor.Month, 1);
        var last = first.AddMonths(1).AddDays(-1);
        var gridStart = CalendarRules.StartOfWeek(first, start);
        return ((last.DayNumber - gridStart.DayNumber) / 7) + 1;
    }

    /// <summary>
    /// [S-1263] The cell height of a month drawn over the agenda: square to the column, between the
    /// dots' 24 and 32, so the month stays a calendar and the list gets the rest.
    /// </summary>
    public static double SquareCell(double width) => Math.Clamp(Math.Max(0, width) / 7, 24, 32);

    // ---- week and day ([S-1265], [S-1266]) -------------------------------------------------------

    /// <summary>[S-1265] A week column this wide gives a timed chip its end time and its location.</summary>
    public const double WeekDetailFrom = 100;

    /// <summary>[S-1266] The day timeline's hour: a list row, or a compact row when that fits all fifteen hours unscrolled.</summary>
    public static double HourHeight(double available)
    {
        var hours = CalendarRules.TimelineEndHour - CalendarRules.TimelineFirstHour;
        return available >= hours * 32 && available < hours * 40 ? 32 : 40;
    }

    // ---- agenda ([S-1267]) -----------------------------------------------------------------------

    /// <summary>[S-1267] From this width an agenda row puts location and organizer in columns of their own.</summary>
    public const double AgendaColumnsFrom = 480;

    /// <summary>[S-1267] From this width the agenda flows its days into two columns.</summary>
    public const double AgendaTwoColumnsFrom = 720;

    /// <summary>
    /// [S-1267] Whether a day group gets its heading. On a short tile the first group's "Today" is
    /// implied (it is what a small agenda is about) and its line goes to an event instead; any other
    /// day keeps its heading, so an item is never shown without the day it falls on.
    /// </summary>
    public static bool ShowsHeading(DateOnly day, DateOnly today, bool first, double height)
        => !(first && day == today && height > 0 && height < 128);

    // ---- keys ([S-1269]) ---------------------------------------------------------------------------

    /// <summary>
    /// [S-1269] The classic keys (S-31..34): Left / Right step the period, Up / Down move the cursor
    /// day a week in the month and week layouts, Home goes to today, Enter opens the cursor day's
    /// first event. A layout that does not page takes only Enter. [S-1984] Page Up / Page Down page
    /// too (a month in the month layouts), so the arrows' meaning never has to be learnt to page.
    /// </summary>
    public static CalendarKeyCommand Key(WidgetKey key, CalendarLayout layout)
    {
        var pages = Pages(layout);
        var weeks = layout is CalendarLayout.Month or CalendarLayout.MonthAgendaBelow or CalendarLayout.MonthAgendaBeside or CalendarLayout.Week;

        return key switch
        {
            WidgetKey.Left when pages => CalendarKeyCommand.Back,
            WidgetKey.Right when pages => CalendarKeyCommand.Forward,
            WidgetKey.PageUp when pages => CalendarKeyCommand.Back,
            WidgetKey.PageDown when pages => CalendarKeyCommand.Forward,
            WidgetKey.Up when weeks => CalendarKeyCommand.WeekBack,
            WidgetKey.Down when weeks => CalendarKeyCommand.WeekForward,
            WidgetKey.Home when pages => CalendarKeyCommand.Today,
            WidgetKey.Enter => CalendarKeyCommand.OpenFirst,
            _ => CalendarKeyCommand.None,
        };
    }
}
