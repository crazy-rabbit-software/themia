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

namespace Themia.Widget.WeekPlanner.Tests;

/// <summary>[S-5320] [S-5325] The week planner's config, setup and sync (moved from Core's WeekPlannerTests, unchanged; the rules' tests stay there).</summary>
public sealed class WeekPlannerTests
{
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
        Assert.IsType<WeekPlannerConfig>(Themia.Core.Tests.ConfigJson.Read("WeekPlanner", """{"kind":"weekPlanner","taskAccountId":null}"""));
    }

    [Fact]
    public void S5325_the_planner_waits_for_setup_only_while_neither_source_is_signed_in()
    {
        var planner = new WeekPlannerConfig();
        Assert.True(WidgetSetup.Needed(planner, FeatureSet.None));
        Assert.False(WidgetSetup.Needed(planner, HostFeatures.Calendar));
        Assert.False(WidgetSetup.Needed(planner, HostFeatures.Tasks));
        Assert.True(WidgetSetup.Needed(new WeekPlannerConfig { ShowTasks = false }, HostFeatures.Tasks));
    }

    [Fact]
    public void S5325_the_planner_syncs_its_calendars_and_its_task_account_today()
    {
        var state = AppState.CreateDefault();
        var widget = WidgetInstance.Create(new WidgetId("weekPlanner"), new Themia.Core.Geometry.FracRect(0.1, 0.1, 0.3, 0.3));
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

    [Fact]
    public void S5320_S14410_S14502_the_days_are_the_cards_width_and_its_rows_a_new_cards_height()
    {
        // Owner, 2026-11-20: "Anything that's just 'how many of the same item' (days, hours, weeks, processes) follows
        // the card's size." A new planner is three days of its rows; the card shows what its box holds, never stored.
        var config = new WeekPlannerConfig { RowsPerDay = 5 };
        var kind = new WeekPlannerModule().Descriptor;
        var sizing = kind.Sizing(config);

        Assert.Equal(new SizeDip(3 * WeekPlannerRules.ColumnWidth, 30 + (5 * 30)), sizing.Natural);
        Assert.Equal(30 + (7 * 30), kind.Sizing(new WeekPlannerConfig { RowsPerDay = 7 }).Natural.Height);
        Assert.Equal(5, WeekPlannerRules.DaysIn(5 * WeekPlannerRules.ColumnWidth, Themia.Core.Widgets.ItemGrid.Default()));
    }
}
