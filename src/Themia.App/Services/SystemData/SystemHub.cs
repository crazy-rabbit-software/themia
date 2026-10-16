using Microsoft.UI.Dispatching;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.SystemData;

namespace Themia.App.Services.SystemData;

/// <summary>
/// [S-2500] The one place system widgets get their figures from: CPU, memory, disk, network, battery,
/// GPU, processes, ping. Owned by the host for the app's lifetime (<see cref="AppServices.System"/>);
/// a view watches in Attach and lets go in Detach, and never reads a counter or sends a probe itself.
/// <para>
/// [S-2501] [S-2503] Each source (plus its target: the ping host) is read once per tick of the hub's
/// one cadence however many widgets show it. [S-2513] What it reads is what every widget in every view
/// needs (<see cref="SetInterests"/>), from app start, so a view switched to already has its history
/// ([S-2504]); a watch holds a feed too (a render, a preview). [S-2505] A feed with neither is dropped
/// with its history, and its source closed.
/// </para>
/// <para>
/// UI thread only: every read, delivery and callback happens on the dispatcher, async reads come
/// back to it, and the system events that ask for a read ([S-2509]) are marshalled onto it.
/// </para>
/// </summary>
internal sealed partial class SystemHub : IDisposable
{
    private const string Scope = "system-hub";

    private readonly Ticker _ticker;
    private readonly DispatcherQueue _dispatcher;
    private readonly FeedTable<Feed> _feeds;
    private readonly List<Feed> _order = [];

    private SystemInterests _interests = SystemInterests.None;
    private IDisposable? _tick;
    private bool _ticking;
    private bool _joinedDuringTick;
    private bool _disposed;

    public SystemHub(Ticker ticker, DispatcherQueue dispatcher)
    {
        ArgumentNullException.ThrowIfNull(ticker);
        ArgumentNullException.ThrowIfNull(dispatcher);
        _ticker = ticker;
        _dispatcher = dispatcher;
        _feeds = new FeedTable<Feed>(OpenFeed);
    }

    /// <summary>Open feeds (each one source read). Exposed for diagnostics (the switch check, [S-2511]).</summary>
    public int FeedCount => _feeds.Count;

    /// <summary>Feeds the state wants ([S-2513]).</summary>
    public int WantedCount => _feeds.Wanted;

    /// <summary>Charted series kept, over every feed.</summary>
    public int SeriesCount => _order.Sum(f => f.Series.Count);

    /// <summary>Whether the hub holds its one ticker subscription ([S-2511]).</summary>
    public bool HoldsTicker => _tick is not null;

    /// <summary>
    /// [S-2513] Read what the widgets of every view need: feeds newly wanted are opened and read at
    /// once, series newly wanted start their history, and what nothing wants or holds any more is
    /// dropped. Called with the whole state's interests at start and on every state change; a change
    /// that wants the same is nothing.
    /// </summary>
    public void SetInterests(SystemInterests interests)
    {
        ArgumentNullException.ThrowIfNull(interests);

        if (_disposed || interests.SameAs(_interests))
        {
            return;
        }

        _interests = interests;

        var dropped = _feeds.Want(interests.Feeds, out var created);

        foreach (var gone in dropped)
        {
            Close(gone);
        }

        foreach (var feed in _order)
        {
            feed.WantSeries(interests.Series.Where(s => s.Key == feed.Key).Select(s => s.Instance));
            feed.Want(interests);
        }

        if (created.Count > 0)
        {
            ReadNew();
        }

        UpdateTicker();
    }

    /// <summary>
    /// [S-2506] Watch a source's snapshots. Never calls back from inside this call: the current value,
    /// when there is one, is <see cref="FeedWatch{T}.Value"/>; every read after this is
    /// <paramref name="onSample"/>, on the UI thread. Dispose to let go.
    /// </summary>
    public FeedWatch<T> Watch<T>(SourceId<T> source, Action<T> onSample, string? target = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(onSample);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var key = FeedKey.Create(source.Source, target);

        if (Join(key) is not Feed<T> feed)
        {
            // A SourceId declared with the wrong snapshot type: a programming error, caught on first use.
            throw new InvalidOperationException($"{key} does not read {typeof(T).Name}");
        }

        var watch = new FeedWatch<T>(this, feed, onSample, _ticker.CurrentFaultSink);
        feed.Watches.Add(watch);
        return watch;
    }

    /// <summary>
    /// [S-2504] [S-2506] Watch one charted series. The reading and the history so far are on the watch
    /// at once; each read after this is <paramref name="onUpdate"/>, with the point it added.
    /// </summary>
    public SeriesWatch WatchSeries(SeriesInterest series, Action<SeriesUpdate> onUpdate)
    {
        ArgumentNullException.ThrowIfNull(onUpdate);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var feed = Join(series.Key);
        var held = feed.SeriesFor(series.Instance ?? string.Empty);
        var watch = new SeriesWatch(this, feed, held, onUpdate, _ticker.CurrentFaultSink);
        held.Watches.Add(watch);
        return watch;
    }

