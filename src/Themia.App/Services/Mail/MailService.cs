using System.Net.Http;
using Themia.App.Services.Accounts;
using Themia.App.Services.Sync;
using Themia.Core.Accounts;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Mail;
using Themia.Core.Sync;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.App.Services.Mail;

/// <summary>
/// [S-546] [S-2620] Mail for every Mail widget: one per app, reached through <see cref="AppServices.Mail"/>,
/// built on Themia's own accounts (<see cref="AccountService"/>) and the offline store
/// (<see cref="SyncHost"/>).
/// <para>
/// <b>Sync owns the network.</b> Each mail account has a <see cref="Box"/>: the newest messages of
/// every folder a Mail widget in any view reads ([S-2608]), read from the account's shelf off the UI
/// thread at start ([S-2693]) and saved back after every change. Each folder is an entry of the one sync
/// engine ([S-2691]): its counts are probed every <see cref="MailRefresh.ProbeInterval"/> (the accounts
/// cadence) and its list fetched only when they moved or the list is old (<see cref="MailRefresh"/>);
/// failures back off; what is in flight is cancelled when the account leaves or the service closes.
/// The cadence rides the shared <see cref="Ticker"/>, so it stops while the desktop is hidden.
/// </para>
/// <para>
/// <b>Widgets only read.</b> A feed a widget opens is a view over the boxes: it draws what is stored
/// at once (a view switch, a card rebuild, a cold start offline) and never starts a request by
/// being opened — unless the folder has never been synced, or the widget wants more rows than are held.
/// </para>
/// <para>
/// <b>Changes</b> ([S-2624]) show at once and wait in the account's queue (the one <see cref="WriteDrain{TResult}"/>,
/// [S-2692], persisted in the shelf) until the provider has them: one at a time, in order; offline or
/// throttled they wait and retry; refused they are taken back and the header says why.
/// </para>
/// <para>
/// Everything here runs on the UI thread: the ticker fires there and every await resumes there, so
/// the state has one writer and needs no lock. IMAP socket work runs on the thread pool.
/// </para>
/// </summary>
internal sealed class MailService : IMailSource, IDisposable
{
    private const string Scope = "mail";

    private readonly AccountService _accounts;
    private readonly SyncHost _sync;
    private readonly SyncEngine<FolderSync> _engine;
    private readonly Dictionary<string, Box> _boxes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Feed> _feeds = new(StringComparer.Ordinal);

    public MailService(AccountService accounts, Ticker ticker, SyncHost sync)
    {
        ArgumentNullException.ThrowIfNull(ticker);
        _accounts = accounts;
        _sync = sync;
        _engine = new SyncEngine<FolderSync>(new SyncEntries<FolderSync>(Scope), FetchFolderAsync, () => DateTimeOffset.UtcNow, ticker.Subscribe, f => f.Box.MaySend)
        {
            // More rows wanted than held is a reason to ask, whatever the schedule says.
            AlsoDue = f => f.Rows > f.Held && f.LastFetch is not null && f.Schedule.Status == FetchStatus.Ok,
        };
        _engine.Changed += _ => PublishAll();
        _engine.Polled += DrainAll;
        _accounts.AccountsChanged += OnAccountsChanged;
        _sync.InterestsChanged += Reconcile;
        _sync.NetworkAvailable += OnNetworkAvailable;
        Reconcile();
    }

    public event Action? AccountsChanged;

    public IReadOnlyList<MailAccount> Accounts =>
        [.. _accounts.WithFeature(HostFeatures.Mail).Select(a => new MailAccount(a.Id, a.Provider, a.Label, a.Address, a.Problem))];

    public IMailFeed Open(MailQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        ObjectDisposedException.ThrowIf(_engine.Disposed, this);

        if (!_feeds.TryGetValue(query.Key, out var feed))
        {
            feed = new Feed(this, query);
            _feeds[query.Key] = feed;
            Reconcile();
        }

        var handle = new Handle(feed);
        feed.Handles.Add(handle);
        feed.Publish();
        return handle;
    }

    private void Release(Handle handle)
    {
        var feed = handle.Feed;
        feed.Handles.Remove(handle);

        if (feed.Handles.Count == 0)
        {
            // The folder stays synced while a widget in any view reads it; only the view goes.
            _feeds.Remove(feed.Query.Key);
        }
    }

    // -----------------------------------------------------------------------------------------
    // What to sync
    // -----------------------------------------------------------------------------------------

