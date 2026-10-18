using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Core.Office;

/// <summary>What a key does to the calendar ([S-1269], [S-2710]).</summary>
public enum CalendarKeyCommand
{
    None,

    /// <summary>One unit back: a month, a week, a day, a year, or a page of the agenda.</summary>
    Back,

    Forward,

    /// <summary>The cursor day a week back (month and week layouts).</summary>
    WeekBack,

    WeekForward,

    /// <summary>Back to today (the agenda's first page).</summary>
    Today,

    /// <summary>Open the first event of the cursor day, or the agenda page's first event.</summary>
    OpenFirst,
}

/// <summary>[S-2702] How much of a weekday's name a column header has room for.</summary>
public enum WeekdayLabel
{
    /// <summary>No header row: the day numbers need the height more.</summary>
    None,

    /// <summary>One letter ("M").</summary>
    Narrow,

    /// <summary>The two-letter initials ("Mo").</summary>
    Initials,

    /// <summary>The abbreviation ("Mon").</summary>
    Short,
}

/// <summary>
/// [S-2702] What a month grid of a given box draws: its week rows, the weekday header, the cell
/// height, how the cells show their days ([S-572]), how many chip lines a cell holds, and whether a
/// chip leads with its start time. A cell too short for dots under its number but wide enough for
/// them beside it (a wide, short tile) puts them there (<c>InlineDots</c>).
/// </summary>
public sealed record MonthFit(int Rows, WeekdayLabel Weekdays, double HeaderHeight, double CellHeight, MonthDensity Density, int ChipLines, double ChipLine, bool Times, bool InlineDots);

/// <summary>
/// [S-2703] One item of a week row as a bar: the columns it covers (clipped to the row), the line
/// it sits on, and whether it runs on past either end of the row.
/// </summary>
public sealed record LaneItem(DayItem Item, int First, int Last, int Lane, bool ContinuesBefore, bool ContinuesAfter);

/// <summary>[S-2704] What a week of a given box draws.</summary>
public sealed record WeekFit(WeekdayLabel Weekdays, bool Timeline, bool Dots, bool Detail);

/// <summary>[S-2705] The hours a timeline draws: the first, how many, and each one's height.</summary>
public readonly record struct HourWindow(int First, int Count, double HourHeight)
{
    public int End => First + Count;
}

/// <summary>
/// [S-2718] The day's all-day band: how many lines, each one's height, and whether the last line is
/// shared among the items left over (chips, then "+N") rather than one item's own bar.
/// </summary>
public sealed record DayBandFit(int Lines, double LineHeight, bool SharedLast);

/// <summary>
/// [S-2707] What a year of a given box draws: how many months, in how many columns and rows, the
/// side of a day cell, and whether the cells carry day numbers and a weekday row.
/// </summary>
public sealed record YearFit(int Months, int Columns, int Rows, double DayCell, bool Numbers, bool Weekdays);

/// <summary>
/// [S-2700] The calendar's layout math: each configured layout fitted to the box it has. The widget
/// draws only the configured layout; at a small size the layout sheds detail within itself (a month
/// keeps being a month), it never becomes another one. Pure, so the thresholds are pinned by tests
/// rather than by eye. Sizes are the view's own slot (the card less its header), in DIPs; line
/// heights come from the type ramp, so the user's text size moves every threshold with it.
/// </summary>
public static class CalendarFit
{
    // ---- month ([S-2702]) ------------------------------------------------------------------------

    /// <summary>[S-1264] Weekday names ("Mon") over the month and week columns once a column is this wide.</summary>
    public const double WeekdayNamesFrom = 64;

    /// <summary>[S-2702] The two-letter initials from this column width; narrower takes one letter.</summary>
    public const double WeekdayInitialsFrom = 22;

    /// <summary>[S-2702] Below this column width even one letter does not fit beside its neighbours.</summary>
    public const double WeekdayNarrowFrom = 10;

