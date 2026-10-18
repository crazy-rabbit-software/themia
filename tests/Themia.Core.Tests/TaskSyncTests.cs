using System.Text.Json;
using Themia.Core.Accounts;
using Themia.Core.Model;
using Themia.Core.Sync;
using Themia.Core.Tasks;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// The account side of the Tasks widget: optimistic edits racing refreshes, the provider wire, the
/// Today fan-out (ported from the classic app's <c>todo_today_*</c> Rust tests).
/// </summary>
public sealed class TaskSyncTests
{
    private static TaskItem T(string id, bool done = false, string? title = null) => new(id, "L1", title ?? id, done);

    // ---- [S-597] optimistic edits ------------------------------------------------------------------

    [Fact]
    public void S597_a_completion_shows_at_once_and_a_failure_takes_it_back()
    {
        var edits = new TaskEdits();
        edits.Landed([T("a"), T("b")], edits.FetchStarted());

        var ticket = edits.Begin(TaskEdit.Complete("a", true));
        Assert.True(edits.Current[0].Completed);

        edits.Failed(ticket);
        Assert.False(edits.Current[0].Completed);
        Assert.Equal(0, edits.Pending);
    }

    [Fact]
    public void S597_a_confirmed_change_survives_a_refresh_that_started_before_it_was_confirmed()
    {
        var edits = new TaskEdits();
        edits.Landed([T("a")], edits.FetchStarted());

        var stale = edits.FetchStarted(); // a poll goes out...
        var ticket = edits.Begin(TaskEdit.Complete("a", true));
        edits.Succeeded(ticket); // ...the write is confirmed...
        edits.Landed([T("a")], stale); // ...and the poll lands without it.

        Assert.True(Assert.Single(edits.Current).Completed);
        Assert.Equal(1, edits.Pending);

        edits.Landed([T("a", done: true)], edits.FetchStarted()); // a poll that started after the write
        Assert.Equal(0, edits.Pending);
        Assert.True(Assert.Single(edits.Current).Completed);
    }

    [Fact]
    public void S597_an_unconfirmed_change_stays_on_top_of_any_refresh()
    {
        var edits = new TaskEdits();
        edits.Begin(TaskEdit.Rename("a", "Renamed"));
        edits.Landed([T("a", title: "Old")], edits.FetchStarted());

        Assert.Equal("Renamed", Assert.Single(edits.Current).Title);
    }

    [Fact]
    public void S597_a_failed_delete_puts_the_task_back_where_it_was()
    {
        var edits = new TaskEdits();
        edits.Landed([T("a"), T("b"), T("c")], edits.FetchStarted());

        var ticket = edits.Begin(TaskEdit.Delete("b"));
        Assert.Equal(["a", "c"], edits.Current.Select(t => t.Id));

        edits.Failed(ticket);
        Assert.Equal(["a", "b", "c"], edits.Current.Select(t => t.Id));
    }

    [Fact]
    public void S599_a_created_task_appears_at_the_bottom_then_takes_the_providers_id()
    {
        var edits = new TaskEdits();
        edits.Landed([T("a")], edits.FetchStarted());

        var ticket = edits.Begin(TaskEdit.Create(T("temp-1", title: "New")));
        Assert.Equal(["a", "temp-1"], edits.Current.Select(t => t.Id));
        Assert.True(edits.IsUnconfirmedCreate("temp-1"));

        edits.Succeeded(ticket, T("server-9", title: "New"));
        Assert.Equal(["a", "server-9"], edits.Current.Select(t => t.Id));
        Assert.False(edits.IsUnconfirmedCreate("server-9"));
    }

    [Fact]
    public void S599_a_create_the_refresh_already_holds_is_not_doubled()
    {
        var edits = new TaskEdits();
        var ticket = edits.Begin(TaskEdit.Create(T("temp-1", title: "New")));
        edits.Succeeded(ticket, T("server-9", title: "New"));
        var raced = edits.FetchStarted() - 10; // started before the confirmation, but the POST had landed

        edits.Landed([T("a"), T("server-9", title: "New")], raced);

        Assert.Equal(["a", "server-9"], edits.Current.Select(t => t.Id));
    }

    // ---- [S-591] the wire --------------------------------------------------------------------------

    [Fact]
    public void S591_maps_a_Graph_task_and_every_status_but_completed_is_open()
    {
        var done = JsonSerializer.Deserialize("""{"id":"t1","title":"Ship","status":"completed","importance":"high","dueDateTime":{"dateTime":"2026-08-10T00:00:00.0000000","timeZone":"UTC"}}""", TaskWireContext.Default.GraphTodoTask)!;
        var waiting = JsonSerializer.Deserialize("""{"id":"t2","status":"waitingOnOthers"}""", TaskWireContext.Default.GraphTodoTask)!;

        Assert.Equal(new TaskItem("t1", "L1", "Ship", true, true, "2026-08-10T00:00:00.0000000"), TaskWire.FromGraph(done, "L1"));
        Assert.False(TaskWire.FromGraph(waiting, "L1")!.Completed);
        Assert.Null(TaskWire.FromGraph(new GraphTodoTask(), "L1"));
    }

