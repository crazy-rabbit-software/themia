using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Habits;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.HabitTracker.Tests;

/// <summary>[S-1429] The Habits widget's options on its properties menu: the dots' own under Style, the lists, the resets, the data folder.</summary>
public sealed class HabitTrackerMenuTests
{
    private int _saves;
    private WidgetTextAsk? _asked;
    private WidgetConfirm? _confirm;
    private string? _opened;

    private WidgetMenuLines Build(HabitTrackerConfig config, IHabitLists? pool)
    {
        var lines = new WidgetMenuLines(() => _saves++, ask => _asked = ask, confirm => _confirm = confirm);
        HabitTrackerMenu.Lines(lines, config, pool, path => _opened = path);
        return lines;
    }

    private static Pool Two() => new([new("a", "Gym", [new("h1", "Run", []), new("h2", "Lift", [])]), new("b", "Reading", [])]);

    private static string ListTitle => HabitTrackerStr.WidgetsB_Settings_Habits_List.Text();

    private static ItemMenuEntry Line(WidgetMenuLines lines, string text) => lines.Lines.Single(l => l.Text == text);

    [Fact]
    public void S1429_the_dots_offer_streaks_and_history_under_style_and_the_others_nothing()
    {
        Assert.Equal(
            [HabitTrackerStr.WidgetsB_Settings_Habits_Streaks_Label.Text(), HabitTrackerStr.WidgetsB_Settings_Habits_History_Label.Text()],
            Build(new HabitTrackerConfig { Style = HabitTrackerStyle.Dots }, Two()).StyleLines.Select(l => l.Text));
        Assert.Empty(Build(new HabitTrackerConfig { Style = HabitTrackerStyle.Heatmap }, Two()).StyleLines);
    }

    [Fact]
    public void S1429_appearance_then_list_resets_and_data_folder_and_the_interval_only_when_custom()
    {
        var lines = Build(new HabitTrackerConfig(), Two());
        Assert.Equal(0, lines.CardAt);
        Assert.Equal(
            [ListTitle, HabitTrackerStr.WidgetsB_Settings_Habits_Resets_Label.Text(), HabitTrackerStr.WidgetsB_Settings_Habits_DataFolder_Label.Text()],
            lines.Lines.Select(l => l.Text));

        var custom = Build(new HabitTrackerConfig { Period = HabitPeriod.Custom }, Two());
        Assert.Contains(custom.Lines, l => l.Text == HabitTrackerStr.HabitTracker_IntervalLabel.Text());
    }

    [Fact]
    public void S1429_the_list_shown_has_the_dot_and_picking_another_saves_once()
    {
        var config = new HabitTrackerConfig { ListId = "a" };
        var lists = Line(Build(config, Two()), ListTitle);
        Assert.Equal("Gym", lists.Value);

        var gym = lists.Children!.Single(l => l.Text == "Gym");
        Assert.True(gym.Checked);
        Assert.Equal(HabitTrackerStr.WidgetsB_Settings_Habits_HabitCount.Plural(2), gym.Value);
        gym.Action!();
        Assert.Equal(0, _saves);

        lists.Children!.Single(l => l.Text == "Reading").Action!();
        Assert.Equal("b", config.ListId);
        Assert.Equal(1, _saves);
    }

    [Fact]
    public void S1429_add_a_list_creates_one_and_shows_it()
    {
        var pool = Two();
        var config = new HabitTrackerConfig { ListId = "a" };
        Line(Build(config, pool), ListTitle).Children!.Single(l => l.Text == HabitTrackerStr.WidgetsB_Settings_Habits_AddList.Text()).Action!();

        Assert.Equal(3, pool.Lists.Count);
        Assert.Equal(pool.Lists[^1].Id, config.ListId);
        Assert.Equal(1, _saves);
    }

    [Fact]
    public void S1429_rename_renames_the_list_shown_in_the_pool()
    {
        var pool = Two();
        Line(Build(new HabitTrackerConfig { ListId = "b" }, pool), ListTitle).Children!.Single(l => l.Text == Str.Menu_Rename.Text()).Action!();
        Assert.Equal("Reading", _asked!.Initial);

        _asked.Done(" Books ");
        Assert.Equal("Books", pool.Find("b")!.Name);
        Assert.Equal(1, _saves);
    }