    /// <summary>[S-1264] A month chip leads with its start time once a cell is this wide (the classic S-44).</summary>
    public const double ChipTimeFrom = 100;

    /// <summary>A chip's line with its gap, and a tall cell's roomier one ([S-1264]).</summary>
    public static double ChipLine => FluentType.Caption.LineHeight + 1;

    public static double RoomyChipLine => FluentType.Caption.LineHeight + 4;

    /// <summary>A cell from this height gets the roomier chip line.</summary>
    public const double RoomyCellFrom = 90;

    /// <summary>[S-2702] A number-only cell this wide holds its dots beside the number.</summary>
    public const double InlineDotsFrom = 48;

    /// <summary>A month cell's own margin, padding and gap under its number, which chips cannot use.</summary>
    public const double MonthCellChrome = 4;

    /// <summary>
    /// [S-1264] How many week rows a month needs: the weeks holding one of its days (four to six).
    /// </summary>
    public static int MonthRows(DateOnly anchor, WeekStartDay start)
    {
        var first = new DateOnly(anchor.Year, anchor.Month, 1);
        var last = first.AddMonths(1).AddDays(-1);
        var gridStart = CalendarDays.StartOfWeek(first, start);
        return ((last.DayNumber - gridStart.DayNumber) / 7) + 1;
    }

    /// <summary>[S-2702] The weekday label a column of this width holds.</summary>
    public static WeekdayLabel Weekdays(double columnWidth)
        => columnWidth >= WeekdayNamesFrom ? WeekdayLabel.Short
            : columnWidth >= WeekdayInitialsFrom ? WeekdayLabel.Initials
            : columnWidth >= WeekdayNarrowFrom ? WeekdayLabel.Narrow
            : WeekdayLabel.None;

    /// <summary>
    /// [S-2702] The month grid for a box: only the weeks holding a day of the month; the weekday
    /// header while the cells keep a day number's line under it (else the header goes first, the
    /// numbers are the month); then chips, dots or the number alone by the cell ([S-572]).
    /// </summary>
    public static MonthFit Month(double width, double height, int rows)
    {
        rows = Math.Clamp(rows, 4, 6);
        var line = FluentType.Caption.LineHeight;
        var columnWidth = Math.Max(0, width) / 7;
        var labels = Weekdays(columnWidth);

        var room = new Disclosure(width, height);
        var header = 0.0;
        if (labels != WeekdayLabel.None && room.Fits(line + (rows * line)))
        {
            room.Take(line);
            header = line;
        }
        else
        {
            labels = WeekdayLabel.None;
        }

        var cellHeight = room.Remaining / rows;
        var density = CalendarRules.MonthCellDensity(columnWidth, cellHeight);
        var chipLine = cellHeight >= RoomyCellFrom ? RoomyChipLine : ChipLine;
        var chipLines = density == MonthDensity.Chips ? CalendarRules.ChipCapacity(cellHeight - MonthCellChrome, line, chipLine) : 0;

        return new MonthFit(rows, labels, header, cellHeight, density, chipLines, chipLine, density == MonthDensity.Chips && columnWidth >= ChipTimeFrom, density == MonthDensity.Number && columnWidth >= InlineDotsFrom);
    }

    // ---- bars ([S-2703]) ---------------------------------------------------------------------------

