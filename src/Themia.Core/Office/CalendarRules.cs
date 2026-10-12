using System.Globalization;
using Themia.Core.Localization;
using Themia.Core.Model;

namespace Themia.Core.Office;

/// <summary>
/// The calendar widget's arithmetic, kept out of the view so it can be pinned against a fixed day
/// and a fixed zone: which calendars to ask, what range a view covers, which events fall on a day,
/// how overlapping events share a column, and which colour a calendar gets.
/// <para>
/// Every method takes "today" and the zone it needs as arguments. Nothing here reads the clock or
/// <see cref="TimeZoneInfo.Local"/>, which is what lets the tests run at any date on any machine.
/// </para>
/// </summary>
public static class CalendarRules
{
    /// <summary>
    /// [S-566] The fixed six-colour palette a calendar gets by its target index when the user has not
    /// picked one. Data colours, documented in SPEC, not the design accent: they tell calendars apart
    /// and must stay the same whatever the theme.
    /// </summary>
    public static IReadOnlyList<string> Palette { get; } = ["#60cdff", "#ff9660", "#a8e6a3", "#ffcc70", "#c8b3f7", "#ff8bab"];

    /// <summary>What the settings page calls each palette colour, in palette order.</summary>
    public static IReadOnlyList<Str> PaletteNames { get; } =
    [
        Str.WidgetsA_Calendar_Color_Blue,
        Str.WidgetsA_Calendar_Color_Orange,
        Str.WidgetsA_Calendar_Color_Green,
        Str.WidgetsA_Calendar_Color_Yellow,
        Str.WidgetsA_Calendar_Color_Purple,
        Str.WidgetsA_Calendar_Color_Pink,
    ];

    /// <summary>The agenda looks this many days ahead, as the original did.</summary>
    public const int AgendaDays = 30;

    // ---- colours -----------------------------------------------------------------------------

    /// <summary>The palette colour for a target index, wrapping past six and never negative.</summary>
    public static string PaletteColor(int index)
    {
        var n = Palette.Count;
        return Palette[((index % n) + n) % n];
    }

    /// <summary>
    /// [S-566] A colour as it may be used: <c>#rrggbb</c> kept (lower-cased), Apple's
    /// <c>#rrggbbaa</c> cut to <c>#rrggbb</c>, and Graph's <c>""</c> / <c>"auto"</c> or anything else
    /// null, so the palette takes over. Remote and persisted colour strings are untrusted.
    /// </summary>
    public static string? NormalizeColor(string? color)
    {
        var s = color?.Trim();
        if (s is not { Length: 7 or 9 } || s[0] != '#')
        {
            return null;
        }

        for (var i = 1; i < s.Length; i++)
        {
            if (!char.IsAsciiHexDigit(s[i]))
            {
                return null;
            }
        }

        return s[..7].ToLowerInvariant();
    }

    /// <summary>True only for a well-formed <c>#rrggbb</c>.</summary>
    public static bool IsHexColor(string? color)
        => color is { Length: 7 } && color[0] == '#' && color.AsSpan(1).ContainsAnyExcept("0123456789abcdefABCDEF") == false;

    /// <summary>
    /// [S-566] The key a calendar's colour is stored under: its own id when a specific calendar was
    /// chosen, else the account id — so a single-calendar user's colour stays put.
    /// </summary>
    public static string ColorKey(string accountId, string? calendarId)
        => string.IsNullOrWhiteSpace(calendarId) ? accountId : calendarId;

    /// <summary>[S-566] A valid override for the key wins; anything else falls back to the palette.</summary>
    public static string ResolveColor(string key, int index, IReadOnlyDictionary<string, string>? overrides)
        => overrides is not null && overrides.TryGetValue(key, out var chosen) && IsHexColor(chosen)
            ? chosen.ToLowerInvariant()
            : PaletteColor(index);

    /// <summary>
    /// [S-566] Whether chips carry their calendar's colour: when more than one calendar is shown, or
    /// when the user picked a colour for one that is.
    /// </summary>
    public static bool ShowColors(IReadOnlyList<CalendarTarget> targets, IReadOnlyDictionary<string, string>? overrides)
    {
        if (targets.Count > 1)
        {
            return true;
        }

        return overrides is not null
            && targets.Any(t => overrides.TryGetValue(ColorKey(t.AccountId, t.CalendarId), out var c) && IsHexColor(c));
    }

