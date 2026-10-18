namespace Themia.Core.Tasks;

/// <summary>What an optimistic change does to the list on screen.</summary>
public enum TaskEditKind
{
    SetCompleted,
    Rename,
    Delete,
    Create,
}

/// <summary>One change the user made that the provider has not confirmed yet.</summary>
/// <param name="TaskId">The task it is about; for <see cref="TaskEditKind.Create"/> a temporary id.</param>
/// <param name="Created">The row a create shows: the optimistic one, then the provider's once it answers.</param>
/// <param name="At">[S-612] When a task was ticked: its completion until the provider's own arrives.</param>
public sealed record TaskEdit(TaskEditKind Kind, string TaskId, bool Completed = false, string? Title = null, TaskItem? Created = null, DateTimeOffset? At = null)
{
    public static TaskEdit Complete(string taskId, bool completed, DateTimeOffset? at = null) => new(TaskEditKind.SetCompleted, taskId, Completed: completed, At: at);

    public static TaskEdit Rename(string taskId, string title) => new(TaskEditKind.Rename, taskId, Title: title);

    public static TaskEdit Delete(string taskId) => new(TaskEditKind.Delete, taskId);

    public static TaskEdit Create(TaskItem optimistic) => new(TaskEditKind.Create, optimistic.Id, Created: optimistic);
}

/// <summary>
/// [S-597] The list on screen = what the provider last said + the user's changes it has not caught
/// up with yet. Optimistic: a tick shows the moment it is clicked; a failure takes it back.
/// <para>
/// The subtle part is a refresh racing a write. A fetch that <i>started</i> before a change was
/// confirmed can come back without it, and dropping the overlay on confirmation would flash the old
/// state until the next refresh. So a confirmed change stays on top until a fetch that started after
/// the confirmation lands; only then is the provider's own answer trusted to include it. A change
/// still in flight always stays. Ordering is by a logical clock, never a wall clock.
/// </para>
/// <para>
/// Not thread-safe: owned by one feed on the UI thread.
/// </para>
/// </summary>
public sealed class TaskEdits
{
    private readonly List<Entry> _entries = [];
    private IReadOnlyList<TaskItem> _base = [];
    private long _clock;

    /// <summary>What the provider last said, without the overlay.</summary>
    public IReadOnlyList<TaskItem> Fetched => _base;

    /// <summary>How many changes are on top of <see cref="Fetched"/>, confirmed or not.</summary>
    public int Pending => _entries.Count;

    /// <summary>Stamp a fetch as it starts; hand the stamp back to <see cref="Landed"/>.</summary>
    public long FetchStarted() => ++_clock;

    /// <summary>
    /// A fetch landed. The provider's answer becomes the base, and every confirmed change older than
    /// the fetch is dropped: the provider has it now. Unconfirmed changes stay.
    /// </summary>
    public void Landed(IReadOnlyList<TaskItem> tasks, long startedAt)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        _base = tasks;
        _entries.RemoveAll(e => e.ConfirmedAt is { } at && at < startedAt);
    }

    /// <summary>Show a change now; returns the ticket to confirm or roll it back with.</summary>
    public long Begin(TaskEdit edit)
    {
        ArgumentNullException.ThrowIfNull(edit);
        var ticket = ++_clock;
        _entries.Add(new Entry(ticket, edit));
        return ticket;
    }

    /// <summary>
    /// The provider confirmed a change. A create hands over the provider's task, which replaces the
    /// optimistic row in place, under its real id.
    /// </summary>
    public void Succeeded(long ticket, TaskItem? created = null)
    {
        var index = _entries.FindIndex(e => e.Ticket == ticket);
        if (index < 0)
        {
            return;
        }

        var entry = _entries[index];
        var edit = entry.Edit;

        if (edit.Kind == TaskEditKind.Create && created is not null)
        {
            edit = edit with { TaskId = created.Id, Created = created };
        }

        _entries[index] = entry with { Edit = edit, ConfirmedAt = ++_clock };
    }

    /// <summary>The provider refused a change: take it back. The base still holds what was there.</summary>
    public void Failed(long ticket) => _entries.RemoveAll(e => e.Ticket == ticket);

    /// <summary>Whether a task is a create the provider has not confirmed: it has no real id to write to yet.</summary>
    public bool IsUnconfirmedCreate(string taskId)
        => _entries.Any(e => e.ConfirmedAt is null && e.Edit.Kind == TaskEditKind.Create && e.Edit.TaskId == taskId);

    /// <summary>Forget everything: a different list, account or filter is a different feed.</summary>
    public void Clear()
    {
        _entries.Clear();
        _base = [];
    }

    /// <summary>The list as the user should see it: the base with every change applied in order.</summary>
    public IReadOnlyList<TaskItem> Current
    {
        get
        {
            if (_entries.Count == 0)
            {
                return _base;
            }

            var list = _base.ToList();

            foreach (var (_, edit, _) in _entries)
            {
                var index = list.FindIndex(t => t.Id == edit.TaskId);

                switch (edit.Kind)
                {
                    // [S-612] A tick carries its moment, so Today's "finished today" keeps the row; a
                    // reopen clears it. A task the provider already holds as finished keeps its own.
                    case TaskEditKind.SetCompleted when index >= 0:
                        list[index] = list[index] with
                        {
                            Completed = edit.Completed,
                            CompletedOn = !edit.Completed ? null
                                : list[index] is { Completed: true, CompletedOn: { } held } ? held
                                : edit.At is { } at ? TaskStamp.At(at) : null,
                        };
                        break;

                    case TaskEditKind.Rename when index >= 0 && edit.Title is { } title:
                        list[index] = list[index] with { Title = title };
                        break;

                    case TaskEditKind.Delete when index >= 0:
                        list.RemoveAt(index);
                        break;

                    // A create the base already holds (a fetch that raced the POST) is not doubled.
                    case TaskEditKind.Create when index < 0 && edit.Created is { } created:
                        list.Add(created);
                        break;
                }
            }

            return list;
        }
    }

    private sealed record Entry(long Ticket, TaskEdit Edit, long? ConfirmedAt = null);
}
