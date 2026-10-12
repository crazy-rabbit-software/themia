using Themia.Core.Tasks;

namespace Themia.Core.Sync;

/// <summary>[S-2650] One task account's shelf: its lists, the tasks of every list a widget reads, and the queued changes.</summary>
public sealed class TasksShelf : ISyncShelf
{
    /// <summary>
    /// v2: tasks carry when they were finished and lists whether they are To Do's own ([S-612],
    /// [S-614]). A v1 shelf (and its change cursors, which never asked for the completion) is a stale
    /// cache, read again in full ([S-2605]).
    /// </summary>
    public const int Version = 2;

    public int SchemaVersion { get; set; } = Version;

    public string Owner { get; set; } = string.Empty;

    public List<StoredTaskList>? Lists { get; set; }

    public DateTimeOffset? ListsAt { get; set; }

    public List<TaskListShelf> Held { get; set; } = [];

    public List<PendingWrite> Pending { get; set; } = [];
}

public sealed class StoredTaskList
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public bool IsDefault { get; set; }

    /// <summary>[S-614] One of To Do's own lists, which cannot be renamed.</summary>
    public bool BuiltIn { get; set; }
}

/// <summary>One list's tasks as last synced; <see cref="IncludesCompleted"/> says whether finished ones were asked for.</summary>
public sealed class TaskListShelf
{
    public string ListId { get; set; } = string.Empty;

    public DateTimeOffset At { get; set; }

    public bool IncludesCompleted { get; set; }

    public List<StoredTask> Tasks { get; set; } = [];

    /// <summary>[S-2680] Where the provider's changes to this list continue from; null when it offers none.</summary>
    public SyncCursor? Cursor { get; set; }
}

public sealed class StoredTask
{
    public string Id { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public bool Completed { get; set; }

    public bool Important { get; set; }

    public string? DueRaw { get; set; }

    /// <summary>[S-612] When it was finished, as <see cref="TaskStamp.Wire"/>; validated on load.</summary>
    public string? CompletedOn { get; set; }
}

/// <summary>[S-2650]..[S-2653] The task shelf's bounds, validation and conversions.</summary>
public static class TaskShelves
{
    /// <summary>The most lists kept: Today's own bound ([S-596]).</summary>
    public const int MaxLists = TaskRules.TodayMaxLists;

    /// <summary>The most tasks kept per list: what one read returns at most ([S-591]).</summary>
    public const int MaxTasksPerList = 400;

    private const int MaxText = 1000;

    public static StoredTask Store(TaskItem task) => new()
    {
        Id = task.Id,
        Title = Cut(task.Title),
        Completed = task.Completed,
        Important = task.Important,
        DueRaw = task.DueRaw is { Length: <= 64 } due ? due : null,
        CompletedOn = task.Completed ? task.CompletedOn?.Wire() : null,
    };

    public static TaskItem Restore(StoredTask task, string listId)
        => new(task.Id, listId, task.Title, task.Completed, task.Important, task.DueRaw, task.Completed ? TaskStamp.Parse(task.CompletedOn) : null);

    public static StoredTaskList Store(TaskListInfo list) => new() { Id = list.Id, Name = Cut(list.Name), IsDefault = list.IsDefault, BuiltIn = list.BuiltIn };

    public static TaskListInfo Restore(StoredTaskList list) => new(list.Id, list.Name, list.IsDefault, list.BuiltIn);

    /// <summary>
    /// [S-614] The lists as the widget should name them: each list renamed by the last of
    /// <paramref name="renames"/> that names it (queued, or confirmed but not yet read back).
    /// Returns the input itself when nothing is renamed.
    /// </summary>
    public static IReadOnlyList<TaskListInfo> Renamed(IReadOnlyList<TaskListInfo> lists, IEnumerable<(string ListId, string Name)> renames)
    {
        ArgumentNullException.ThrowIfNull(lists);
        ArgumentNullException.ThrowIfNull(renames);

        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (listId, name) in renames)
        {
            names[listId] = name;
        }

        return names.Count == 0 || !lists.Any(l => names.ContainsKey(l.Id))
            ? lists
            : [.. lists.Select(l => names.TryGetValue(l.Id, out var name) ? l with { Name = name } : l)];
    }

    /// <summary>[S-2686] What a list's cursor was issued for: the list, and whether finished tasks are held.</summary>
    public static string CursorScope(string listId, bool includeCompleted) => $"{listId}|{(includeCompleted ? "all" : "open")}";

    public static bool IsTask(PendingKind kind)
        => kind is PendingKind.TaskComplete or PendingKind.TaskReopen or PendingKind.TaskRename or PendingKind.TaskDelete or PendingKind.TaskCreate
            or PendingKind.TaskListRename;

