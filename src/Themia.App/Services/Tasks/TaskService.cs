using Themia.App.Services.Sync;
using Themia.Core.Accounts;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Sync;
using Themia.Core.Tasks;
using Themia.Core.Widgets;

namespace Themia.App.Services.Tasks;

/// <summary>
/// One widget's view of an account's tasks: one list, or Today across all of them ([S-596]).
/// <para>
/// A view, not a store: the tasks, the pending changes ([S-597]) and how the last sync went live in
/// the account's box ([S-2650]), so every widget on the account — a list and Today — shows the same
/// ticks, and a widget that comes back (a view switch) finds them at once. Lives on the UI thread;
/// <see cref="Changed"/> is raised there.
/// </para>
/// </summary>
internal sealed class TaskFeed : ITaskFeed
{
    internal TaskFeed(TaskService.Box box, string listId, bool showCompleted)
    {
        Owner = box;
        ListId = listId;
        ShowCompleted = showCompleted;
    }

    internal TaskService.Box Owner { get; }

    public string AccountId => Owner.AccountId;

    /// <summary>A real list id, or <see cref="TaskRules.TodayListId"/>.</summary>
    public string ListId { get; }

    public bool ShowCompleted { get; }

    public bool IsToday => TaskRules.IsToday(ListId);

    /// <summary>[S-621] The account's tasks are read-only (CalDAV).</summary>
    public bool ReadOnly => Owner.ReadOnly;

    /// <summary>The account's changes: shared by every feed on it.</summary>
    public TaskEdits Edits => Owner.Edits;

    /// <summary>What the widget draws: the stored tasks with the pending changes applied, this list's or every list's for Today.</summary>
    public IReadOnlyList<TaskItem> Tasks
    {
        get
        {
            var all = Owner.Edits.Current;
            return IsToday ? all : [.. all.Where(t => t.ListId == ListId)];
        }
    }

    /// <summary>Whether there is anything to draw: this list (or, for Today, the account) has been synced at least once.</summary>
    public bool Loaded => IsToday ? Owner.HeldAny : Owner.Holds(ListId);

    /// <summary>A sync is out.</summary>
    public bool Busy => Owner.Busy;

    /// <summary>How the last sync failed, or null when it worked.</summary>
    public AccountException? Failure => Owner.Failure;

    /// <summary>[S-2653] The last change the provider refused, or the changes still waiting, in words; else null.</summary>
    public string? WriteProblem => Owner.WriteNote;

    /// <summary>When what is drawn was synced.</summary>
    public DateTimeOffset? FetchedAt => IsToday ? Owner.LastSync : Owner.At(ListId);

    /// <summary>[S-614] The account's lists as last read, renames still on their way included; null before the first read.</summary>
    public IReadOnlyList<TaskListInfo>? Lists => Owner.Lists;

    public event Action? Changed;

    internal int Holders { get; set; }

    internal string Key => Of(AccountId, ListId, ShowCompleted);

    internal static string Of(string accountId, string listId, bool showCompleted) => $"{accountId}\n{listId}\n{showCompleted}";

    internal void Raise()
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
                Log.Error("tasks", "a task-feed listener threw", ex);
            }
        }
    }
}

/// <summary>
/// [S-591] [S-2650] Tasks for every widget, from one place: Themia's own accounts through
/// <see cref="ITaskRemote"/> and the offline store, and the lists kept on this PC through
/// <see cref="Local"/>. One instance, owned by the host and reached through <see cref="AppServices.Tasks"/>.
/// <para>
/// <b>Sync owns the network.</b> Each task account a widget in any view reads ([S-2608]) has a
/// <see cref="Box"/>, an entry of the one sync engine ([S-2691]): its lists and the tasks of every list a
/// widget shows (all of them once Today is shown, [S-2652]), read from the account's shelf off the UI thread
/// at start ([S-2693]) and saved after every change. One sync per account reads the lists, then each wanted
/// list in turn, at the fastest refresh any of its widgets asks for; a Refresh while one is out runs once more
/// after it; failures back off; what is in flight is cancelled when the account leaves or the service closes.
/// A widget's own tick asks nothing.
/// </para>
/// <para>
/// <b>Writing</b> is optimistic, queued and in order ([S-597], [S-2653]): the change shows at once and
/// waits in the account's persisted queue (the one <see cref="WriteDrain{TResult}"/>, [S-2692]) until the
/// provider confirms it; offline or throttled it waits and retries, still shown; a refusal takes it back and
/// says why.
/// </para>
/// </summary>
internal sealed class TaskService : ITaskSource, IDisposable
{
    private const string Scope = "tasks";

