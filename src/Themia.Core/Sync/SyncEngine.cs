using Themia.Core.Accounts;
using Themia.Core.Diagnostics;
using Themia.Core.Widgets;

namespace Themia.Core.Sync;

/// <summary>[S-2691] What one run of a synced thing came to: the one vocabulary's status, a line for the log, and a time named by the provider.</summary>
/// <param name="Status">Ok (or Empty), else the failure.</param>
/// <param name="Detail">One line for the log; never shown as is.</param>
/// <param name="RetryAt">
/// When the provider may be asked again: after a failure the next try waits at least until then ([S-9901]); after a
/// success it is a spent budget, and the next ask waits for it too.
/// </param>
public readonly record struct SyncRun(FetchStatus Status, string? Detail = null, DateTimeOffset? RetryAt = null)
{
    public static SyncRun Ok { get; } = new(FetchStatus.Ok);

    /// <summary>A typed account failure: its status, its sentence and the provider's "not before".</summary>
    public static SyncRun Of(AccountException failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return new(failure.ToFetchStatus(), failure.Message, failure.RetryAt);
    }

    public bool Succeeded => Status is FetchStatus.Ok or FetchStatus.Empty;
}

/// <summary>
/// [S-2691] One thing a sync keeps fresh (a mail folder, a calendar, an address book, a task account, a web answer, a
/// service query): its key, its <see cref="SyncSchedule"/>, whether a widget in some view reads it and whether the
/// user asked for it. What it holds is the subclass's.
/// </summary>
public abstract class SyncEntry
{
    protected SyncEntry(string key, TimeSpan interval)
    {
        ArgumentNullException.ThrowIfNull(key);
        Key = key;
        Schedule = new SyncSchedule(interval);
    }

    /// <summary>Unique in its table; never a token.</summary>
    public string Key { get; }

    public SyncSchedule Schedule { get; }

    /// <summary>A widget in some view reads it ([S-2608]): only these are run on the timer.</summary>
    public bool Wanted { get; internal set; }

    /// <summary>The user asked for it (Refresh): the run that is out or next is theirs, past the caches.</summary>
    public bool Asked { get; internal set; }

    /// <summary>The run that is out, while one is.</summary>
    public Task? Flight { get; internal set; }

    /// <summary>The user asked while a run was out: one more run follows it.</summary>
    internal bool Again { get; set; }

    /// <summary>The last status logged, so a change of state is one log line, never one per attempt.</summary>
    internal FetchStatus? Reported { get; set; }

    /// <summary>Whether it should run now; an entry with more than one clock (a calendar's near and whole window) widens it.</summary>
    public virtual bool IsDue(DateTimeOffset now) => Schedule.IsDue(now);

    /// <summary>What the log calls it: the provider, the account id, the folder or list; never a token or an item's text.</summary>
    public virtual string Subject => Key;
}

