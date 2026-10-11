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
/// [S-2661] [S-2663] The background sync of the web widgets' answers, the one place they are fetched:
/// every answer some widget in some view reads is asked on its widgets' interval (the shortest when
/// several share it), backs off on failure (<see cref="SyncSchedule"/>, the shared <c>Backoff</c>),
/// wakes when the network comes back and goes at once on Refresh. A widget draws <see cref="Answer"/>
/// and hears <see cref="Updated"/>; it never fetches.
/// <para>
/// UI thread: the ticker and the widgets are there, and each fetch resumes there.
/// </para>
/// </summary>
internal sealed class AnswerSync<T> : IDisposable
    where T : class
{
    private readonly string _scope;
    private readonly Ticker _ticker;
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private IDisposable? _tick;
    private bool _disposed;

    /// <param name="scope">The log scope, and the subject of its transition lines.</param>
    public AnswerSync(string scope, Ticker ticker)
    {
        _scope = scope;
        _ticker = ticker;
    }

    /// <summary>Raised on the UI thread with the key of an answer whose value or state changed.</summary>
    public event Action<string>? Updated;

    /// <summary>How the last attempt for a key went (its value may be null: a failure with nothing held).</summary>
    public Fetched<T>? Last(string key) => _entries.TryGetValue(key, out var entry) ? entry.Last : null;

    public bool Busy(string key) => _entries.TryGetValue(key, out var entry) && entry.Schedule.Busy;

    /// <summary>
    /// What a widget draws for a key: the newest good answer, annotated with the failure of the last
    /// attempt when that attempt could not improve on it (the value stays on screen, its age and the
    /// problem in the header). Null when nothing was ever held and nothing was asked yet.
    /// </summary>
    public Fetched<T>? Answer(string key)
    {
        if (!_entries.TryGetValue(key, out var entry))
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
            return last is null ? held : held.Stale(last.Status, last.Detail);
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

        Poll(force: null);
    }

    /// <summary>The user's Refresh: these answers now, past freshness and back-off (not past a provider's "slow down").</summary>
    public void Refresh(IEnumerable<AnswerWant<T>> wants)
    {
        ArgumentNullException.ThrowIfNull(wants);

        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var want in wants)
        {
            var entry = Track(want);
            entry.Schedule.Force(DateTimeOffset.UtcNow);
            keys.Add(entry.Key);
        }

        Poll(force: keys);
    }

    /// <summary>
    /// [S-2608] What every widget in every view reads now. An answer none of them reads any more (a
    /// place edited away) stops being synced; its last value stays in the cache.
    /// </summary>
    public void Reconcile(IEnumerable<AnswerWant<T>> wants)
    {
        ArgumentNullException.ThrowIfNull(wants);

        if (_disposed)
        {
            return;
        }

        foreach (var entry in _entries.Values)
        {
            entry.Wanted = false;
        }

        foreach (var want in wants)
        {
            Track(want);
        }

        foreach (var key in _entries.Where(e => !e.Value.Wanted && !e.Value.Schedule.Busy).Select(e => e.Key).ToList())
        {
            _entries.Remove(key);
        }

        EnsureTicking();
        Poll(force: null);
    }

    /// <summary>[S-2609] The network is back: every wait that was only about the network ends.</summary>
    public void Wake()
    {
        foreach (var entry in _entries.Values)
        {
            entry.Schedule.Wake();
        }

        Poll(force: null);
    }

    private Entry Track(AnswerWant<T> want)
    {
        if (!_entries.TryGetValue(want.Key, out var entry))
        {
            entry = new Entry(want);
            entry.Schedule.Seed(want.Held()?.At.ToUniversalTime(), DateTimeOffset.UtcNow);
            _entries[want.Key] = entry;
        }

        entry.Schedule.Interval = want.Interval < entry.Schedule.Interval || !entry.Wanted ? want.Interval : entry.Schedule.Interval;
        entry.Wanted = true;
        EnsureTicking();
        return entry;
    }

    private void EnsureTicking()
    {
        if (_disposed)
        {
            return;
        }

        if (_entries.Count > 0)
        {
            _tick ??= _ticker.Subscribe(TimeSpan.FromMinutes(1), () => Poll(force: null));
        }
        else
        {
            _tick?.Dispose();
            _tick = null;
        }
    }

    private void Poll(HashSet<string>? force)
    {
        if (_disposed)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var entry in _entries.Values.ToArray())
        {
            if (entry.Schedule.IsDue(now))
            {
                _ = RunAsync(entry, force?.Contains(entry.Key) == true);
            }
        }
    }

    private async Task RunAsync(Entry entry, bool force)
    {
        entry.Schedule.Busy = true;
        Raise(entry.Key);

        try
        {
            var result = await entry.Load(entry.Schedule.Interval, force).ConfigureAwait(true);
            entry.Last = result;
            var now = DateTimeOffset.UtcNow;

            if (result.Status is FetchStatus.Ok or FetchStatus.Empty)
            {
                entry.Schedule.Succeeded(now);
            }
            else
            {
                entry.Schedule.Failed(result.Status, result.Detail, now);
            }

            Note(entry, result.Status, result.Detail);
        }
        catch (Exception ex)
        {
            // The loaders turn every failure into a status; anything here is a bug, and the answer
            // backs off instead of retrying every tick.
            Log.Error(_scope, "a background fetch threw", ex);
            entry.Schedule.Failed(FetchStatus.BadResponse, ex.Message, DateTimeOffset.UtcNow);
        }
        finally
        {
            entry.Schedule.Busy = false;
        }

        Raise(entry.Key);
    }

    /// <summary>One log line per change of state, not one per attempt: an outage is one line, not one a minute.</summary>
    private void Note(Entry entry, FetchStatus status, string? detail)
    {
        if (status == entry.Reported)
        {
            return;
        }

        var previous = entry.Reported;
        entry.Reported = status;

        if (status == FetchStatus.Ok)
        {
            if (previous is not null)
            {
                Log.Info(_scope, "answering again");
            }

            return;
        }

        var line = string.IsNullOrWhiteSpace(detail) ? FetchState.Message(status) : $"{FetchState.Message(status)} ({detail.Trim()})";
        if (FetchState.LogLevelFor(status) == LogLevel.Warn)
        {
            Log.Warn(_scope, line);
        }
        else
        {
            Log.Debug(_scope, line);
        }
    }

    private void Raise(string key)
    {
        if (_disposed || Updated is not { } updated)
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
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _tick?.Dispose();
        _tick = null;
        _entries.Clear();
        Updated = null;
    }

    private sealed class Entry(AnswerWant<T> want)
    {
        public string Key { get; } = want.Key;

        public Func<TimeSpan, bool, Task<Fetched<T>>> Load { get; } = want.Load;

        public Func<Fetched<T>?> Held { get; } = want.Held;

        public SyncSchedule Schedule { get; } = new(want.Interval);

        public Fetched<T>? Last { get; set; }

        /// <summary>The last status logged, so a transition is logged once.</summary>
        public FetchStatus? Reported { get; set; }

        /// <summary>A widget in some view reads it.</summary>
        public bool Wanted { get; set; }
    }
}