    /// <summary>
    /// Follow the accounts and the widgets: a box per mail account that something reads, each folder
    /// at the largest count anything asked for this run. A new folder, or one asked for more rows
    /// than it holds, is due at once. A stored folder nobody reads stays stored but is not synced.
    /// </summary>
    private void Reconcile()
    {
        if (_engine.Disposed)
        {
            return;
        }

        var accounts = Accounts;
        var wanted = new Dictionary<string, Dictionary<MailFolder, int>>(StringComparer.Ordinal);

        void Want(IReadOnlyList<string> accountIds, MailFolder folder, int count)
        {
            foreach (var account in MailScreens.Targets(accounts, accountIds))
            {
                if (!wanted.TryGetValue(account.Id, out var folders))
                {
                    folders = [];
                    wanted[account.Id] = folders;
                }

                folders[folder] = Math.Max(folders.GetValueOrDefault(folder), Math.Clamp(count, 1, MailLimits.PerFolder));
            }
        }

        foreach (var interest in _sync.Interests.Mail)
        {
            Want(interest.AccountIds, interest.Folder, interest.Count);
        }

        foreach (var feed in _feeds.Values)
        {
            Want(feed.Query.AccountIds, feed.Query.Folder, feed.Query.Count);
        }

        foreach (var id in _boxes.Keys.Where(id => !wanted.ContainsKey(id) || accounts.All(a => a.Id != id)).ToList())
        {
            var gone = _boxes[id];
            _boxes.Remove(id);
            _engine.Entries.RemoveWhere(f => ReferenceEquals(f.Box, gone));
            gone.Cancel();
        }

        foreach (var (id, _) in wanted)
        {
            if (accounts.FirstOrDefault(a => a.Id == id) is not { } account)
            {
                continue;
            }

            if (!_boxes.TryGetValue(id, out var box))
            {
                box = new Box(this, account);
                _boxes[id] = box;
                _ = box.LoadAsync();
            }

            box.Usable = account.Problem is null;
        }

        // [S-3782] Each folder is probed at the shared accounts cadence.
        var interval = _sync.Interests.AccountInterval;
        _engine.Reconcile(() =>
        {
            foreach (var (id, folders) in wanted)
            {
                if (!_boxes.TryGetValue(id, out var box))
                {
                    continue;
                }

                foreach (var (folder, count) in folders)
                {
                    var sync = _engine.Entries.Want(FolderSync.KeyOf(id, folder), () => box.Folder(folder));
                    sync.Rows = Math.Max(sync.Rows, count);
                    sync.Schedule.Interval = interval;
                }
            }
        }, drop: false);

        PublishAll();
    }

    private void OnNetworkAvailable()
    {
        foreach (var box in _boxes.Values)
        {
            box.Drain.Wake();
        }

        _engine.Wake();
    }

    /// <summary>The user's Refresh of a feed: its folder in each of its accounts now, and their queues woken.</summary>
    private void Refresh(Feed feed)
    {
        if (_engine.Disposed)
        {
            return;
        }

        var forced = new List<FolderSync>();
        foreach (var account in MailScreens.Targets(Accounts, feed.Query.AccountIds))
        {
            if (_boxes.TryGetValue(account.Id, out var box))
            {
                box.Drain.Wake();
                if (_engine.Entries.Find(FolderSync.KeyOf(account.Id, feed.Query.Folder)) is { } folder)
                {
                    forced.Add(folder);
                }
            }
        }

        _engine.Force(forced);
    }

    /// <summary>After every look at what is due: each account's queue sends what is ready.</summary>
    private void DrainAll()
    {
        foreach (var box in _boxes.Values.ToArray())
        {
            _ = box.Drain.DrainAsync();
        }
    }

    private void PublishAll()
    {
        foreach (var feed in _feeds.Values.ToArray())
        {
            feed.Publish();
        }
    }

    private void OnAccountsChanged()
    {
        Reconcile();
        AccountsChanged?.Invoke();
    }

    // -----------------------------------------------------------------------------------------
    // Changes
    // -----------------------------------------------------------------------------------------

    public async Task<string?> ChangeAsync(MailMessage message, MailChange change)
    {
        ArgumentNullException.ThrowIfNull(message);

        // [S-544] Gmail is read through gmail.readonly: nothing in it can be changed from here.
        if (!(message.Provider == HostProviders.Microsoft || message.Provider == HostProviders.Imap))
        {
            return Str.Shell_Mail_ReadOnly.Text();
        }

        if (message.Provider == HostProviders.Imap && ImapMessageId.Parse(message.RawId) is null)
        {
            return Str.Shell_Mail_BadHandle.Text();
        }

        if (!_boxes.TryGetValue(message.AccountId, out var box))
        {
            return Str.Shell_Accounts_NotSignedIn.Text();
        }

        return await box.EnqueueAsync(message, change).ConfigureAwait(true);
    }