    [Fact]
    public void S1429_deleting_a_list_is_always_asked_first_and_names_what_goes()
    {
        var pool = Two();
        var delete = Line(Build(new HabitTrackerConfig { ListId = "a" }, pool), ListTitle).Children!
            .Single(l => l.Text == HabitTrackerStr.WidgetsB_Settings_Habits_DeleteList.Text());
        Assert.True(delete.Danger);

        delete.Action!();
        Assert.Equal(2, pool.Lists.Count);
        var confirm = Assert.IsType<WidgetConfirm>(_confirm);
        Assert.Equal(HabitTrackerStr.WidgetsB_Settings_Habits_DeleteFull.Plural(2, ("name", "Gym")), confirm.Question);

        confirm.Done();
        Assert.Null(pool.Find("a"));
        Assert.Equal(1, _saves);
    }

    [Fact]
    public void S1429_a_read_only_pool_greys_every_change_and_no_pool_says_so()
    {
        var pool = Two();
        pool.Writable = false;
        var lines = Line(Build(new HabitTrackerConfig { ListId = "a" }, pool), ListTitle).Children!;
        Assert.All(lines.Where(l => l.Glyph is not null), l => Assert.True(l.Disabled));

        var none = Line(Build(new HabitTrackerConfig(), null), ListTitle).Children!;
        Assert.True(Assert.Single(none).Note);
    }

    [Fact]
    public void S1429_the_interval_is_read_by_the_cadence_rule()
    {
        var config = new HabitTrackerConfig { Period = HabitPeriod.Custom };
        Line(Build(config, Two()), HabitTrackerStr.HabitTracker_IntervalLabel.Text()).Action!();
        var ask = _asked!;

        Assert.NotNull(ask.Refuse("0"));
        Assert.NotNull(ask.Refuse("1441"));
        Assert.NotNull(ask.Refuse("1.5"));
        ask.Done("45");

        Assert.Equal(45, config.CustomMinutes);
        Assert.Equal(1, _saves);
    }

    [Fact]
    public void S1430_the_data_folder_opens_where_habits_json_is()
    {
        var pool = Two();
        pool.FilePath = @"C:\Data\habits.json";
        var folder = Line(Build(new HabitTrackerConfig(), pool), HabitTrackerStr.WidgetsB_Settings_Habits_DataFolder_Label.Text()).Children!;

        folder.Single(l => !l.Note).Action!();
        Assert.Equal(@"C:\Data\habits.json", _opened);
        Assert.True(Line(Build(new HabitTrackerConfig(), Two()), HabitTrackerStr.WidgetsB_Settings_Habits_DataFolder_Label.Text()).Children!.Single(l => !l.Note).Disabled);
    }

    /// <summary>Lists in memory, changed by the same <see cref="HabitLists"/> functions the host's pool applies.</summary>
    private sealed class Pool(IReadOnlyList<HabitList> lists) : IHabitLists
    {
        public event Action? Changed;

        public IReadOnlyList<HabitList> Lists { get; private set; } = lists;

        public bool Writable { get; set; } = true;

        public string? Problem => null;

        public string? FilePath { get; set; }

        public HabitList? Find(string? id) => HabitLists.Find(Lists, id);

        public string? Create(string baseName)
        {
            var id = HabitLists.NewId("list");
            Apply(HabitLists.Add(Lists, id, HabitLists.UniqueName(Lists, baseName), baseName));
            return id;
        }

        public string? FirstOrCreate() => HabitLists.First(Lists) ?? Create(HabitLists.NewListName);

        public void Rename(string id, string name) => Apply(HabitLists.Rename(Lists, id, name));

        public void Delete(string id) => Apply(HabitLists.Remove(Lists, id));

        public void Update(string listId, Func<IReadOnlyList<Habit>, IReadOnlyList<Habit>> change) => throw new NotSupportedException();

        private void Apply(IReadOnlyList<HabitList> next)
        {
            Lists = next;
            Changed?.Invoke();
        }
    }
}
