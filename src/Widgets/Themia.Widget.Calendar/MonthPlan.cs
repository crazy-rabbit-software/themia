using Themia.Core.Model;
using Themia.Core.Office;
using Themia.Sdk;

namespace Themia.Widget.Calendar;

/// <summary>[S-11547] How a month chip is drawn: a span's bar (all-day, several days, a birthday) or a timed event's line.</summary>
public enum MonthChipLook
{
    /// <summary>On the chip fill, its calendar's colour as the bar at its start, across the days it covers.</summary>
    Bar,

    /// <summary>No fill: its calendar's dot, the start time where the column holds it, the title.</summary>
    Timed,
}

/// <summary>
/// [S-11547] One week row of the month with titles: the chips it draws (each item's bar clipped to the
/// row, on lines below <see cref="MonthPlan.Lines"/>), and for each of its seven days how many items
/// it has and how many of them did not get a line (its "+k more").
/// </summary>
public sealed record MonthWeek(DateOnly Start, IReadOnlyList<LaneItem> Shown, IReadOnlyList<int> Items, IReadOnlyList<int> Hidden);

/// <summary>
/// [S-11547] The month with titles, Outlook's and Windows' month view: six week rows (every month one
/// height), each day cell its number over the config's lines of event chips (<see cref="MonthPlan.Lines"/>,
/// 1..6), a span one bar across its days and on into the next row, then "+k more" on the number's line
/// for the rest. Pure: the counts, the lanes and the overflow are pinned by tests, never by eye.
/// </summary>
public static class MonthPlan
{
    public const int MinLines = 1;

    public const int MaxLines = 6;

    /// <summary>A new month with titles lists three events a day.</summary>
    public const int DefaultLines = 3;

    /// <summary>[S-11547] The number's line says "+k more" in words from this column width; narrower "+k".</summary>
    public const double MoreWordsFrom = 80;

    /// <summary>[S-11547] The chip lines a day cell holds: the config's, 1..6.</summary>
    public static int Lines(int lines) => Math.Clamp(lines, MinLines, MaxLines);

    /// <summary>[S-11544] The first day of each of the six week rows the month of <paramref name="anchor"/> draws.</summary>
    public static IReadOnlyList<DateOnly> Weeks(DateOnly anchor, WeekStartDay start)
    {
        var cells = CalendarRules.MonthCells(anchor, start);
        return [.. Enumerable.Range(0, CalendarSizing.MonthWeeks).Select(r => cells[r * 7])];
    }

    /// <summary>
    /// [S-11547] One week row from <paramref name="weekStart"/>: every item of its days packed on lines
    /// (<see cref="CalendarFit.Bars"/>: a span one bar, the fewest lines), the chips on the first
    /// <paramref name="lines"/> of them shown, and per day its items and those left for "+k more".
    /// </summary>
    public static MonthWeek Week(IEnumerable<CalendarEvent> events, IEnumerable<Birthday> birthdays, DateOnly weekStart, TimeZoneInfo zone, int lines)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(birthdays);
        ArgumentNullException.ThrowIfNull(zone);

        var shownLines = Lines(lines);
        var bars = CalendarFit.Bars(events, birthdays, weekStart, 7, zone, singleTimed: true);
        var items = new int[7];
        var hidden = new int[7];
        for (var c = 0; c < 7; c++)
        {
            items[c] = bars.Count(b => b.First <= c && c <= b.Last);
            hidden[c] = CalendarFit.Hidden(bars, c, shownLines);
        }

        return new MonthWeek(weekStart, [.. bars.Where(b => b.Lane < shownLines)], items, hidden);
    }

    /// <summary>[S-11547] A chip's look: a bar for an all-day event, one over several days or a birthday; a timed line otherwise.</summary>
    public static MonthChipLook Look(LaneItem chip, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(chip);
        return chip.Item.Event is { AllDay: false } e && !CalendarFit.IsMultiDay(e, zone) ? MonthChipLook.Timed : MonthChipLook.Bar;
    }

    /// <summary>
    /// [S-11547] Whether a chip is dimmed as the days around the month are: every day it covers in its
    /// row is another month's than <paramref name="month"/>'s (a span reaching into the month is not).
    /// </summary>
    public static bool Dimmed(LaneItem chip, DateOnly weekStart, DateOnly month)
    {
        ArgumentNullException.ThrowIfNull(chip);
        for (var c = chip.First; c <= chip.Last; c++)
        {
            var day = weekStart.AddDays(c);
            if (day.Year == month.Year && day.Month == month.Month)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>[S-11547] Whether "+k more" is in words: a column of at least <see cref="MoreWordsFrom"/>.</summary>
    public static bool MoreWords(double column) => column >= MoreWordsFrom;
}
