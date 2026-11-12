#if DEBUG
using Themia.App.Services.Tasks;
using Themia.App.Shell.Gallery;
using Themia.Core.Model;
using Themia.Core.Tasks;
using Themia.Sdk.Samples;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only: a list on this PC held in memory, so the rows and the add footer
/// ([S-780], [S-3850]) can be looked at without an account or the user's own lists. <c>stale</c> is an
/// account's list held from 20 minutes ago whose last refresh failed (no network), through a fixture
/// feed that asks nothing; <c>failed</c> the same failure with nothing held (its "Try again");
/// <c>nolocal</c> a list on this PC that is gone (its "Start a list"); <c>caldav</c> a CalDAV account's
/// list ([S-621]: read-only, no add row, a due date, a high-priority task and one finished).
/// </summary>
internal sealed partial class TasksView : IWidgetPreviewState
{
    private const string PreviewListId = "preview-list";

    public IReadOnlyList<string> PreviewStates { get; } = ["tasks", "few", "empty", "done", "stale", "failed", "nolocal", "caldav"];

    public Task ShowPreviewStateAsync(string state)
    {
        if (state == "nolocal")
        {
            _config.Source = TaskSource.Local;
            _config.LocalListId = "gone";
            _feed = null;
            _local = new MemoryTaskLists([]);
            _localListId = null;
            PublishTitle();
            Render();
            return Task.CompletedTask;
        }

        if (state == "caldav")
        {
            var today = SampleDay.Today;
            string Day(int days) => TaskStamp.OfDay(today.AddDays(days)).Wire();

            _config.Source = TaskSource.Account;
            _config.ListId = "preview-account-list";
            _config.ListName = "Household";
            _config.ShowCompleted = true;
            _local = null;
            _localListId = null;
            _feed = new PreviewTaskFeed(
            true,
            [
                new("c1", "preview-account-list", "Take the recycling out", false, DueRaw: Day(0)),
                new("c2", "preview-account-list", "Renew the bike insurance", false, Important: true, DueRaw: Day(3)),
                new("c3", "preview-account-list", "Descale the kettle", false, DueRaw: Day(-1)),
                new("c4", "preview-account-list", "Fix the bathroom tap", false),
                new("c5", "preview-account-list", "Order birthday present", true, CompletedOn: TaskStamp.At(SampleDay.MinutesAgo(90))),
            ],
            SampleDay.Now,
            readOnly: true);
            PublishTitle();
            Render();
            return Task.CompletedTask;
        }

        if (state is "stale" or "failed")
        {
            _config.Source = TaskSource.Account;
            _config.ListId = "preview-account-list";
            _config.ListName = "Errands";
            _config.ShowCompleted = false;
            _local = null;
            _localListId = null;
            _feed = new PreviewTaskFeed(
            state == "stale",
            [
                new("a1", "preview-account-list", "Pick up the parcel", false),
                new("a2", "preview-account-list", "Water the plants", false),
                new("a3", "preview-account-list", "Send the invoice", false),
            ],
            SampleDay.MinutesAgo(20));
            PublishTitle();
            Render();
            return Task.CompletedTask;
        }

        LocalTask[]? tasks = state switch
        {
            // [S-2055] Robin's errands, the week planner's list.
            "tasks" => [.. SampleTasks.Errands.Select((task, i) => new LocalTask($"t{i + 1}", task.Title, false))],
            "few" => [new("t1", "Pick up the parcel", false), new("t2", "Water the plants", false)],

            // [S-784] Finished tasks shown, under their heading.
            "done" =>
            [
                new("t1", "Pick up the parcel", false),
                new("t2", "Water the plants", true),
                new("t3", "Send the invoice", false),
                new("t4", "Call Grandma", true),
                new("t5", "Back up the laptop", false),
            ],
            "empty" => [],
            _ => null,
        };

        if (tasks is null)
        {
            return Task.CompletedTask;
        }

        _config.Source = TaskSource.Local;
        _config.ShowCompleted = state == "done";
        _config.LocalListId = PreviewListId;
        _local = new MemoryTaskLists([new LocalTaskList(PreviewListId, SampleTasks.ListName, tasks)]);
        _localListId = PreviewListId;
        PublishTitle();
        Render();
        return Task.CompletedTask;
    }

    /// <summary>An account's list as a feed holds it after a refresh that failed; nothing is ever asked.</summary>
    private sealed class PreviewTaskFeed(bool loaded, IReadOnlyList<TaskItem> tasks, DateTimeOffset fetchedAt, bool readOnly = false) : ITaskFeed
    {
        public event Action? Changed { add { } remove { } }

        public string AccountId => "preview-account";

        public string ListId => "preview-account-list";

        public bool ShowCompleted => false;

        public bool IsToday => false;

        public bool ReadOnly => readOnly;

        public TaskEdits Edits { get; } = new();

        public IReadOnlyList<TaskItem> Tasks => loaded ? tasks : [];

        public bool Loaded => loaded;

        public bool Busy => false;

        public Themia.Core.Accounts.AccountException? Failure { get; } = readOnly ? null : new(Themia.Core.Accounts.AccountError.Offline, "no network");

        public string? WriteProblem => null;

        public DateTimeOffset? FetchedAt => loaded ? fetchedAt : null;

        public IReadOnlyList<TaskListInfo>? Lists => null;
    }
}
#endif