    // ---- targets -----------------------------------------------------------------------------

    /// <summary>
    /// [S-567] The (account, calendar) pairs to fetch: one per selected calendar, or one default
    /// target for an account with no selection. Blank and duplicate ids from the (untrusted) config
    /// are dropped.
    /// </summary>
    public static IReadOnlyList<CalendarTarget> Targets(
        IEnumerable<string> accountIds,
        IReadOnlyDictionary<string, List<string>>? calendars)
    {
        var targets = new List<CalendarTarget>();

        foreach (var accountId in accountIds)
        {
            var chosen = calendars is not null && calendars.TryGetValue(accountId, out var list) && list is not null
                ? list.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList()
                : [];

            if (chosen.Count == 0)
            {
                targets.Add(new CalendarTarget(accountId, null));
                continue;
            }

            foreach (var calendarId in chosen)
            {
                targets.Add(new CalendarTarget(accountId, calendarId));
            }
        }

        return targets;
    }

    /// <summary>
    /// [S-562] The accounts a widget shows: the ones it chose that still exist, or every account
    /// that can feed it when it chose none. Order follows the available list, so colours are stable.
    /// </summary>
    public static IReadOnlyList<string> SelectAccounts(IReadOnlyList<string> chosen, IReadOnlyList<string> available)
        => chosen.Count == 0
            ? available
            : available.Where(id => chosen.Contains(id, StringComparer.Ordinal)).ToList();

    /// <summary>
    /// [S-567] The calendar list after toggling one calendar of an account. Empty means "default",
    /// so deselecting the last one drops the key and the account goes back to its default calendar.
    /// </summary>
    public static Dictionary<string, List<string>> ToggleCalendar(
        IReadOnlyDictionary<string, List<string>> current,
        string accountId,
        string calendarId,
        IReadOnlyList<CalendarInfo> available)
    {
        var next = current.ToDictionary(kv => kv.Key, kv => kv.Value.ToList(), StringComparer.Ordinal);

        var selection = next.TryGetValue(accountId, out var explicitList) && explicitList.Count > 0
            ? explicitList
            : available.Where(c => c.IsDefault).Select(c => c.Id).ToList();

        selection = selection.Contains(calendarId, StringComparer.Ordinal)
            ? selection.Where(id => id != calendarId).ToList()
            : [.. selection, calendarId];

        if (selection.Count == 0)
        {
            next.Remove(accountId);
        }
        else
        {
            next[accountId] = selection;
        }

        return next;
    }

    /// <summary>Whether a calendar is shown: on the explicit list, or the default when there is none.</summary>
    public static bool IsCalendarShown(IReadOnlyDictionary<string, List<string>> current, string accountId, CalendarInfo calendar)
        => current.TryGetValue(accountId, out var list) && list.Count > 0
            ? list.Contains(calendar.Id, StringComparer.Ordinal)
            : calendar.IsDefault;

    // ---- weeks -------------------------------------------------------------------------------

    /// <summary>[S-564] The weekday a week starting on <paramref name="start"/> begins with.</summary>
    public static DayOfWeek FirstDay(WeekStartDay start) => start switch
    {
        WeekStartDay.Sunday => DayOfWeek.Sunday,
        WeekStartDay.Saturday => DayOfWeek.Saturday,
        _ => DayOfWeek.Monday,
    };

    /// <summary>[S-564] The week start a language or Windows names by its first weekday; a day no calendar starts on is Monday.</summary>
    public static WeekStartDay WeekStartOf(DayOfWeek first) => first switch
    {
        DayOfWeek.Sunday => WeekStartDay.Sunday,
        DayOfWeek.Saturday => WeekStartDay.Saturday,
        _ => WeekStartDay.Monday,
    };

    /// <summary>[S-564] Days to step back from <paramref name="day"/> to reach the first day of its week.</summary>
    public static int WeekStartOffset(DayOfWeek day, WeekStartDay start)
        => ((int)day - (int)FirstDay(start) + 7) % 7;

    public static DateOnly StartOfWeek(DateOnly day, WeekStartDay start) => day.AddDays(-WeekStartOffset(day.DayOfWeek, start));