    /// <summary>
    /// [S-2651] A task shelf that opened: lists need an id (repeats and the overflow dropped); a held
    /// list needs an id and appears once; tasks need an id, appear once per list and stop at
    /// <see cref="MaxTasksPerList"/>, and a completion that is not a date is dropped; the queue keeps
    /// only task and list changes, and a rename or create without a title or name is dropped.
    /// </summary>
    public static TasksShelf Validate(TasksShelf shelf, ref int dropped)
    {
        ArgumentNullException.ThrowIfNull(shelf);

        List<StoredTaskList>? lists = null;
        if (shelf.Lists is { } stored)
        {
            lists = [];
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var list in stored)
            {
                if (list is null || string.IsNullOrEmpty(list.Id) || !ids.Add(list.Id) || lists.Count >= MaxLists)
                {
                    dropped++;
                    continue;
                }

                lists.Add(new StoredTaskList { Id = list.Id, Name = Cut(list.Name), IsDefault = list.IsDefault, BuiltIn = list.BuiltIn });
            }
        }

        var held = new List<TaskListShelf>();
        var heldIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var list in shelf.Held ?? [])
        {
            if (list is null || string.IsNullOrEmpty(list.ListId) || TaskRules.IsToday(list.ListId) || !heldIds.Add(list.ListId) || held.Count >= MaxLists)
            {
                dropped++;
                continue;
            }

            var tasks = new List<StoredTask>();
            var taskIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var task in list.Tasks ?? [])
            {
                if (task is null || string.IsNullOrEmpty(task.Id) || !taskIds.Add(task.Id) || tasks.Count >= MaxTasksPerList)
                {
                    dropped++;
                    continue;
                }

                tasks.Add(new StoredTask
                {
                    Id = task.Id,
                    Title = Cut(task.Title),
                    Completed = task.Completed,
                    Important = task.Important,
                    DueRaw = task.DueRaw is { Length: <= 64 } due ? due : null,

                    // [S-612] Only a finished task has a completion, and only one that reads as a date.
                    CompletedOn = task.Completed && TaskStamp.Parse(task.CompletedOn) is { } done ? done.Wire() : null,
                });
            }

            held.Add(new TaskListShelf
            {
                ListId = list.ListId,
                At = list.At,
                IncludesCompleted = list.IncludesCompleted,
                Tasks = tasks,
                Cursor = SyncCursors.Validate(list.Cursor, ref dropped),
            });
        }

        var pending = WriteQueue.Validate(shelf.Pending, ref dropped);
        var kept = pending
            .Where(w => IsTask(w.Kind) && w.Container.Length > 0
                && (w.Kind is not (PendingKind.TaskRename or PendingKind.TaskCreate or PendingKind.TaskListRename) || !string.IsNullOrWhiteSpace(w.Text)))
            .ToList();
        dropped += pending.Count - kept.Count;

        return new TasksShelf { Owner = shelf.Owner, Lists = lists, ListsAt = lists is null ? null : shelf.ListsAt, Held = held, Pending = kept };
    }

    /// <summary>
    /// [S-2652] The lists an account sync reads: every list a widget shows, every list once Today is
    /// shown (Today is built from all of them), and finished tasks only for a list someone shows them
    /// for — a list of four hundred finished tasks would otherwise push the open ones past the bound.
    /// </summary>
    public static IReadOnlyList<(string ListId, bool IncludeCompleted)> Wanted(
        IReadOnlyList<TaskListInfo> lists,
        IEnumerable<(string ListId, bool ShowCompleted)> interests)
    {
        ArgumentNullException.ThrowIfNull(lists);
        ArgumentNullException.ThrowIfNull(interests);

        var wanted = new Dictionary<string, bool>(StringComparer.Ordinal);
        var known = new HashSet<string>(lists.Select(l => l.Id), StringComparer.Ordinal);

        foreach (var (listId, showCompleted) in interests)
        {
            if (TaskRules.IsToday(listId))
            {
                foreach (var list in lists.Take(MaxLists))
                {
                    wanted[list.Id] = wanted.GetValueOrDefault(list.Id) || showCompleted;
                }

                continue;
            }

            var id = listId.Length == 0 ? TaskRules.DefaultList(lists)?.Id : listId;
            if (id is not null && (known.Count == 0 || known.Contains(id)))
            {
                wanted[id] = wanted.GetValueOrDefault(id) || showCompleted;
            }
        }

        return [.. wanted.Take(MaxLists).Select(pair => (pair.Key, pair.Value))];
    }

    private static string Cut(string? text) => text is null ? string.Empty : text.Length <= MaxText ? text : text[..MaxText];
}
