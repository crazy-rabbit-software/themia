using Themia.Core.Diagnostics;
using Themia.Core.Sync;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Sdk.Services;

namespace Themia.Core.Services;

/// <summary>A signed-in account as the service sync sees it: its id, provider, name, and whether only the user can mend it.</summary>
public sealed record ServiceAccount(string Id, ProviderId Provider, string Label, bool NeedsSignIn = false);

/// <summary>
/// [S-9925] One thing the service sync keeps fresh: a query's answer, or a replica, for one account
/// (or anonymously), shared by every widget that reads it.
/// </summary>
public sealed class ServiceEntry
{
    internal ServiceEntry(string key, ProviderId provider, string? accountId, IServiceQuery? query, IServiceReplicaQuery? replica, TimeSpan interval)
    {
        Key = key;
        Provider = provider;
        AccountId = accountId;
        Query = query;
        Replica = replica;
        Schedule = new SyncSchedule(interval);
    }

    /// <summary><c>provider|account|query</c>: never a token.</summary>
    public string Key { get; }

    public ProviderId Provider { get; }

    /// <summary>The account, or null for an anonymous read.</summary>
    public string? AccountId { get; }

    public IServiceQuery? Query { get; private set; }

    public IServiceReplicaQuery? Replica { get; }

    /// <summary>The most items any widget reading it shows.</summary>
    public int Count { get; internal set; } = 1;

    public SyncSchedule Schedule { get; }

    /// <summary>What it last held (kept through a failure, [S-25]).</summary>
    public IReadOnlyList<KitItem> Items { get; internal set; } = [];

    public KitSummary? Summary { get; internal set; }

    public LocalText? EmptySentence { get; internal set; }

    /// <summary>When what it holds was fetched; null while nothing is held.</summary>
    public DateTimeOffset? HeldAt { get; internal set; }

    /// <summary>The replica's JSON and token, for a replica entry.</summary>
    public string? ReplicaJson { get; internal set; }

    public string? SyncToken { get; internal set; }

    public DateTimeOffset? FullAt { get; internal set; }

    /// <summary>When the bucket it counts against may be asked again, when a block is known.</summary>
    public DateTimeOffset? RetryAt { get; internal set; }

    internal bool Wanted { get; set; }

    internal FetchStatus Reported { get; set; } = FetchStatus.Loading;

    internal void Retarget(IServiceQuery query) => Query = query;
}

/// <summary>
/// [S-9925] The service sync's state, pure and on one thread: which entries the widgets want (from
/// their interests and the signed-in accounts), when each is due (the provider's cadence, never below
/// its floors, never before a block ends, [S-9908]), what each holds, and what a widget reads of them
/// (<see cref="Read"/>: the accounts' items merged, one outcome per account, [S-9909]). The host runs
/// the due entries through the one client and hands the results back; it never decides any of this.
/// </summary>
public sealed class ServiceEntries
{
    private const string Scope = "service";

    private readonly Dictionary<string, ServiceEntry> _entries = new(StringComparer.Ordinal);
    private readonly Func<ProviderId, ServiceDescriptor?> _descriptor;
    private IReadOnlyList<ServiceAccount> _accounts = [];

