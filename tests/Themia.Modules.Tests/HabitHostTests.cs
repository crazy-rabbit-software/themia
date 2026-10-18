using Themia.Core.Geometry;
using Themia.Core.Habits;
using Themia.Core.Model;
using Themia.Core.Office;
using Themia.Core.Storage;
using Themia.Core.Tasks;
using Themia.Core.Tests;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>[S-812] [S-1420] [S-1422] [S-3802] [S-3850] The habit tracker under the root's rules: its keys without a focused row and the reorder keys beside the task list's, its add footer agreeing with the task list's, its settings through a restart and a hand-edited config repaired: the config and its ladder are the habit tracker module's, so they run here. Moved from Core's KeyboardLeaseTests, WidgetKeyMapTests, AddFooterTests and TrackerPersistenceTests, unchanged in what they assert.</summary>
public sealed class HabitHostTests : IDisposable
{
    // The size tiers' slots (card less a 44px header), as AddFooterTests has them.
    private static readonly WidgetArea XS = new(120, 76);
    private static readonly WidgetArea S = new(200, 106);
    private static readonly WidgetArea M = new(320, 196);
    private static readonly WidgetArea Wide = new(480, 116);
    private static readonly WidgetArea Tall = new(200, 356);
    private static readonly WidgetArea L = new(560, 376);
    private static readonly WidgetArea XL = new(800, 516);

    private readonly string _dir;
    private readonly string _path;

    public HabitHostTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "themia-tests", Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(_dir);
        _path = Path.Combine(_dir, "state.json");
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
            // ignore: temp cleanup is best effort and must not fail a test run
        }
    }

    private AppStateStore NewStore() => new(_path, TimeSpan.Zero);

    /// <summary>Save a widget of this kind with its config filled in, and read it back.</summary>
    private async Task<T> RoundTripAsync<T>(WidgetId kind, Action<T> fill)
        where T : WidgetConfig
    {
        var state = AppState.CreateDefault();
        var view = state.ActiveView();
        var widget = WidgetInstance.Create(kind, new FracRect(0.1, 0.1, 0.2, 0.2));

        fill((T)widget.Config);
        view.Widgets.Add(widget);

        var store = NewStore();
        store.SaveSoon(state);
        await store.FlushAsync();

        var loaded = NewStore().Load();

        Assert.Equal(LoadOutcome.Loaded, loaded.Outcome);

        var reloaded = loaded.State.ActiveView().Widgets.Single(w => w.Kind == kind);
        return Assert.IsType<T>(reloaded.Config);
    }

    [Theory]
    [InlineData(WidgetKey.Down, HabitKeyCommand.Focus, 0)]
    [InlineData(WidgetKey.Tab, HabitKeyCommand.Focus, 0)]
    [InlineData(WidgetKey.ShiftTab, HabitKeyCommand.Focus, 2)]
    [InlineData(WidgetKey.End, HabitKeyCommand.Focus, 2)]
    [InlineData(WidgetKey.Enter, HabitKeyCommand.None, -1)]
    [InlineData(WidgetKey.Delete, HabitKeyCommand.None, -1)]
    [InlineData(WidgetKey.Escape, HabitKeyCommand.Release, -1)]
    public void S812_habits_without_a_focused_row(WidgetKey key, HabitKeyCommand command, int index)
        => Assert.Equal(new HabitKeyResult(command, index), HabitLayout.Decide(key, -1, 3));

    [Fact]
    public void S812_keys_the_lists_do_not_answer_do_nothing()
    {
        Assert.Equal(KitKeyCommand.None, KitKeys.Decide(WidgetKey.Tab, 1, 3).Command);
        Assert.Equal(HabitKeyCommand.None, HabitLayout.Decide(WidgetKey.Left, 1, 3).Command);
    }

    [Fact]
    public void S3802_a_focused_task_or_habit_moves_one_place_and_not_past_an_end()
    {
        Assert.Equal(new KitKeyResult(KitKeyCommand.MoveUp, 1), KitKeys.Decide(WidgetKey.MoveUp, 1, 3));
        Assert.Equal(new KitKeyResult(KitKeyCommand.MoveDown, 1), KitKeys.Decide(WidgetKey.MoveDown, 1, 3));
        Assert.Equal(KitKeyCommand.None, KitKeys.Decide(WidgetKey.MoveUp, 0, 3).Command);
        Assert.Equal(KitKeyCommand.None, KitKeys.Decide(WidgetKey.MoveDown, 2, 3).Command);
        Assert.Equal(KitKeyCommand.None, KitKeys.Decide(WidgetKey.MoveDown, -1, 3).Command);

        Assert.Equal(new HabitKeyResult(HabitKeyCommand.MoveUp, 2), HabitLayout.Decide(WidgetKey.MoveUp, 2, 3));
        Assert.Equal(HabitKeyCommand.None, HabitLayout.Decide(WidgetKey.MoveDown, 2, 3).Command);
        Assert.Equal(HabitKeyCommand.None, HabitLayout.Decide(WidgetKey.MoveUp, 0, 3).Command);
    }

    [Fact]
    public void S3850_habits_and_tasks_agree_at_every_tier()
    {
        foreach (var area in new[] { XS, S, M, Wide, Tall, L, XL })
        {
            foreach (var count in new[] { 0, 3 })
            {
                var habits = HabitLayout.Plan(area, new HabitLayoutInput(count, true, true, 7), 40, 32).AddRow;
                var tasks = TaskLayout.Plan(area, new TaskLayoutInput(count, true, false, true, Themia.Core.Model.TasksStyle.List, 2), 40, 32).AddRow;
                Assert.Equal(habits, tasks);
            }
        }
    }

    [Fact]
    public async Task S1420_a_habit_widgets_settings_survive_a_restart()
    {
        var config = await RoundTripAsync<HabitTrackerConfig>(new WidgetId("habitTracker"), habits =>
        {
            habits.ListId = "list-morning";
            habits.Period = HabitPeriod.Custom;
            habits.CustomMinutes = 45;
            habits.HistoryDots = 14;
            habits.ShowStreak = false;
        });

        Assert.Equal("list-morning", config.ListId);
        Assert.Equal(HabitPeriod.Custom, config.Period);
        Assert.Equal(45, config.CustomMinutes);
        Assert.Equal(14, config.HistoryDots);
        Assert.False(config.ShowStreak);
    }

    [Fact]
    public void S1422_a_hand_edited_habit_config_is_repaired_to_its_ranges()
    {
        var config = new HabitTrackerConfig { Period = (HabitPeriod)99, CustomMinutes = 0, HistoryDots = 500 };

        var repairs = config.Repair();

        Assert.Equal(3, repairs.Count);
        Assert.Equal(HabitPeriod.Daily, config.Period);
        Assert.Equal(HabitCadence.MinCustomMinutes, config.CustomMinutes);
        Assert.Equal(HabitRules.MaxHistory, config.HistoryDots);
    }
}
