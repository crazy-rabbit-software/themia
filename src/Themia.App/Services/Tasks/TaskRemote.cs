using System.Globalization;
using System.Net.Http;
using Themia.App.Services.Accounts;
using Themia.Core.Accounts;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Sync;
using Themia.Core.Tasks;
using Themia.Sdk;

namespace Themia.App.Services.Tasks;

/// <summary>An account that can feed the Tasks widget, as the widget and its settings see it.</summary>
/// <param name="Problem">Why it cannot be used until the user signs in again, or null.</param>
internal sealed record TaskAccount(string Id, ProviderId Provider, string Label, string? Problem);

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

    /// <summary>
    /// [S-2686] One real list's tasks (never Today: the service fans that out): the changes since
    /// <paramref name="cursor"/> folded into <paramref name="held"/> when the cursor is usable, else
    /// the list in full. Returns the tasks and the cursor to keep (null when none can follow).
    /// </summary>
    Task<(IReadOnlyList<TaskItem> Tasks, SyncCursor? Cursor)> SyncListAsync(
        string accountId, string listId, bool includeCompleted, IReadOnlyList<TaskItem>? held, SyncCursor? cursor, CancellationToken cancellation);

    Task SetCompletedAsync(string accountId, string listId, string taskId, bool completed, CancellationToken cancellation);

    Task RenameAsync(string accountId, string listId, string taskId, string title, CancellationToken cancellation);

    Task DeleteAsync(string accountId, string listId, string taskId, CancellationToken cancellation);

    Task<TaskItem> CreateAsync(string accountId, string listId, string title, DateOnly? due, CancellationToken cancellation);

    /// <summary>[S-614] Rename a list at the provider.</summary>
    Task RenameListAsync(string accountId, string listId, string name, CancellationToken cancellation);
}

/// <summary>
/// [S-591] Microsoft To Do (Graph) and Google Tasks through <see cref="AccountService"/>: every
/// request authorized, retried on a throttle and refreshed on a 401 there, failing with a typed
/// <see cref="AccountException"/>. Addresses, bodies and mapping are <see cref="TaskWire"/>'s.
/// </summary>
internal sealed class AccountTaskRemote(AccountService accounts) : ITaskRemote
{
    private const string Scope = "tasks";

    /// <summary>The most entries one change round reads: a list's whole history of finished tasks, within reason.</summary>
    private const int MaxRound = 2000;

    public IReadOnlyList<TaskAccount> Accounts =>
    [
        .. accounts.WithFeature(HostFeatures.Tasks)
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

        if (provider == HostProviders.Google)
        {
            var lists = await accounts.GetGooglePagesAsync(accountId, address, "items", TaskWireContext.Default.GoogleTaskList, 100, cancellation)
                .ConfigureAwait(false);
            return TaskWire.FromGoogle(lists);
        }

        var graph = await accounts.GetGraphPagesAsync(accountId, address, TaskWireContext.Default.GraphTodoList, 100, cancellation)
            .ConfigureAwait(false);
        return [.. graph.Select(TaskWire.FromGraph).OfType<TaskListInfo>()];
    }

    /// <summary>[S-2690] Lists whose change requests Graph or Google refused this session: read the plain way.</summary>
    private readonly DeltaFallback _fallback = new(Scope);

    public Task<(IReadOnlyList<TaskItem> Tasks, SyncCursor? Cursor)> SyncListAsync(
        string accountId, string listId, bool includeCompleted, IReadOnlyList<TaskItem>? held, SyncCursor? cursor, CancellationToken cancellation)
    {
        var provider = Provider(accountId);
        return _fallback.ReadAsync($"{accountId}|{listId}", $"{accountId}: list {listId}",
            () => ChangesAsync(provider, accountId, listId, includeCompleted, held, cursor, cancellation),
            () => PlainAsync(provider, accountId, listId, includeCompleted, cancellation));
    }