    [Fact]
    public void S591_maps_a_Google_task_and_drops_hidden_and_deleted_ones()
    {
        var task = JsonSerializer.Deserialize("""{"id":"g1","title":"Call","status":"needsAction","due":"2026-08-10T00:00:00.000Z"}""", TaskWireContext.Default.GoogleTask)!;
        Assert.Equal(new TaskItem("g1", "L1", "Call", false, false, "2026-08-10T00:00:00.000Z"), TaskWire.FromGoogle(task, "L1"));
        Assert.Null(TaskWire.FromGoogle(new GoogleTask { Id = "x", Hidden = true }, "L1"));
        Assert.Null(TaskWire.FromGoogle(new GoogleTask { Id = "x", Deleted = true }, "L1"));
    }

    [Fact]
    public void S591_Graph_marks_its_default_list_and_Google_treats_the_first_as_default()
    {
        Assert.True(TaskWire.FromGraph(new GraphTodoList { Id = "a", DisplayName = "Tasks", WellKnownListName = "defaultList" })!.IsDefault);
        Assert.False(TaskWire.FromGraph(new GraphTodoList { Id = "b", WellKnownListName = "none" })!.IsDefault);

        var google = TaskWire.FromGoogle([new GoogleTaskList { Title = "no id" }, new GoogleTaskList { Id = "g1", Title = "Mine" }, new GoogleTaskList { Id = "g2" }]);
        Assert.Equal([true, false], google.Select(l => l.IsDefault));
    }

    [Fact]
    public void S591_ids_are_escaped_into_the_path_never_a_second_segment()
    {
        var uri = TaskWire.Task(HostProviders.Microsoft, "AQMk/ab==", "t/../x");
        Assert.StartsWith("https://graph.microsoft.com/v1.0/me/todo/lists/AQMk%2Fab%3D%3D/tasks/", uri.AbsoluteUri, StringComparison.Ordinal);
        Assert.DoesNotContain("/../", uri.AbsoluteUri, StringComparison.Ordinal);

        Assert.Contains("showCompleted=false", TaskWire.Tasks(HostProviders.Google, "g1", false).Query, StringComparison.Ordinal);
    }

    [Fact]
    public void S597_completion_bodies_speak_each_providers_vocabulary()
    {
        Assert.Equal("""{"status":"completed"}""", TaskWire.CompleteBody(HostProviders.Microsoft, true));
        Assert.Equal("""{"status":"notStarted"}""", TaskWire.CompleteBody(HostProviders.Microsoft, false));
        Assert.Equal("""{"status":"completed"}""", TaskWire.CompleteBody(HostProviders.Google, true));
        Assert.Equal("""{"status":"needsAction","completed":null}""", TaskWire.CompleteBody(HostProviders.Google, false));
    }

    [Fact]
    public void S600_a_title_with_a_quote_is_escaped_not_interpolated()
    {
        using var document = JsonDocument.Parse(TaskWire.RenameBody("say \"hi\"\n}"));
        Assert.Equal("say \"hi\"\n}", document.RootElement.GetProperty("title").GetString());
        Assert.Single(document.RootElement.EnumerateObject());
    }

    [Fact]
    public void S596_a_Google_create_due_today_is_UTC_midnight()
    {
        using var document = JsonDocument.Parse(TaskWire.CreateBody(HostProviders.Google, "x", new DateOnly(2026, 8, 10)));
        Assert.Equal("2026-08-10T00:00:00.000Z", document.RootElement.GetProperty("due").GetString());
    }

    [Fact]
    public void S591_reads_back_the_task_a_create_answered_with()
    {
        Assert.Equal("n1", TaskWire.ParseCreated(HostProviders.Microsoft, """{"id":"n1","title":"x","status":"notStarted"}""", "L1")!.Id);
        Assert.Null(TaskWire.ParseCreated(HostProviders.Google, "<html>", "L1"));
    }

    // ---- [S-596] the Today fan-out -------------------------------------------------------------------

    private static readonly IReadOnlyList<TaskListInfo> TwoLists = [new("L1", "One", true), new("L2", "Two", false)];

    [Fact]
    public async Task S596_fans_out_across_lists()
    {
        var got = await TaskFanOut.AcrossListsAsync(
            TwoLists,
            (id, _) => Task.FromResult<IReadOnlyList<TaskItem>>([new TaskItem($"{id}-t", id, "x", false)]),
            CancellationToken.None);

        Assert.Equal(["L1-t", "L2-t"], got.Select(t => t.Id));
    }