    private static readonly TimeSpan DefaultInterval = TimeSpan.FromSeconds(60);

    private readonly ITaskRemote _remote;
    private readonly SyncHost _sync;
    private readonly SyncEngine<Box> _engine;
    private readonly Dictionary<string, TaskFeed> _feeds = new(StringComparer.Ordinal);

    public TaskService(ITaskRemote remote, LocalTaskPool local, Ticker ticker, SyncHost sync)
    {
        ArgumentNullException.ThrowIfNull(ticker);
        _remote = remote;
        _sync = sync;
        Local = local;
        _engine = new SyncEngine<Box>(new SyncEntries<Box>(Scope), static (box, _, cancellation) => box.SyncAsync(cancellation), () => DateTimeOffset.UtcNow, ticker.Subscribe, box => box.MaySend);
        _engine.Changed += box => box.Raise();
        _engine.Polled += DrainAll;
        _remote.AccountsChanged += OnAccountsChanged;
        _sync.InterestsChanged += Reconcile;
        _sync.NetworkAvailable += OnNetworkAvailable;
        Reconcile();
    }

    /// <summary>Raised on the UI thread when the accounts that can feed tasks changed.</summary>
    public event Action? AccountsChanged;

    /// <summary>The lists kept on this PC.</summary>
    public LocalTaskPool Local { get; }

    ILocalTaskLists ITaskSource.Local => Local;

    public IReadOnlyList<TaskAccount> Accounts => _remote.Accounts;

    public TaskAccount? Find(string? accountId)
        => string.IsNullOrEmpty(accountId) ? null : Accounts.FirstOrDefault(a => a.Id == accountId);

    /// <summary>
    /// An account's lists: the stored ones when there are, else read now (single-flight). A failure
    /// with nothing stored throws, so the caller can say why.
    /// </summary>
    public async Task<IReadOnlyList<TaskListInfo>> ListsAsync(string accountId, bool fresh = false)
    {
        var box = BoxFor(accountId) ?? throw new AccountException(AccountError.UnknownAccount, Str.Shell_Accounts_NotSignedIn.Text());
        await box.Loaded.ConfigureAwait(true);

        if (!fresh && box.Lists is { } held)
        {
            return held;
        }

        box.ListsFlight ??= box.LoadListsAsync(box.Cancellation);
        try
        {
            return await box.ListsFlight.ConfigureAwait(true);
        }
        catch (OperationCanceledException) when (box.Cancellation.IsCancellationRequested)
        {
            // The account left (or the service closed) while its lists were read.
            throw new AccountException(AccountError.UnknownAccount, Str.Shell_Accounts_NotSignedIn.Text());
        }
        finally
        {
            box.ListsFlight = null;
        }
    }

    /// <summary>Hold a feed. Every <see cref="Acquire"/> is matched by one <see cref="Release"/>.</summary>
    public ITaskFeed Acquire(string accountId, string listId, bool showCompleted)
    {
        var key = TaskFeed.Of(accountId, listId, showCompleted);

        if (!_feeds.TryGetValue(key, out var feed))
        {
            var box = _engine.Entries.Want(accountId, () => Make(accountId));
            feed = new TaskFeed(box, listId, showCompleted);
            _feeds[key] = feed;
            _engine.EnsureTicking();
        }

        feed.Holders++;
        return feed;
    }

    public void Release(ITaskFeed shared)
    {
        var feed = (TaskFeed)shared;
        ArgumentNullException.ThrowIfNull(feed);

        if (--feed.Holders <= 0 && _feeds.TryGetValue(feed.Key, out var held) && ReferenceEquals(held, feed))
        {
            // The list stays synced while a widget in any view shows it; only the view goes.
            _feeds.Remove(feed.Key);
        }
    }