    /// <summary>The last local day an event shows on: an all-day one's last date, a timed one's last minute.</summary>
    public static DateOnly LastDay(CalendarEvent item, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(zone);

        if (item.AllDay)
        {
            return item.EndDay > item.FirstDay ? item.EndDay.AddDays(-1) : item.FirstDay;
        }

        if (item.End <= item.Start)
        {
            return CalendarRules.StartDay(item, zone);
        }

        // The end is exclusive: a meeting that ends at midnight does not show on the next day.
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(item.End.AddTicks(-1), zone).DateTime);
    }

    /// <summary>Whether an event covers more than one local day: a bar across a week's columns.</summary>
    public static bool IsMultiDay(CalendarEvent item, TimeZoneInfo zone)
        => LastDay(item, zone) > CalendarRules.StartDay(item, zone);

    /// <summary>
    /// [S-2703] A week row's items as bars on lines, so a multi-day event is one bar across its days
    /// rather than a copy in each cell. All-day and multi-day events and birthdays always; single-day
    /// timed events only when <paramref name="singleTimed"/> (the month, where every item is a chip
    /// line; the week lists those in its columns). Packed greedily from the first column, the
    /// longest first, so no two bars share a line on the same day and the fewest lines are used.
    /// </summary>
    public static IReadOnlyList<LaneItem> Bars(
        IEnumerable<CalendarEvent> events,
        IEnumerable<Birthday> birthdays,
        DateOnly first,
        int days,
        TimeZoneInfo zone,
        bool singleTimed)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(birthdays);

        days = Math.Max(1, days);
        var last = first.AddDays(days - 1);
        var spans = new List<(DayItem Item, DateOnly Start, DateOnly End, int Rank)>();

        foreach (var b in birthdays)
        {
            for (var day = first; day <= last; day = day.AddDays(1))
            {
                if (BirthdayRules.OccursOn(b, day))
                {
                    spans.Add((new DayItem(null, b), day, day, 0));
                }
            }
        }

        foreach (var e in events)
        {
            var start = CalendarRules.StartDay(e, zone);
            var end = LastDay(e, zone);
            if (end < first || start > last || (!e.AllDay && !singleTimed && end == start))
            {
                continue;
            }

            spans.Add((new DayItem(e, null), start, end, e.AllDay ? 1 : 2));
        }

        var ordered = spans
            .Select(s => (s.Item, s.Start, s.End, s.Rank, First: Column(s.Start), Last: Column(s.End)))
            .OrderBy(s => s.First)
            .ThenByDescending(s => s.Last - s.First)
            .ThenBy(s => s.Rank)
            .ThenBy(s => s.Item.Event?.Start ?? DateTimeOffset.MinValue)
            .ThenBy(s => s.Item.Event?.Subject ?? s.Item.Birthday?.Name, StringComparer.Ordinal)
            .ToList();

        var laneEnds = new List<int>();
        var result = new List<LaneItem>(ordered.Count);

        foreach (var s in ordered)
        {
            var lane = laneEnds.FindIndex(end => end < s.First);
            if (lane < 0)
            {
                lane = laneEnds.Count;
                laneEnds.Add(s.Last);
            }
            else
            {
                laneEnds[lane] = s.Last;
            }

            result.Add(new LaneItem(s.Item, s.First, s.Last, lane, s.Start < first, s.End > last));
        }

        return result;

        int Column(DateOnly day) => Math.Clamp(day.DayNumber - first.DayNumber, 0, days - 1);
    }

    /// <summary>[S-2703] How many of a column's bars sit on a line past <paramref name="lines"/>: its "+N".</summary>
    public static int Hidden(IReadOnlyList<LaneItem> bars, int column, int lines)
        => bars.Count(b => b.First <= column && column <= b.Last && b.Lane >= lines);

    /// <summary>How many lines a set of bars needs (0 for none).</summary>
    public static int LaneCount(IReadOnlyList<LaneItem> bars) => bars.Count == 0 ? 0 : bars.Max(b => b.Lane) + 1;

    // ---- week ([S-2704]) ---------------------------------------------------------------------------

    /// <summary>[S-2704] A week column this wide gives a timed chip its end time and its location.</summary>
    public const double WeekDetailFrom = 100;

    /// <summary>[S-2704] Below this column width an event is a dot, not a chip with words.</summary>
    public const double WeekChipsFrom = 40;

    /// <summary>[S-2704] The week is an hour grid from this slot height and column width; else a list per day.</summary>
    public const double WeekTimelineFrom = 280;

    /// <summary>
    /// [S-2704] The week for a box: the hour grid on a large tile, else a list per column (dots when
    /// narrow). [S-2719] <paramref name="days"/> columns share the width: fewer days, wider columns.
    /// </summary>
    public static WeekFit Week(double width, double height, int days)
    {
        var columnWidth = Math.Max(0, width) / Math.Max(1, days);
        var timeline = columnWidth >= WeekdayNamesFrom && height >= WeekTimelineFrom;

        // A week column always says which day it is: one letter at the least, over its number.
        var labels = Weekdays(columnWidth);
        if (labels == WeekdayLabel.None)
        {
            labels = WeekdayLabel.Narrow;
        }

        return new WeekFit(labels, timeline, columnWidth < WeekChipsFrom, columnWidth >= WeekDetailFrom);
    }

    // ---- hours ([S-2705]) --------------------------------------------------------------------------

    /// <summary>[S-2705] An hour is never drawn shorter than an event's one line.</summary>
    public static double MinHour => FluentType.Caption.LineHeight + 4;

    /// <summary>[S-2705] Nor taller than a roomy list row: past it the timeline shows more of the day instead.</summary>
    public const double MaxHour = 48;

    /// <summary>
    /// [S-2705] The hours a timeline of <paramref name="available"/> height draws, filling it with whole
    /// hours. The day it wants is 07–22 widened to every timed event on it. When that fits at
    /// <see cref="MinHour"/> or more it is drawn whole, and a tall box widens it further (toward
    /// midnight both ways) rather than stretch an hour past <see cref="MaxHour"/>. When it does not
    /// fit, as many hours as do, placed to start just before <paramref name="focusHour"/> (the next
    /// event, or now) without leaving the wanted day.
    /// </summary>
    public static HourWindow Hours(double available, int earliestHour, int latestHour, int focusHour)
    {
        var first = Math.Clamp(Math.Min(CalendarRules.TimelineFirstHour, earliestHour), 0, 23);
        var end = Math.Clamp(Math.Max(CalendarRules.TimelineEndHour, latestHour), first + 1, 24);

        if (!(available > 0) || !double.IsFinite(available))
        {
            return new HourWindow(first, end - first, MinHour);
        }

        var fit = Math.Clamp(Disclosure.WholeRows(available, MinHour), 1, 24);
        var span = end - first;

        if (fit >= span)
        {
            // Room for the whole day: a tall box shows more of it rather than fatter hours.
            var wanted = Math.Clamp((int)Math.Ceiling(available / MaxHour), span, Math.Min(24, fit));
            // Grow on the side with more of the night left, so the extra hours sit evenly round it.
            while (end - first < wanted)
            {
                if (end < 24 && (24 - end >= first || first == 0))
                {
                    end++;
                }
                else
                {
                    first--;
                }
            }

            return new HourWindow(first, end - first, available / (end - first));
        }

        var start = Math.Clamp(focusHour - 1, first, end - fit);
        return new HourWindow(start, fit, available / fit);
    }

    // ---- year ([S-2707]) ---------------------------------------------------------------------------

    /// <summary>[S-2707] A year's day cell is never smaller than this: past it, fewer months.</summary>
    public const double MinYearCell = 4;

    /// <summary>The month counts a year falls back through, and their arrangements.</summary>
    private static readonly (int Months, (int Columns, int Rows)[] Shapes)[] YearSteps =
    [
        (12, [(4, 3), (3, 4), (6, 2), (2, 6), (12, 1), (1, 12)]),
        (6, [(3, 2), (2, 3), (6, 1), (1, 6)]),
        (4, [(2, 2), (4, 1), (1, 4)]),
        (3, [(3, 1), (1, 3)]),
        (2, [(2, 1), (1, 2)]),
        (1, [(1, 1)]),
    ];

    /// <summary>
    /// [S-2707] The year for a box: twelve small months in whichever of 4×3, 3×4, 6×2, 2×6, 12×1 or 1×12
    /// gives them the largest day cell (the shape decides); where even the best leaves a cell under
    /// <see cref="MinYearCell"/>, six, four, three, two or one month around the current one. A cell
    /// that holds a caption line carries its day number (and a weekday row when that still does);
    /// a smaller one is a square, filled when the day has something.
    /// </summary>
    public static YearFit Year(double width, double height, double gap)
    {
        var title = FluentType.Caption.LineHeight;
        var line = FluentType.Caption.LineHeight;
        var digits = FluentType.EstimateWidth(FluentType.Caption, 2);

        foreach (var (months, shapes) in YearSteps)
        {
            YearFit? best = null;
            foreach (var (columns, rows) in shapes)
            {
                var miniWidth = (Math.Max(0, width) - ((columns - 1) * gap)) / columns;
                var miniHeight = ((Math.Max(0, height) - ((rows - 1) * gap)) / rows) - title;
                var across = miniWidth / 7;

                // Richest first: numbers under a weekday row, numbers alone, squares.
                var withWeekdays = Math.Min(across, miniHeight / 7);
                var plain = Math.Min(across, miniHeight / 6);
                var fit = withWeekdays >= line && across >= digits ? new YearFit(months, columns, rows, withWeekdays, true, true)
                    : plain >= line && across >= digits ? new YearFit(months, columns, rows, plain, true, false)
                    : new YearFit(months, columns, rows, plain, false, false);

                if (best is null || Better(fit, best))
                {
                    best = fit;
                }
            }

            if (best is not null && best.DayCell >= MinYearCell)
            {
                return best;
            }
        }

        return new YearFit(1, 1, 1, MinYearCell, false, false);

        static bool Better(YearFit a, YearFit b)
            => a.Numbers != b.Numbers ? a.Numbers : a.Weekdays != b.Weekdays ? a.Weekdays : a.DayCell > b.DayCell + 0.01;
    }

    /// <summary>
    /// [S-2707] The first of the months a year shows: January for all twelve, else the window of
    /// <paramref name="months"/> around the anchor's month (one before it when there is room), kept
    /// inside the anchor's year so the title stays true.
    /// </summary>
    public static int FirstMonth(DateOnly anchor, int months)
    {
        months = Math.Clamp(months, 1, 12);
        var before = months <= 2 ? 0 : (months - 1) / 2;
        return Math.Clamp(anchor.Month - before, 1, 13 - months);
    }

    /// <summary>
    /// [S-2707] Every day from <paramref name="first"/> to <paramref name="last"/> that has something on
    /// it, with its items in a day's order ([S-570]): one pass over the events rather than one per
    /// day, since a year is 365 days of them.
    /// </summary>
    public static IReadOnlyDictionary<DateOnly, IReadOnlyList<DayItem>> ByDay(
        IEnumerable<CalendarEvent> events,
        IEnumerable<Birthday> birthdays,
        DateOnly first,
        DateOnly last,
        TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(birthdays);

        var byDay = new Dictionary<DateOnly, List<DayItem>>();
        void Add(DateOnly day, DayItem item)
        {
            if (!byDay.TryGetValue(day, out var list))
            {
                byDay[day] = list = [];
            }

            list.Add(item);
        }

        foreach (var b in birthdays)
        {
            for (var year = first.Year; year <= last.Year; year++)
            {
                if (BirthdayRules.OccurrenceIn(b, year) is { } on && on >= first && on <= last)
                {
                    Add(on, new DayItem(null, b));
                }
            }
        }

        foreach (var e in events
            .OrderBy(e => e.AllDay ? 0 : 1)
            .ThenBy(e => e.Start)
            .ThenBy(e => e.Subject, StringComparer.Ordinal))
        {
            var from = CalendarRules.StartDay(e, zone);
            var to = LastDay(e, zone);
            for (var day = from < first ? first : from; day <= to && day <= last; day = day.AddDays(1))
            {
                Add(day, new DayItem(e, null));
            }
        }

        return byDay.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<DayItem>)kv.Value);
    }

    /// <summary>[S-2707] The day cells of one small month: its weeks' days, null outside the month, six rows.</summary>
    public static IReadOnlyList<DateOnly?> MiniMonth(int year, int month, WeekStartDay start)
    {
        var first = new DateOnly(year, month, 1);
        var gridStart = CalendarDays.StartOfWeek(first, start);
        var cells = new DateOnly?[42];
        for (var i = 0; i < 42; i++)
        {
            var day = gridStart.AddDays(i);
            cells[i] = day.Month == month && day.Year == year ? day : null;
        }

        return cells;
    }

    // ---- day ([S-1266]) ----------------------------------------------------------------------------

    /// <summary>[S-2705] The hour a timeline opens at: the next event still to end today, else the first; else now or the morning.</summary>
    public static int FocusHour(IEnumerable<CalendarEvent> timed, DateOnly day, DateOnly today, DateTimeOffset now, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(timed);
        var list = timed.Where(e => !e.AllDay).OrderBy(e => e.Start).ToList();
        // Today: the next event still to end, else the time now (the day's events are behind it).
        var pick = day == today ? list.FirstOrDefault(e => e.End > now) : list.FirstOrDefault();

        if (pick is not null)
        {
            var start = TimeZoneInfo.ConvertTime(pick.Start, zone);
            return DateOnly.FromDateTime(start.DateTime) < day ? 0 : start.Hour;
        }

        return day == today ? TimeZoneInfo.ConvertTime(now, zone).Hour : CalendarRules.TimelineFirstHour + 1;
    }

    /// <summary>[S-2705] The earliest and latest hour a day's timed events need on its timeline.</summary>
    public static (int Earliest, int Latest) EventHours(IEnumerable<CalendarEvent> timed, DateOnly day, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(timed);
        var earliest = 24;
        var latest = 0;
        var dayStart = CalendarDays.LocalMidnight(day, zone);
        var dayEnd = CalendarDays.LocalMidnight(day.AddDays(1), zone);

        foreach (var e in timed.Where(e => !e.AllDay))
        {
            var from = e.Start < dayStart ? 0 : TimeZoneInfo.ConvertTime(e.Start, zone).Hour;

            // The hour the event ends in counts whole; one with no length needs its starting hour.
            int to;
            if (e.End >= dayEnd)
            {
                to = 24;
            }
            else if (e.End <= e.Start)
            {
                to = from + 1;
            }
            else
            {
                var end = TimeZoneInfo.ConvertTime(e.End, zone);
                to = end.Hour + (end.Minute > 0 || end.Second > 0 ? 1 : 0);
            }

            earliest = Math.Min(earliest, from);
            latest = Math.Max(latest, Math.Min(24, to));
        }

        return (earliest, latest);
    }

    /// <summary>
    /// [S-2705] Where a timed event sits in an hour window, as fractions of its height: the top and the
    /// height, at least a quarter of an hour's line, clipped to the window. Null when it is wholly outside.
    /// </summary>
    public static (double Top, double Height)? Place(DateTimeOffset start, DateTimeOffset end, DateOnly day, TimeZoneInfo zone, HourWindow window)
    {
        var origin = CalendarDays.LocalMidnight(day, zone).AddHours(window.First);
        var span = window.Count * 60.0;
        var from = (start - origin).TotalMinutes;

        // At least half an hour tall, so a short meeting still holds its title.
        var to = Math.Max(end > start ? (end - origin).TotalMinutes : from, from + 30);

        if (to <= 0 || from >= span)
        {
            return null;
        }

        var top = Math.Clamp(from, 0, span) / span;
        var bottom = Math.Clamp(to, 0, span) / span;

        // An event clipped to a sliver at the window's edge keeps a quarter hour's height.
        var height = Math.Max(bottom - top, 0.25 / window.Count);
        return (Math.Min(top, 1 - height), height);
    }

    // ---- the agenda: the kit's paged schedule ([S-7035], [S-7036]: KitPages, KitDays, KitLadder) ------

    // ---- wrapped titles ([S-2717]) -----------------------------------------------------------------

    /// <summary>
    /// [S-2717] What a timed block of <paramref name="lines"/> whole caption lines shows, in priority
    /// order: the title; its time; the title's second line when it wraps; then where and who ([S-1266]).
    /// </summary>
    public static (int TitleLines, bool Time, bool Meta) BlockParts(int lines, bool titleWraps, bool hasMeta)
    {
        var room = Math.Max(1, lines) - 1;
        var time = room > 0;
        room -= time ? 1 : 0;
        var title = titleWraps && room > 0 ? Themia.Sdk.Kit.KitPages.MaxTitleLines : 1;
        room -= title - 1;
        return (title, time, hasMeta && room > 0);
    }

    // ---- the day's all-day band ([S-2718]) ---------------------------------------------------------

    /// <summary>[S-2718] At most this many span bars over a day's timeline; the rest share the last line.</summary>
    public const int DayBandMaxBars = 3;

    /// <summary>[S-2718] The timeline under the band keeps at least this many hours.</summary>
    public const int DayBandKeepsHours = 3;

    /// <summary>[S-2718] A span bar: a body line with room around it, so it reads as a bar, not a chip.</summary>
    public static double DayBar => FluentType.Body.LineHeight + 8;

    /// <summary>
    /// [S-2718] The day's all-day band for <paramref name="items"/> all-day and multi-day items over a
    /// slot of <paramref name="height"/>: one full-width bar per item, as many as fit (up to three)
    /// while the timeline keeps <see cref="DayBandKeepsHours"/> hours; when items are left over, the
    /// last line is shared among them ([S-2706]). A box too short for one bar falls back to that one
    /// shared chip line over an hour; too short even for that, no band (the items are on the hover).
    /// </summary>
    public static DayBandFit DayBand(double height, int items)
    {
        if (items <= 0)
        {
            return new DayBandFit(0, 0, false);
        }

        var room = new Disclosure(1, height);
        room.Reserve(DayBandKeepsHours * MinHour);

        var bars = 0;
        while (bars < Math.Min(items, DayBandMaxBars) && room.Take(DayBar))
        {
            bars++;
        }

        if (bars > 0)
        {
            return new DayBandFit(bars, DayBar, bars < items);
        }

        var chips = new Disclosure(1, height);
        chips.Reserve(MinHour);
        return chips.Take(ChipLine) ? new DayBandFit(1, ChipLine, true) : new DayBandFit(0, 0, false);
    }

    // ---- keys ([S-2710]) ---------------------------------------------------------------------------

    /// <summary>
    /// [S-2710] One key map for every layout: Left / Right and Page Up / Page Down step the layout's
    /// own unit (a month, a week, a day, a year, an agenda page); Up / Down move the cursor day a week
    /// in the month and week; Home goes to today; Enter opens the cursor day's first event (the
    /// page's first in the agenda). A year has no events to open.
    /// </summary>
    public static CalendarKeyCommand Key(WidgetKey key, CalendarStyle layout)
    {
        var weeks = layout is CalendarStyle.Month or CalendarStyle.Week;

        return key switch
        {
            WidgetKey.Left or WidgetKey.PageUp => CalendarKeyCommand.Back,
            WidgetKey.Right or WidgetKey.PageDown => CalendarKeyCommand.Forward,
            WidgetKey.Up when weeks => CalendarKeyCommand.WeekBack,
            WidgetKey.Down when weeks => CalendarKeyCommand.WeekForward,
            WidgetKey.Home => CalendarKeyCommand.Today,
            WidgetKey.Enter when layout != CalendarStyle.Year => CalendarKeyCommand.OpenFirst,
            _ => CalendarKeyCommand.None,
        };
    }
}