    /// <summary>[S-564] The seven weekdays in column order: Mon..Sun, Sun..Sat or Sat..Fri.</summary>
    public static IReadOnlyList<DayOfWeek> WeekdayOrder(WeekStartDay start)
    {
        var first = (int)FirstDay(start);
        return [.. Enumerable.Range(0, 7).Select(i => (DayOfWeek)((first + i) % 7))];
    }

    // ---- ranges and navigation -----------------------------------------------------------------

    /// <summary>
    /// [S-568] The days a layout covers, both inclusive, which is what is fetched. Month: whole weeks
    /// from the week holding the 1st to the week holding the last day. Week: the week holding the
    /// anchor. Day: the anchor. Year ([S-2707]): 1 January to 31 December of the anchor's year.
    /// Agenda: today and the next <see cref="AgendaDays"/> days, wherever the anchor is.
    /// </summary>
    public static (DateOnly First, DateOnly Last) ViewRange(CalendarLayout view, DateOnly anchor, DateOnly today, WeekStartDay start)
    {
        switch (view)
        {
            case CalendarLayout.Month:
                var firstOfMonth = new DateOnly(anchor.Year, anchor.Month, 1);
                var lastOfMonth = firstOfMonth.AddMonths(1).AddDays(-1);
                return (StartOfWeek(firstOfMonth, start), StartOfWeek(lastOfMonth, start).AddDays(6));

            case CalendarLayout.Week:
                var weekStart = StartOfWeek(anchor, start);
                return (weekStart, weekStart.AddDays(6));

            case CalendarLayout.Day:
                return (anchor, anchor);

            case CalendarLayout.Year:
                return (new DateOnly(anchor.Year, 1, 1), new DateOnly(anchor.Year, 12, 31));

            default:
                return (today, today.AddDays(AgendaDays));
        }
    }

    /// <summary>The 42 days of a month grid, from the start of the first week.</summary>
    public static IReadOnlyList<DateOnly> MonthCells(DateOnly anchor, WeekStartDay start)
    {
        var first = StartOfWeek(new DateOnly(anchor.Year, anchor.Month, 1), start);
        return [.. Enumerable.Range(0, 42).Select(first.AddDays)];
    }

    /// <summary>
    /// [S-569] The anchor one step back or forward: a month (the day clamped to the shorter month),
    /// a week, a day or a year (29 February clamped to the 28th). The agenda pages through its rows
    /// ([S-2709]), not its dates, so its anchor does not move.
    /// </summary>
    public static DateOnly Step(CalendarLayout view, DateOnly anchor, int direction) => view switch
    {
        CalendarLayout.Month => anchor.AddMonths(Math.Sign(direction)),
        CalendarLayout.Week => anchor.AddDays(7 * Math.Sign(direction)),
        CalendarLayout.Day => anchor.AddDays(Math.Sign(direction)),
        CalendarLayout.Year => anchor.AddYears(Math.Sign(direction)),
        _ => anchor,
    };

    /// <summary>
    /// [S-569] Whether a day is in the period on screen, so the today button hides when it would do
    /// nothing. A month counts only its own days, not the neighbours' that pad its weeks; the agenda
    /// always holds today (its paging is by rows, [S-2709]).
    /// </summary>
    public static bool InView(CalendarLayout view, DateOnly anchor, DateOnly today, WeekStartDay start, DateOnly day)
    {
        if (view == CalendarLayout.Month)
        {
            return day.Year == anchor.Year && day.Month == anchor.Month;
        }

        var (first, last) = ViewRange(view, anchor, today, start);
        return day >= first && day <= last;
    }

    /// <summary>
    /// [S-568] The instants to ask the provider for: local midnight of the first day to local
    /// midnight after the last, in <paramref name="zone"/>. The original asked from UTC midnight,
    /// which in any zone east of UTC dropped the first hours of the first day.
    /// </summary>
    public static (DateTimeOffset From, DateTimeOffset To) FetchWindow(DateOnly first, DateOnly last, TimeZoneInfo zone)
        => (LocalMidnight(first, zone), LocalMidnight(last.AddDays(1), zone));

    /// <summary>Midnight of <paramref name="day"/> in <paramref name="zone"/>, never throwing in a DST gap.</summary>
    public static DateTimeOffset LocalMidnight(DateOnly day, TimeZoneInfo zone)
    {
        var clock = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return new DateTimeOffset(clock, zone.GetUtcOffset(clock));
    }