    /// <summary>
    /// [S-2609] Sync the feed's account. Only <paramref name="force"/> asks (a user action, a changed
    /// setting, a list never synced): the timer is the service's, not the widget's.
    /// </summary>
    public void Refresh(ITaskFeed shared, TimeSpan interval, bool force)
    {
        var feed = (TaskFeed)shared;
        ArgumentNullException.ThrowIfNull(feed);

        if (_engine.Disposed || !force)
        {
            return;
        }

        _engine.Force([feed.Owner]);
    }

    // ---- writes ----------------------------------------------------------------------------------

    /// <summary>[S-597] Complete or reopen a task, optimistically.</summary>
    public Task SetCompletedAsync(ITaskFeed shared, TaskItem task, bool completed)
    {
        var feed = (TaskFeed)shared;
        ArgumentNullException.ThrowIfNull(feed);
        ArgumentNullException.ThrowIfNull(task);

        if (feed.ReadOnly || feed.Edits.IsUnconfirmedCreate(task.Id))
        {
            return Task.CompletedTask; // [S-621] read-only, or no real id to write to yet
        }

        return feed.Owner.Enqueue(completed ? PendingKind.TaskComplete : PendingKind.TaskReopen, task.Id, task.ListId, null, null, TaskEdit.Complete(task.Id, completed, DateTimeOffset.UtcNow));
    }

    /// <summary>[S-600] Rename a task. A blank or unchanged title writes nothing.</summary>
    public Task RenameAsync(ITaskFeed shared, TaskItem task, string title)
    {
        var feed = (TaskFeed)shared;
        ArgumentNullException.ThrowIfNull(feed);
        ArgumentNullException.ThrowIfNull(task);

        var clean = title.Trim();
        if (feed.ReadOnly || clean.Length == 0 || clean == task.Title || feed.Edits.IsUnconfirmedCreate(task.Id))
        {
            return Task.CompletedTask;
        }

        return feed.Owner.Enqueue(PendingKind.TaskRename, task.Id, task.ListId, clean, null, TaskEdit.Rename(task.Id, clean));
    }

    /// <summary>
    /// [S-614] Rename an account list. Queued and in order like every task change ([S-2610]): the new
    /// name shows at once (the header, Today's row names, every widget on the account), waits offline,
    /// and a refusal puts the old one back and says why. A blank or unchanged name, Today and To Do's
    /// own lists write nothing.
    /// </summary>
    public Task RenameListAsync(string accountId, TaskListInfo list, string name)
    {
        ArgumentNullException.ThrowIfNull(list);

        if (!TaskRules.CanRename(list) || TaskRules.RenameTo(list.Name, name) is not { } clean || BoxFor(accountId) is not { ReadOnly: false } box)
        {
            return Task.CompletedTask;
        }

        return box.Enqueue(PendingKind.TaskListRename, list.Id, list.Id, clean, null, null);
    }

    /// <summary>[S-597] Delete a task; a refusal puts it back where it was.</summary>
    public Task DeleteAsync(ITaskFeed shared, TaskItem task)
    {
        var feed = (TaskFeed)shared;
        ArgumentNullException.ThrowIfNull(feed);
        ArgumentNullException.ThrowIfNull(task);

        if (feed.ReadOnly || feed.Edits.IsUnconfirmedCreate(task.Id))
        {
            return Task.CompletedTask;
        }

        return feed.Owner.Enqueue(PendingKind.TaskDelete, task.Id, task.ListId, null, null, TaskEdit.Delete(task.Id));
    }

    /// <summary>
    /// [S-599] Create a task at the bottom of the list it was typed into. From Today it is created due
    /// today in the account's default list ([S-596]), or it would vanish from the view it was typed
    /// into. Returns the provider's task once it confirmed, the temporary one while it waits to sync,
    /// or null when nothing was created.
    /// </summary>
    public async Task<TaskItem?> CreateAsync(ITaskFeed shared, string title)
    {
        var feed = (TaskFeed)shared;
        ArgumentNullException.ThrowIfNull(feed);

        var clean = title.Trim();
        if (clean.Length == 0 || feed.ReadOnly)
        {
            return null;
        }

        var box = feed.Owner;
        string listId;
        DateOnly? due = null;

        if (feed.IsToday)
        {
            IReadOnlyList<TaskListInfo> lists;
            try
            {
                lists = await ListsAsync(feed.AccountId).ConfigureAwait(true);
            }
            catch (AccountException ex)
            {
                box.Refuse(Str.Shell_Common_ProblemReason.Format(("problem", Str.Shell_Tasks_AddFailed.Text()), ("reason", ex.Message)));
                Log.Warn(Scope, "could not read the lists to add a task to", ex);
                return null;
            }

            if (TaskRules.CreateTarget(lists) is not { } target)
            {
                box.Refuse(Str.Shell_Tasks_NoList.Text());
                return null;
            }

            listId = target.Id;
            due = DateOnly.FromDateTime(DateTime.Now);
        }
        else
        {
            listId = feed.ListId;
        }

        var optimistic = Box.Optimistic($"pending-{Guid.NewGuid():n}", listId, clean, due);
        return await box.EnqueueCreateAsync(optimistic, due).ConfigureAwait(true);
    }

