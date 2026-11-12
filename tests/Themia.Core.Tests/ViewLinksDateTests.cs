using Themia.Core.Links;
using Themia.Core.Office;
using Themia.Core.Tasks;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-11920]..[S-11929] A day on the view's links: what day a task, an event, a habit's period stand for; where a planner and screen time go for a selected day.</summary>
public sealed class ViewLinksDateTests
{
    private static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;

    private static readonly TimeZoneInfo Tokyo = TimeZoneInfo.CreateCustomTimeZone("Test+9", TimeSpan.FromHours(9), "Test+9", "Test+9");

    private static readonly TimeZoneInfo Hawaii = TimeZoneInfo.CreateCustomTimeZone("Test-10", TimeSpan.FromHours(-10), "Test-10", "Test-10");

    [Fact]
    public void S11921_a_task_is_its_due_day_and_none_without_one()
    {
        Assert.Equal(new DateLink(new DateOnly(2026, 11, 13)), TaskDue.DayLink("2026-11-13T00:00:00.0000000", Utc));
        Assert.Equal(new DateLink(new DateOnly(2026, 11, 13)), TaskDue.DayLink("2026-11-13", Hawaii));
        Assert.Null(TaskDue.DayLink(null, Utc));
        Assert.Null(TaskDue.DayLink("", Utc));
        Assert.Null(TaskDue.DayLink("not a date", Utc));
    }

    [Fact]
    public void S11921_a_due_written_as_the_creators_midnight_in_utc_is_the_viewers_local_day()
    {
        // To Do's mobile app writes a UTC+7 user's "due the 10th" as the 9th 17:00 UTC: the 10th in Tokyo too.
        Assert.Equal(new DateLink(new DateOnly(2026, 8, 10)), TaskDue.DayLink("2026-08-09T17:00:00.0000000", Tokyo));
    }

    [Fact]
    public void S11922_a_timed_event_is_its_local_start_day()
    {
        var late = new CalendarEvent
        {
            Id = "e",
            Start = new DateTimeOffset(2026, 11, 12, 20, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 11, 12, 21, 0, 0, TimeSpan.Zero),
        };

        Assert.Equal(new DateLink(new DateOnly(2026, 11, 12)), CalendarShare.Day(late, Utc));
        Assert.Equal(new DateLink(new DateOnly(2026, 11, 13)), CalendarShare.Day(late, Tokyo));
    }

    [Fact]
    public void S11922_an_all_day_event_is_its_date_in_every_zone()
    {
        var holiday = CalendarEvent.AllDayEvent("h", "Holiday", new DateOnly(2026, 11, 13), new DateOnly(2026, 11, 15));

        Assert.Equal(new DateLink(new DateOnly(2026, 11, 13)), CalendarShare.Day(holiday, Hawaii));
        Assert.Equal(new DateLink(new DateOnly(2026, 11, 13)), CalendarShare.Day(holiday, Tokyo));
    }

    [Fact]
    public void S11926_only_a_daily_period_is_a_day()
    {
        var start = new DateTimeOffset(2026, 11, 12, 0, 0, 0, TimeSpan.Zero);

        Assert.Equal(new DateOnly(2026, 11, 12), HabitPeriods.Day(new HabitBucket("2026-11-12", start), HabitPeriod.Daily));
        Assert.Null(HabitPeriods.Day(new HabitBucket("2026-W46", start), HabitPeriod.Weekly));
        Assert.Null(HabitPeriods.Day(new HabitBucket("2026-11", start), HabitPeriod.Monthly));
        Assert.Null(HabitPeriods.Day(new HabitBucket("2026-11-12-H09", start), HabitPeriod.Hourly));
        Assert.Null(HabitPeriods.Day(new HabitBucket("broken", start), HabitPeriod.Daily));
    }

    [Fact]
    public void S11927_the_planner_starts_at_the_selected_day_else_today()
    {
        var today = new DateOnly(2026, 11, 12);

        Assert.Equal(today, WeekPlannerRules.Start(today, null));
        Assert.Equal(today.AddDays(9), WeekPlannerRules.Start(today, today.AddDays(9)));
        Assert.Equal(today.AddDays(-2), WeekPlannerRules.Start(today, today.AddDays(-2)));
        Assert.Equal((today.AddDays(9), today.AddDays(15)), WeekPlannerRules.Range(today.AddDays(9)));
    }

    [Fact]
    public void S11927_a_planner_from_another_day_keeps_todays_tasks_under_today_only()
    {
        var today = new DateOnly(2026, 11, 12);
        var now = new DateTimeOffset(2026, 11, 12, 10, 0, 0, TimeSpan.Zero);
        TaskItem[] tasks = [new("due", "list", "due", false, DueRaw: TaskDue.Wire(today))];

        var later = WeekPlannerRules.Build([], [], tasks, today, Utc, now, first: today.AddDays(3));
        Assert.Equal(today.AddDays(3), later[0].Day);
        Assert.All(later, day => Assert.Empty(day.Tasks));

        var before = WeekPlannerRules.Build([], [], tasks, today, Utc, now, first: today.AddDays(-2));
        Assert.Equal(today.AddDays(-2), before[0].Day);
        Assert.Empty(before[0].Tasks);
        Assert.Equal(["due"], before[2].Tasks.Select(t => t.Id));
    }

    [Fact]
    public void S11928_a_selected_day_pins_screen_time_as_far_as_its_arrows_go()
    {
        var today = new LocalDay(new DateOnly(2026, 11, 12));

        Assert.Null(ScreenTimeRules.PinFor(today.Date, today));
        Assert.Null(ScreenTimeRules.PinFor(today.Date.AddDays(5), today));
        Assert.Equal("2026-11-10", ScreenTimeRules.PinFor(today.Date.AddDays(-2), today));
        Assert.Equal(today.AddDays(-ScreenTimeRules.MaxHistoryOffset).Key, ScreenTimeRules.PinFor(today.Date.AddDays(-400), today));
    }

    [Fact]
    public void S11924_a_day_is_found_among_a_periods_days_or_not()
    {
        ScreenTimeDay[] days = [new("2026-11-10", 0, [], 0), new("2026-11-11", 0, [], 0), new("2026-11-12", 0, [], 0)];

        Assert.Equal(1, ScreenTimeRules.DayIndex(days, new DateOnly(2026, 11, 11)));
        Assert.Null(ScreenTimeRules.DayIndex(days, new DateOnly(2026, 11, 13)));
    }
}
