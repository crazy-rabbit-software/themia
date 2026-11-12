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
public readonly record struct PlannerPlan(int Columns, bool Stacked, bool Headings, bool Times = true);

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

    /// <summary>[S-5321] The days the planner fetches, whatever it shows, so a resize never asks again.</summary>
    public static (DateOnly First, DateOnly Last) Range(DateOnly today) => (today, today.AddDays(Days - 1));

    /// <summary>
    /// [S-5322] The planner's days from today: each day's events as a calendar day lists them
    /// ([S-570]); today without the timed events that have already ended (what is ahead, as the
    /// agenda's [S-571]); and under today the open tasks due today or overdue ([S-596]).
    /// </summary>
    public static IReadOnlyList<PlannerDay> Build(
        IEnumerable<CalendarEvent> events,
        IEnumerable<Birthday> birthdays,
        IEnumerable<TaskItem> tasks,
        DateOnly today,
        TimeZoneInfo zone,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(birthdays);
        ArgumentNullException.ThrowIfNull(tasks);
        ArgumentNullException.ThrowIfNull(zone);

        var allEvents = events.ToList();
        var allBirthdays = birthdays.ToList();
        var due = TaskRules.Today(tasks.Where(t => !t.Completed), today, zone);
        var days = new List<PlannerDay>(Days);

        for (var i = 0; i < Days; i++)
        {
            var day = today.AddDays(i);
            var items = CalendarRules.DayItems(allEvents, allBirthdays, day, zone);

            if (i == 0)
            {
                items = [.. items.Where(item => item.Event is not { AllDay: false } e || (e.End > e.Start ? e.End > now : e.Start >= now))];
            }

            days.Add(new PlannerDay(day, items, i == 0 ? due : []));
        }

        return days;
    }

    /// <summary>
    /// [S-5321] The planner's shape in the view's area (the card less its header; rows carry the side
    /// inset, so columns meet): as many day columns as each keeps 240, at most the week; a single
    /// column stacks the days one under another from a regular height, else it is today alone with no
    /// heading. A timed event shows its start from a column of 180.
    /// </summary>
    public static PlannerPlan Plan(WidgetArea area)
    {
        if (!area.IsMeasured)
        {
            return new PlannerPlan(1, false, false, false);
        }

        var columns = Math.Clamp((int)Math.Floor(area.Width / MinColumn), 1, Days);
        var times = area.Width / columns >= TimesFrom;

        if (columns == 1)
        {
            var stacked = area.HeightClass >= SizeClass.Regular;
            return new PlannerPlan(1, stacked, stacked, times);
        }

        return new PlannerPlan(columns, false, true, times);
    }

    /// <summary>
    /// [S-5323] What each column shows: its day's heading, then as many whole rows as fit; when the
    /// day has more, the last row says how many ("+3"). An empty today still takes one row, to say
    /// nothing is planned.
    /// </summary>
    public static IReadOnlyList<PlannerSlot> Columns(IReadOnlyList<int> counts, int columns, double height, double heading, double row, bool headings)
    {
        ArgumentNullException.ThrowIfNull(counts);

        var slots = new List<PlannerSlot>();
        for (var day = 0; day < Math.Min(columns, counts.Count); day++)
        {
            var room = height - (headings ? heading : 0);
            var fit = Disclosure.WholeRows(room, row);
            var (shown, hidden) = Cut(counts[day], fit);
            slots.Add(new PlannerSlot(day, shown, hidden, headings));
        }

        return slots;
    }

    /// <summary>
    /// [S-5323] One column of days, one under another: today always (its heading, then its rows,
    /// or the one row saying nothing is planned), then each later day that has something, while its
    /// heading and at least one row still fit. A day cut short says how many more it has, and ends
    /// the list.
    /// </summary>
    public static IReadOnlyList<PlannerSlot> Stack(IReadOnlyList<int> counts, double height, double heading, double row)
    {
        ArgumentNullException.ThrowIfNull(counts);

        var slots = new List<PlannerSlot>();
        var room = height;

        for (var day = 0; day < counts.Count; day++)
        {
            if (day > 0 && counts[day] == 0)
            {
                continue;
            }

            var fit = Disclosure.WholeRows(room - heading, row);
            if (fit < 1)
            {
                break;
            }

            var (shown, hidden) = Cut(counts[day], fit);
            slots.Add(new PlannerSlot(day, shown, hidden, true));

            if (hidden > 0)
            {
                break;
            }

            room -= heading + (Math.Max(1, shown) * row);
        }

        return slots;
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