    [Fact]
    public async Task S596_skips_a_failing_list()
    {
        var skipped = new List<string>();
        var got = await TaskFanOut.AcrossListsAsync(
            TwoLists,
            (id, _) => id == "L1"
                ? Task.FromException<IReadOnlyList<TaskItem>>(new AccountException(AccountError.Forbidden, "no"))
                : Task.FromResult<IReadOnlyList<TaskItem>>([new TaskItem("t", id, "x", false)]),
            CancellationToken.None,
            (list, _) => skipped.Add(list.Id));

        Assert.Equal(["t"], got.Select(t => t.Id));
        Assert.Equal(["L1"], skipped);
    }

    [Fact]
    public async Task S596_surfaces_the_error_when_every_list_fails()
    {
        var error = await Assert.ThrowsAsync<AccountException>(() => TaskFanOut.AcrossListsAsync(
            TwoLists,
            (_, _) => Task.FromException<IReadOnlyList<TaskItem>>(new AccountException(AccountError.SignInRequired, "sign in")),
            CancellationToken.None));

        Assert.Equal(AccountError.SignInRequired, error.Error);
    }

    [Fact]
    public async Task S596_an_account_with_no_lists_has_an_empty_Today_not_an_error()
        => Assert.Empty(await TaskFanOut.AcrossListsAsync([], (_, _) => throw new InvalidOperationException(), CancellationToken.None));

    [Fact]
    public async Task S596_stops_at_fifty_lists()
    {
        var lists = Enumerable.Range(0, 80).Select(i => new TaskListInfo($"L{i}", "x", false)).ToList();
        var asked = 0;

        await TaskFanOut.AcrossListsAsync(
            lists,
            (_, _) =>
            {
                asked++;
                return Task.FromResult<IReadOnlyList<TaskItem>>([]);
            },
            CancellationToken.None);

        Assert.Equal(TaskRules.TodayMaxLists, asked);
    }

    // ---- [S-612] when a task was finished ------------------------------------------------------------

    [Fact]
    public void S612_the_completion_is_parsed_at_the_boundary_and_an_open_task_has_none()
    {
        var graph = JsonSerializer.Deserialize("""{"id":"t1","status":"completed","completedDateTime":{"dateTime":"2026-08-10T00:00:00.0000000","timeZone":"UTC"}}""", TaskWireContext.Default.GraphTodoTask)!;
        var reopened = JsonSerializer.Deserialize("""{"id":"t2","status":"notStarted","completedDateTime":{"dateTime":"2026-08-10T00:00:00.0000000","timeZone":"UTC"}}""", TaskWireContext.Default.GraphTodoTask)!;
        var google = JsonSerializer.Deserialize("""{"id":"g1","status":"completed","completed":"2026-08-10T18:30:00.000Z"}""", TaskWireContext.Default.GoogleTask)!;
        var garbled = JsonSerializer.Deserialize("""{"id":"g2","status":"completed","completed":"soon"}""", TaskWireContext.Default.GoogleTask)!;

        Assert.Equal(TaskStamp.OfDay(new DateOnly(2026, 8, 10)), TaskWire.FromGraph(graph, "L1")!.CompletedOn);
        Assert.Null(TaskWire.FromGraph(reopened, "L1")!.CompletedOn);
        Assert.Equal(TaskStamp.At(new DateTimeOffset(2026, 8, 10, 18, 30, 0, TimeSpan.Zero)), TaskWire.FromGoogle(google, "L1")!.CompletedOn);
        Assert.Null(TaskWire.FromGoogle(garbled, "L1")!.CompletedOn);
        Assert.Contains("completedDateTime", TaskWire.GraphTasksDelta("L1").Query, StringComparison.Ordinal);
    }

    [Fact]
    public void S612_a_tick_carries_its_moment_until_the_provider_answers_and_a_reopen_clears_it()
    {
        var at = new DateTimeOffset(2026, 8, 10, 9, 0, 0, TimeSpan.Zero);
        var edits = new TaskEdits();
        edits.Landed([T("a")], edits.FetchStarted());

        var tick = edits.Begin(TaskEdit.Complete("a", true, at));
        Assert.Equal(TaskStamp.At(at), Assert.Single(edits.Current).CompletedOn);

        edits.Failed(tick);
        edits.Landed([T("a", done: true) with { CompletedOn = TaskStamp.OfDay(new DateOnly(2026, 8, 7)) }], edits.FetchStarted());
        edits.Begin(TaskEdit.Complete("a", false));
        Assert.Null(Assert.Single(edits.Current).CompletedOn);
    }

