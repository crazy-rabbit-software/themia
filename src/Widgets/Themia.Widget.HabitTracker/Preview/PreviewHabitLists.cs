#if DEBUG
using Themia.Core.Habits;
using Themia.Sdk.WinUI;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only: habit lists held in memory for a preview, never the user's own. Every change is
/// the same <see cref="HabitLists"/> function the host's pool applies; nothing is written anywhere.
/// </summary>
internal sealed class PreviewHabitLists(IReadOnlyList<HabitList> lists) : IHabitLists
{
    private IReadOnlyList<HabitList> _lists = lists;

    public event Action? Changed;

    public IReadOnlyList<HabitList> Lists => _lists;

    public bool Writable => true;

    public string? Problem => null;

    public string? FilePath => null;

    public HabitList? Find(string? id) => HabitLists.Find(_lists, id);

    public string? Create(string baseName)
    {
        var id = HabitLists.NewId("list");
        Apply(HabitLists.Add(_lists, id, HabitLists.UniqueName(_lists, baseName), baseName));
        return id;
    }

    public string? FirstOrCreate() => HabitLists.First(_lists) ?? Create(HabitLists.NewListName);

    public void Rename(string id, string name) => Apply(HabitLists.Rename(_lists, id, name));

    public void Delete(string id) => Apply(HabitLists.Remove(_lists, id));

    public void Update(string listId, Func<IReadOnlyList<Habit>, IReadOnlyList<Habit>> change)
    {
        ArgumentNullException.ThrowIfNull(change);
        if (Find(listId) is not { } list)
        {
            return;
        }

        var habits = change(list.Habits);
        if (!ReferenceEquals(habits, list.Habits))
        {
            Apply(HabitLists.SetHabits(_lists, listId, habits));
        }
    }

    private void Apply(IReadOnlyList<HabitList> next)
    {
        _lists = next;
        Changed?.Invoke();
    }
}
#endif
