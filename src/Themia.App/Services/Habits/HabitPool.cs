using Themia.Core.Diagnostics;
using Themia.Core.Habits;
using Themia.Core.Localization;
using Themia.Core.Storage;

namespace Themia.App.Services.Habits;

/// <summary>
/// [S-1420] The habit lists kept on this PC: one pool, shared by every Habits widget and its
/// settings, so two widgets can show one list, ticking a box in one ticks it in the other, and
/// deleting a widget never takes its habits.
/// <para>
/// The UI thread is the only mutator, and every mutation is a pure function from
/// <see cref="HabitLists"/> applied to an immutable snapshot, so what is saved is always a whole pool.
/// Saves are single-flight and coalesced: a burst of ticks while one write is out produces one more
/// write, of the latest snapshot, and never two writers at once.
/// </para>
/// </summary>
internal sealed class HabitPool
{
    private const string Scope = "habits";

    private readonly HabitListStore? _store;
    private IReadOnlyList<HabitList> _lists;
    private Task _saving = Task.CompletedTask;
    private bool _saveQueued;

    /// <param name="store">Where the pool lives, or null to keep it in memory only (a debug render).</param>
    public HabitPool(HabitListStore? store, IReadOnlyList<HabitList>? seed = null)
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
                ? Str.Shell_Store_HabitsRecovered.Format(("detail", load.Detail))
                : Str.Shell_Store_HabitsReadOnly.Format(("detail", load.Detail))
            : null;

        Log.Info(Scope, $"{_lists.Count} habit list(s) loaded from {store.Path}");
    }

    /// <summary>Raised on the UI thread after every change.</summary>
    public event Action? Changed;

    public IReadOnlyList<HabitList> Lists => _lists;

    /// <summary>False when the file exists but could not be read: nothing is written over it.</summary>
    public bool Writable { get; }

    /// <summary>Why the file could not be used at start, or null.</summary>
    public string? Problem { get; }

    /// <summary>[S-1430] Where <c>habits.json</c> lives, or null for a pool kept in memory only (a debug render).</summary>
    public string? FilePath => _store?.Path;

    public HabitList? Find(string? id) => HabitLists.Find(_lists, id);

    /// <summary>[S-1429] Create a list named uniquely from <paramref name="baseName"/>; returns its id, or null when read-only.</summary>
    public string? Create(string baseName)
    {
        if (!Writable)
        {
            Log.Warn(Scope, "the habit lists are read-only this session; no list was created");
            return null;
        }

        var id = HabitLists.NewId("list");
        Apply(HabitLists.Add(_lists, id, HabitLists.UniqueName(_lists, baseName), baseName));
        return id;
    }

    /// <summary>[S-1420] The list a widget with none chosen shows: the first, or a new one when there are none.</summary>
    public string? FirstOrCreate() => HabitLists.First(_lists) ?? Create(HabitLists.NewListName);

    public void Rename(string id, string name) => Apply(HabitLists.Rename(_lists, id, name));

    public void Delete(string id) => Apply(HabitLists.Remove(_lists, id));

    /// <summary>[S-1420] The one write path for every habit change in a list. A change that returns its input writes nothing.</summary>
    public void Update(string listId, Func<IReadOnlyList<Habit>, IReadOnlyList<Habit>> change)
    {
        ArgumentNullException.ThrowIfNull(change);

        if (Find(listId) is not { } list)
        {
            // The list went while a widget still showed it; the write is dropped, not a resurrection.
            Log.Warn(Scope, $"a change to habit list {listId} was dropped: it no longer exists");
            return;
        }

        var habits = change(list.Habits);
        if (!ReferenceEquals(habits, list.Habits))
        {
            Apply(HabitLists.SetHabits(_lists, listId, habits));
        }
    }

    /// <summary>Wait for the last change to reach the disk. For shutdown.</summary>
    public Task FlushAsync() => _saving;

    private void Apply(IReadOnlyList<HabitList> next)
    {
        if (!Writable)
        {
            Log.Warn(Scope, "the habit lists are read-only this session; the change was not made");
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

    private async Task SaveLoopAsync(HabitListStore store)
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
                Log.Error(Scope, "a habit-list listener threw", ex);
            }
        }
    }
}
