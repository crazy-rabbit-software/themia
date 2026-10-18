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
/// entries exist (<see cref="ServiceEntries"/>, pure); this runs the due ones through the one client
/// (<see cref="ServiceClient"/>) on the shared ticker at a minute, wakes them when the network comes
/// back ([S-2609]), keeps what they hold in each account's sealed <c>service</c> shelf ([S-9926])
/// and tells the widgets reading them. A sign-out forgets the account here; its shelf is purged with
/// the rest ([S-2607]).
/// <para>UI thread: the ticker and the widgets are there, and each fetch resumes there.</para>
/// </summary>
internal sealed class ServiceSync : IServiceSources, IDisposable
{
    private const string Scope = "service";

    private readonly SyncHost? _sync;
    private readonly AccountService? _accounts;
    private readonly Ticker _ticker;
    private readonly IServiceTransport _transport;
    private readonly ServiceMemory _memory = new();
    private readonly ServiceEntries _entries;
    private readonly Dictionary<ProviderId, ServiceClient> _clients = [];
    private readonly HashSet<string> _seeded = new(StringComparer.Ordinal);
    private readonly List<Source> _shown = [];
    private readonly Func<IReadOnlyList<ServiceAccount>> _accountList;
    private IDisposable? _tick;
    private bool _disposed;

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
        _ticker = ticker;
        _transport = transport;
        _accountList = accounts;
        _entries = new ServiceEntries(id => ProviderCatalog.All.FirstOrDefault(p => p.Id == id)?.Service);
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

    private void OnAccountsChanged() => Reconcile();

    private void OnSignedOut(string accountId)
    {
        _memory.ForgetAccount(accountId);
        _entries.ForgetAccount(accountId);
        _seeded.Remove(accountId);
        Raise();
    }

    /// <summary>[S-2608] What every widget in every view reads, and every widget on screen.</summary>
    private void Reconcile()
    {
        if (_disposed)
        {
            return;
        }

        var wanted = (_sync?.Interests.Services ?? []).Concat(_shown.Select(s => s.Interest)).ToList();
        var now = DateTimeOffset.UtcNow;
        _entries.Reconcile(wanted, _accountList(), Cadence, now);
        SeedShelves(now);
        EnsureTicking();
        Poll();
        Raise();
    }

    /// <summary>[S-3780] The provider's row in Settings › Widgets › Updates.</summary>
    private static TimeSpan Cadence(ProviderId provider)
        => ProviderCatalog.All.FirstOrDefault(p => p.Id == provider)?.Service is { } descriptor
            ? UpdateCadence.Interval(UpdateCadence.Current, descriptor.Cadence)
            : TimeSpan.FromMinutes(15);

    /// <summary>[S-9926] Each account's shelf, once a run, before its first fetch.</summary>
    private void SeedShelves(DateTimeOffset now)
    {
        if (_sync is null)
        {
            return;
        }

        foreach (var account in _entries.All.Select(e => e.AccountId).OfType<string>().Distinct().Where(a => !_seeded.Contains(a)).ToList())
        {
            _seeded.Add(account);
            var read = _sync.Vault.Load(SyncKind.Service, account, SyncJsonContext.Default.ServiceShelf, ServiceShelf.Version);
            if (read.Value is not { } shelf)
            {
                if (read.Outcome == ShelfReadOutcome.Discarded)
                {
                    Log.Warn(Scope, $"an account's service shelf was set aside ({read.Detail}); reading it again");
                }

                continue;
            }

            var repairs = new List<string>();
            _entries.Seed(shelf, account, now, repairs);
            foreach (var repair in repairs.Distinct())
            {
                Log.Info(Scope, repair);
            }
        }
    }

    private void Wake()
    {
        _entries.Wake();
        Poll();
    }

    private void EnsureTicking()
    {
        if (_disposed)
        {
            return;
        }

        if (_entries.All.Count > 0)
        {
            _tick ??= _ticker.Subscribe(TimeSpan.FromMinutes(1), Poll);
        }
        else
        {
            _tick?.Dispose();
            _tick = null;
        }
    }

    private void Poll()
    {
        if (_disposed)
        {
            return;
        }

        foreach (var entry in _entries.Due(DateTimeOffset.UtcNow))
        {
            _ = RunAsync(entry);
        }
    }

    private ServiceClient Client(ProviderId provider, ServiceDescriptor descriptor)
    {
        if (!_clients.TryGetValue(provider, out var client))
        {
            client = new ServiceClient(descriptor, _transport, _memory);
            _clients[provider] = client;
        }

        return client;
    }

    private async Task RunAsync(ServiceEntry entry)
    {
        if (ProviderCatalog.All.FirstOrDefault(p => p.Id == entry.Provider)?.Service is not { } descriptor)
        {
            return;
        }

        entry.Schedule.Busy = true;
        Raise();

        ServiceFetch fetch;
        try
        {
            var client = Client(entry.Provider, descriptor);
            fetch = entry.Replica is { } replica && entry.AccountId is { } account
                ? await client.SyncAsync(account, replica, entry.ReplicaJson, ServiceEntries.TokenToSend(entry, DateTimeOffset.UtcNow), () => DateTimeOffset.UtcNow, CancellationToken.None).ConfigureAwait(true)
                : await client.FetchAsync(entry.AccountId, entry.Query!, entry.Count, () => DateTimeOffset.UtcNow, CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // The client turns every failure into a status; anything here is a bug, and the entry
            // backs off instead of retrying every tick.
            Log.Error(Scope, "a service fetch threw", ex);
            fetch = ServiceFetch.Failed(FetchStatus.BadResponse, ex.Message);
        }

        if (_disposed)
        {
            return;
        }

        _entries.Complete(entry, fetch, DateTimeOffset.UtcNow);

        if (fetch.Status == FetchStatus.Ok && entry.AccountId is { } owner && _sync is not null)
        {
            _sync.Vault.SaveSoon(SyncKind.Service, owner, _entries.ToShelf(owner, entry.Provider), SyncJsonContext.Default.ServiceShelf);
        }

        Raise();
    }

    private void Raise()
    {
        if (!_disposed)
        {
            Updated?.Invoke();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _tick?.Dispose();
        _tick = null;

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
                owner.Poll();
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
