using Themia.Sdk.Kit;
using Themia.Sdk;
using Themia.Core.Office;
using Themia.Core.Tasks;

namespace Themia.Core.Widgets;

/// <summary>[S-5322] One day of the planner: its events (birthdays first, as a calendar day's) and, today only, the tasks due.</summary>
public sealed record PlannerDay(DateOnly Day, IReadOnlyList<DayItem> Events, IReadOnlyList<TaskItem> Tasks)
{
    /// <summary>The rows the day has to show.</summary>
    public int Count => Events.Count + Tasks.Count;
}

/// <summary>[S-5323] What a column (or, stacked, a stretch of the list) shows of a day: its first rows, and how many more there are.</summary>
/// <param name="Day">Index into the planner's days, today being 0.</param>
/// <param name="Shown">Rows drawn, events first.</param>
/// <param name="Hidden">Rows left out, said as "+N" on the last row.</param>
/// <param name="Heading">Whether the day's heading ("Today", "Tomorrow", "Wed 14 Oct") is drawn.</param>
public readonly record struct PlannerSlot(int Day, int Shown, int Hidden, bool Heading);

/// <summary>[S-5321] How the planner lays its days out at one size.</summary>
/// <param name="Columns">Days side by side, today first.</param>
/// <param name="Stacked">One column holding several days one under another, as an agenda does.</param>
/// <param name="Headings">Whether a lone day still names itself: not on a tile only big enough for its rows.</param>
/// <param name="Times">Whether an event row carries its start: not in a column too narrow for "10:30 AM" and a word of the title.</param>

/// <summary>
/// [S-5320]..[S-5323] The week planner's rules: which days, what is on each, and how much of it a
/// size shows. Pure, so the ladder is tested without a window.
/// </summary>
public static class WeekPlannerRules
{
    /// <summary>[S-5321] The planner's week: today and the six days after it. A rolling week, so nothing behind today is drawn.</summary>
    public const int Days = 7;

    /// <summary>
    /// [S-5321] A day's column at its narrowest, full width (its rows carry the card's side inset): a
    /// title and its time on one row of 28 ([S-6711]).
    /// </summary>
    public const double MinColumn = 240;

    /// <summary>[S-5321] From this column width a timed event's row ends in its start (a compact column keeps the title).</summary>
    public const double TimesFrom = 180;

    /// <summary>[S-5321] The days the planner fetches from its first day, whatever it shows, so a resize never asks again.</summary>
    public static (DateOnly First, DateOnly Last) Range(DateOnly first) => (first, first.AddDays(Days - 1));

    /// <summary>
    /// [S-11927] The planner's first day: the day selected on the view (<c>SelectedDate</c>, a calendar's
    /// click) when there is one, whichever side of today it is, so the planner reads that day and the six
    /// after it; else today.
    /// </summary>
    public static DateOnly Start(DateOnly today, DateOnly? selected) => selected ?? today;

    /// <summary>
    /// [S-5322] The planner's days from <paramref name="first"/> (today unless a day is selected,
    /// [S-11927]): each day's events as a calendar day lists them ([S-570]); today without the timed
    /// events that have already ended (what is ahead, as the agenda's [S-571]); and under today, when it
    /// is among the days, the open tasks due today or overdue ([S-596]).
    /// </summary>
    public static IReadOnlyList<PlannerDay> Build(
        IEnumerable<CalendarEvent> events,
        IEnumerable<Birthday> birthdays,
        IEnumerable<TaskItem> tasks,
        DateOnly today,
        TimeZoneInfo zone,
        DateTimeOffset now,
        DateOnly? first = null)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(birthdays);
        ArgumentNullException.ThrowIfNull(tasks);
        ArgumentNullException.ThrowIfNull(zone);

        var allEvents = events.ToList();
        var allBirthdays = birthdays.ToList();
        var due = TaskRules.Today(tasks.Where(t => !t.Completed), today, zone);
        var days = new List<PlannerDay>(Days);

        var start = first ?? today;
        for (var i = 0; i < Days; i++)
        {
            var day = start.AddDays(i);
            var items = CalendarRules.DayItems(allEvents, allBirthdays, day, zone);

            if (day == today)
            {
                items = [.. items.Where(item => item.Event is not { AllDay: false } e || (e.End > e.Start ? e.End > now : e.Start >= now))];
            }

            days.Add(new PlannerDay(day, items, day == today ? due : []));
        }

        return days;
    }

    /// <summary>
    /// [S-11410] What each column shows: its day's heading, then its rows, as many as the config says;
    /// when the day has more, the last row says how many ("+3"). An empty today still takes one row, to
    /// say nothing is planned.
    /// </summary>
    public static IReadOnlyList<PlannerSlot> Columns(IReadOnlyList<int> counts, int days, int rows)
    {
        ArgumentNullException.ThrowIfNull(counts);

        var slots = new List<PlannerSlot>();
        for (var day = 0; day < Math.Min(days, counts.Count); day++)
        {
            var (shown, hidden) = Cut(counts[day], rows);
            slots.Add(new PlannerSlot(day, shown, hidden, true));
        }

        return slots;
    }

    /// <summary>[S-11410] A day column's one width: a title worth reading beside its start.</summary>
    public const double ColumnWidth = 200;

    /// <summary>[S-11410] The most rows a day shows.</summary>
    public const int MaxRows = 12;

    /// <summary>
    /// [S-11410] The planner's size: the days side by side, each a column of <see cref="ColumnWidth"/>
    /// (its rows carry the side inset, so the columns meet), one width; the height the content top, a
    /// day's heading (28), its rows (28 each) and the inset, stepped by a row.
    /// </summary>
    public static WidgetSizing Sizing(int days, int rows)
    {
        var width = Math.Clamp(days, 1, Days) * ColumnWidth;
        return WidgetSizing.Range(new(width, 1), new(width, 1), new(width, 1))
            .WithHeightSteps(new SizeSteps(FamilySpace.ContentTop + FamilyShape.Row1 + FamilySpace.Inset, FamilyShape.Row1, 1, MaxRows, Math.Clamp(rows, 1, MaxRows)));
    }

    /// <summary>A day's rows in <paramref name="fit"/> whole rows: all of them, or all but one and a "+N" row; in one row its first item.</summary>
    private static (int Shown, int Hidden) Cut(int count, int fit)
    {
        // No whole row: nothing, not even the "+N" (it would need a row of its own).
        if (fit <= 0)
        {
            return (0, 0);
        }

        // One row is the first item, never a lone "+N" that names nothing ([S-666]: the widget's own item first).
        if (count <= fit || fit == 1)
        {
            return (Math.Min(count, fit), 0);
        }

        var shown = fit - 1;
        return (shown, count - shown);
    }
}
