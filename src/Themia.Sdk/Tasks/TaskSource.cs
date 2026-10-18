using Themia.Core.Accounts;
using Themia.Core.Tasks;
using Themia.Sdk;

namespace Themia.App.Services.Tasks;

/// <summary>[S-590] An account that holds task lists, as the task widgets list it.</summary>
public sealed record TaskAccount(string Id, ProviderId Provider, string Label, string? Problem);

/// <summary>
/// [S-590] [S-6913] Where the task widgets read and write: the host's one task service (the accounts'
/// lists through the offline store and their pending writes, [S-2610], and the lists kept on this
/// PC). A widget acquires a feed for the list it shows and releases it when it lets go; the last one
/// to let go stops its sync. Everything here is on the UI thread.
/// </summary>
public interface ITaskSource
{
    /// <summary>Raised when an account with tasks is added, removed, renamed or changes health.</summary>
    event Action? AccountsChanged;

    /// <summary>Every account that can hold task lists, in the order they were added.</summary>
    IReadOnlyList<TaskAccount> Accounts { get; }

    /// <summary>The lists kept on this PC.</summary>
    ILocalTaskLists Local { get; }

    TaskAccount? Find(string? accountId);

    /// <summary>An account's lists, held for a while; <paramref name="fresh"/> asks again.</summary>
    Task<IReadOnlyList<TaskListInfo>> ListsAsync(string accountId, bool fresh = false);

    /// <summary>A shared feed of one list (or Today) of an account.</summary>
    ITaskFeed Acquire(string accountId, string listId, bool showCompleted);

    void Release(ITaskFeed feed);

    /// <summary>The feed's account on <paramref name="interval"/>; <paramref name="force"/> is the user's Refresh.</summary>
    void Refresh(ITaskFeed feed, TimeSpan interval, bool force);

    Task SetCompletedAsync(ITaskFeed feed, TaskItem task, bool completed);

    Task RenameAsync(ITaskFeed feed, TaskItem task, string title);

    Task RenameListAsync(string accountId, TaskListInfo list, string name);

    Task DeleteAsync(ITaskFeed feed, TaskItem task);

    Task<TaskItem?> CreateAsync(ITaskFeed feed, string title);
}

/// <summary>One widget's view of a shared list of an account.</summary>
public interface ITaskFeed
{
    string AccountId { get; }

    string ListId { get; }

    bool ShowCompleted { get; }

    bool IsToday { get; }

    /// <summary>[S-2610] The writes not yet confirmed, over what the account holds.</summary>
    TaskEdits Edits { get; }

    IReadOnlyList<TaskItem> Tasks { get; }

    /// <summary>Something is held for the list (a sync landed or a shelf loaded).</summary>
    bool Loaded { get; }

    bool Busy { get; }

    /// <summary>Why the last sync failed, or null.</summary>
    AccountException? Failure { get; }

    /// <summary>Why the last write was refused, or null.</summary>
    string? WriteProblem { get; }

    DateTimeOffset? FetchedAt { get; }

    IReadOnlyList<TaskListInfo>? Lists { get; }

    /// <summary>Raised on the UI thread whenever anything above changed.</summary>
    event Action? Changed;
}

/// <summary>[S-3850] The task lists kept on this PC: the host's one pool, saved atomically.</summary>
public interface ILocalTaskLists
{
    event Action? Changed;

    bool Writable { get; }

    string? Problem { get; }

    /// <summary>Every list, in their order.</summary>
    IReadOnlyList<LocalTaskList> Lists { get; }

    LocalTaskList? Find(string? id);

    string Create(string baseName);

    string Ensure(string? preferred, string baseName);

    void Rename(string id, string name);

    void Delete(string id);

    void Update(string listId, Func<IReadOnlyList<LocalTask>, IReadOnlyList<LocalTask>> change);
}