    private async Task ApplyAsync(string accountId, ProviderId provider, string rawId, MailFolder folder, MailChange change, CancellationToken cancellation)
    {
        switch (provider)
        {
            case var pid when pid == HostProviders.Microsoft:
                var url = change switch
                {
                    MailChange.MarkRead or MailChange.MarkUnread => MailWire.GraphMessage(rawId),
                    _ => MailWire.GraphMove(rawId),
                };

                if (change is MailChange.MarkRead or MailChange.MarkUnread)
                {
                    await _accounts.SendJsonAsync(accountId, HttpMethod.Patch, url, MailWire.GraphReadBody(change == MailChange.MarkRead), cancellation).ConfigureAwait(true);
                }
                else if (change == MailChange.Delete && folder == MailFolder.Deleted)
                {
                    // Already in Deleted Items: Graph's DELETE moves it to Recoverable Items.
                    await _accounts.SendJsonAsync(accountId, HttpMethod.Delete, MailWire.GraphMessage(rawId), null, cancellation).ConfigureAwait(true);
                }
                else
                {
                    await _accounts.SendJsonAsync(accountId, HttpMethod.Post, url, MailWire.GraphMoveBody(Destination(change)), cancellation).ConfigureAwait(true);
                }

                return;

            case var pid when pid == HostProviders.Imap:
                if (ImapMessageId.Parse(rawId) is not { } id)
                {
                    throw new AccountException(AccountError.Refused, Str.Shell_Mail_BadHandle.Text());
                }

                var login = _accounts.GetImapCredentials(accountId);
                await Task.Run(() => change switch
                {
                    MailChange.MarkRead => ImapMailClient.MarkReadAsync(login, id, true, cancellation),
                    MailChange.MarkUnread => ImapMailClient.MarkReadAsync(login, id, false, cancellation),
                    MailChange.Delete when id.Folder == MailFolder.Deleted => ImapMailClient.ExpungeAsync(login, id, cancellation),
                    _ => ImapMailClient.MoveAsync(login, id, Destination(change), cancellation),
                }, cancellation).ConfigureAwait(true);
                return;

            default:
                throw new AccountException(AccountError.Refused, Str.Shell_Mail_ReadOnly.Text());
        }
    }

    private static MailFolder Destination(MailChange change) => change switch
    {
        MailChange.Archive => MailFolder.Archive,
        MailChange.Junk => MailFolder.Junk,
        _ => MailFolder.Deleted,
    };

    public bool Open(MailMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (MailLinks.Open(message, OutlookInstalled) is not { } link)
        {
            // issue #45a: an IMAP message has no web view. Say so in the log rather than hand the
            // shell an empty string, which opens Explorer.
            Log.Info(Scope, $"nothing to open for a {message.Provider.Value} message (no web view)");
            return false;
        }

        return ShellLauncher.OpenMailLink(link) == LaunchOutcome.Opened;
    }

    public bool Reply(MailMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (MailLinks.Reply(message, OutlookInstalled) is not { } link)
        {
            Log.Info(Scope, "no usable address to reply to");
            return false;
        }

        return ShellLauncher.OpenMailLink(link) == LaunchOutcome.Opened;
    }

    public bool Compose(MailAccount account, IReadOnlyList<MailMessage> known)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(known);