    [Fact]
    public void S612_a_stored_completion_round_trips_and_one_that_is_not_a_date_is_dropped()
    {
        var task = T("t", done: true) with { CompletedOn = TaskStamp.At(new DateTimeOffset(2026, 8, 10, 18, 30, 0, TimeSpan.Zero)) };
        Assert.Equal(task, TaskShelves.Restore(TaskShelves.Store(task), "L1"));

        var shelf = new TasksShelf
        {
            Owner = "a",
            Held =
            [
                new TaskListShelf
                {
                    ListId = "L1",
                    Tasks =
                    [
                        new StoredTask { Id = "x", Title = "x", Completed = true, CompletedOn = "soon" },
                        new StoredTask { Id = "y", Title = "y", Completed = false, CompletedOn = "2026-08-10" },
                        new StoredTask { Id = "z", Title = "z", Completed = true, CompletedOn = "2026-08-10" },
                    ],
                },
            ],
        };

        var dropped = 0;
        var tasks = Assert.Single(TaskShelves.Validate(shelf, ref dropped).Held).Tasks;
        Assert.Equal([null, null, "2026-08-10"], tasks.Select(t => t.CompletedOn));
    }

    // ---- [S-614] renaming a list ---------------------------------------------------------------------

    [Fact]
    public void S614_To_Dos_own_lists_are_built_in_and_a_users_list_is_not()
    {
        Assert.True(TaskWire.FromGraph(new GraphTodoList { Id = "a", WellKnownListName = "defaultList" })!.BuiltIn);
        Assert.True(TaskWire.FromGraph(new GraphTodoList { Id = "f", WellKnownListName = "flaggedEmails" })!.BuiltIn);
        Assert.False(TaskWire.FromGraph(new GraphTodoList { Id = "b", WellKnownListName = "none" })!.BuiltIn);
        Assert.False(TaskWire.FromGraph(new GraphTodoList { Id = "c" })!.BuiltIn);
        Assert.All(TaskWire.FromGoogle([new GoogleTaskList { Id = "g1" }]), l => Assert.False(l.BuiltIn));

        var list = new TaskListInfo("f", "Flagged email", false, BuiltIn: true);
        Assert.Equal(list, TaskShelves.Restore(TaskShelves.Store(list)));
    }

    [Fact]
    public void S614_a_list_rename_is_a_PATCH_of_the_name_alone_to_the_escaped_list()
    {
        Assert.Equal("https://graph.microsoft.com/v1.0/me/todo/lists/AQ%2Fb%3D", TaskWire.List(HostProviders.Microsoft, "AQ/b=").AbsoluteUri);
        Assert.Equal("https://tasks.googleapis.com/tasks/v1/users/@me/lists/g1", TaskWire.List(HostProviders.Google, "g1").AbsoluteUri);

        using var graph = JsonDocument.Parse(TaskWire.RenameListBody(HostProviders.Microsoft, "Say \"hi\""));
        Assert.Equal("Say \"hi\"", Assert.Single(graph.RootElement.EnumerateObject(), p => p.Name == "displayName").Value.GetString());
        Assert.Single(graph.RootElement.EnumerateObject());

        using var google = JsonDocument.Parse(TaskWire.RenameListBody(HostProviders.Google, "Home"));
        Assert.Equal("Home", google.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    public void S614_a_second_rename_of_a_list_replaces_the_first_in_the_queue_and_survives_a_restart()
    {
        var now = new DateTimeOffset(2026, 8, 10, 9, 0, 0, TimeSpan.Zero);
        Sync.PendingWrite Rename(string id, string name) => new(id, Sync.PendingKind.TaskListRename, "L1", "L1", name, null, now);

        var queue = Sync.WriteQueue.Add([Rename("1", "Work")], Rename("2", "Home"), headInFlight: false)!;
        Assert.Equal(["2"], queue.Select(w => w.Id));

        var dropped = 0;
        var shelf = new TasksShelf { Owner = "a", Pending = [Rename("3", "Home"), Rename("4", " ")] };
        Assert.Equal(["3"], TaskShelves.Validate(shelf, ref dropped).Pending.Select(w => w.Id));
        Assert.Equal(1, dropped);
    }

    [Fact]
    public void S614_queued_and_confirmed_renames_name_the_lists_the_last_one_winning()
    {
        IReadOnlyList<TaskListInfo> lists = [new("a", "Work", true), new("b", "Shop", false)];

        Assert.Same(lists, TaskShelves.Renamed(lists, []));
        Assert.Same(lists, TaskShelves.Renamed(lists, [("gone", "x")]));
        Assert.Equal(["Work", "Groceries"], TaskShelves.Renamed(lists, [("b", "Food"), ("b", "Groceries")]).Select(l => l.Name));
    }
}
