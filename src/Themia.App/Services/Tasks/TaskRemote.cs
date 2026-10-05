using System.Net.Http;
using Themia.App.Services.Accounts;
using Themia.Core.Accounts;
using Themia.Core.Localization;
using Themia.Core.Tasks;

namespace Themia.App.Services.Tasks;

/// <summary>An account that can feed the Tasks widget, as the widget and its settings see it.</summary>
/// <param name="Problem">Why it cannot be used until the user signs in again, or null.</param>
internal sealed record TaskAccount(string Id, AccountProvider Provider, string Label, string? Problem);

/// <summary>
/// Where account tasks come from. One implementation, over Themia's own accounts.
/// </summary>
internal interface ITaskRemote
{
    /// <summary>Microsoft and Google accounts, in the order they were added.</summary>
    IReadOnlyList<TaskAccount> Accounts { get; }

    /// <summary>Raised on the UI thread when an account is added, removed or changes health.</summary>
    event Action? AccountsChanged;

    Task<IReadOnlyList<TaskListInfo>> ListsAsync(string accountId, CancellationToken cancellation);

    /// <summary>One real list's tasks (never Today: the service fans that out).</summary>
    Task<IReadOnlyList<TaskItem>> TasksAsync(string accountId, string listId, bool showCompleted, CancellationToken cancellation);

    Task SetCompletedAsync(string accountId, string listId, string taskId, bool completed, CancellationToken cancellation);

    Task RenameAsync(string accountId, string listId, string taskId, string title, CancellationToken cancellation);

    Task DeleteAsync(string accountId, string listId, string taskId, CancellationToken cancellation);

    Task<TaskItem> CreateAsync(string accountId, string listId, string title, DateOnly? due, CancellationToken cancellation);
}

/// <summary>
/// [S-591] Microsoft To Do (Graph) and Google Tasks through <see cref="AccountService"/>: every
/// request authorized, retried on a throttle and refreshed on a 401 there, failing with a typed
/// <see cref="AccountException"/>. Addresses, bodies and mapping are <see cref="TaskWire"/>'s.
/// </summary>
internal sealed class AccountTaskRemote(AccountService accounts) : ITaskRemote
{
    public IReadOnlyList<TaskAccount> Accounts =>
    [
        .. accounts.WithFeature(AccountFeatures.Tasks)
            .Where(a => TaskWire.Supports(a.Provider))
            .Select(a => new TaskAccount(a.Id, a.Provider, a.Label, a.Problem)),
    ];

    public event Action? AccountsChanged
    {
        add => accounts.AccountsChanged += value;
        remove => accounts.AccountsChanged -= value;
    }

    public async Task<IReadOnlyList<TaskListInfo>> ListsAsync(string accountId, CancellationToken cancellation)
    {
        var provider = Provider(accountId);
        var address = TaskWire.Lists(provider);

        if (provider == AccountProvider.Google)
        {
            var lists = await accounts.GetGooglePagesAsync(accountId, address, "items", TaskWireContext.Default.GoogleTaskList, 100, cancellation)
                .ConfigureAwait(false);
            return TaskWire.FromGoogle(lists);
        }

        var graph = await accounts.GetGraphPagesAsync(accountId, address, TaskWireContext.Default.GraphTodoList, 100, cancellation)
            .ConfigureAwait(false);
        return [.. graph.Select(TaskWire.FromGraph).OfType<TaskListInfo>()];
    }

    public async Task<IReadOnlyList<TaskItem>> TasksAsync(string accountId, string listId, bool showCompleted, CancellationToken cancellation)
    {
        var provider = Provider(accountId);
        var address = TaskWire.Tasks(provider, listId, showCompleted);

        IEnumerable<TaskItem> tasks;
        if (provider == AccountProvider.Google)
        {
            var google = await accounts.GetGooglePagesAsync(accountId, address, "items", TaskWireContext.Default.GoogleTask, TaskWire.MaxTasksPerList, cancellation)
                .ConfigureAwait(false);
            tasks = google.Select(t => TaskWire.FromGoogle(t, listId)).OfType<TaskItem>();
        }
        else
        {
            var graph = await accounts.GetGraphPagesAsync(accountId, address, TaskWireContext.Default.GraphTodoTask, TaskWire.MaxTasksPerList, cancellation)
                .ConfigureAwait(false);
            tasks = graph.Select(t => TaskWire.FromGraph(t, listId)).OfType<TaskItem>();
        }

        return [.. showCompleted ? tasks : tasks.Where(t => !t.Completed)];
    }

    public Task SetCompletedAsync(string accountId, string listId, string taskId, bool completed, CancellationToken cancellation)
    {
        var provider = Provider(accountId);
        return accounts.SendJsonAsync(accountId, HttpMethod.Patch, TaskWire.Task(provider, listId, taskId), TaskWire.CompleteBody(provider, completed), cancellation);
    }

    public Task RenameAsync(string accountId, string listId, string taskId, string title, CancellationToken cancellation)
        => accounts.SendJsonAsync(accountId, HttpMethod.Patch, TaskWire.Task(Provider(accountId), listId, taskId), TaskWire.RenameBody(title), cancellation);

    public Task DeleteAsync(string accountId, string listId, string taskId, CancellationToken cancellation)
        => accounts.SendJsonAsync(accountId, HttpMethod.Delete, TaskWire.Task(Provider(accountId), listId, taskId), null, cancellation);

    public async Task<TaskItem> CreateAsync(string accountId, string listId, string title, DateOnly? due, CancellationToken cancellation)
    {
        var provider = Provider(accountId);
        var reply = await accounts.SendJsonAsync(accountId, HttpMethod.Post, TaskWire.Create(provider, listId), TaskWire.CreateBody(provider, title, due), cancellation)
            .ConfigureAwait(false);

        return TaskWire.ParseCreated(provider, reply.Body, listId)
            ?? throw new AccountException(AccountError.BadResponse, Str.Shell_Tasks_CreatedUndescribed.Text(), reply.Status);
    }

    private AccountProvider Provider(string accountId)
        => accounts.Find(accountId) is { } account && TaskWire.Supports(account.Provider)
            ? account.Provider
            : throw new AccountException(AccountError.UnknownAccount, Str.Shell_Accounts_NotSignedIn.Text());
}
