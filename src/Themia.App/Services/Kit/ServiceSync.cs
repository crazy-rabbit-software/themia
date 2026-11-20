using Themia.App.Services.Accounts;
using Themia.App.Services.Sync;
using Themia.Core.Accounts;
using Themia.Core.Diagnostics;
using Themia.Core.Services;
using Themia.Core.Sync;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Sdk.Services;
using Themia.Sdk.WinUI;

namespace Themia.App.Services.Kit;

/// <summary>[S-9903] The desktop's transport: every request through the account service's one path.</summary>
internal sealed class AccountTransport(AccountService accounts) : IServiceTransport
{
    public Task<ServiceReply> SendAsync(string? accountId, ServiceDescriptor descriptor, ServiceRequest request, string? ifNoneMatch, CancellationToken cancellation)
        => accounts.SendServiceAsync(accountId, descriptor, request, ifNoneMatch, cancellation);
}

/// <summary>
/// [S-9925] [S-2600] The host's one service sync: the only code that asks a kit service for anything.
/// The widgets' interests (every view, shown or not, [S-2608]) and the signed-in accounts say which
/// entries exist (<see cref="ServiceEntries"/>, pure); the one sync engine ([S-2691]) runs the due ones
/// through the one client (<see cref="ServiceClient"/>) on the shared ticker at a minute, wakes them when the
/// network comes back ([S-2609]) and cancels what is in flight on dispose; what they hold is kept in each
/// account's sealed <c>service</c> shelf ([S-9926], read off the UI thread, [S-2693]) and the widgets reading
/// them are told. A sign-out forgets the account here; its shelf is purged with the rest ([S-2607]).
/// <para>UI thread: the ticker and the widgets are there, and each fetch resumes there.</para>
/// </summary>
internal sealed class ServiceSync : IServiceSources, IDisposable
{
    private const string Scope = "service";

    private readonly SyncHost? _sync;
    private readonly AccountService? _accounts;
    private readonly IServiceTransport _transport;
    private readonly ServiceMemory _memory = new();
    private readonly ServiceEntries _entries;
    private readonly SyncEngine<ServiceEntry> _engine;
    private readonly Dictionary<ProviderId, ServiceClient> _clients = [];
    private readonly HashSet<string> _seeded = new(StringComparer.Ordinal);

    /// <summary>[S-2693] Accounts whose shelf is still being read: nothing of theirs is asked before it lands.</summary>
    private readonly HashSet<string> _loading = new(StringComparer.Ordinal);
    private readonly List<Source> _shown = [];
    private readonly Func<IReadOnlyList<ServiceAccount>> _accountList;

    /// <summary>The desktop's sync, over the account service and the offline store.</summary>
    public ServiceSync(SyncHost sync, AccountService accounts, Ticker ticker)
        : this(ticker, new AccountTransport(accounts), () => AccountsOf(accounts))
    {
        _sync = sync;
        _accounts = accounts;
        _sync.InterestsChanged += Reconcile;
        _sync.NetworkAvailable += Wake;
        _accounts.AccountsChanged += OnAccountsChanged;
        _accounts.SignedOut += OnSignedOut;
        Reconcile();
    }

    /// <summary>A sync over another transport (a debug preview's recorded answers) and accounts of its own; it stores nothing.</summary>
    internal ServiceSync(Ticker ticker, IServiceTransport transport, Func<IReadOnlyList<ServiceAccount>> accounts)
    {
        ArgumentNullException.ThrowIfNull(ticker);
        _transport = transport;
        _accountList = accounts;
        _entries = new ServiceEntries(id => ProviderCatalog.All.FirstOrDefault(p => p.Id == id)?.Service);
        _engine = new SyncEngine<ServiceEntry>(_entries.Table, FetchAsync, () => DateTimeOffset.UtcNow, ticker.Subscribe, MayAsk);
        _engine.Changed += _ => Raise();
    }

    /// <summary>Raised on the UI thread when an entry's answer or state changed.</summary>
    public event Action? Updated;

    public IServiceSource Open(ServiceInterest interest)
    {
        ArgumentNullException.ThrowIfNull(interest);
        var source = new Source(this, interest);
        _shown.Add(source);
        return source;
    }

    private static IReadOnlyList<ServiceAccount> AccountsOf(AccountService accounts)
        => [.. accounts.Accounts
            .Where(a => ProviderCatalog.All.Any(p => p.Id == a.Provider && p.Service is not null))
            .Select(a => new ServiceAccount(a.Id, a.Provider, a.Label, a.NeedsSignIn))];

    private bool MayAsk(ServiceEntry entry) => _entries.MayAsk(entry) && !(entry.AccountId is { } account && _loading.Contains(account));

    private void OnAccountsChanged() => Reconcile();

    private void OnSignedOut(string accountId)
    {
        _memory.ForgetAccount(accountId);
        _entries.ForgetAccount(accountId);
        _seeded.Remove(accountId);
        _loading.Remove(accountId);
        Raise();
    }

    /// <summary>[S-2608] What every widget in every view reads, and every widget on screen.</summary>
    private void Reconcile()
    {
        if (_engine.Disposed)
        {
            return;
        }

        var wanted = (_sync?.Interests.Services ?? []).Concat(_shown.Select(s => s.Interest)).ToList();
        var now = DateTimeOffset.UtcNow;
        _entries.Reconcile(wanted, _accountList(), Cadence, now);
        SeedShelves();
        _engine.EnsureTicking();
        _engine.Poll();
        Raise();
    }

