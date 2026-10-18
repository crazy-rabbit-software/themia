#if DEBUG
using Themia.App.Services.Tasks;
using Themia.Core.Tasks;

namespace Themia.App.Widgets;

/// <summary>A preview's task lists: held in memory, never saved (the host's pool with no store, for a debug render).</summary>
internal sealed class MemoryTaskLists(IReadOnlyList<LocalTaskList> seed) : ILocalTaskLists
{
    private List<LocalTaskList> _lists = [.. seed];

    public event Action? Changed;

    public bool Writable => true;

    public string? Problem => null;

    public IReadOnlyList<LocalTaskList> Lists => _lists;

    public LocalTaskList? Find(string? id) => LocalTaskLists.Find(_lists, id);

    public string Create(string baseName)
    {
        var id = $"list-{Guid.NewGuid():n}";
        Apply(LocalTaskLists.Add(_lists, id, LocalTaskLists.UniqueName(_lists, baseName), baseName));
        return id;
    }

    public string Ensure(string? preferred, string baseName) => LocalTaskLists.Ensure(_lists, preferred) ?? Create(baseName);

    public void Rename(string id, string name) => Apply(LocalTaskLists.Rename(_lists, id, name));

    public void Delete(string id) => Apply(LocalTaskLists.Remove(_lists, id));

    public void Update(string listId, Func<IReadOnlyList<LocalTask>, IReadOnlyList<LocalTask>> change)
    {
        if (Find(listId) is { } list && change(list.Tasks) is var tasks && !ReferenceEquals(tasks, list.Tasks))
        {
            Apply(LocalTaskLists.SetTasks(_lists, listId, tasks));
        }
    }

    private void Apply(IReadOnlyList<LocalTaskList> next)
    {
        _lists = [.. next];
        Changed?.Invoke();
    }
}
#endif
