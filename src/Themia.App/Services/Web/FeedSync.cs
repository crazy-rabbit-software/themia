using Themia.App.Services.Sync;
using Themia.Core.Diagnostics;
using Themia.Core.Sync;
using Themia.Core.Widgets;

namespace Themia.App.Services.Web;

/// <summary>
/// [S-2661] The feeds every Rss widget in every view reads, kept fresh in the background: the one place
/// a feed is fetched. A widget draws what <see cref="Peek"/> holds (restored from the store at
/// launch, [S-1300]) and hears <see cref="Updated"/>; a view switch, a card rebuild or a resize asks
/// nothing. Each feed is asked on its widgets' refresh interval (the shortest, when several share it),
/// backs off on failure, wakes when the network comes back, and goes at once on Refresh.
/// <para>
/// UI thread: the ticker and the widgets are there, and each fetch resumes there.
/// </para>
/// </summary>
internal sealed class FeedSync : IDisposable
{
    private const string Scope = "rss";

    private readonly SyncHost _sync;
    private readonly Ticker _ticker;
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private IDisposable? _tick;
    private bool _disposed;

    public FeedSync(SyncHost sync, Ticker ticker)
    {
        _sync = sync;
        _ticker = ticker;
        _sync.InterestsChanged += Reconcile;
        _sync.NetworkAvailable += OnNetworkAvailable;
        Reconcile();
    }

    /// <summary>Raised on the UI thread with the address of a feed whose answer or state changed.</summary>
    public event Action<string>? Updated;

    /// <summary>The last good answer for a feed at a cap, regardless of age; never asks.</summary>
    public static Fetched<FeedDocument>? Peek(string url, int maxItems) => FeedService.Peek(url, maxItems);

    /// <summary>How the last attempt for a feed went (its value may be null: a failure with nothing held).</summary>
    public Fetched<FeedDocument>? Last(string url, int maxItems)
        => _entries.TryGetValue(FeedService.Key(url, maxItems), out var entry) ? entry.Last : null;

    public bool Busy(string url, int maxItems)
        => _entries.TryGetValue(FeedService.Key(url, maxItems), out var entry) && entry.Schedule.Busy;

    /// <summary>
    /// A shown widget's feeds. Nothing is asked for a feed already held; one never fetched (a feed
    /// just typed in) is asked now.
    /// </summary>
    public void Show(IReadOnlyList<string> urls, int maxItems, TimeSpan interval)
    {
        ArgumentNullException.ThrowIfNull(urls);

        foreach (var url in urls)
        {
            Track(url, maxItems, interval);
        }

        Poll(force: null);
    }

    /// <summary>[S-1303] The user's Refresh: these feeds now, past freshness and back-off.</summary>
    public void Refresh(IReadOnlyList<string> urls, int maxItems, TimeSpan interval)
    {
        ArgumentNullException.ThrowIfNull(urls);

        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var url in urls)
        {
            var entry = Track(url, maxItems, interval);
            entry.Schedule.Force(DateTimeOffset.UtcNow);
            keys.Add(entry.Key);
        }

        Poll(force: keys);
    }

    private Entry Track(string url, int maxItems, TimeSpan interval)
    {
        var key = FeedService.Key(url, maxItems);
        if (!_entries.TryGetValue(key, out var entry))
        {
            entry = new Entry(key, url, maxItems, interval);
            entry.Schedule.Seed(Peek(url, maxItems)?.At.ToUniversalTime(), DateTimeOffset.UtcNow);
            _entries[key] = entry;
        }

        entry.Schedule.Interval = interval < entry.Schedule.Interval || !entry.Wanted ? interval : entry.Schedule.Interval;
        entry.Wanted = true;
        EnsureTicking();
        return entry;
    }

    private void Reconcile()
    {
        if (_disposed)
        {
            return;
        }

        foreach (var entry in _entries.Values)
        {
            entry.Wanted = false;
        }

        foreach (var interest in _sync.Interests.Feeds)
        {
            Track(interest.Url, interest.MaxItems, interest.Interval);
        }

        // The interests cover every widget of every view; a feed none of them reads any more (an
        // address edited away) stops being synced.
        foreach (var key in _entries.Where(e => !e.Value.Wanted && !e.Value.Schedule.Busy).Select(e => e.Key).ToList())
        {
            _entries.Remove(key);
        }

        EnsureTicking();
        Poll(force: null);
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

    private void OnNetworkAvailable()
    {
        foreach (var entry in _entries.Values)
        {
            entry.Schedule.Wake();
        }

        Poll(force: null);
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
        Raise(entry.Url);

        try
        {
            var result = await FeedService.LoadAsync(entry.Url, entry.MaxItems, entry.Schedule.Interval, CancellationToken.None, force).ConfigureAwait(true);
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
        }
        catch (Exception ex)
        {
            // LoadAsync turns every failure into a status; anything here is a bug, and the feed
            // backs off instead of retrying every tick.
            Log.Error(Scope, "a feed sync threw", ex);
            entry.Schedule.Failed(FetchStatus.BadResponse, ex.Message, DateTimeOffset.UtcNow);
        }
        finally
        {
            entry.Schedule.Busy = false;
        }

        Raise(entry.Url);
    }

    private void Raise(string url)
    {
        if (_disposed || Updated is not { } updated)
        {
            return;
        }

        foreach (var handler in updated.GetInvocationList().Cast<Action<string>>())
        {
            try
            {
                handler(url);
            }
            catch (Exception ex)
            {
                // One widget's handler must not stop the others hearing it.
                Log.Error(Scope, "an Rss widget's update handler threw", ex);
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
        _sync.InterestsChanged -= Reconcile;
        _sync.NetworkAvailable -= OnNetworkAvailable;
        _tick?.Dispose();
        _tick = null;
        _entries.Clear();
        Updated = null;
    }

    private sealed class Entry(string key, string url, int maxItems, TimeSpan interval)
    {
        public string Key { get; } = key;

        public string Url { get; } = url;

        public int MaxItems { get; } = maxItems;

        public SyncSchedule Schedule { get; } = new(interval);

        public Fetched<FeedDocument>? Last { get; set; }

        /// <summary>A widget in some view reads it.</summary>
        public bool Wanted { get; set; }
    }
}
