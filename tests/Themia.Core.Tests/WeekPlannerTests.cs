using System.Text.Json;
using Themia.Core.Accounts;
using Themia.Core.Model;
using Themia.Core.Office;
using Themia.Core.Storage;
using Themia.Core.Sync;
using Themia.Core.Tasks;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-5320]..[S-5325] The week planner (issue #160).</summary>
public class WeekPlannerTests
{
    private static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;
    private static readonly DateOnly Today = new(2026, 10, 12);
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 10, 0, 0, TimeSpan.Zero);

    private static CalendarEvent At(string subject, int day, int hour, int minutes) => new()
    {
        Id = $"{subject}-{day}",
        Subject = subject,
        Start = new DateTimeOffset(2026, 10, 12, hour, 0, 0, TimeSpan.Zero).AddDays(day),
        End = new DateTimeOffset(2026, 10, 12, hour, 0, 0, TimeSpan.Zero).AddDays(day).AddMinutes(minutes),
    };

    private static TaskItem Due(string id, DateOnly? due, bool completed = false)
        => new(id, "list", id, completed, DueRaw: due is { } day ? TaskDue.Wire(day) : null);

    [Fact]
    public void S5321_the_planner_reads_a_rolling_week_from_today()
    {
        Assert.Equal((Today, Today.AddDays(6)), WeekPlannerRules.Range(Today));
    }

    [Fact]
    public void S5322_tasks_due_today_or_overdue_stand_under_today_only()
    {
        var tasks = new[]
        {
            Due("today", Today),
            Due("overdue", Today.AddDays(-3)),
            Due("tomorrow", Today.AddDays(1)),
            Due("undated", null),
            Due("done", Today, completed: true),
        };

        var days = WeekPlannerRules.Build([], [], tasks, Today, Utc, Now);
        Assert.Equal(WeekPlannerRules.Days, days.Count);
        Assert.Equal(["overdue", "today"], days[0].Tasks.Select(t => t.Id));
        Assert.All(days.Skip(1), day => Assert.Empty(day.Tasks));
    }

    [Fact]
    public void S5322_today_drops_what_has_ended_and_every_day_keeps_its_own_events()
    {
        var events = new[]
        {
            At("ended", 0, 8, 60),
            At("running", 0, 9, 120),
            At("later", 0, 15, 30),
            At("tomorrow", 1, 9, 30),
            CalendarEvent.AllDayEvent("holiday", "Holiday", Today, Today.AddDays(1)),
        };

        var days = WeekPlannerRules.Build(events, [new Birthday("Ana", 10, 13, null)], [], Today, Utc, Now);

        Assert.Equal(["Holiday", "running", "later"], days[0].Events.Select(i => i.Event!.Subject));
        Assert.Equal(2, days[1].Events.Count);
        Assert.NotNull(days[1].Events[0].Birthday);
        Assert.Equal("tomorrow", days[1].Events[1].Event!.Subject);
        Assert.All(days.Skip(2), day => Assert.Equal(0, day.Count));
    }

    [Fact]
    public void S11410_a_column_shows_its_rows_and_says_how_many_more()
    {
        var slots = WeekPlannerRules.Columns([5, 2, 0, 4], days: 3, rows: 3);

        Assert.Equal(3, slots.Count);
        Assert.Equal(new PlannerSlot(0, 2, 3, true), slots[0]);
        Assert.Equal(new PlannerSlot(1, 2, 0, true), slots[1]);
        Assert.Equal(new PlannerSlot(2, 0, 0, true), slots[2]);

        // One row: the day's first item, not a lone "+3" that names nothing.
        Assert.Equal(new PlannerSlot(0, 1, 0, true), WeekPlannerRules.Columns([3], 1, 1)[0]);
    }

    [Fact]
    public void S11410_S14500_a_new_planner_is_three_days_of_its_rows_and_every_cell_between()
    {
        // [S-14500] The heading and the rows at their optimal (30 each), a day's column 200 (whole cells on the default grid);
        // a new one three days, its rows; every cell from one day and one row up ([S-14410]: the days are the card's).
        var five = WeekPlannerRules.Sizing(5);
        Assert.Equal(new SizeDip(600, 30 + (5 * 30)), five.Natural);
        Assert.Equal(new SizeDip(200, 60), five.Min);
        Assert.Equal(1400, five.Max.Width);
        Assert.Empty(five.Problems());
    }

    [Fact]
    public void S14410_S14502_the_days_are_as_many_as_the_width_holds()
    {
        var grid = Themia.Core.Widgets.ItemGrid.Default();
        Assert.Equal(1, WeekPlannerRules.DaysIn(150, grid));
        Assert.Equal(1, WeekPlannerRules.DaysIn(399, grid));
        Assert.Equal(2, WeekPlannerRules.DaysIn(400, grid));
        Assert.Equal(3, WeekPlannerRules.DaysIn(610, grid));
        Assert.Equal(WeekPlannerRules.Days, WeekPlannerRules.DaysIn(5000, grid));
    }
}