/// <summary>
/// [S-2691] The table of a sync, pure and on one thread: which entries exist and which a widget reads (a reconcile marks
/// them, the ones none reads any more go unless a run is out), which are due, the user's Refresh, the network coming
/// back, and what a finished run does to the schedule (a success waits the interval, or a spent budget's end; a
/// failure backs off, never before the provider's own time) with one log line per change of state. A host runs it
/// through <see cref="SyncEngine{TEntry}"/>; a pure owner (the service layer's entries) drives it itself.
/// </summary>
public sealed class SyncEntries<TEntry>(string scope)
    where TEntry : SyncEntry
{
    private readonly Dictionary<string, TEntry> _entries = new(StringComparer.Ordinal);

    public string Scope { get; } = scope ?? throw new ArgumentNullException(nameof(scope));

    public IReadOnlyCollection<TEntry> All => _entries.Values;

    public int Count => _entries.Count;

    public TEntry? Find(string key) => _entries.GetValueOrDefault(key);

    /// <summary>The entry under <paramref name="key"/>, made by <paramref name="create"/> when there is none; not marked wanted.</summary>
    public TEntry Hold(string key, Func<TEntry> create)
    {
        ArgumentNullException.ThrowIfNull(create);
        if (!_entries.TryGetValue(key, out var entry))
        {
            entry = create();
            if (entry.Key != key)
            {
                throw new ArgumentException($"an entry made for '{key}' is keyed '{entry.Key}'", nameof(create));
            }

            _entries[key] = entry;
        }

        return entry;
    }

    /// <summary>As <see cref="Hold"/>, and a widget reads it.</summary>
    public TEntry Want(string key, Func<TEntry> create)
    {
        var entry = Hold(key, create);
        entry.Wanted = true;
        return entry;
    }

    /// <summary>
    /// [S-2608] What every widget in every view reads now: <paramref name="want"/> marks them (through
    /// <see cref="Want"/>); with <paramref name="drop"/> the ones none reads any more are let go (a run that is out
    /// lands first), else they stay held but are not run. Returns the entries let go.
    /// </summary>
    public IReadOnlyList<TEntry> Reconcile(Action want, bool drop = true)
    {
        ArgumentNullException.ThrowIfNull(want);
        foreach (var entry in _entries.Values)
        {
            entry.Wanted = false;
        }

        want();

        if (!drop)
        {
            return [];
        }

        var gone = _entries.Values.Where(e => !e.Wanted && !e.Schedule.Busy).ToList();
        foreach (var entry in gone)
        {
            _entries.Remove(entry.Key);
        }

        return gone;
    }

    public bool Remove(string key) => _entries.Remove(key);

    /// <summary>Let go of every entry that matches (an account signed out); returns them.</summary>
    public IReadOnlyList<TEntry> RemoveWhere(Func<TEntry, bool> match)
    {
        ArgumentNullException.ThrowIfNull(match);
        var gone = _entries.Values.Where(match).ToList();
        foreach (var entry in gone)
        {
            _entries.Remove(entry.Key);
        }

        return gone;
    }

    /// <summary>The wanted entries due now that <paramref name="usable"/> allows (an account only the user can mend is not asked).</summary>
    public IReadOnlyList<TEntry> Due(DateTimeOffset now, Func<TEntry, bool>? usable = null)
        => [.. _entries.Values.Where(e => e.Wanted && e.IsDue(now) && (usable?.Invoke(e) ?? true))];

    /// <summary>The user's Refresh: it goes now, past freshness and back-off (not past a provider's "slow down"); asked while a run is out, one more follows.</summary>
    public void Force(TEntry entry, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(entry);
        entry.Schedule.Force(now);
        entry.Asked = true;
        if (entry.Schedule.Busy)
        {
            entry.Again = true;
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

    /// <summary>A run starts: nothing starts a second one beside it.</summary>
    public void Begin(TEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        entry.Schedule.Busy = true;
    }

    /// <summary>
    /// A run came back: a success waits the interval (or a spent budget's end), a failure backs off, never before the
    /// provider's own time ([S-9901]); one log line when the state changed, never one per attempt.
    /// </summary>
    public void Complete(TEntry entry, SyncRun run, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(entry);

        entry.Schedule.Busy = false;
        if (run.Succeeded)
        {
            entry.Schedule.Succeeded(now);
            if (run.RetryAt is { } block && block > now)
            {
                entry.Schedule.Hold(block - now, now);
            }
        }
        else
        {
            entry.Schedule.Failed(run.Status, run.Detail, now, run.RetryAt);
        }

        Note(entry, run, now);
    }

    /// <summary>A run that was cancelled (the owner closed): no outcome, no back-off.</summary>
    public static void Abandon(TEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        entry.Schedule.Busy = false;
        entry.Asked = false;
        entry.Again = false;
    }

    private void Note(TEntry entry, SyncRun run, DateTimeOffset now)
    {
        var status = run.Succeeded ? FetchStatus.Ok : run.Status;
        if (status == entry.Reported)
        {
            return;
        }

        var first = entry.Reported is null;
        entry.Reported = status;

        if (status == FetchStatus.Ok)
        {
            if (!first)
            {
                Log.Info(Scope, $"{entry.Subject}: answering again");
            }

            return;
        }

        var wait = entry.Schedule.NextAttempt - now;
        var line = string.IsNullOrWhiteSpace(run.Detail)
            ? $"{entry.Subject}: {status}; next try in {wait.TotalMinutes:F0} min"
            : $"{entry.Subject}: {status} ({run.Detail.Trim()}); next try in {wait.TotalMinutes:F0} min";

        switch (FetchState.LogLevelFor(status))
        {
            case LogLevel.Debug: Log.Debug(Scope, line); break;
            case LogLevel.Info: Log.Info(Scope, line); break;
            default: Log.Warn(Scope, line); break;
        }
    }
}

/// <summary>
/// [S-2691] The one sync engine every host sync runs on (the web answers, the kit services, mail, tasks, calendars and
/// contacts): a table of entries (<see cref="SyncEntries{TEntry}"/>), a tick at a minute while it holds any, the due
/// wanted ones run through the owner's fetch, the network coming back and the user's Refresh, and one cancellation
/// for everything in flight that <see cref="Dispose"/> trips (a fetch never outlives its owner, and what it brings back
/// after is dropped). The clock is handed in. The owner's fetch maps what came back onto its entry and says how it
/// went; the engine never decides what an entry holds.
/// <para>One thread: the ticker and the owner are there, and each run resumes there.</para>
/// </summary>
public sealed class SyncEngine<TEntry> : IDisposable
    where TEntry : SyncEntry
{
    /// <summary>One run of an entry: <paramref name="asked"/> when the user asked for it (past the caches).</summary>
    public delegate Task<SyncRun> Fetch(TEntry entry, bool asked, CancellationToken cancellation);

    /// <summary>How often the due entries are looked at: a minute, the finest cadence any sync asks for.</summary>
    public static readonly TimeSpan TickEvery = TimeSpan.FromMinutes(1);

    private readonly Fetch _fetch;
    private readonly Func<DateTimeOffset> _clock;
    private readonly Func<TimeSpan, Action, IDisposable>? _subscribe;
    private readonly Func<TEntry, bool>? _usable;
    private readonly CancellationTokenSource _cancel = new();
    private IDisposable? _tick;

    /// <param name="entries">The table (an owner with rules of its own over it keeps it; else a new one).</param>
    /// <param name="fetch">One run of an entry. A typed account failure it throws is its outcome; anything else is a bug, logged, and backs off.</param>
    /// <param name="clock">Now, in UTC.</param>
    /// <param name="subscribe">The shared ticker (interval, callback) → subscription; null runs only when asked (tests, previews).</param>
    /// <param name="usable">Whether an entry may be asked now (its account signed in); null: always.</param>
    public SyncEngine(SyncEntries<TEntry> entries, Fetch fetch, Func<DateTimeOffset> clock, Func<TimeSpan, Action, IDisposable>? subscribe = null, Func<TEntry, bool>? usable = null)
    {
        Entries = entries ?? throw new ArgumentNullException(nameof(entries));
        _fetch = fetch ?? throw new ArgumentNullException(nameof(fetch));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _subscribe = subscribe;
        _usable = usable;
    }

    /// <summary>Raised on the owner's thread when an entry starts or finishes a run.</summary>
    public event Action<TEntry>? Changed;

    /// <summary>Raised after every look at the due entries (each tick, a reconcile, the network back): the owner's queues drain here.</summary>
    public event Action? Polled;

    /// <summary>An entry that must run whatever its schedule says (more rows wanted than held); asked on every look.</summary>
    public Func<TEntry, bool>? AlsoDue { get; init; }

    public SyncEntries<TEntry> Entries { get; }

    public DateTimeOffset Now => _clock();

    /// <summary>Tripped by <see cref="Dispose"/>: every fetch in flight hears it.</summary>
    public CancellationToken Cancellation => _cancel.Token;

    public bool Disposed { get; private set; }

    /// <summary>[S-2608] What every widget reads now (<see cref="SyncEntries{TEntry}.Reconcile"/>), then the tick follows the table and what is due runs.</summary>
    public IReadOnlyList<TEntry> Reconcile(Action want, bool drop = true)
    {
        if (Disposed)
        {
            return [];
        }

        var gone = Entries.Reconcile(want, drop);
        EnsureTicking();
        Poll();
        return gone;
    }

    /// <summary>The tick runs while the table holds anything, and stops when it is empty.</summary>
    public void EnsureTicking()
    {
        if (Disposed || _subscribe is null)
        {
            return;
        }

        if (Entries.Count > 0)
        {
            _tick ??= _subscribe(TickEvery, Tick);
        }
        else
        {
            _tick?.Dispose();
            _tick = null;
        }
    }

    private void Tick() => Poll();

    /// <summary>Run every wanted entry that is due (or that <see cref="AlsoDue"/> says must go), then tell the owner (<see cref="Polled"/>).</summary>
    public void Poll()
    {
        if (Disposed)
        {
            return;
        }

        var now = Now;
        foreach (var entry in Entries.All.ToArray())
        {
            if (!entry.Wanted || entry.Schedule.Busy || !(_usable?.Invoke(entry) ?? true))
            {
                continue;
            }

            if (entry.IsDue(now) || (AlsoDue?.Invoke(entry) ?? false))
            {
                _ = RunAsync(entry);
            }
        }

        try
        {
            Polled?.Invoke();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Error(Entries.Scope, "a sync's poll handler threw", ex);
        }
    }

    /// <summary>The user's Refresh of these entries: they go now (one more after a run that is out), then what is due runs.</summary>
    public void Force(IEnumerable<TEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var now = Now;
        foreach (var entry in entries)
        {
            Entries.Force(entry, now);
        }

        Poll();
    }

    /// <summary>[S-2609] The network is back: every wait only about the network ends, and what is due runs.</summary>
    public void Wake()
    {
        Entries.Wake();
        Poll();
    }

    /// <summary>
    /// Run <paramref name="entry"/> now, whatever its schedule says (a widget waiting on a first read, the menu's
    /// list): single-flight, so a run that is out is the answer. Never throws.
    /// </summary>
    public Task RunAsync(TEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (Disposed)
        {
            return Task.CompletedTask;
        }

        if (entry.Schedule.Busy)
        {
            return entry.Flight ?? Task.CompletedTask;
        }

        var flight = RunOnceAsync(entry);
        if (!flight.IsCompleted)
        {
            entry.Flight = flight;
        }

        return flight;
    }

    private async Task RunOnceAsync(TEntry entry)
    {
        var asked = entry.Asked;
        Entries.Begin(entry);
        Raise(entry);

        SyncRun run;
        try
        {
            run = await _fetch(entry, asked, _cancel.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException) when (_cancel.IsCancellationRequested || !ReferenceEquals(Entries.Find(entry.Key), entry))
        {
            // ignore: the owner closed, or let the entry go mid-run (its account left); nobody is waiting for this run.
            SyncEntries<TEntry>.Abandon(entry);
            entry.Flight = null;
            return;
        }
        catch (AccountException ex)
        {
            run = SyncRun.Of(ex);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // The fetches turn every failure into a status; anything here is a bug, and the entry backs off
            // instead of retrying every tick.
            Log.Error(Entries.Scope, $"{entry.Subject}: a sync run threw", ex);
            run = new SyncRun(FetchStatus.BadResponse, ex.Message);
        }

        entry.Flight = null;
        if (Disposed)
        {
            SyncEntries<TEntry>.Abandon(entry);
            return;
        }

        Entries.Complete(entry, run, Now);
        entry.Asked = false;
        Raise(entry);

        if (entry.Again)
        {
            entry.Again = false;
            entry.Asked = true;
            entry.Schedule.Force(Now);
            await RunAsync(entry).ConfigureAwait(true);
        }
    }

    private void Raise(TEntry entry)
    {
        if (Disposed || Changed is not { } changed)
        {
            return;
        }

        foreach (var handler in changed.GetInvocationList().Cast<Action<TEntry>>())
        {
            try
            {
                handler(entry);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                // One listener must not stop the others hearing it.
                Log.Error(Entries.Scope, "a sync listener threw", ex);
            }
        }
    }

    public void Dispose()
    {
        if (Disposed)
        {
            return;
        }

        Disposed = true;
        _tick?.Dispose();
        _tick = null;
        Changed = null;
        Polled = null;

        try
        {
            _cancel.Cancel();
        }
        catch (AggregateException ex)
        {
            // A registration on the token threw while cancelling; the runs are still told.
            Log.Error(Entries.Scope, "cancelling the sync's runs threw", ex);
        }

        // Not disposed: a run still out reads the token it was handed; the source holds no timer.
    }
}
