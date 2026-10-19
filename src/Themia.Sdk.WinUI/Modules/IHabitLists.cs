using Themia.Core.Habits;

namespace Themia.Sdk.WinUI;

/// <summary>
/// [S-1420] The habit lists kept on this PC as a habit tracker and its properties menu reach them
/// (<see cref="IWidgetHost.Habits"/>, <see cref="SettingsHost.Habits"/>): the host's one pool, so two
/// widgets can show one list and ticking a box in one ticks it in the other. The UI thread is the only
/// mutator; every change is a pure <see cref="HabitLists"/> function on an immutable snapshot. The file
/// (<c>habits.json</c>), its validation and its atomic, coalesced saves stay the host's.
/// </summary>
public interface IHabitLists
{
    /// <summary>Raised on the UI thread after every change.</summary>
    event Action? Changed;

    IReadOnlyList<HabitList> Lists { get; }

    /// <summary>False when the file exists but could not be read: nothing is written over it.</summary>
    bool Writable { get; }

    /// <summary>Why the file could not be used at start, or null.</summary>
    string? Problem { get; }

    /// <summary>[S-1430] Where <c>habits.json</c> lives, or null for lists kept in memory only (a debug render).</summary>
    string? FilePath { get; }

    HabitList? Find(string? id);

    /// <summary>[S-1429] Create a list named uniquely from <paramref name="baseName"/>; its id, or null when read-only.</summary>
    string? Create(string baseName);

    /// <summary>[S-1420] The list a widget with none chosen shows: the first, or a new one when there are none.</summary>
    string? FirstOrCreate();

    void Rename(string id, string name);

    void Delete(string id);

    /// <summary>[S-1420] The one write path for every habit change in a list. A change that returns its input writes nothing.</summary>
    void Update(string listId, Func<IReadOnlyList<Habit>, IReadOnlyList<Habit>> change);
}