    /// <summary>[S-2686] By the list's changes: from a usable cursor, else a fresh round (Graph delta, Google with an <c>updatedMin</c> cursor).</summary>
    private async Task<(IReadOnlyList<TaskItem> Tasks, SyncCursor? Cursor)> ChangesAsync(
        ProviderId provider, string accountId, string listId, bool includeCompleted, IReadOnlyList<TaskItem>? held, SyncCursor? cursor, CancellationToken cancellation)
    {
        var kind = provider == HostProviders.Google ? CursorKind.GoogleUpdatedMin : CursorKind.GraphDelta;
        var scope = TaskShelves.CursorScope(listId, includeCompleted);
        var now = DateTimeOffset.UtcNow;
        var check = held is null ? CursorCheck.Missing : SyncCursors.Check(cursor, kind, scope, now);

        if (check == CursorCheck.Usable && held is not null && cursor is not null)
        {
            try
            {
                return provider == HostProviders.Google
                    ? await GoogleChangesAsync(accountId, listId, includeCompleted, held, cursor, now, cancellation).ConfigureAwait(false)
                    : await GraphRoundAsync(accountId, listId, includeCompleted, held, cursor, now, cancellation).ConfigureAwait(false);
            }
            catch (AccountException ex) when (DeltaRules.Rejected(ex.HttpStatus, ex.ProviderMessage))
            {
                // [S-2681] The provider no longer knows the cursor: the list in full, now.
                Log.Info(Scope, $"{accountId}: list {listId} refused its change cursor (HTTP {ex.HttpStatus}); reading it in full");
            }
        }
        else if (check is CursorCheck.Stale or CursorCheck.OtherScope)
        {
            Log.Debug(Scope, $"{accountId}: list {listId} read in full ({check})");
        }

        return provider == HostProviders.Google
            ? await GoogleFullAsync(accountId, listId, includeCompleted, scope, now, cancellation).ConfigureAwait(false)
            : await GraphRoundAsync(accountId, listId, includeCompleted, null, null, now, cancellation).ConfigureAwait(false);
    }

    /// <summary>
    /// [S-2686] Graph: a delta round, from the start (the whole list, finished tasks filtered as the
    /// list wants) or from the stored delta link (only what changed). One path for both.
    /// </summary>
    private async Task<(IReadOnlyList<TaskItem>, SyncCursor?)> GraphRoundAsync(
        string accountId, string listId, bool includeCompleted, IReadOnlyList<TaskItem>? held, SyncCursor? cursor, DateTimeOffset now, CancellationToken cancellation)
    {
        var first = cursor is null ? TaskWire.GraphTasksDelta(listId) : new Uri(cursor.Value);
        var round = await accounts.GetDeltaAsync(accountId, first, DeltaDialect.Graph, TaskWireContext.Default.GraphTodoTask, MaxRound, cancellation,
            ("Prefer", "odata.maxpagesize=200")).ConfigureAwait(false);

        var changed = round.Items.Select(t => TaskWire.FromGraph(t, listId)).OfType<TaskItem>().ToList();
        var (tasks, whole) = DeltaMerge.Tasks(held ?? [], changed, round.Removed, includeCompleted, TaskWire.MaxTasksPerList);

        SyncCursor? next = round.Cursor is { } link && whole
            ? cursor is null
                ? SyncCursors.Start(CursorKind.GraphDelta, link, TaskShelves.CursorScope(listId, includeCompleted), now)
                : SyncCursors.Advance(cursor, link, now)
            : null;
        return (tasks, next);
    }