    /// <summary>
    /// [S-568] The period's title: <c>October 2026</c>, <c>Sep 28 – Oct 4</c>, <c>Sun, Oct 4</c>,
    /// <c>2026</c>. Agenda has none. Names and order from <see cref="LocaleFormat"/> for
    /// <paramref name="locale"/> ([S-1106]).
    /// </summary>
    public static string Title(CalendarLayout view, DateOnly anchor, WeekStartDay start, Locale locale)
    {
        switch (view)
        {
            case CalendarLayout.Month:
                return LocaleFormat.Date(locale, anchor, DateStyle.MonthYear);

            case CalendarLayout.Week:
                var first = StartOfWeek(anchor, start);
                return Range(first, first.AddDays(6), locale);

            case CalendarLayout.Day:
                return LocaleFormat.Date(locale, anchor, DateStyle.WeekdayMonthDayShort);

            case CalendarLayout.Year:
                return Strings.Format(locale, Str.WidgetsA_Calendar_YearTitle, ("year", anchor.Year.ToString(CultureInfo.InvariantCulture)));

            default:
                return string.Empty;
        }
    }

    /// <summary>"Sep 28 – Oct 4": two short dates as one span, in the language's own wording.</summary>
    public static string Range(DateOnly first, DateOnly last, Locale locale)
        => Strings.Format(locale, Str.WidgetsA_Calendar_Range,
            ("from", LocaleFormat.Date(locale, first, DateStyle.MonthDayShort)),
            ("to", LocaleFormat.Date(locale, last, DateStyle.MonthDayShort)));

    // ---- what falls on a day -----------------------------------------------------------------

    /// <summary>
    /// [S-570] Whether an event shows on a day. All-day: the half-open date span [first, end) — the
    /// exclusive end is never shortened, or a one-day event would vanish. Timed: it overlaps the
    /// local day; an event with no length shows on the day it starts.
    /// </summary>
    public static bool OccursOn(CalendarEvent item, DateOnly day, TimeZoneInfo zone)
    {
        if (item.AllDay)
        {
            return item.FirstDay <= day && day < item.EndDay;
        }

        var dayStart = LocalMidnight(day, zone);
        var dayEnd = LocalMidnight(day.AddDays(1), zone);

        if (item.End <= item.Start)
        {
            return item.Start >= dayStart && item.Start < dayEnd;
        }

        return item.Start < dayEnd && item.End > dayStart;
    }