    private Feed Join(FeedKey key)
    {
        var feed = _feeds.Join(key, out var created);

        if (created)
        {
            // A new feed is read now, through the due path, so nothing reads twice. A feed made from
            // inside a delivery (a widget that starts a second watch on a reading) is read at the end
            // of that pass.
            if (_ticking)
            {
                _joinedDuringTick = true;
                UpdateTicker();
            }
            else if (!UpdateTicker())
            {
                OnTick();
            }
        }

        return feed;
    }

    private Feed OpenFeed(FeedKey key)
    {
        var feed = OpenSource(key);
        feed.Want(_interests);
        _order.Add(feed);
        _order.Sort(static (a, b) => a.Key.Source != b.Key.Source
            ? a.Key.Source.CompareTo(b.Key.Source)
            : string.CompareOrdinal(a.Key.Target, b.Key.Target));
        UpdateEventHooks();
        return feed;
    }

    /// <summary>Called by a watch's Dispose. The feed goes when the state does not want it either.</summary>
    internal void Leave(Feed feed)
    {
        if (_disposed)
        {
            return;
        }

        var gone = _feeds.Leave(feed.Key, out var known);

        if (!known)
        {
            Log.Warn(Scope, $"a watch on {feed.Key} let go of a feed it did not hold");
            return;
        }

        if (gone is not null)
        {
            Close(gone);
            UpdateTicker();
        }
    }

    /// <summary>
    /// [S-2510] Read a feed's source now, out of cadence (Refresh): a reading, and a point in its
    /// history — a probe asked for is a probe made.
    /// </summary>
    internal void Refresh(Feed feed)
    {
        if (!_disposed && _feeds.TryGet(feed.Key, out var live) && ReferenceEquals(live, feed))
        {
            feed.Read(point: true);
        }
    }

    /// <summary>[S-2509] Read every feed of a source now (a system event said its figures changed): a reading, no point.</summary>
    private void RefreshSource(SystemSource source)
    {
        foreach (var feed in _order.ToArray())
        {
            if (feed.Key.Source == source && !feed.Closed)
            {
                feed.Read(point: false);
            }
        }
    }

    /// <summary>[S-2503] Hold the one ticker subscription while any feed is open. True when it subscribed (which ticks at once).</summary>
    private bool UpdateTicker()
    {
        if (_feeds.Count == 0)
        {
            _tick?.Dispose();
            _tick = null;
            return false;
        }

        if (_tick is not null)
        {
            return false;
        }

        _tick = _ticker.SubscribeUnscoped(SystemCadence.Tick, OnTick);
        return true;
    }

    /// <summary>The feeds just made, read now (each is due: it has never been read).</summary>
    private void ReadNew()
    {
        if (_ticking)
        {
            _joinedDuringTick = true;
        }
        else
        {
            OnTick();
        }
    }

    private void OnTick()
    {
        // A subscription made from inside a tick ticks at once; the outer pass already covers it.
        if (_disposed || _ticking)
        {
            return;
        }

        _ticking = true;

        try
        {
            var now = DateTimeOffset.Now;
            ReadDue(now);

            // Only the feeds made during the pass are due again: their first read.
            for (var pass = 0; _joinedDuringTick && pass < 4; pass++)
            {
                _joinedDuringTick = false;
                ReadDue(now);
            }

            _joinedDuringTick = false;
        }
        catch (Exception ex)
        {
            // The hub is every system widget's clock: one bad pass must not stop the next.
            Log.Error(Scope, "a hub tick failed", ex);
        }
        finally
        {
            _ticking = false;
        }
    }

    /// <summary>[S-2501] [S-2503] Read each source whose own period moved on, in source order.</summary>
    private void ReadDue(DateTimeOffset now)
    {
        foreach (var feed in _order.ToArray())
        {
            if (!feed.Closed && SystemCadence.IsDue(feed.LastPeriod, now, feed.Key.Source, out var period))
            {
                feed.LastPeriod = period;
                feed.Read(point: true);
            }
        }
    }

    private void Close(Feed feed)
    {
        _order.Remove(feed);
        feed.Close();
        UpdateEventHooks();
        Log.Debug(Scope, $"closed {feed.Key}: nothing wants or holds it");
    }

    /// <summary>
    /// A subscriber's callback threw. Logged, and handed to the card it belongs to ([S-891]) so the
    /// view is replaced by its error card; every other subscriber still gets the reading.
    /// </summary>
    internal static void ReportFault(Exception error, Action<Exception>? sink, FeedKey key)
    {
        Log.Error(Scope, $"a subscriber of {key} threw", error);

        if (sink is null)
        {
            return;
        }

        try
        {
            sink(error);
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "reporting a subscriber failure threw", ex);
        }
    }

    /// <summary>A reading that says the source failed, for a series whose projection threw.</summary>
    private static MeterReading Failed() => MeterReading.Unavailable(Str.WidgetsB_Meters_Error.Text());

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _tick?.Dispose();
        _tick = null;

        foreach (var feed in _feeds.Clear())
        {
            feed.Close();
        }

        _order.Clear();
        UpdateEventHooks();
    }
}