    /// <summary>
    /// [S-2690] The list in full the plain way, as before incremental sync, with no cursor: for a list
    /// whose change request was refused. Graph is filtered here ([S-591]); Google asks without the finished ones.
    /// </summary>
    private async Task<(IReadOnlyList<TaskItem>, SyncCursor?)> PlainAsync(
        ProviderId provider, string accountId, string listId, bool includeCompleted, CancellationToken cancellation)
    {
        var address = TaskWire.Tasks(provider, listId, includeCompleted);
        var all = provider == HostProviders.Google
            ? (await accounts.GetGooglePagesAsync(accountId, address, "items", TaskWireContext.Default.GoogleTask, TaskWire.MaxTasksPerList, cancellation)
                .ConfigureAwait(false)).Select(t => TaskWire.FromGoogle(t, listId)).OfType<TaskItem>().ToList()
            : (await accounts.GetGraphPagesAsync(accountId, address, TaskWireContext.Default.GraphTodoTask, TaskWire.MaxTasksPerList, cancellation)
                .ConfigureAwait(false)).Select(t => TaskWire.FromGraph(t, listId)).OfType<TaskItem>().ToList();

        var (tasks, _) = DeltaMerge.Tasks([], all, [], includeCompleted, TaskWire.MaxTasksPerList);
        return (tasks, null);
    }

    /// <summary>The whole Google list, and an <c>updatedMin</c> cursor a little before the request ([S-2686]).</summary>
    private async Task<(IReadOnlyList<TaskItem>, SyncCursor?)> GoogleFullAsync(
        string accountId, string listId, bool includeCompleted, string scope, DateTimeOffset now, CancellationToken cancellation)
    {
        var google = await accounts.GetGooglePagesAsync(accountId, TaskWire.Tasks(HostProviders.Google, listId, includeCompleted), "items",
            TaskWireContext.Default.GoogleTask, TaskWire.MaxTasksPerList, cancellation).ConfigureAwait(false);

        var all = google.Select(t => TaskWire.FromGoogle(t, listId)).OfType<TaskItem>().ToList();
        var (tasks, whole) = DeltaMerge.Tasks([], all, [], includeCompleted, TaskWire.MaxTasksPerList);
        return (tasks, whole ? SyncCursors.Start(CursorKind.GoogleUpdatedMin, Instant(now - DeltaRules.ClockOverlap), scope, now) : null);
    }

    /// <summary>The Google tasks changed since the cursor's instant, deleted and hidden ones included ([S-2686]).</summary>
    private async Task<(IReadOnlyList<TaskItem>, SyncCursor?)> GoogleChangesAsync(
        string accountId, string listId, bool includeCompleted, IReadOnlyList<TaskItem> held, SyncCursor cursor, DateTimeOffset now, CancellationToken cancellation)
    {
        // Validated on load ([S-2681]): the value parses.
        var since = DateTimeOffset.Parse(cursor.Value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
        var round = await accounts.GetDeltaAsync(accountId, TaskWire.GoogleTasksSince(listId, since), DeltaDialect.GoogleTasks,
            TaskWireContext.Default.GoogleTask, MaxRound, cancellation).ConfigureAwait(false);

        var changed = round.Items.Select(t => TaskWire.FromGoogle(t, listId)).OfType<TaskItem>().ToList();
        var (tasks, whole) = DeltaMerge.Tasks(held, changed, round.Removed, includeCompleted, TaskWire.MaxTasksPerList);
        return (tasks, round.Complete && whole ? SyncCursors.Advance(cursor, Instant(now - DeltaRules.ClockOverlap), now) : null);
    }

    private static string Instant(DateTimeOffset moment) => moment.UtcDateTime.ToString("o", CultureInfo.InvariantCulture);

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

    public Task RenameListAsync(string accountId, string listId, string name, CancellationToken cancellation)
    {
        var provider = Provider(accountId);
        return accounts.SendJsonAsync(accountId, HttpMethod.Patch, TaskWire.List(provider, listId), TaskWire.RenameListBody(provider, name), cancellation);
    }

    private ProviderId Provider(string accountId)
        => accounts.Find(accountId) is { } account && TaskWire.Supports(account.Provider)
            ? account.Provider
            : throw new AccountException(AccountError.UnknownAccount, Str.Shell_Accounts_NotSignedIn.Text());
}
