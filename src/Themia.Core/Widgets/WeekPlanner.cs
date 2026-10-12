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

    /// <summary>[S-5321] A day column narrower than this cannot hold "09:30" and a word of a title.</summary>
    public const double MinColumn = 140;

    /// <summary>[S-5321] Under this a column drops the events' start times: the title is what is left to read.</summary>
    public const double MinTimedColumn = 120;

    /// <summary>Between two day columns.</summary>
    public const double ColumnGap = 8;

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
    /// [S-5321] Days side by side as the width allows (<see cref="MinColumn"/> each, up to the
    /// week). One column stacks the days that have something under today when the tile is at least
    /// Regular tall; a smaller one is today alone, without its heading at Compact height.
    /// </summary>
    public static PlannerPlan Plan(WidgetArea box)
    {
        if (!box.IsMeasured)
        {
            return new PlannerPlan(1, false, false, false);
        }

        var columns = Math.Clamp((int)Math.Floor((box.Width + ColumnGap) / (MinColumn + ColumnGap)), 1, Days);
        var times = (box.Width - (ColumnGap * (columns - 1))) / columns >= MinTimedColumn;

        // A strip too short for more than a couple of rows still shows its days side by side, but a
        // column on a Compact tile is today alone.
        if (columns == 1)
        {
            var stacked = box.HeightClass >= SizeClass.Regular;
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

    /// <summary>A day's rows in <paramref name="fit"/> whole rows: all of them, or all but one and a "+N" row.</summary>
    private static (int Shown, int Hidden) Cut(int count, int fit)
    {
        // No whole row: nothing, not even the "+N" (it would need a row of its own).
        if (fit <= 0)
        {
            return (0, 0);
        }

        if (count <= fit)
        {
            return (count, 0);
        }

        var shown = Math.Max(0, fit - 1);
        return (shown, count - shown);
    }
}