    public void Dispose()
    {
        if (_engine.Disposed)
        {
            return;
        }

        // [S-2691] Every sync and send in flight hears it; what they bring back after is dropped.
        _engine.Dispose();
        _remote.AccountsChanged -= OnAccountsChanged;
        _sync.InterestsChanged -= Reconcile;
        _sync.NetworkAvailable -= OnNetworkAvailable;
        AccountsChanged = null;
        _feeds.Clear();

        foreach (var box in _engine.Entries.All)
        {
            box.Cancel();
        }
    }

    // ---- what to sync ----------------------------------------------------------------------------

    /// <summary>A box for an account, its shelf read off the UI thread ([S-2693]).</summary>
    private Box Make(string accountId)
    {
        var box = new Box(this, accountId);
        box.Loaded = box.LoadAsync();
        return box;
    }

    private Box? BoxFor(string accountId)
    {
        if (_engine.Entries.Find(accountId) is { } box)
        {
            return box;
        }

        if (Find(accountId) is null)
        {
            return null;
        }

        box = _engine.Entries.Hold(accountId, () => Make(accountId));
        _engine.EnsureTicking();
        return box;
    }

    /// <summary>The account a widget's interest names: its own, else the first task account ([S-590]).</summary>
    private string? Resolve(string accountId)
        => accountId.Length > 0 ? Find(accountId)?.Id : Accounts.FirstOrDefault()?.Id;

    /// <summary>Follow the accounts and the widgets of every view: an account a widget reads is a wanted entry.</summary>
    private void Reconcile()
    {
        if (_engine.Disposed)
        {
            return;
        }

        var known = new HashSet<string>(Accounts.Select(a => a.Id), StringComparer.Ordinal);
        foreach (var gone in _engine.Entries.RemoveWhere(b => !known.Contains(b.AccountId)))
        {
            gone.Cancel();
        }

        _engine.Reconcile(() =>
        {
            foreach (var box in _engine.Entries.All)
            {
                box.Interests.Clear();
            }

            foreach (var interest in _sync.Interests.Tasks)
            {
                if (Resolve(interest.AccountId) is { } id && BoxFor(id) is { } box)
                {
                    box.Interests.Add((interest.ListId, interest.ShowCompleted, interest.Interval));
                }
            }

            foreach (var box in _engine.Entries.All.ToList())
            {
                if (box.Interests.Count > 0 || box.Feeds.Any())
                {
                    _engine.Entries.Want(box.Key, () => box);
                }

                box.Schedule.Interval = box.Interests.Count == 0 ? DefaultInterval : box.Interests.Min(i => i.Interval);
            }
        }, drop: false);
    }

    /// <summary>After every look at what is due: each account's queue sends what is ready.</summary>
    private void DrainAll()
    {
        foreach (var box in _engine.Entries.All.ToArray())
        {
            _ = box.Drain.DrainAsync();
        }
    }

    private void OnNetworkAvailable()
    {
        foreach (var box in _engine.Entries.All)
        {
            box.Drain.Wake();
        }

        _engine.Wake();
    }

