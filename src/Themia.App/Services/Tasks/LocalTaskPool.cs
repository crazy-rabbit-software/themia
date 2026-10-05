using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Storage;
using Themia.Core.Tasks;

namespace Themia.App.Services.Tasks;

/// <summary>
/// [S-603] The task lists kept on this PC: one pool, shared by every Tasks widget and its settings,
/// so renaming a list retitles every widget showing it and deleting a widget never takes its tasks.
/// <para>
/// The UI thread is the only mutator, and every mutation is a pure function from
/// <see cref="LocalTaskLists"/> applied to an immutable snapshot, so what is saved is always a whole
/// pool. Saves are single-flight and coalesced: a burst of changes while one write is out produces
/// one more write, of the latest snapshot, and never two writers at once.
/// </para>
/// </summary>
internal sealed class LocalTaskPool
{
    private const string Scope = "tasks";

    private readonly LocalTaskStore? _store;
    private IReadOnlyList<LocalTaskList> _lists;
    private Task _saving = Task.CompletedTask;
    private bool _saveQueued;

    /// <param name="store">Where the pool lives, or null to keep it in memory only (a debug render).</param>
    public LocalTaskPool(LocalTaskStore? store, IReadOnlyList<LocalTaskList>? seed = null)
    {
        _store = store;

        if (store is null)
        {
            _lists = seed ?? [];
            Writable = true;
            return;
        }

        var load = store.Load();
        _lists = load.Lists;
        Writable = load.Writable;
        Problem = load.Outcome == LoadOutcome.Recovered
            ? load.Writable
                ? Str.Shell_Store_ListsRecovered.Format(("detail", load.Detail))
                : Str.Shell_Store_ListsReadOnly.Format(("detail", load.Detail))
            : null;

        Log.Info(Scope, $"{_lists.Count} list(s) on this PC loaded from {store.Path}");
    }

    /// <summary>Raised on the UI thread after every change.</summary>
    public event Action? Changed;

    public IReadOnlyList<LocalTaskList> Lists => _lists;

    /// <summary>False when the file exists but could not be read: nothing is written over it.</summary>
    public bool Writable { get; }

    /// <summary>Why the file could not be used at start, or null.</summary>
    public string? Problem { get; }

    public LocalTaskList? Find(string? id) => LocalTaskLists.Find(_lists, id);

    /// <summary>[S-604] Create a list named uniquely from <paramref name="baseName"/>; returns its id.</summary>
    public string Create(string baseName)
    {
        var id = $"list-{Guid.NewGuid():n}";
        Apply(LocalTaskLists.Add(_lists, id, LocalTaskLists.UniqueName(_lists, baseName), baseName));
        return id;
    }

    /// <summary>[S-604] The list a widget switching to this PC should show, creating one only when there are none.</summary>
    public string Ensure(string? preferred, string baseName) => LocalTaskLists.Ensure(_lists, preferred) ?? Create(baseName);

    public void Rename(string id, string name) => Apply(LocalTaskLists.Rename(_lists, id, name));

    public void Delete(string id) => Apply(LocalTaskLists.Remove(_lists, id));

    /// <summary>[S-603] The one write path for every task change in a list.</summary>
    public void Update(string listId, Func<IReadOnlyList<LocalTask>, IReadOnlyList<LocalTask>> change)
    {
        ArgumentNullException.ThrowIfNull(change);

        if (Find(listId) is not { } list)
        {
            // The list went while a widget still showed it; the write is dropped, not a resurrection.
            Log.Warn(Scope, $"a change to list {listId} was dropped: it no longer exists");
            return;
        }

        var tasks = change(list.Tasks);
        if (!ReferenceEquals(tasks, list.Tasks))
        {
            Apply(LocalTaskLists.SetTasks(_lists, listId, tasks));
        }
    }

    /// <summary>Wait for the last change to reach the disk. For shutdown.</summary>
    public Task FlushAsync() => _saving;

    private void Apply(IReadOnlyList<LocalTaskList> next)
    {
        if (!Writable)
        {
            Log.Warn(Scope, "the lists on this PC are read-only this session; the change was not made");
            return;
        }

        _lists = next;
        RaiseChanged();
        QueueSave();
    }

    private void QueueSave()
    {
        if (_store is null)
        {
            return;
        }

        if (!_saving.IsCompleted)
        {
            // One write is out; the next one takes whatever the pool is when it finishes.
            _saveQueued = true;
            return;
        }

        _saving = SaveLoopAsync(_store);
    }

    private async Task SaveLoopAsync(LocalTaskStore store)
    {
        do
        {
            _saveQueued = false;

            try
            {
                await store.SaveAsync(_lists).ConfigureAwait(true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // The pool in memory is still right; the next change tries the disk again.
                Log.Error(Scope, $"could not save {store.Path}", ex);
            }
        }
        while (_saveQueued);
    }

    private void RaiseChanged()
    {
        if (Changed is not { } changed)
        {
            return;
        }

        foreach (var listener in changed.GetInvocationList().Cast<Action>())
        {
            try
            {
                listener();
            }
            catch (Exception ex)
            {
                // One widget that throws must not stop the others from hearing about the change.
                Log.Error(Scope, "a task-list listener threw", ex);
            }
        }
    }
}