    /// <summary>[S-3780] The provider's row in ⚙ › Online updates.</summary>
    private static TimeSpan Cadence(ProviderId provider)
        => ProviderCatalog.All.FirstOrDefault(p => p.Id == provider)?.Service is { } descriptor
            ? UpdateCadence.Interval(UpdateCadence.Current, descriptor.Cadence)
            : TimeSpan.FromMinutes(15);

    /// <summary>[S-9926] [S-2693] Each account's shelf, once a run, read off the UI thread before its first fetch.</summary>
    private void SeedShelves()
    {
        if (_sync is null)
        {
            return;
        }

        foreach (var account in _entries.All.Select(e => e.AccountId).OfType<string>().Distinct().Where(a => !_seeded.Contains(a)).ToList())
        {
            _seeded.Add(account);
            _loading.Add(account);
            _ = SeedAsync(_sync, account);
        }
    }

    private async Task SeedAsync(SyncHost sync, string account)
    {
        ShelfRead<ServiceShelf> read;
        try
        {
            read = await sync.Vault.LoadAsync(SyncKind.Service, account, SyncJsonContext.Default.ServiceShelf, ServiceShelf.Version).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // The read never throws for bad data; anything here is a bug, and the account syncs from nothing.
            Log.Error(Scope, "reading an account's service shelf threw", ex);
            read = new ShelfRead<ServiceShelf>(null, ShelfReadOutcome.Discarded, ex.Message);
        }

        if (!_loading.Remove(account) || _engine.Disposed)
        {
            // Signed out while it was read: nothing of it is used.
            return;
        }

        if (read.Value is { } shelf)
        {
            var repairs = new List<string>();
            _entries.Seed(shelf, account, DateTimeOffset.UtcNow, repairs);
            foreach (var repair in repairs.Distinct())
            {
                Log.Info(Scope, repair);
            }
        }
        else if (read.Outcome == ShelfReadOutcome.Discarded)
        {
            Log.Warn(Scope, $"an account's service shelf was set aside ({read.Detail}); reading it again");
        }

        _engine.Poll();
        Raise();
    }

    private void Wake() => _engine.Wake();

    private ServiceClient Client(ProviderId provider, ServiceDescriptor descriptor)
    {
        if (!_clients.TryGetValue(provider, out var client))
        {
            client = new ServiceClient(descriptor, _transport, _memory);
            _clients[provider] = client;
        }

        return client;
    }

    /// <summary>One run of an entry: the query's pages or the replica's sync through the one client, held and shelved.</summary>
    private async Task<SyncRun> FetchAsync(ServiceEntry entry, bool asked, CancellationToken cancellation)
    {
        if (ProviderCatalog.All.FirstOrDefault(p => p.Id == entry.Provider)?.Service is not { } descriptor)
        {
            return new SyncRun(FetchStatus.NotConfigured, "no such service in this build");
        }

        var client = Client(entry.Provider, descriptor);
        var fetch = entry.Replica is { } replica && entry.AccountId is { } account
            ? await client.SyncAsync(account, replica, entry.ReplicaJson, ServiceEntries.TokenToSend(entry, DateTimeOffset.UtcNow), () => DateTimeOffset.UtcNow, cancellation).ConfigureAwait(true)
            : await client.FetchAsync(entry.AccountId, entry.Query!, entry.Count, () => DateTimeOffset.UtcNow, cancellation).ConfigureAwait(true);

        ServiceEntries.Apply(entry, fetch, DateTimeOffset.UtcNow);

        if (fetch.Status == FetchStatus.Ok && entry.AccountId is { } owner && _sync is not null && !_engine.Disposed)
        {
            _sync.Vault.SaveSoon(SyncKind.Service, owner, _entries.ToShelf(owner, entry.Provider), SyncJsonContext.Default.ServiceShelf);
        }

        return new SyncRun(fetch.Status, fetch.Detail, fetch.RetryAt);
    }

    private void Raise()
    {
        if (!_engine.Disposed)
        {
            Updated?.Invoke();
        }
    }

    public void Dispose()
    {
        if (_engine.Disposed)
        {
            return;
        }

        _engine.Dispose();

        if (_sync is not null)
        {
            _sync.InterestsChanged -= Reconcile;
            _sync.NetworkAvailable -= Wake;
        }

        if (_accounts is not null)
        {
            _accounts.AccountsChanged -= OnAccountsChanged;
            _accounts.SignedOut -= OnSignedOut;
        }
    }

    /// <summary>One widget's view of the sync: it reads, says it is shown, and asks for Refresh.</summary>
    private sealed class Source(ServiceSync owner, ServiceInterest interest) : IServiceSource
    {
        private bool _listening;

        public ServiceInterest Interest { get; private set; } = interest;

        public event Action? Changed
        {
            add
            {
                ChangedHandlers += value;
                if (!_listening)
                {
                    _listening = true;
                    owner.Updated += OnUpdated;
                }
            }

            remove => ChangedHandlers -= value;
        }

        private event Action? ChangedHandlers;

        public bool Busy => owner._entries.Busy(Interest);

        public KitRead Read() => owner._entries.Read(Interest, DateTimeOffset.UtcNow);

        public void Show(bool refresh)
        {
            owner.Reconcile();
            if (refresh)
            {
                owner._entries.Force(Interest, DateTimeOffset.UtcNow);
                owner._engine.Poll();
            }
        }

        public void Retarget(ServiceInterest interest)
        {
            ArgumentNullException.ThrowIfNull(interest);
            Interest = interest;
            owner.Reconcile();
        }

        private void OnUpdated() => ChangedHandlers?.Invoke();

        public void Dispose()
        {
            owner._shown.Remove(this);
            if (_listening)
            {
                owner.Updated -= OnUpdated;
                _listening = false;
            }
        }
    }
}