    private void OnAccountsChanged()
    {
        Reconcile();

        if (AccountsChanged is not { } changed)
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
                Log.Error(Scope, "an accounts listener threw", ex);
            }
        }
    }

    // ---- one account -----------------------------------------------------------------------------

    /// <summary>[S-2650] One task account: its lists, its held tasks, its queue, its shelf; an entry of the sync engine.</summary>
    internal sealed class Box : SyncEntry
    {
        private readonly TaskService _owner;
        private readonly Dictionary<string, (DateTimeOffset At, bool IncludesCompleted, IReadOnlyList<TaskItem> Tasks, SyncCursor? Cursor)> _held = new(StringComparer.Ordinal);
        private readonly Dictionary<string, long> _tickets = new(StringComparer.Ordinal);
        private readonly CancellationTokenSource _cancel;

        /// <summary>
        /// [S-614] Renames the provider confirmed, by the list clock: kept on top of the lists until a
        /// read that started after the confirmation lands, so a racing read cannot flash the old name.
        /// </summary>
        private readonly List<(string ListId, string Name, long ConfirmedAt)> _listRenames = [];
        private IReadOnlyList<TaskListInfo>? _lists;
        private long _listClock;
        private DateTimeOffset? _listsAt;
        private string? _refused;

        public Box(TaskService owner, string accountId)
            : base(accountId, DefaultInterval)
        {
            _owner = owner;
            AccountId = accountId;
            _cancel = CancellationTokenSource.CreateLinkedTokenSource(owner._engine.Cancellation);
            Drain = new WriteDrain<TaskItem>(Scope, accountId, SendAsync, () => DateTimeOffset.UtcNow, () => MaySend, _cancel.Token);
            Drain.Settled += OnSettled;
        }

        public string AccountId { get; }

        public TaskEdits Edits { get; } = new();

        public WriteDrain<TaskItem> Drain { get; }

        /// <summary>[S-2693] The account's shelf read, off the UI thread.</summary>
        public Task Loaded { get; set; } = Task.CompletedTask;

        /// <summary>The shelf is still being read: nothing is asked or sent, nothing saved over it.</summary>
        public bool Loading { get; private set; } = true;

        public CancellationToken Cancellation => _cancel.Token;

        /// <summary>The lists as last read, with the renames still queued or not yet read back on top ([S-614]).</summary>
        public IReadOnlyList<TaskListInfo>? Lists => _lists is null
            ? null
            : TaskShelves.Renamed(_lists, [
                .. _listRenames.Select(r => (r.ListId, r.Name)),
                .. Drain.Pending.Where(w => w.Kind == PendingKind.TaskListRename && w.Text is not null).Select(w => (w.Target, w.Text!)),
            ]);

        public Task<IReadOnlyList<TaskListInfo>>? ListsFlight { get; set; }

        /// <summary>(list, show completed, refresh) of every widget on this account in any view.</summary>
        public List<(string ListId, bool ShowCompleted, TimeSpan Interval)> Interests { get; } = [];

        public IEnumerable<TaskFeed> Feeds => _owner._feeds.Values.Where(f => ReferenceEquals(f.Owner, this));

        public bool Busy => Schedule.Busy;

        public AccountException? Failure { get; private set; }

        public bool HeldAny => _held.Count > 0 || _lists is { Count: 0 };

        public DateTimeOffset? LastSync => Schedule.LastSuccess;

        public bool Usable => _owner.Find(AccountId) is { Problem: null };

        /// <summary>Whether it may be asked or send now: signed in, its shelf read, its account still here.</summary>
        public bool MaySend => Usable && !Loading && !_cancel.IsCancellationRequested;

        /// <summary>[S-621] Nothing is written for this account: every write below returns before it queues.</summary>
        public bool ReadOnly => _owner.Find(AccountId) is { ReadOnly: true };

        /// <summary>
        /// The last refusal, else — once the head has had to wait (offline, throttled, sign-in) — how
        /// many changes are queued. A change on its way out normally says nothing.
        /// </summary>
        public string? WriteNote => _refused
            ?? (Drain.Waiting is > 0 and var waiting ? Str.Sync_Pending.Plural(waiting, ("count", waiting)) : null);

        public override string Subject => AccountId;

        public bool Holds(string listId) => _held.ContainsKey(listId);

        public DateTimeOffset? At(string listId) => _held.TryGetValue(listId, out var held) ? held.At : null;

        public static TaskItem Optimistic(string id, string listId, string title, DateOnly? due)
            => new(id, listId, title, false, false, due is { } day ? $"{TaskDue.Wire(day)}T00:00:00.0000000" : null);

        /// <summary>[S-2653] [S-2693] The account's shelf, read off the UI thread: its lists, held tasks and the queue, shown again.</summary>
        public async Task LoadAsync()
        {
            ShelfRead<TasksShelf> read;
            try
            {
                read = await _owner._sync.Vault.LoadAsync(SyncKind.Tasks, AccountId, SyncJsonContext.Default.TasksShelf, TasksShelf.Version).ConfigureAwait(true);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                // The read never throws for bad data; anything here is a bug, and the account syncs from nothing.
                Log.Error(Scope, "reading a task shelf threw", ex);
                read = new ShelfRead<TasksShelf>(null, ShelfReadOutcome.Discarded, ex.Message);
            }

            if (_cancel.IsCancellationRequested)
            {
                return;
            }

            var queued = Drain.Pending.Count > 0;
            if (read.Value is { } stored)
            {
                var dropped = 0;
                var shelf = TaskShelves.Validate(stored, ref dropped);
                if (dropped > 0)
                {
                    Log.Warn(Scope, $"{AccountId}: {dropped} unusable task entries dropped from the store");
                }

                if (shelf.Lists is { } lists)
                {
                    _lists = [.. lists.Select(TaskShelves.Restore)];
                    _listsAt = shelf.ListsAt;
                }

                foreach (var list in shelf.Held)
                {
                    _held[list.ListId] = (list.At, list.IncludesCompleted, [.. list.Tasks.Select(t => TaskShelves.Restore(t, list.ListId))], list.Cursor);
                }

                Edits.Landed(Base(), Edits.FetchStarted());
                Schedule.Seed(shelf.Held.Count > 0 ? shelf.Held.Min(l => l.At) : shelf.ListsAt, DateTimeOffset.UtcNow);
                if (Asked)
                {
                    // The user's Refresh came while the shelf was read: it still goes now.
                    Schedule.Force(DateTimeOffset.UtcNow);
                }

                // [S-3823] What is stored, for ⚙ › Accounts.
                _owner._sync.Report(AccountId, SyncStats.Of(shelf));

                // [S-2653] Changes that were waiting when the app last closed are shown again and resent.
                foreach (var write in shelf.Pending)
                {
                    // A list rename has no task edit: the lists themselves show it ([S-614]).
                    if (Edit(write) is { } edit)
                    {
                        _tickets[write.Id] = Edits.Begin(edit);
                    }
                }

                Drain.Restore([.. shelf.Pending, .. Drain.Pending]);
            }

            Loading = false;
            if (queued)
            {
                Save();
            }

            _owner._engine.Poll();
            Raise();
        }

        private static TaskEdit? Edit(PendingWrite write) => write.Kind switch
        {
            PendingKind.TaskComplete => TaskEdit.Complete(write.Target, true, write.Queued),
            PendingKind.TaskReopen => TaskEdit.Complete(write.Target, false),
            PendingKind.TaskRename => TaskEdit.Rename(write.Target, write.Text ?? string.Empty),
            PendingKind.TaskDelete => TaskEdit.Delete(write.Target),
            PendingKind.TaskListRename => null,
            _ => TaskEdit.Create(Optimistic(write.Target, write.Container, write.Text ?? string.Empty, write.Due)),
        };

        private List<TaskItem> Base() => [.. _held.Values.SelectMany(h => h.Tasks)];

        public async Task<IReadOnlyList<TaskListInfo>> LoadListsAsync(CancellationToken cancellation)
        {
            var started = ++_listClock;
            var lists = await _owner._remote.ListsAsync(AccountId, cancellation).ConfigureAwait(true);
            Log.Debug(Scope, $"{lists.Count} task list(s) on {AccountId}");
            _lists = [.. lists.Take(TaskShelves.MaxLists)];
            _listRenames.RemoveAll(r => r.ConfirmedAt < started);
            _listsAt = DateTimeOffset.UtcNow;
            Save();
            return Lists!;
        }

        /// <summary>[S-2652] One run of the account: the lists, then each wanted list in turn.</summary>
        public async Task<SyncRun> SyncAsync(CancellationToken cancellation)
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation, _cancel.Token);
            var token = linked.Token;
            var stamp = Edits.FetchStarted();

            try
            {
                var lists = await LoadListsAsync(token).ConfigureAwait(true);
                var interests = Interests.Select(i => (i.ListId, i.ShowCompleted))
                    .Concat(Feeds.Select(f => (f.ListId, f.ShowCompleted)));
                var wanted = TaskShelves.Wanted(lists, interests);

                var failures = 0;
                AccountException? first = null;

                foreach (var (listId, includeCompleted) in wanted)
                {
                    try
                    {
                        // [S-2686] Only the changes since the list's cursor, when it has a usable one.
                        var was = _held.TryGetValue(listId, out var held) ? held : default;
                        var (tasks, cursor) = await _owner._remote.SyncListAsync(AccountId, listId, includeCompleted, was.Tasks, was.Cursor, token).ConfigureAwait(true);
                        _held[listId] = (DateTimeOffset.UtcNow, includeCompleted, [.. tasks.Take(TaskShelves.MaxTasksPerList)], cursor);
                    }
                    catch (AccountException ex)
                    {
                        // [S-596] One list failing costs that list, not the account; the held copy stays.
                        failures++;
                        first ??= ex;
                        Log.Warn(Scope, $"list {listId} of {AccountId} skipped: {ex.Message}");
                    }
                }

                // A list that is gone at the provider is gone here too.
                var present = new HashSet<string>(lists.Select(l => l.Id), StringComparer.Ordinal);
                foreach (var gone in _held.Keys.Where(id => !present.Contains(id)).ToList())
                {
                    _held.Remove(gone);
                }

                if (first is not null && failures == wanted.Count)
                {
                    throw first;
                }

                Edits.Landed(Base(), stamp);
                Failure = null;
                Save();
                return SyncRun.Ok;
            }
            catch (AccountException ex)
            {
                Failure = ex;
                return SyncRun.Of(ex);
            }
            catch (Exception ex) when (ex is not OperationCanceledException and not OutOfMemoryException)
            {
                // Anything the account layer did not already type is a bug or a provider oddity; the
                // widget says the service answered unexpectedly and the log says what it was.
                Log.Error(Scope, $"syncing the tasks of {AccountId} failed unexpectedly", ex);
                Failure = new AccountException(AccountError.BadResponse, Str.Shell_Tasks_Unreadable.Text(), 0, ex);
                return new SyncRun(FetchStatus.BadResponse, ex.Message);
            }
        }

        // ---- the queue ----------------------------------------------------------------------------

        public Task Enqueue(PendingKind kind, string taskId, string listId, string? text, DateOnly? due, TaskEdit? edit)
        {
            var write = new PendingWrite(Guid.NewGuid().ToString("n"), kind, taskId, listId, text, due, DateTimeOffset.UtcNow);
            return Add(write, edit) ? Drain.DrainAsync() : Task.CompletedTask;
        }

        public async Task<TaskItem?> EnqueueCreateAsync(TaskItem optimistic, DateOnly? due)
        {
            var write = new PendingWrite(Guid.NewGuid().ToString("n"), PendingKind.TaskCreate, optimistic.Id, optimistic.ListId, optimistic.Title, due, DateTimeOffset.UtcNow);
            if (!Add(write, TaskEdit.Create(optimistic)))
            {
                return null;
            }

            // The provider's task once it answers; while it waits (offline, behind another change) the temporary
            // row, which the widget pins at the bottom like any other ([S-599]); nothing once refused.
            return await Drain.SendNowAsync(write.Id).ConfigureAwait(true) switch
            {
                { Result: WriteResult.Done } done => done.Value,
                { Result: WriteResult.Refused } => null,
                _ => Drain.Pending.Any(w => w.Id == write.Id) ? CreatedRow(write) : null,
            };
        }

        public void Refuse(string sentence)
        {
            _refused = sentence;
            Raise();
        }

        /// <summary>Queue a write and show its edit; queued writes it folds away lose theirs ([S-2611]).</summary>
        private bool Add(PendingWrite write, TaskEdit? edit)
        {
            if (Drain.Add(write) is not { } added)
            {
                Refuse(Str.Sync_QueueFull.Text());
                return false;
            }

            foreach (var folded in added.Folded)
            {
                if (_tickets.Remove(folded.Id, out var ticket))
                {
                    Edits.Failed(ticket);
                }
            }

            _refused = null;

            if (edit is not null && added.Kept)
            {
                _tickets[write.Id] = Edits.Begin(edit);
            }

            Save();
            Raise();
            return added.Kept;
        }

        private void OnSettled(PendingWrite write, WriteSent<TaskItem> sent)
        {
            var ticket = _tickets.GetValueOrDefault(write.Id);
            switch (sent.Result)
            {
                case WriteResult.Done when write.Kind == PendingKind.TaskListRename:
                    _listRenames.Add((write.Target, write.Text ?? string.Empty, ++_listClock));
                    break;

                case WriteResult.Done:
                    _tickets.Remove(write.Id);
                    Edits.Succeeded(ticket, sent.Value);
                    if (sent.Value is { } created)
                    {
                        Drain.Retarget(write.Target, created.Id);
                    }

                    break;

                case WriteResult.Refused:
                    _tickets.Remove(write.Id);
                    Edits.Failed(ticket);
                    _refused = Str.Shell_Common_ProblemReason.Format(("problem", Failed(write.Kind).Text()), ("reason", sent.Problem ?? string.Empty));
                    break;
            }

            Save();
            Raise();
        }

        private static TaskItem CreatedRow(PendingWrite write) => Optimistic(write.Target, write.Container, write.Text ?? string.Empty, write.Due);

        private static Str Failed(PendingKind kind) => kind switch
        {
            PendingKind.TaskComplete => Str.Shell_Tasks_CompleteFailed,
            PendingKind.TaskReopen => Str.Shell_Tasks_ReopenFailed,
            PendingKind.TaskRename => Str.Shell_Tasks_RenameFailed,
            PendingKind.TaskDelete => Str.Shell_Tasks_DeleteFailed,
            PendingKind.TaskListRename => Str.WidgetsC_Tasks_RenameListFailed,
            _ => Str.Shell_Tasks_AddFailed,
        };

        private async Task<WriteSent<TaskItem>> SendAsync(PendingWrite write, CancellationToken cancellation)
        {
            var remote = _owner._remote;
            TaskItem? created = null;
            switch (write.Kind)
            {
                case PendingKind.TaskComplete or PendingKind.TaskReopen:
                    await remote.SetCompletedAsync(AccountId, write.Container, write.Target, write.Kind == PendingKind.TaskComplete, cancellation).ConfigureAwait(true);
                    break;
                case PendingKind.TaskRename:
                    await remote.RenameAsync(AccountId, write.Container, write.Target, write.Text ?? string.Empty, cancellation).ConfigureAwait(true);
                    break;
                case PendingKind.TaskDelete:
                    await remote.DeleteAsync(AccountId, write.Container, write.Target, cancellation).ConfigureAwait(true);
                    break;
                case PendingKind.TaskListRename:
                    await remote.RenameListAsync(AccountId, write.Target, write.Text ?? string.Empty, cancellation).ConfigureAwait(true);
                    break;
                default:
                    created = await remote.CreateAsync(AccountId, write.Container, write.Text ?? string.Empty, write.Due, cancellation).ConfigureAwait(true);
                    break;
            }

            return WriteSent<TaskItem>.Done(created);
        }

        public void Raise()
        {
            foreach (var feed in Feeds.ToList())
            {
                feed.Raise();
            }
        }

        private void Save()
        {
            // An account signed out mid-sync must not be written back after its purge, nor a shelf still being read over.
            if (Loading || _owner.Find(AccountId) is null)
            {
                return;
            }

            var shelf = new TasksShelf
            {
                Owner = AccountId,
                Lists = _lists is { } lists ? [.. lists.Select(TaskShelves.Store)] : null,
                ListsAt = _lists is null ? null : _listsAt,
                Held = [.. _held.Select(pair => new TaskListShelf
                {
                    ListId = pair.Key,
                    At = pair.Value.At,
                    IncludesCompleted = pair.Value.IncludesCompleted,
                    Tasks = [.. pair.Value.Tasks.Select(TaskShelves.Store)],
                    Cursor = pair.Value.Cursor,
                })],
                Pending = [.. Drain.Pending],
            };

            _owner._sync.Vault.SaveSoon(SyncKind.Tasks, AccountId, shelf, SyncJsonContext.Default.TasksShelf);
            _owner._sync.Report(AccountId, SyncStats.Of(shelf));
        }

        /// <summary>The account left, or the service closed: what is in flight hears it, and its queue stops.</summary>
        public void Cancel()
        {
            try
            {
                _cancel.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // ignore: already torn down.
            }
        }
    }
}