        return ShellLauncher.OpenMailLink(MailLinks.Compose(account, known)) == LaunchOutcome.Opened;
    }

    /// <summary>Whether Outlook's <c>ms-outlook:</c> protocol is registered; one check shared with the calendar ([S-1268]).</summary>
    private static bool OutlookInstalled => ShellLauncher.OutlookInstalled;

    public void Dispose()
    {
        if (_engine.Disposed)
        {
            return;
        }

        // [S-2691] Every fetch and send in flight hears it; what they bring back after is dropped.
        _engine.Dispose();
        _accounts.AccountsChanged -= OnAccountsChanged;
        _sync.InterestsChanged -= Reconcile;
        _sync.NetworkAvailable -= OnNetworkAvailable;

        foreach (var box in _boxes.Values)
        {
            box.Cancel();
        }

        _boxes.Clear();
        _feeds.Clear();
        AccountsChanged = null;
    }

    // -----------------------------------------------------------------------------------------
    // Fetching
    // -----------------------------------------------------------------------------------------

    private async Task<MailFolderStatus> ProbeAsync(Box box, MailFolder folder, CancellationToken cancellation)
    {
        switch (box.Provider)
        {
            case var pid when pid == HostProviders.Microsoft:
                var graph = await _accounts.GetJsonAsync(box.AccountId, MailWire.GraphFolder(folder), MailWireContext.Default.GraphMailFolder, cancellation).ConfigureAwait(true);
                return new MailFolderStatus(graph.TotalItemCount ?? 0, graph.UnreadItemCount ?? 0);

            case var pid when pid == HostProviders.Google:
                var label = await _accounts.GetJsonAsync(box.AccountId, MailWire.GmailLabelCounts(folder), MailWireContext.Default.GmailLabel, cancellation).ConfigureAwait(true);
                return new MailFolderStatus(label.MessagesTotal ?? 0, label.MessagesUnread ?? 0);

            case var pid when pid == HostProviders.Imap:
                var login = _accounts.GetImapCredentials(box.AccountId);
                return await Task.Run(() => ImapMailClient.StatusAsync(login, folder, cancellation), cancellation).ConfigureAwait(true);

            default:
                throw new AccountException(AccountError.Refused, Str.Shell_Mail_NoMail.Format(("provider", box.Provider.Label())));
        }
    }

    /// <summary>
    /// The newest <paramref name="count"/> of a folder, and the cursor to keep: [S-2687] Graph and
    /// [S-2689] IMAP read only what changed since <paramref name="cursor"/> when it is usable; Gmail
    /// keeps its own incremental read ([S-2623]).
    /// </summary>
    private async Task<(IReadOnlyList<MailMessage> Messages, SyncCursor? Cursor)> FetchAsync(
        Box box, MailFolder folder, int count, IReadOnlyList<MailMessage> held, SyncCursor? cursor, MailFolderStatus counts, CancellationToken cancellation)
    {
        switch (box.Provider)
        {
            case var pid when pid == HostProviders.Microsoft:
                return await FetchGraphAsync(box, folder, count, held, cursor, counts, cancellation).ConfigureAwait(true);

            case var pid when pid == HostProviders.Google:
                return (await FetchGmailAsync(box, folder, count, held, cancellation).ConfigureAwait(true), null);

            case var pid when pid == HostProviders.Imap:
                var login = _accounts.GetImapCredentials(box.AccountId);
                return await Task.Run(() => ImapMailClient.SyncAsync(login, box.AccountId, folder, count, held, cursor, counts.Total, cancellation), cancellation)
                    .ConfigureAwait(true);

            default:
                throw new AccountException(AccountError.Refused, Str.Shell_Mail_NoMail.Format(("provider", box.Provider.Label())));
        }
    }

    /// <summary>The most entries one Graph mail delta round reads.</summary>
    private const int MaxMailRound = 500;

    /// <summary>
    /// [S-2687] Graph: the folder's delta since the cursor, folded into the newest N. In full — no
    /// usable cursor, the provider refused it, or a removal left the list short — the newest N are
    /// listed and a delta round bounded to their dates starts the next cursor; what that round
    /// says is folded in, so the list is as of the round.
    /// </summary>
    private Task<(IReadOnlyList<MailMessage>, SyncCursor?)> FetchGraphAsync(
        Box box, MailFolder folder, int count, IReadOnlyList<MailMessage> held, SyncCursor? cursor, MailFolderStatus counts, CancellationToken cancellation)
        => _fallback.ReadAsync($"{box.AccountId}|{folder.Wire()}", $"{box.AccountId}: {folder.Wire()}",
            () => GraphChangesAsync(box, folder, count, held, cursor, counts, cancellation),
            async () => ((IReadOnlyList<MailMessage>)await GraphNewestAsync(box, folder, count, cancellation).ConfigureAwait(true), (SyncCursor?)null));

    /// <summary>[S-2690] Folders whose delta Graph refused this session: the newest N listed, no cursor.</summary>
    private readonly DeltaFallback _fallback = new(Scope);

    /// <summary>The newest <paramref name="count"/> of a folder, listed the plain way.</summary>
    private async Task<List<MailMessage>> GraphNewestAsync(Box box, MailFolder folder, int count, CancellationToken cancellation)
    {
        var graph = await _accounts.GetGraphPagesAsync(
            box.AccountId, MailWire.GraphMessages(folder, count), MailWireContext.Default.GraphMailMessage, count, cancellation).ConfigureAwait(true);
        return graph.Select(m => MailWire.FromGraph(m, box.AccountId, folder)).OfType<MailMessage>().ToList();
    }

    private async Task<(IReadOnlyList<MailMessage>, SyncCursor?)> GraphChangesAsync(
        Box box, MailFolder folder, int count, IReadOnlyList<MailMessage> held, SyncCursor? cursor, MailFolderStatus counts, CancellationToken cancellation)
    {
        var scope = MailShelves.CursorScope(folder, count);
        var now = DateTimeOffset.UtcNow;

        return await DeltaSync.ChangesOrFullAsync<(IReadOnlyList<MailMessage>, SyncCursor?)>(
            SyncCursors.Check(cursor, CursorKind.GraphDelta, scope, now),
            async () =>
            {
                var round = await GraphMailRoundAsync(box, folder, new Uri(cursor!.Value), cancellation).ConfigureAwait(true);
                var (messages, refill) = DeltaMerge.Mail(held, round.Changed, round.Removed, count, DeltaMerge.ByReceived, counts.Total);
                return new((messages, round.Cursor is { } link ? SyncCursors.Advance(cursor, link, now) : null), Short: refill);
            },
            async () =>
            {
                var newest = await GraphNewestAsync(box, folder, count, cancellation).ConfigureAwait(true);
                var start = await GraphMailRoundAsync(box, folder, MailWire.GraphMessagesDelta(folder, DeltaMerge.MailFilter(newest, count)), cancellation).ConfigureAwait(true);
                var (merged, _) = DeltaMerge.Mail(newest, start.Changed, start.Removed, count, DeltaMerge.ByReceived, counts.Total);
                return (merged, start.Cursor is { } next ? SyncCursors.Start(CursorKind.GraphDelta, next, scope, now) : null);
            },
            Scope,
            $"{box.AccountId}: {folder.Wire()}").ConfigureAwait(true);
    }

    private async Task<(IReadOnlyList<MailMessage> Changed, IReadOnlyList<string> Removed, string? Cursor)> GraphMailRoundAsync(
        Box box, MailFolder folder, Uri first, CancellationToken cancellation)
    {
        var round = await _accounts.GetDeltaAsync(box.AccountId, first, DeltaDialect.Graph, MailWireContext.Default.GraphMailMessage, MaxMailRound, cancellation,
            ("Prefer", "odata.maxpagesize=50")).ConfigureAwait(true);
        return ([.. round.Items.Select(m => MailWire.FromGraph(m, box.AccountId, folder)).OfType<MailMessage>()], round.Removed, round.Cursor);
    }

    /// <summary>
    /// [S-2623] Gmail lists ids only, so each message is a second request (metadata: three headers and
    /// the snippet, never a body). Incremental: a message already held is not asked for again — its
    /// read state comes from a second id list of the folder's unread messages — so a sync where two
    /// messages arrived costs three requests, not fifty-two. Four at a time for the rest.
    /// </summary>
    private async Task<IReadOnlyList<MailMessage>> FetchGmailAsync(Box box, MailFolder folder, int count, IReadOnlyList<MailMessage> held, CancellationToken cancellation)
    {
        var ids = await _accounts.GetGooglePagesAsync(
            box.AccountId, MailWire.GmailList(folder, count), "messages", MailWireContext.Default.GmailListItem, count, cancellation).ConfigureAwait(true);

        var known = held.GroupBy(m => m.RawId, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var wanted = ids.Where(i => !string.IsNullOrEmpty(i.Id)).Select(i => i.Id!).Distinct(StringComparer.Ordinal).ToList();

        HashSet<string>? unread = null;
        if (wanted.Any(known.ContainsKey))
        {
            var unreadIds = await _accounts.GetGooglePagesAsync(
                box.AccountId, MailWire.GmailUnreadList(folder, count), "messages", MailWireContext.Default.GmailListItem, count, cancellation).ConfigureAwait(true);
            unread = new HashSet<string>(unreadIds.Where(i => !string.IsNullOrEmpty(i.Id)).Select(i => i.Id!), StringComparer.Ordinal);
        }

        using var gate = new SemaphoreSlim(4);

        async Task<MailMessage?> One(string id)
        {
            if (known.TryGetValue(id, out var stored) && unread is not null)
            {
                return stored with { IsRead = !unread.Contains(id) };
            }

            await gate.WaitAsync(cancellation).ConfigureAwait(false);
            try
            {
                var message = await _accounts.GetJsonAsync(box.AccountId, MailWire.GmailMetadata(id), MailWireContext.Default.GmailMessage, cancellation).ConfigureAwait(false);
                return MailWire.FromGmail(message, box.AccountId, box.Address, folder);
            }
            catch (AccountException ex) when (ex.Error == AccountError.NotFound)
            {
                // Deleted between the list and this request: simply not there any more.
                return null;
            }
            finally
            {
                gate.Release();
            }
        }

        var messages = await Task.WhenAll(wanted.Select(One)).ConfigureAwait(true);
        return [.. messages.OfType<MailMessage>()];
    }

    /// <summary>
    /// One run of a folder: its counts probed, and its list fetched when they moved, the list is old, more rows
    /// are wanted than held or the user asked. A refusal of an IMAP login is told to the account service.
    /// </summary>
    private async Task<SyncRun> FetchFolderAsync(FolderSync folder, bool asked, CancellationToken cancellation)
    {
        var box = folder.Box;
        var started = box.Tick();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation, box.Cancellation);
        var token = linked.Token;

        try
        {
            var counts = await ProbeAsync(box, folder.Folder, token).ConfigureAwait(true);
            var count = Math.Clamp(Math.Max(folder.Rows, 1), 1, MailLimits.PerFolder);
            var fetched = false;

            if (asked || count > folder.Held || MailRefresh.NeedsFetch(folder.Known, counts, folder.LastFetch, DateTimeOffset.UtcNow))
            {
                (folder.Messages, folder.Cursor) = await FetchAsync(box, folder.Folder, count, folder.Messages, folder.Cursor, counts, token).ConfigureAwait(true);
                folder.LastFetch = DateTimeOffset.UtcNow;
                folder.Held = count;
                box.Landed(started);
                fetched = true;
            }

            var changed = fetched || folder.Known != counts;
            folder.Known = counts;
            folder.CurrentAt = DateTimeOffset.UtcNow;

            if (changed)
            {
                box.Save();
            }

            return SyncRun.Ok;
        }
        catch (AccountException ex) when (ex.Error == AccountError.SignInRequired && box.Provider == HostProviders.Imap)
        {
            // An IMAP refusal happens on our socket, not in the account service's HTTP path,
            // so the service has to be told for the account to say "sign in again".
            _accounts.ReportImapLoginRefused(box.AccountId);
            throw;
        }
    }

    // -----------------------------------------------------------------------------------------
    // One account
    // -----------------------------------------------------------------------------------------

    /// <summary>One folder of one account, as stored and as last synced: an entry of the sync engine.</summary>
    private sealed class FolderSync(Box box, MailFolder folder) : SyncEntry(KeyOf(box.AccountId, folder), MailRefresh.ProbeInterval)
    {
        public static string KeyOf(string accountId, MailFolder folder) => $"{accountId}|{folder.Wire()}";

        public Box Box { get; } = box;

        public MailFolder Folder { get; } = folder;

        public IReadOnlyList<MailMessage> Messages { get; set; } = [];

        public MailFolderStatus? Known { get; set; }

        public DateTimeOffset? LastFetch { get; set; }

        /// <summary>
        /// [S-559] When the stored list was last known current: fetched, or a probe found its counts
        /// unchanged ([S-546]). What "updated" means to the user; <see cref="LastFetch"/> is the cadence's.
        /// </summary>
        public DateTimeOffset? CurrentAt { get; set; }

        /// <summary>How many newest messages were last fetched.</summary>
        public int Held { get; set; }

        /// <summary>[S-2680] Where the provider's changes to this folder continue from; null when none can follow.</summary>
        public SyncCursor? Cursor { get; set; }

        /// <summary>How many the widgets want; never shrinks during a run.</summary>
        public int Rows { get; set; }

        public override string Subject => $"{Box.AccountId}: {Folder.Wire()}";
    }

    /// <summary>One mail account: its folders, its queue, its shelf.</summary>
    private sealed class Box
    {
        private readonly MailService _owner;
        private readonly Dictionary<MailFolder, FolderSync> _folders = [];
        private readonly CancellationTokenSource _cancel;

        /// <summary>Confirmed changes kept on top until a fetch that started after them lands ([S-2622]).</summary>
        private readonly List<(PendingWrite Write, long Confirmed)> _echo = [];

        private long _clock;
        private string? _refused;

        public Box(MailService owner, MailAccount account)
        {
            _owner = owner;
            AccountId = account.Id;
            Provider = account.Provider;
            Address = account.Address;
            _cancel = CancellationTokenSource.CreateLinkedTokenSource(owner._engine.Cancellation);
            Drain = new WriteDrain<bool>(Scope, account.Id, SendAsync, () => DateTimeOffset.UtcNow, () => MaySend, _cancel.Token);
            Drain.Settled += OnSettled;
        }

        public string AccountId { get; }

        public ProviderId Provider { get; }

        public string Address { get; }

        /// <summary>False while the account needs signing in: nothing is asked, what is stored stays.</summary>
        public bool Usable { get; set; } = true;

        /// <summary>[S-2693] The shelf is still being read: nothing is asked or sent, nothing saved over it.</summary>
        public bool Loading { get; private set; } = true;

        public bool MaySend => Usable && !Loading && !_cancel.IsCancellationRequested;

        public CancellationToken Cancellation => _cancel.Token;

        public WriteDrain<bool> Drain { get; }

        public string? Refused => _refused;

        public FolderSync Folder(MailFolder folder)
        {
            if (!_folders.TryGetValue(folder, out var sync))
            {
                sync = new FolderSync(this, folder);
                _folders[folder] = sync;
            }

            return sync;
        }

        public long Tick() => ++_clock;

        /// <summary>A fetch that started at <paramref name="started"/> landed: confirmed changes older than it are in it.</summary>
        public void Landed(long started) => _echo.RemoveAll(e => e.Confirmed < started);

        /// <summary>[S-2693] The account's shelf, read off the UI thread; what was asked meanwhile is kept.</summary>
        public async Task LoadAsync()
        {
            ShelfRead<MailShelf> read;
            try
            {
                read = await _owner._sync.Vault.LoadAsync(SyncKind.Mail, AccountId, SyncJsonContext.Default.MailShelf, MailShelf.Version).ConfigureAwait(true);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                // The read never throws for bad data; anything here is a bug, and the account syncs from nothing.
                Log.Error(Scope, "reading a mail shelf threw", ex);
                read = new ShelfRead<MailShelf>(null, ShelfReadOutcome.Discarded, ex.Message);
            }

            if (_cancel.IsCancellationRequested)
            {
                return;
            }

            var queued = Drain.Pending.Count > 0;
            if (read.Value is { } stored)
            {
                var dropped = 0;
                var shelf = MailShelves.Validate(stored, ref dropped);
                if (dropped > 0)
                {
                    Log.Warn(Scope, $"{AccountId}: {dropped} unusable entries dropped from the stored mail");
                }

                var now = DateTimeOffset.UtcNow;
                foreach (var held in shelf.Folders)
                {
                    var folder = Folder(held.Folder);
                    folder.Messages = [.. held.Messages.Select(m => MailShelves.Restore(m, AccountId, Provider, held.Folder))];
                    folder.Known = held.Total is { } total && held.Unread is { } unread ? new MailFolderStatus(total, unread) : null;
                    folder.LastFetch = held.At;
                    folder.CurrentAt = held.At;
                    folder.Held = held.Count;
                    folder.Cursor = held.Cursor;
                    folder.Schedule.Seed(held.At, now);
                    if (folder.Asked)
                    {
                        // The user's Refresh came while the shelf was read: it still goes now.
                        folder.Schedule.Force(now);
                    }
                }

                Drain.Restore([.. shelf.Pending, .. Drain.Pending]);

                // [S-3823] What is stored, for settings › Accounts.
                _owner._sync.Report(AccountId, SyncStats.Of(shelf));
            }

            Loading = false;
            if (queued)
            {
                Save();
            }

            _owner._engine.Poll();
            _owner.PublishAll();
        }

        // ---- the queue ----------------------------------------------------------------------------

        public async Task<string?> EnqueueAsync(MailMessage message, MailChange change)
        {
            var write = new PendingWrite(Guid.NewGuid().ToString("n"), MailShelves.Kind(change), message.RawId, message.Folder.Wire(), null, null, DateTimeOffset.UtcNow);

            if (Drain.Add(write) is null)
            {
                return Str.Sync_QueueFull.Text();
            }

            _refused = null;
            Save();
            _owner.PublishAll();

            // A change queued behind others answers at once: it shows already, and its outcome
            // reaches the header. The head's first attempt answers the menu click: done or waiting
            // is fine, a refusal is said.
            var sent = await Drain.SendNowAsync(write.Id).ConfigureAwait(true);
            return sent is { Result: WriteResult.Refused } refused ? refused.Problem : null;
        }

        private async Task<WriteSent<bool>> SendAsync(PendingWrite write, CancellationToken cancellation)
        {
            var folder = MailNames.ParseFolder(write.Container) ?? MailFolder.Inbox;
            await _owner.ApplyAsync(AccountId, Provider, write.Target, folder, MailShelves.Change(write.Kind), cancellation).ConfigureAwait(true);
            return WriteSent<bool>.Done(true);
        }

        private void OnSettled(PendingWrite write, WriteSent<bool> sent)
        {
            if (sent.Result == WriteResult.Done)
            {
                Commit(write);
            }
            else if (sent.Result == WriteResult.Refused)
            {
                _refused = sent.Problem ?? Str.WidgetsB_Email_ChangeFailed.Text();

                // Taken back: the next sync of that folder fetches the truth.
                if (MailNames.ParseFolder(write.Container) is { } folder && _folders.TryGetValue(folder, out var sync))
                {
                    sync.Known = null;
                    sync.Schedule.Force(DateTimeOffset.UtcNow);
                }
            }

            Save();
            _owner.PublishAll();
        }

        /// <summary>A confirmed change becomes part of the stored list, and stays on top until a newer fetch has it.</summary>
        private void Commit(PendingWrite write)
        {
            if (MailNames.ParseFolder(write.Container) is { } folder && _folders.TryGetValue(folder, out var sync))
            {
                sync.Messages = MailShelves.Overlay(sync.Messages, [write], folder);
            }

            _echo.Add((write, ++_clock));
        }

        // ---- reading ------------------------------------------------------------------------------

        /// <summary>A folder as a widget sees it: the stored list, the confirmed changes, the queued ones.</summary>
        public IReadOnlyList<MailMessage> Messages(MailFolder folder)
        {
            if (!_folders.TryGetValue(folder, out var sync))
            {
                return [];
            }

            var confirmed = MailShelves.Overlay(sync.Messages, [.. _echo.Select(e => e.Write)], folder);
            return MailShelves.Overlay(confirmed, [.. Drain.Pending], folder);
        }

        public MailAccountOutcome Outcome(MailFolder folder)
        {
            if (!_folders.TryGetValue(folder, out var sync))
            {
                return new MailAccountOutcome(AccountId, MailAccountState.Pending, FetchStatus.Loading, null);
            }

            var schedule = sync.Schedule;
            var state = schedule.Status switch
            {
                FetchStatus.Ok => MailAccountState.Ok,
                FetchStatus.Loading => MailAccountState.Pending,
                _ => MailAccountState.Failed,
            };

            // [S-2612] [S-559] When the list on screen was last known current: the failure's stamp and the Refresh command's age.
            DateTimeOffset? at = sync.LastFetch is not null && sync.CurrentAt is { } current ? current.ToLocalTime() : null;
            return new MailAccountOutcome(AccountId, state, schedule.Status, schedule.Detail, sync.Known, at);
        }

        /// <summary>Whether the user's own Refresh of this folder is out; the header's busy ring is only for that.</summary>
        public bool Busy(MailFolder folder) => _folders.TryGetValue(folder, out var sync) && sync.Schedule.Busy && sync.Asked;

        public void Save()
        {
            // An account signed out mid-sync must not be written back after its purge, nor a shelf still being read over.
            if (Loading || _owner._accounts.Find(AccountId) is null)
            {
                return;
            }

            var shelf = new MailShelf
            {
                Owner = AccountId,
                Folders = [.. _folders.Values.Where(f => f.LastFetch is not null).Select(f => new MailFolderShelf
                {
                    Folder = f.Folder,
                    At = f.LastFetch!.Value,
                    Count = f.Held,
                    Total = f.Known?.Total,
                    Unread = f.Known?.Unread,
                    Messages = [.. f.Messages.Take(MailLimits.PerFolder).Select(MailShelves.Store)],
                    Cursor = f.Cursor,
                })],
                Pending = [.. Drain.Pending],
            };

            _owner._sync.Vault.SaveSoon(SyncKind.Mail, AccountId, shelf, SyncJsonContext.Default.MailShelf);
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

    /// <summary>One query's view over the boxes, and the widgets watching it.</summary>
    private sealed class Feed(MailService service, MailQuery query)
    {
        public MailQuery Query { get; } = query;

        public MailService Owner => service;

        public List<Handle> Handles { get; } = [];

        public MailFeedSnapshot Snapshot { get; private set; } = MailFeedSnapshot.Empty;

        public void Publish()
        {
            var targets = MailScreens.Targets(service.Accounts, Query.AccountIds).Where(a => a.Problem is null).ToList();
            var boxes = targets.Select(a => service._boxes.GetValueOrDefault(a.Id)).OfType<Box>().ToList();

            var pending = boxes.Sum(b => b.Drain.Waiting);
            var note = boxes.Select(b => b.Refused).FirstOrDefault(r => r is not null)
                ?? (pending > 0 ? Str.Sync_Pending.Plural(pending, ("count", pending)) : null);

            Snapshot = new MailFeedSnapshot(
                MailList.Merge(boxes.Select(b => b.Messages(Query.Folder)), Query.Count, MailSortOrder.NewestFirst),
                [.. targets.Select(a => service._boxes.TryGetValue(a.Id, out var box)
                    ? box.Outcome(Query.Folder)
                    : new MailAccountOutcome(a.Id, MailAccountState.Pending, FetchStatus.Loading, null))],
                boxes.Any(b => b.Busy(Query.Folder)),
                note);

            foreach (var handle in Handles.ToArray())
            {
                handle.Publish();
            }
        }
    }

    /// <summary>A widget's view of a feed.</summary>
    private sealed class Handle(Feed feed) : IMailFeed
    {
        private bool _disposed;

        public Feed Feed { get; } = feed;

        public event Action? Updated;

        public MailFeedSnapshot Snapshot => Feed.Snapshot;

        public void Refresh()
        {
            if (!_disposed)
            {
                Feed.Owner.Refresh(Feed);
            }
        }

        public void Publish()
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                Updated?.Invoke();
            }
            catch (Exception ex)
            {
                // One widget's handler must not stop the others hearing about the same feed.
                Log.Error(Scope, "a Mail widget's update handler threw", ex);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Updated = null;
            Feed.Owner.Release(this);
        }
    }
}
