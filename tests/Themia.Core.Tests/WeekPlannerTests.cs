using System.Text.Json;
using Themia.Core.Accounts;
using Themia.Core.Model;
using Themia.Core.Office;
using Themia.Core.Storage;
using Themia.Core.Sync;
using Themia.Core.Tasks;
using Themia.Core.Widgets;
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

    private static WidgetArea Box(string tier)
    {
        var t = WidgetTiers.All.Single(x => x.Name == tier);
        return new WidgetArea(t.Width - 24, t.Height - 44 - 24);
    }

    [Fact]
    public void S5320_the_config_round_trips_and_is_a_calendar_with_tasks()
    {
        var config = new WeekPlannerConfig { ShowTasks = false, TaskAccountId = "acc", AccountIds = ["a"], ShowBirthdays = false };
        var json = JsonSerializer.Serialize<WidgetConfig>(config, StateJson.WidgetConfig);
        Assert.Contains("\"weekPlanner\"", json, StringComparison.Ordinal);

        var back = Assert.IsType<WeekPlannerConfig>(JsonSerializer.Deserialize(json, StateJson.WidgetConfig));
        Assert.False(back.ShowTasks);
        Assert.Equal("acc", back.TaskAccountId);
        Assert.Equal(["a"], back.AccountIds);
        Assert.Empty(back.Repair());

        var clone = Assert.IsType<WeekPlannerConfig>(back.Clone());
        Assert.Equal("acc", clone.TaskAccountId);
        Assert.False(clone.ShowTasks);

        Assert.True(new WeekPlannerConfig().ShowTasks);
        Assert.Equal(HeaderPlacement.Top, WidgetInstance.DefaultHeader(WidgetKind.WeekPlanner));
        Assert.IsType<WeekPlannerConfig>(ConfigJson.Read("WeekPlanner", """{"kind":"weekPlanner","taskAccountId":null}"""));
    }

    [Fact]
    public void S5321_the_planner_reads_a_rolling_week_from_today()
    {
        Assert.Equal((Today, Today.AddDays(6)), WeekPlannerRules.Range(Today));
    }

    [Fact]
    public void S5321_small_tiles_show_today_and_wider_ones_more_days_side_by_side()
    {
        var xs = WeekPlannerRules.Plan(Box("XS"));
        Assert.Equal(1, xs.Columns);
        Assert.False(xs.Stacked);
        Assert.False(xs.Headings);
        Assert.False(xs.Times);

        var tall = WeekPlannerRules.Plan(Box("Tall"));
        Assert.Equal(1, tall.Columns);
        Assert.True(tall.Stacked);
        Assert.True(tall.Times);

        var wide = WeekPlannerRules.Plan(Box("Wide"));
        var l = WeekPlannerRules.Plan(Box("L"));
        var xl = WeekPlannerRules.Plan(Box("XL"));
        Assert.True(wide.Columns >= 2);
        Assert.True(xl.Columns > l.Columns && l.Columns > 1);
        Assert.True(xl.Columns <= WeekPlannerRules.Days);
        Assert.True(xl.Times && wide.Times);
        Assert.Equal(WeekPlannerRules.Days, WeekPlannerRules.Plan(new WidgetArea(2000, 400)).Columns);
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
    public void S5323_a_column_shows_whole_rows_and_says_how_many_more()
    {
        var slots = WeekPlannerRules.Columns([5, 2, 0], 3, height: 24 + (3 * 24), heading: 24, row: 24, headings: true);

        Assert.Equal(3, slots.Count);
        Assert.Equal(new PlannerSlot(0, 2, 3, true), slots[0]);
        Assert.Equal(new PlannerSlot(1, 2, 0, true), slots[1]);
        Assert.Equal(new PlannerSlot(2, 0, 0, true), slots[2]);

        // No whole row: nothing, not a "+N" squeezed in.
        Assert.Equal(new PlannerSlot(0, 0, 0, true), WeekPlannerRules.Columns([3], 1, 30, 24, 24, true)[0]);
    }

    [Fact]
    public void S5323_stacked_days_skip_empty_ones_and_stop_where_the_room_ends()
    {
        // Today 2 rows, tomorrow nothing, then 1, then 4: room for today, the third day and a cut fourth.
        var slots = WeekPlannerRules.Stack([2, 0, 1, 4, 1], height: (3 * 20) + (5 * 20), heading: 20, row: 20);

        Assert.Equal([0, 2, 3], slots.Select(s => s.Day));
        Assert.Equal(new PlannerSlot(0, 2, 0, true), slots[0]);
        Assert.Equal(new PlannerSlot(2, 1, 0, true), slots[1]);
        Assert.Equal(new PlannerSlot(3, 1, 3, true), slots[2]);

        // A day whose heading and one row do not fit is not started.
        Assert.Single(WeekPlannerRules.Stack([1, 1], height: 50, heading: 20, row: 20));
    }

    [Fact]
    public void S5325_the_planner_waits_for_setup_only_while_neither_source_is_signed_in()
    {
        var planner = new WeekPlannerConfig();
        Assert.True(WidgetSetup.Needed(planner, AccountFeatures.None));
        Assert.False(WidgetSetup.Needed(planner, AccountFeatures.Calendar));
        Assert.False(WidgetSetup.Needed(planner, AccountFeatures.Tasks));
        Assert.True(WidgetSetup.Needed(new WeekPlannerConfig { ShowTasks = false }, AccountFeatures.Tasks));
    }

    [Fact]
    public void S5325_the_planner_syncs_its_calendars_and_its_task_account_today()
    {
        var state = AppState.CreateDefault();
        var widget = WidgetInstance.Create(WidgetKind.WeekPlanner, new Geometry.FracRect(0.1, 0.1, 0.3, 0.3));
        var config = (WeekPlannerConfig)widget.Config;
        config.AccountIds = ["cal"];
        config.TaskAccountId = "tasks";
        state.Views[0].Widgets.Add(widget);

        var interests = SyncInterests.From(state);
        Assert.Contains(interests.Calendar, c => c.AccountIds.SequenceEqual(["cal"]));
        Assert.Contains(interests.Tasks, t => t.AccountId == "tasks" && t.ListId == TaskRules.TodayListId);

        config.ShowTasks = false;
        Assert.DoesNotContain(SyncInterests.From(state).Tasks, t => t.AccountId == "tasks");
    }
}
