using System.Text.Json;
using Themia.Core.Habits;
using Themia.Core.Storage;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-1420]/[S-1425]/[S-1429] The device-wide habit lists: the trust boundary at <c>habits.json</c>,
/// the store's atomic single-writer file, and every in-widget mutation.
/// </summary>
public sealed class HabitListsTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "themia-tests", Guid.NewGuid().ToString("n"));

    public HabitListsTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
            // ignore: a temp folder another process still holds is cleaned up by the OS
        }
    }

    private static IReadOnlyList<Habit> Three() =>
    [
        new("a", "Read", ["2026-06-14"]),
        new("b", "Walk", []),
        new("c", "Stretch", ["2026-06-15"]),
    ];

    // ---- [S-1420] the trust boundary ------------------------------------------------------------

    [Fact]
    public void S1420_malformed_lists_habits_and_keys_are_dropped_and_their_siblings_kept()
    {
        const string json = """
            {
              "schemaVersion": 1,
              "lists": [
                { "id": "l1", "name": "Morning", "habits": [
                    { "id": "h1", "name": "Read", "log": ["2026-06-15", 7, "", "2026-06-15", "2026-W24"] },
                    { "id": "h1", "name": "Duplicate id" },
                    { "name": "No id" },
                    { "id": "h2", "name": "No log" }
                ] },
                { "id": "l1", "name": "Duplicate list" },
                { "id": "", "name": "Empty id" },
                { "id": "l2", "name": "Bad habits", "habits": "nope" },
                42
              ]
            }
            """;

        var (lists, dropped, refusal) = HabitListStore.Parse(json);

        Assert.Null(refusal);
        Assert.Equal(new[] { "l1", "l2" }, lists.Select(l => l.Id));
        Assert.Equal(new[] { "h1", "h2" }, lists[0].Habits.Select(h => h.Id));
        Assert.Equal(new[] { "2026-06-15", "2026-W24" }, lists[0].Habits[0].Log);
        Assert.Empty(lists[0].Habits[1].Log);
        Assert.Empty(lists[1].Habits);
        Assert.Equal(9, dropped);
    }

    [Theory]
    [InlineData("")]
    [InlineData("[]")]
    [InlineData("{ \"lists\": [] }")]
    [InlineData("{ \"schemaVersion\": 2, \"lists\": [] }")]
    [InlineData("{ not json")]
    public void S1420_a_document_that_cannot_be_trusted_is_refused_whole(string json)
    {
        var (lists, _, refusal) = HabitListStore.Parse(json);

        Assert.NotNull(refusal);
        Assert.Empty(lists);
    }

    [Fact]
    public async Task S1420_the_pool_round_trips_through_the_file()
    {
        var store = new HabitListStore(Path.Combine(_dir, HabitListStore.FileName));
        HabitList[] lists = [new("l1", "Morning", Three()), new("l2", "Evening", [])];

        await store.SaveAsync(lists);
        var load = store.Load();

        Assert.Equal(LoadOutcome.Loaded, load.Outcome);
        Assert.True(load.Writable);
        Assert.Equal(2, load.Lists.Count);
        Assert.Equal(new[] { "Read", "Walk", "Stretch" }, load.Lists[0].Habits.Select(h => h.Name));
        Assert.Equal(new[] { "2026-06-15" }, load.Lists[0].Habits[2].Log);
        Assert.False(File.Exists(store.Path + $".{Environment.ProcessId}.tmp"));
    }

    [Fact]
    public void S1420_no_file_is_a_quiet_empty_pool()
    {
        var load = new HabitListStore(Path.Combine(_dir, HabitListStore.FileName)).Load();

        Assert.Equal(LoadOutcome.Fresh, load.Outcome);
        Assert.Empty(load.Lists);
        Assert.True(load.Writable);
    }

    [Fact]
    public void S1420_a_corrupt_file_is_set_aside_never_overwritten()
    {
        var path = Path.Combine(_dir, HabitListStore.FileName);
        File.WriteAllText(path, "{ garbage");

        var load = new HabitListStore(path).Load();

        Assert.Equal(LoadOutcome.Recovered, load.Outcome);
        Assert.True(load.Writable);
        Assert.False(File.Exists(path));
        Assert.Single(Directory.GetFiles(_dir, HabitListStore.FileName + ".corrupt-*"));
    }

    [Fact]
    public void S1420_the_file_sits_beside_the_state_file()
        => Assert.Equal(Path.Combine(_dir, "habits.json"), HabitListStore.PathBeside(Path.Combine(_dir, "state.json")));

    // ---- [S-1429] lists --------------------------------------------------------------------------

    [Fact]
    public void S1429_a_new_list_is_named_uniquely_and_a_blank_name_falls_back()
    {
        IReadOnlyList<HabitList> lists = [new("l1", "Habits", [])];

        Assert.Equal("Habits 2", HabitLists.UniqueName(lists, "Habits"));
        Assert.Equal("Fallback", HabitLists.Add(lists, "l2", "  ", "Fallback")[1].Name);
    }

    [Fact]
    public void S1429_a_list_name_may_be_blank_while_it_is_typed()
        => Assert.Equal(string.Empty, HabitLists.Rename([new("l1", "Habits", [])], "l1", string.Empty)[0].Name);

    [Fact]
    public void S1429_removing_a_list_takes_its_habits_and_others_are_untouched()
    {
        IReadOnlyList<HabitList> lists = [new("l1", "A", Three()), new("l2", "B", [])];

        var left = HabitLists.Remove(lists, "l1");

        Assert.Equal("l2", Assert.Single(left).Id);
        Assert.Null(HabitLists.Find(left, "l1"));
    }

    [Fact]
    public void S1420_a_widget_with_no_list_takes_the_first_and_only_null_means_create()
    {
        Assert.Null(HabitLists.First([]));
        Assert.Equal("l1", HabitLists.First([new("l1", "A", []), new("l2", "B", [])]));
        Assert.Null(HabitLists.Find([new("l1", "A", [])], string.Empty));
    }

    [Fact]
    public void S1420_a_write_to_a_list_that_has_gone_resurrects_nothing()
    {
        IReadOnlyList<HabitList> lists = [new("l1", "A", [])];

        var after = HabitLists.SetHabits(lists, "gone", Three());

        Assert.Empty(Assert.Single(after).Habits);
    }

    // ---- [S-1425] habits in the widget ------------------------------------------------------------

    [Fact]
    public void S1425_a_habit_is_added_at_the_bottom_trimmed()
    {
        var after = HabitLists.AddHabit(Three(), "  Water  ", "d");

        Assert.Equal("d", after[^1].Id);
        Assert.Equal("Water", after[^1].Name);
        Assert.Empty(after[^1].Log);
    }

    [Fact]
    public void S1425_a_blank_name_adds_nothing_and_writes_nothing()
    {
        var habits = Three();

        Assert.Same(habits, HabitLists.AddHabit(habits, "   ", "d"));
    }

    [Fact]
    public void S1425_a_rename_keeps_the_id_and_the_log()
    {
        var after = HabitLists.RenameHabit(Three(), "a", " Read 30 pages ");

        Assert.Equal("a", after[0].Id);
        Assert.Equal("Read 30 pages", after[0].Name);
        Assert.Equal(new[] { "2026-06-14" }, after[0].Log);
    }

    [Theory]
    [InlineData("a", "")]
    [InlineData("a", "Read")]
    [InlineData("zzz", "New")]
    public void S1425_a_blank_unchanged_or_unknown_rename_is_a_no_op(string id, string name)
    {
        var habits = Three();

        Assert.Same(habits, HabitLists.RenameHabit(habits, id, name));
    }

    [Fact]
    public void S1425_deleting_a_habit_takes_its_log_with_it()
    {
        var after = HabitLists.RemoveHabit(Three(), "c");

        Assert.Equal(new[] { "a", "b" }, after.Select(h => h.Id));
        Assert.DoesNotContain(after, h => h.Log.Contains("2026-06-15"));
    }

    [Theory]
    [InlineData("b", -1, "b,a,c")]
    [InlineData("b", 1, "a,c,b")]
    [InlineData("a", 1, "b,a,c")]
    public void S1425_move_up_and_down_keeps_the_others_in_order(string id, int delta, string expected)
        => Assert.Equal(expected, string.Join(',', HabitLists.MoveHabit(Three(), id, delta).Select(h => h.Id)));

    [Theory]
    [InlineData("a", -1)]
    [InlineData("c", 1)]
    [InlineData("zzz", 1)]
    [InlineData("b", 0)]
    public void S1425_a_move_past_either_end_writes_nothing(string id, int delta)
    {
        var habits = Three();

        Assert.Same(habits, HabitLists.MoveHabit(habits, id, delta));
    }

    [Fact]
    public void S1425_a_drag_writes_the_new_order_and_the_same_order_writes_nothing()
    {
        var habits = Three();

        Assert.Equal("c,a,b", string.Join(',', HabitLists.ReorderHabits(habits, ["c", "a", "b"]).Select(h => h.Id)));
        Assert.Equal("b,a,c", string.Join(',', HabitLists.ReorderHabits(habits, ["b", "zzz", "b"]).Select(h => h.Id)));
        Assert.Same(habits, HabitLists.ReorderHabits(habits, ["a", "b", "c"]));
    }

    [Fact]
    public void S1421_a_tick_lands_on_one_habit_only()
    {
        var after = HabitLists.ToggleHabit(Three(), "b", "2026-06-15");

        Assert.Equal(new[] { "2026-06-15" }, after[1].Log);
        Assert.Equal(new[] { "2026-06-14" }, after[0].Log);
        Assert.Equal(new[] { "2026-06-15" }, after[2].Log);
    }

    [Fact]
    public void S1421_a_tick_on_an_unknown_habit_writes_nothing()
    {
        var habits = Three();

        Assert.Same(habits, HabitLists.ToggleHabit(habits, "zzz", "2026-06-15"));
    }

    [Fact]
    public void S1420_serialized_pools_read_back_identically()
    {
        HabitList[] lists = [new("l1", "Morning \"quotes\"", Three())];

        var (back, dropped, refusal) = HabitListStore.Parse(HabitListStore.Serialize(lists));

        Assert.Null(refusal);
        Assert.Equal(0, dropped);
        Assert.Equal(lists[0].Name, back[0].Name);
        Assert.Equal(lists[0].Habits.Select(h => (h.Id, h.Name, string.Join('|', h.Log))), back[0].Habits.Select(h => (h.Id, h.Name, string.Join('|', h.Log))));
    }

    [Fact]
    public void S1420_a_non_array_lists_value_is_no_lists()
    {
        using var document = JsonDocument.Parse("{\"x\": 1}");
        var dropped = 0;

        Assert.Empty(HabitLists.Parse(document.RootElement, ref dropped));
    }
}