    /// <summary>The local day a timed event starts on, or the first day of an all-day one.</summary>
    public static DateOnly StartDay(CalendarEvent item, TimeZoneInfo zone)
        => item.AllDay ? item.FirstDay : DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(item.Start, zone).DateTime);

    /// <summary>
    /// [S-570] One day's items: birthdays first, then all-day events, then timed events by start,
    /// ties by subject so the order cannot flicker between renders.
    /// </summary>
    public static IReadOnlyList<DayItem> DayItems(
        IEnumerable<CalendarEvent> events,
        IEnumerable<Birthday> birthdays,
        DateOnly day,
        TimeZoneInfo zone)
    {
        var items = new List<DayItem>();
        items.AddRange(birthdays.Where(b => BirthdayRules.OccursOn(b, day)).Select(b => new DayItem(null, b)));

        items.AddRange(events
            .Where(e => OccursOn(e, day, zone))
            .OrderBy(e => e.AllDay ? 0 : 1)
            .ThenBy(e => e.Start)
            .ThenBy(e => e.Subject, StringComparer.Ordinal)
            .Select(e => new DayItem(e, null)));

        return items;
    }

    /// <summary>
    /// [S-571] The agenda: events and birthday occurrences from <paramref name="first"/> to
    /// <paramref name="last"/>, grouped by day in order, at most <paramref name="limit"/> entries in
    /// all. An event that began before the range and is still running is filed under the first day
    /// rather than under a heading that has gone by ([S-185]). A timed event that has already ended
    /// by <paramref name="now"/> is not "coming up" and is left out, so the morning's meetings do
    /// not crowd the afternoon's off a small tile; all-day events stay for their whole day.
    /// </summary>
    public static IReadOnlyList<(DateOnly Day, IReadOnlyList<DayItem> Items)> Agenda(
        IEnumerable<CalendarEvent> events,
        IEnumerable<Birthday> birthdays,
        DateOnly first,
        DateOnly last,
        TimeZoneInfo zone,
        int limit,
        DateTimeOffset now)
    {
        events = events.Where(e => e.AllDay || (e.End > e.Start ? e.End > now : e.Start >= now));

        var byDay = new SortedDictionary<DateOnly, List<DayItem>>();

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
            // Under every day of the range it covers (classic issue #155 listed a Monday-to-Thursday
            // event on the Monday only); one that started earlier begins at the first day.
            var day = StartDay(e, zone);
            if (day < first)
            {
                // Started earlier: only worth listing while it still runs into the range.
                if (!OccursOn(e, first, zone))
                {
                    continue;
                }

                day = first;
            }

            var item = new DayItem(e, null);
            for (var on = day; on <= last && (on == day || OccursOn(e, on, zone)); on = on.AddDays(1))
            {
                Add(on, item);
            }
        }

        var groups = new List<(DateOnly, IReadOnlyList<DayItem>)>();
        var count = 0;

        foreach (var (day, items) in byDay)
        {
            if (count >= limit)
            {
                break;
            }

            // Birthdays lead each day, as in a day cell.
            var ordered = items.Where(i => i.IsBirthday).Concat(items.Where(i => !i.IsBirthday)).Take(limit - count).ToList();
            groups.Add((day, ordered));
            count += ordered.Count;
        }

        return groups;
    }

    /// <summary>
    /// [S-571] The part of an event an agenda row on <paramref name="day"/> shows: the event cut to
    /// that local day, so a timed event running over several days reads 18:00–00:00, then all day,
    /// then 00:00–10:00 rather than repeating its first start under every heading. All-day events
    /// and timed ones covering the whole day are <see cref="AgendaTime.WholeDay"/>.
    /// </summary>
    public static AgendaTime AgendaSpan(CalendarEvent item, DateOnly day, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (item.AllDay)
        {
            return new AgendaTime(item.Start, item.End, WholeDay: true);
        }

        var dayStart = LocalMidnight(day, zone);
        var dayEnd = LocalMidnight(day.AddDays(1), zone);
        var start = item.Start > dayStart ? item.Start : dayStart;
        var end = item.End > item.Start && item.End > dayEnd ? dayEnd : item.End;

        return new AgendaTime(start, end, item.Start <= dayStart && item.End >= dayEnd);
    }

    // ---- layout ------------------------------------------------------------------------------

    /// <summary>
    /// [S-572] How many chips fit in a month cell of a given height: what is left under the day
    /// number, in whole chips, at least one and at most six (past which a cell is noise).
    /// </summary>
    public static int ChipCapacity(double cellHeight, double numberHeight, double chipHeight)
    {
        if (!double.IsFinite(cellHeight) || cellHeight <= 0 || chipHeight <= 0)
        {
            return 2;
        }

        var fit = (int)Math.Floor((cellHeight - numberHeight) / chipHeight);
        return Math.Clamp(fit, 1, 6);
    }

    /// <summary>
    /// [S-573] Side-by-side lanes for overlapping timed events in the day view, so none hides
    /// another. Each run of mutually-overlapping events shares one column count; touching events
    /// (one ends as the next starts) do not overlap. Results are in input order.
    /// </summary>
    public static IReadOnlyList<(int Column, int Columns)> Lanes(IReadOnlyList<(long Start, long End)> spans)
    {
        var n = spans.Count;
        var result = new (int, int)[n];
        var order = Enumerable.Range(0, n).OrderBy(i => spans[i].Start).ThenBy(i => spans[i].End).ToList();

        var group = new List<int>();
        var columnEnds = new List<long>();
        var columnOf = new int[n];
        var groupEnd = long.MinValue;

        void Flush()
        {
            foreach (var i in group)
            {
                result[i] = (columnOf[i], columnEnds.Count);
            }

            group.Clear();
            columnEnds.Clear();
            groupEnd = long.MinValue;
        }

        foreach (var i in order)
        {
            var (start, end) = spans[i];

            if (group.Count > 0 && start >= groupEnd)
            {
                Flush();
            }

            var column = columnEnds.FindIndex(e => e <= start);
            if (column < 0)
            {
                column = columnEnds.Count;
                columnEnds.Add(end);
            }
            else
            {
                columnEnds[column] = end;
            }

            columnOf[i] = column;
            group.Add(i);
            groupEnd = Math.Max(groupEnd, end);
        }

        Flush();
        return result;
    }

    /// <summary>The first and last hour the day timeline draws (7:00 to 22:00, as the original).</summary>
    public const int TimelineFirstHour = 7;

    public const int TimelineEndHour = 22;

    /// <summary>[S-572] How a month cell draws its events at a given cell size.</summary>
    public static MonthDensity MonthCellDensity(double cellWidth, double cellHeight)
        => cellWidth >= 40 && cellHeight >= 34 ? MonthDensity.Chips
            : cellHeight >= 24 ? MonthDensity.Dots
            : MonthDensity.Number;

    // ---- provider datetimes ------------------------------------------------------------------

    /// <summary>
    /// [S-565] A provider datetime: a string with a zone (<c>Z</c>, <c>±HH:MM</c> or <c>±HHMM</c>,
    /// including the negative offsets Google sends for the Americas) is read as given; a zoneless
    /// one is UTC (Graph with <c>Prefer: outlook.timezone="UTC"</c>). Never appends a <c>Z</c> to a
    /// string that already has an offset. Null when it is not a datetime at all.
    /// </summary>
    public static DateTimeOffset? ParseDateTime(string? text)
    {
        var s = text?.Trim();
        if (string.IsNullOrEmpty(s))
        {
            return null;
        }

        // A colon-less offset ("+0200") is not something DateTimeOffset reads; give it its colon.
        var t = s.IndexOf('T', StringComparison.Ordinal);
        if (t > 0 && s.Length >= t + 6)
        {
            var tail = s[^5..];
            if (tail[0] is '+' or '-' && tail.AsSpan(1).ContainsAnyExcept("0123456789") == false && s.Length - 5 > t)
            {
                s = $"{s[..^5]}{tail[..3]}:{tail[3..]}";
            }
        }

        return DateTimeOffset.TryParse(
            s,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AllowWhiteSpaces,
            out var parsed)
            ? parsed
            : null;
    }

    /// <summary>
    /// [S-3611] An all-day event's date as the provider wrote it, never through a zone: a bare date,
    /// or the date of a midnight. A date-time that is not a midnight is the event's zone's midnight
    /// written in another zone (Graph does so when asked for UTC and the event was made elsewhere:
    /// 1 Oct in Zurich arrives as 30 Sep 22:00): it means the nearest midnight, so the clock is
    /// rounded to its nearest whole day. Null when it is not a date at all.
    /// </summary>
    public static DateOnly? ParseAllDayDate(string? text)
    {
        if (ParseDate(text) is not { } day)
        {
            return null;
        }

        var s = text!.Trim();
        if (s.Length <= 10 || ParseDateTime(s) is not { } at || at.TimeOfDay == TimeSpan.Zero)
        {
            return day;
        }

        return DateOnly.FromDateTime(at.DateTime.AddHours(12));
    }

    /// <summary>A <c>yyyy-MM-dd</c> date (or the date part of a longer string), or null.</summary>

    public static DateOnly? ParseDate(string? text)
    {
        var s = text?.Trim();
        return s is { Length: >= 10 }
            && DateOnly.TryParseExact(s[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)
                ? day
                : null;
    }
}

/// <summary>[S-571] What an agenda row's time column reads for one day of an event.</summary>
/// <param name="Start">The start on that day (the day's midnight when it began earlier).</param>
/// <param name="End">The end on that day (the next midnight when it runs on).</param>
/// <param name="WholeDay">All-day, or a timed event covering the whole day: the row says "All day".</param>
public readonly record struct AgendaTime(DateTimeOffset Start, DateTimeOffset End, bool WholeDay);

/// <summary>[S-572] How much a month cell can show of its day's events.</summary>
public enum MonthDensity
{
    /// <summary>Event chips with titles, then "+N".</summary>
    Chips,

    /// <summary>Up to three coloured dots, plus a muted one for more.</summary>
    Dots,

    /// <summary>Only the number; a day with events has it in the strong weight.</summary>
    Number,
}