    /// <param name="descriptor">The descriptor of a provider, or null when the build has no such kit service.</param>
    public ServiceEntries(Func<ProviderId, ServiceDescriptor?> descriptor)
        => _descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));

    public IReadOnlyCollection<ServiceEntry> All => _entries.Values;

    public ServiceEntry? Find(string key) => _entries.GetValueOrDefault(key);

    /// <summary>The accounts an interest reads ([S-7041]): every account of the provider when it names none, else the named ones still signed in.</summary>
    public IReadOnlyList<ServiceAccount> Targets(ServiceInterest interest)
    {
        ArgumentNullException.ThrowIfNull(interest);
        var own = _accounts.Where(a => a.Provider == interest.Provider).ToList();
        if (interest.AccountIds.Count == 0)
        {
            return own;
        }

        var wanted = new HashSet<string>(interest.AccountIds, StringComparer.Ordinal);
        return [.. own.Where(a => wanted.Contains(a.Id))];
    }

    /// <summary>The entry an interest reads for <paramref name="accountId"/> (null: anonymously).</summary>
    public static string KeyOf(ServiceInterest interest, string? accountId)
    {
        ArgumentNullException.ThrowIfNull(interest);
        var what = interest.View is { } view ? "replica:" + view.Replica.Key : interest.QueryKey;
        return $"{interest.Provider.Value}|{accountId ?? "anonymous"}|{what}";
    }

    /// <summary>
    /// [S-2608] What every widget in every view reads now, over the signed-in accounts. An entry none
    /// reads any more stops being synced and is let go (unless a fetch is out: it lands first).
    /// Returns the accounts whose entries changed shape (for the shelf).
    /// </summary>
    public void Reconcile(IReadOnlyList<ServiceInterest> interests, IReadOnlyList<ServiceAccount> accounts, Func<ProviderId, TimeSpan> cadence, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(interests);
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(cadence);

        _accounts = accounts;
        foreach (var entry in _entries.Values)
        {
            entry.Wanted = false;
            entry.Count = 1;
        }

        foreach (var interest in interests)
        {
            if (_descriptor(interest.Provider) is not { } descriptor)
            {
                continue;
            }

            var targets = Targets(interest).Select(a => (string?)a.Id).ToList();
            if (targets.Count == 0 && interest.AccountIds.Count == 0 && descriptor.Anonymous.PublicReads && interest.Query is not null)
            {
                targets.Add(null);
            }

            foreach (var account in targets)
            {
                var key = KeyOf(interest, account);
                if (!_entries.TryGetValue(key, out var entry))
                {
                    entry = new ServiceEntry(key, interest.Provider, account, interest.Query, interest.View?.Replica, Interval(descriptor, cadence(interest.Provider), account));
                    entry.Schedule.Seed(null, now);
                    _entries[key] = entry;
                }
                else if (interest.Query is { } query)
                {
                    entry.Retarget(query);
                }

                entry.Schedule.Interval = Interval(descriptor, cadence(interest.Provider), account);
                entry.Count = Math.Max(entry.Count, interest.Count);
                entry.Wanted = true;
            }
        }

        foreach (var key in _entries.Where(e => !e.Value.Wanted && !e.Value.Schedule.Busy).Select(e => e.Key).ToList())
        {
            _entries.Remove(key);
        }
    }

    /// <summary>[S-9908] The cadence, never below the service's floor, and its anonymous floor for a read without an account.</summary>
    private static TimeSpan Interval(ServiceDescriptor descriptor, TimeSpan cadence, string? account)
    {
        var wait = cadence < descriptor.IdleFloor ? descriptor.IdleFloor : cadence;
        return account is null && wait < descriptor.Anonymous.Floor ? descriptor.Anonymous.Floor : wait;
    }

    /// <summary>The entries due now: not busy, their time come, their account not one only the user can mend.</summary>
    public IReadOnlyList<ServiceEntry> Due(DateTimeOffset now)
        => [.. _entries.Values.Where(e => e.Schedule.IsDue(now) && !NeedsSignIn(e.AccountId))];

    private bool NeedsSignIn(string? accountId) => accountId is not null && _accounts.Any(a => a.Id == accountId && a.NeedsSignIn);

    /// <summary>The user's Refresh of a widget: its entries go now, past freshness and back-off (not past a provider's "slow down").</summary>
    public void Force(ServiceInterest interest, DateTimeOffset now)
    {
        foreach (var entry in EntriesOf(interest))
        {
            entry.Schedule.Force(now);
        }
    }

    /// <summary>[S-2609] The network is back: every wait that was only about the network ends.</summary>
    public void Wake()
    {
        foreach (var entry in _entries.Values)
        {
            entry.Schedule.Wake();
        }
    }

    /// <summary>
    /// A fetch came back. A success replaces what is held and waits the cadence, or its bucket's block
    /// when the success spent the budget; a failure keeps what is held and backs off, never before the
    /// throttle's own time ([S-9901]). One log line per change of state, never one per attempt, and
    /// no item's text in it. True when what a widget reads changed.
    /// </summary>
    public bool Complete(ServiceEntry entry, ServiceFetch fetch, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(fetch);

        entry.Schedule.Busy = false;
        entry.RetryAt = fetch.RetryAt;

        if (fetch.Status == FetchStatus.Ok)
        {
            entry.Schedule.Succeeded(now);
            if (fetch.RetryAt is { } block)
            {
                entry.Schedule.Hold(block - now, now);
            }

            if (fetch.Replica is { } replica)
            {
                entry.ReplicaJson = replica.Json;
                entry.SyncToken = replica.SyncToken;
                if (replica.Full)
                {
                    entry.FullAt = now;
                }
            }
            else
            {
                entry.Items = fetch.Items;
                entry.Summary = fetch.Summary;
                entry.EmptySentence = fetch.EmptySentence;
            }

            entry.HeldAt = now;
        }
        else
        {
            entry.Schedule.Failed(fetch.Status, fetch.Detail, now, fetch.RetryAt);
        }

        Note(entry, fetch.Status, fetch.Detail);
        return true;
    }

    /// <summary>The token a replica entry sends next: none (a full read) when it holds none or its last full read is older than the provider wants.</summary>
    public static string? TokenToSend(ServiceEntry entry, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (entry.Replica is not { } replica || entry.ReplicaJson is null || entry.SyncToken is null)
        {
            return null;
        }

        return entry.FullAt is { } full && now - full < replica.FullReadEvery ? entry.SyncToken : null;
    }

    private static void Note(ServiceEntry entry, FetchStatus status, string? detail)
    {
        if (status == entry.Reported)
        {
            return;
        }

        entry.Reported = status;
        var what = $"{entry.Provider} {(entry.AccountId is null ? "anonymous" : "account")} read";
        if (status == FetchStatus.Ok)
        {
            Log.Info(Scope, $"{what}: answering");
        }
        else
        {
            var line = $"{what}: {status} ({detail})";
            switch (FetchState.LogLevelFor(status))
            {
                case LogLevel.Debug: Log.Debug(Scope, line); break;
                case LogLevel.Info: Log.Info(Scope, line); break;
                default: Log.Warn(Scope, line); break;
            }
        }
    }

    private IEnumerable<ServiceEntry> EntriesOf(ServiceInterest interest)
    {
        foreach (var account in Targets(interest))
        {
            if (_entries.TryGetValue(KeyOf(interest, account.Id), out var entry))
            {
                yield return entry;
            }
        }

        if (_entries.TryGetValue(KeyOf(interest, null), out var anonymous))
        {
            yield return anonymous;
        }
    }

    /// <summary>Whether a fetch for one of the widget's own entries is out ([S-2661]).</summary>
    public bool Busy(ServiceInterest interest) => EntriesOf(interest).Any(e => e.Schedule.Busy);

    /// <summary>
    /// [S-9925] What a widget reads: its accounts' items merged ([S-9909]), one outcome per account (named
    /// when it reads more than one), the accounts it may read with how each stands (a sign-in only the
    /// user can mend, a refusal), the hosts its links may name, and the grant page of a service that
    /// grants per resource. Never asks the network.
    /// </summary>
    public KitRead Read(ServiceInterest interest, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(interest);

        var descriptor = _descriptor(interest.Provider);
        var targets = Targets(interest);
        var per = new List<IReadOnlyList<KitItem>>();
        var outcomes = new List<KitOutcome>();
        var accounts = new List<KitAccount>();
        LocalText? empty = null;

        foreach (var (account, entry) in targets.Select(a => (a, _entries.GetValueOrDefault(KeyOf(interest, a.Id)))))
        {
            var status = entry?.Schedule.Status ?? FetchStatus.Loading;
            accounts.Add(new KitAccount(account.Id, account.Label, account.NeedsSignIn || status == FetchStatus.SignInRequired
                ? KitAccountState.SignInRequired
                : status is FetchStatus.Forbidden or FetchStatus.NotFound ? KitAccountState.Forbidden : KitAccountState.Ok));

            if (entry is null)
            {
                outcomes.Add(new KitOutcome(Owner(targets, account), FetchStatus.Loading));
                continue;
            }

            var page = View(interest, entry, now);
            per.Add(page.Items);
            empty ??= page.EmptySentence;
            outcomes.Add(new KitOutcome(Owner(targets, account), Outcome(entry, page), null, entry.HeldAt, entry.RetryAt));
        }

        if (targets.Count == 0 && _entries.GetValueOrDefault(KeyOf(interest, null)) is { } anonymous)
        {
            var page = View(interest, anonymous, now);
            per.Add(page.Items);
            empty ??= page.EmptySentence;
            outcomes.Add(new KitOutcome(null, Outcome(anonymous, page), null, anonymous.HeldAt, anonymous.RetryAt));
        }

        var anonymousAllowed = descriptor?.Anonymous.PublicReads == true && interest.AccountIds.Count == 0;
        var merged = KitMerge.Merge(per, interest.Count);
        var grant = descriptor?.SignIn.Find<OAuthDeviceCode>()?.GrantPage;

        return new KitRead(
            merged,
            outcomes,
            Accounts: anonymousAllowed && accounts.Count == 0 ? null : accounts,
            Chosen: interest.AccountIds,
            WebHosts: descriptor?.WebHosts ?? [])
        {
            EmptySentence = empty,
            GrantPage = grant,
        };
    }

    private static KitPage View(ServiceInterest interest, ServiceEntry entry, DateTimeOffset now)
    {
        if (interest.View is { } view)
        {
            return entry.ReplicaJson is { Length: > 0 } json ? view.View(json, now) : new KitPage([]);
        }

        return new KitPage(entry.Items, entry.Summary, entry.EmptySentence);
    }

    /// <summary>The outcome a widget sees: the last attempt, and "nothing in it" for an answer with no items.</summary>
    private static FetchStatus Outcome(ServiceEntry entry, KitPage page)
        => entry.Schedule.Status == FetchStatus.Ok && page.Items.Count == 0 && entry.HeldAt is not null ? FetchStatus.Empty : entry.Schedule.Status;

    private static string? Owner(IReadOnlyList<ServiceAccount> targets, ServiceAccount account) => targets.Count > 1 ? account.Label : null;

    // ---- [S-9926] the shelf ---------------------------------------------------------------------

    /// <summary>One account's shelf: what each of its entries holds now.</summary>
    public ServiceShelf ToShelf(string accountId, ProviderId provider)
    {
        var shelf = new ServiceShelf { Owner = accountId, Provider = provider.Value };
        foreach (var entry in _entries.Values.Where(e => e.AccountId == accountId && e.HeldAt is not null).Take(ServiceShelves.MaxEntries))
        {
            if (entry.Replica is { } replica)
            {
                shelf.Replicas.Add(new ServiceShelfReplica { Key = replica.Key, Json = entry.ReplicaJson ?? string.Empty, SyncToken = entry.SyncToken, FetchedAt = entry.HeldAt!.Value, FullAt = entry.FullAt });
            }
            else
            {
                shelf.Entries.Add(new ServiceShelfEntry
                {
                    Key = entry.Query!.Key,
                    FetchedAt = entry.HeldAt!.Value,
                    Items = [.. entry.Items.Select(StoredKitItem.From)],
                    Summary = StoredKitSummary.From(entry.Summary),
                });
            }
        }

        return shelf;
    }

    /// <summary>
    /// [S-9926] Seed an account's entries from its shelf, validated ([S-2605]): an entry's items go
    /// through the item rules again, a replica through the provider's validation. Due at once only
    /// when older than the cadence ([S-2609]).
    /// </summary>
    public void Seed(ServiceShelf shelf, string accountId, DateTimeOffset now, List<string> repairs)
    {
        ArgumentNullException.ThrowIfNull(shelf);
        ArgumentNullException.ThrowIfNull(repairs);

        foreach (var entry in _entries.Values.Where(e => e.AccountId == accountId && e.HeldAt is null))
        {
            if (_descriptor(entry.Provider) is not { } descriptor)
            {
                continue;
            }

            if (entry.Replica is { } replica)
            {
                if (shelf.Replicas.FirstOrDefault(r => r.Key == replica.Key) is { } stored)
                {
                    if (replica.Validate(stored.Json, out var problem) is { } json)
                    {
                        entry.ReplicaJson = json;
                        entry.SyncToken = stored.SyncToken;
                        entry.FullAt = stored.FullAt;
                        entry.HeldAt = stored.FetchedAt;
                        entry.Schedule.Seed(stored.FetchedAt, now);
                    }
                    else
                    {
                        repairs.Add($"a stored replica could not be used ({problem}); reading everything again");
                    }
                }

                continue;
            }

            if (ServiceShelves.Validate(shelf, accountId, entry.Provider.Value, descriptor.WebHosts, descriptor.ItemBound, repairs) is { } entries
                && entries.FirstOrDefault(e => e.Key == entry.Query!.Key) is { Key: not null } held)
            {
                entry.Items = held.Items;
                entry.Summary = held.Summary;
                entry.HeldAt = held.At;
                entry.Schedule.Seed(held.At, now);
            }
        }
    }

    /// <summary>[S-2607] An account signed out: its entries go.</summary>
    public void ForgetAccount(string accountId)
    {
        foreach (var key in _entries.Where(e => e.Value.AccountId == accountId).Select(e => e.Key).ToList())
        {
            _entries.Remove(key);
        }
    }
}
