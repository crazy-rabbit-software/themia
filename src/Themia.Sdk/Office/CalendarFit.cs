using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;

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
/// [S-2702] What a month grid of a given box draws: the week rows it shows (<c>Rows</c> of them from
/// <c>FirstRow</c>: every week of the month, or on a box too short for them the weeks around the
/// focused one), the weekday header, the cell height, how the cells show their days ([S-572]), how
/// many chip lines a cell holds, and whether a chip leads with its start time. A cell too short for
/// dots under its number but wide enough for them beside it (a wide, short tile) puts them there
/// (<c>InlineDots</c>).
/// </summary>
public sealed record MonthFit(int Rows, WeekdayLabel Weekdays, double HeaderHeight, double CellHeight, MonthDensity Density, int ChipLines, double ChipLine, bool Times, bool InlineDots)
{
    /// <summary>The first week row shown (0 unless the box holds fewer rows than the month has).</summary>
    public int FirstRow { get; init; }
}

/// <summary>
/// [S-2703] One item of a week row as a bar: the columns it covers (clipped to the row), the line
/// it sits on, and whether it runs on past either end of the row.
/// </summary>
public sealed record LaneItem(DayItem Item, int First, int Last, int Lane, bool ContinuesBefore, bool ContinuesAfter);

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

    /// <summary>[S-1264] A chip's line: the chip (a Caption line) and the family's 4 under it.</summary>
    public static double ChipLine => FamilyType.Caption.LineHeight + FamilySpace.S1;

    /// <summary>A month cell's padding over its number (the family's 4), which chips cannot use.</summary>
    public const double MonthCellChrome = FamilySpace.S1;

    /// <summary>[S-572] A day's dot: the track's thickness, 4 from its neighbour and from the number.</summary>
    public const double Dot = FamilyShape.Track;

    /// <summary>[S-2702] The weekday label a column of this width holds.</summary>
    public static WeekdayLabel Weekdays(double columnWidth)
        => columnWidth >= WeekdayNamesFrom ? WeekdayLabel.Short
            : columnWidth >= WeekdayInitialsFrom ? WeekdayLabel.Initials
            : columnWidth >= WeekdayNarrowFrom ? WeekdayLabel.Narrow
            : WeekdayLabel.None;

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

    /// <summary>
    /// [S-2281] A week column this wide holds its events' titles (chips, or the hour grid); a narrower
    /// one holds marks, and the chosen day's events are rows under the week.
    /// </summary>
    public const double WeekTitlesFrom = 96;

    /// <summary>[S-2282] A narrow week's day shows at most this many marks; the rest is "+n".</summary>
    public const int WeekMarks = 3;

    /// <summary>[S-2282] A day's marks: one per item up to <see cref="WeekMarks"/>, and how many are left for its "+n".</summary>
    public static (int Shown, int More) Marks(int items)
        => (Math.Clamp(items, 0, WeekMarks), Math.Max(0, items - WeekMarks));

    /// <summary>
    /// [S-2282] The height the week's marks take under the day numbers: the most marks a day shows
    /// (a track each, the family's smallest space apart), and a Caption line for "+n" when a day has more.
    /// </summary>
    /// <param name="counts">How many items each day of the week has.</param>
    public static double MarksHeight(IEnumerable<int> counts)
    {
        ArgumentNullException.ThrowIfNull(counts);
        var shown = 0;
        var more = false;
        foreach (var count in counts)
        {
            var (s, m) = Marks(count);
            shown = Math.Max(shown, s);
            more |= m > 0;
        }

        if (shown == 0)
        {
            return 0;
        }

        return (shown * FamilyShape.Track) + ((shown - 1) * FamilySpace.S1) + (more ? FamilySpace.S1 + FamilyType.Caption.LineHeight : 0);
    }

    /// <summary>
    /// [S-2284] The narrow week's chosen day: the selected day while it is in the week, else today
    /// while the week holds it, else the week's first day.
    /// </summary>
    public static DateOnly ChosenDay(DateOnly? selected, DateOnly first, int days, DateOnly today)
    {
        bool InWeek(DateOnly day) => day >= first && day.DayNumber < first.DayNumber + Math.Max(1, days);
        return selected is { } s && InWeek(s) ? s : InWeek(today) ? today : first;
    }

    // ---- hours ([S-2705]) --------------------------------------------------------------------------

    /// <summary>[S-2705] An hour is never drawn shorter than an event's one line.</summary>
    public static double MinHour => FamilyType.Caption.LineHeight + 4;

    /// <summary>[S-2705] Nor taller than a roomy list row: past it the timeline shows more of the day instead.</summary>
    public const double MaxHour = 48;

    /// <summary>
    /// [S-11543] The window of <paramref name="count"/> hours a timeline the config sized draws, each
    /// <paramref name="available"/> / count tall: the wanted day (07–22 widened to every timed event)
    /// when it is no longer, grown evenly toward midnight where the count is longer; else the count's
    /// hours from just before <paramref name="focusHour"/> (the next event, or now) without leaving it.
    /// </summary>
    public static HourWindow HoursOf(int count, double available, int earliestHour, int latestHour, int focusHour)
    {
        var hours = Math.Clamp(count, 1, 24);
        var first = Math.Clamp(Math.Min(CalendarRules.TimelineFirstHour, earliestHour), 0, 23);
        var end = Math.Clamp(Math.Max(CalendarRules.TimelineEndHour, latestHour), first + 1, 24);
        var height = available > 0 && double.IsFinite(available) ? available / hours : MinHour;

        if (hours >= end - first)
        {
            while (end - first < hours)
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

            return new HourWindow(first, hours, height);
        }

        return new HourWindow(Math.Clamp(focusHour - 1, first, end - hours), hours, height);
    }

    // ---- year ([S-2707]) ---------------------------------------------------------------------------

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

    // ---- the agenda: the kit's paged schedule ([S-7035], [S-7036]: KitPages, KitDays, KitPlanner) ------

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

    /// <summary>[S-2718] A span bar: a body line with room around it, so it reads as a bar, not a chip.</summary>
    public static double DayBar => FamilyType.Body.LineHeight + 8;

    // ---- keys ([S-2710]) ---------------------------------------------------------------------------

    /// <summary>
    /// [S-2710] One key map for every layout: Left / Right and Page Up / Page Down step the layout's
    /// own unit (a month, a week, a day, a year, an agenda page); Up / Down move the cursor day a week
    /// in the month and week; Home goes to today; Enter opens the cursor day's first event. A year has
    /// no events to open; the agenda's cursor keys are its kit list's ([S-3652]).
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
            // [S-3652] The agenda's Enter is its kit list's: the row the cursor is on.
            WidgetKey.Enter when layout is not (CalendarStyle.Year or CalendarStyle.Agenda) => CalendarKeyCommand.OpenFirst,
            _ => CalendarKeyCommand.None,
        };
    }
}
