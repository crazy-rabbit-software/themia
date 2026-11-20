using Themia.Core.Diagnostics;
using Themia.Core.Sync;
using Themia.Core.Widgets;

namespace Themia.App.Services.Web;

/// <summary>
/// One answer a web widget needs (a feed, a reading, a quote): its cache key, the cadence its widgets
/// ask for, how to fetch it, and what the shared cache already holds for it.
/// </summary>
/// <param name="Key">The cache key: everything that changes the answer.</param>
/// <param name="Interval">The widget's refresh interval; the shortest wins when several widgets share the key.</param>
/// <param name="Load">Fetch it, fresh for the interval given, past the cache when the flag is set.</param>
/// <param name="Held">The last good answer the shared cache holds, regardless of age; never asks.</param>
internal sealed record AnswerWant<T>(
    string Key,
    TimeSpan Interval,
    Func<TimeSpan, bool, Task<Fetched<T>>> Load,
    Func<Fetched<T>?> Held)
    where T : class;

/// <summary>
/// [S-2661] [S-2663] The background sync of the web widgets' answers, the one place they are fetched, on the one
/// sync engine ([S-2691]): every answer some widget in some view reads is an entry, asked on its widgets' interval (the
/// shortest when several share it), backed off on failure and past a throttle's own time ([S-2694]), woken when the
/// network comes back and run at once on Refresh. A widget draws <see cref="Answer"/> and hears <see cref="Updated"/>;
/// it never fetches.
/// <para>UI thread: the ticker and the widgets are there, and each fetch resumes there.</para>
/// </summary>
internal sealed class AnswerSync<T> : IDisposable
    where T : class
{
    private readonly string _scope;
    private readonly SyncEngine<Entry> _engine;

    /// <param name="scope">The log scope, and the subject of its transition lines.</param>
    public AnswerSync(string scope, Ticker ticker)
    {
        ArgumentNullException.ThrowIfNull(ticker);
        _scope = scope;
        _engine = new SyncEngine<Entry>(new SyncEntries<Entry>(scope), FetchAsync, () => DateTimeOffset.UtcNow, ticker.Subscribe);
        _engine.Changed += entry => Raise(entry.Key);
    }

    /// <summary>Raised on the UI thread with the key of an answer whose value or state changed.</summary>
    public event Action<string>? Updated;

    /// <summary>How the last attempt for a key went (its value may be null: a failure with nothing held).</summary>
    public Fetched<T>? Last(string key) => _engine.Entries.Find(key)?.Last;

    public bool Busy(string key) => _engine.Entries.Find(key)?.Schedule.Busy == true;

    /// <summary>
    /// What a widget draws for a key: the newest good answer, annotated with the failure of the last
    /// attempt when that attempt could not improve on it (the value stays on screen, its age and the
    /// problem in the header). Null when nothing was ever held and nothing was asked yet.
    /// </summary>
    public Fetched<T>? Answer(string key)
    {
        if (_engine.Entries.Find(key) is not { } entry)
        {
            return null;
        }

        var last = entry.Last;
        if (last is { Value: not null })
        {
            return last;
        }

        if (entry.Held() is { } held)
        {
            return last is null ? held : held.Stale(last.Status, last.Detail, last.RetryAt);
        }

        return last;
    }

    /// <summary>
    /// A shown widget's answers. Nothing is asked for one already held and fresh; one never fetched
    /// (a place just picked, a symbol just typed) is asked now.
    /// </summary>
    public void Show(IEnumerable<AnswerWant<T>> wants)
    {
        ArgumentNullException.ThrowIfNull(wants);

        foreach (var want in wants)
        {
            Track(want);
        }

        _engine.EnsureTicking();
        _engine.Poll();
    }

    /// <summary>The user's Refresh: these answers now, past freshness and back-off (not past a provider's "slow down").</summary>
    public void Refresh(IEnumerable<AnswerWant<T>> wants)
    {
        ArgumentNullException.ThrowIfNull(wants);

        var entries = wants.Select(Track).ToList();
        _engine.EnsureTicking();
        _engine.Force(entries);
    }

    /// <summary>
    /// [S-2608] What every widget in every view reads now. An answer none of them reads any more (a
    /// place edited away) stops being synced; its last value stays in the cache.
    /// </summary>
    public void Reconcile(IEnumerable<AnswerWant<T>> wants)
    {
        ArgumentNullException.ThrowIfNull(wants);
        _engine.Reconcile(() =>
        {
            foreach (var want in wants)
            {
                Track(want);
            }
        });
    }

    /// <summary>[S-2609] The network is back: every wait that was only about the network ends.</summary>
    public void Wake() => _engine.Wake();

    /// <summary>An answer a widget reads: its entry, seeded from what the cache holds, on the shortest interval asked.</summary>
    private Entry Track(AnswerWant<T> want)
    {
        var fresh = _engine.Entries.Find(want.Key) is not { Wanted: true };
        var entry = _engine.Entries.Want(want.Key, () =>
        {
            var made = new Entry(want, _scope);
            made.Schedule.Seed(want.Held()?.At.ToUniversalTime(), DateTimeOffset.UtcNow);
            return made;
        });

        if (fresh || want.Interval < entry.Schedule.Interval)
        {
            entry.Schedule.Interval = want.Interval;
        }

        return entry;
    }

    private static async Task<SyncRun> FetchAsync(Entry entry, bool asked, CancellationToken cancellation)
    {
        var result = await entry.Load(entry.Schedule.Interval, asked).ConfigureAwait(true);
        entry.Last = result;
        return new SyncRun(result.Status, result.Detail, result.RetryAt);
    }

    private void Raise(string key)
    {
        if (_engine.Disposed || Updated is not { } updated)
        {
            return;
        }

        foreach (var handler in updated.GetInvocationList().Cast<Action<string>>())
        {
            try
            {
                handler(key);
            }
            catch (Exception ex)
            {
                // One widget's handler must not stop the others hearing it.
                Log.Error(_scope, "a widget's update handler threw", ex);
            }
        }
    }

    public void Dispose()
    {
        _engine.Dispose();
        Updated = null;
    }

    private sealed class Entry(AnswerWant<T> want, string scope) : SyncEntry(want.Key, want.Interval)
    {
        public Func<TimeSpan, bool, Task<Fetched<T>>> Load { get; } = want.Load;

        public Func<Fetched<T>?> Held { get; } = want.Held;

        public Fetched<T>? Last { get; set; }

        /// <summary>The key may hold a place or an address: the log names the sync, never the key.</summary>
        public override string Subject => $"a {scope} answer";
    }
}
