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
/// [S-2501] Each source (plus its target: the ping host) is read at most once per due tick however
/// many widgets show it, on one ticker subscription at the cadence every feed needs together
/// ([S-2503]). [S-2504] A charted series keeps its history here, not in the view, so a view switch,
/// a card built again, edit mode, a display change or a reopened settings window shows the chart
/// with its history intact. [S-2505] A feed nobody holds lingers, still read, then is dropped with its
/// history, and a source no feed reads is closed.
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
    private readonly Dictionary<(SystemSource Source, string Target), Runner> _runners = [];
    private readonly List<Runner> _order = [];

    private IDisposable? _tick;
    private TimeSpan? _cadence;
    private bool _ticking;
    private bool _joinedDuringTick;
    private bool _disposed;

    public SystemHub(Ticker ticker, DispatcherQueue dispatcher)
    {
        ArgumentNullException.ThrowIfNull(ticker);
        ArgumentNullException.ThrowIfNull(dispatcher);
        _ticker = ticker;
        _dispatcher = dispatcher;
        _feeds = new FeedTable<Feed>(CreateFeed);
    }

    /// <summary>Feeds held or lingering. Exposed for diagnostics (the switch check, [S-2511]).</summary>
    public int FeedCount => _feeds.Count;

    /// <summary>Feeds nobody holds, still read until their linger runs out.</summary>
    public int LingeringCount => _feeds.Lingering;

    /// <summary>Sources open now.</summary>
    public int SourceCount => _runners.Count;

    /// <summary>Whether the hub holds its one ticker subscription ([S-2511]).</summary>
    public bool HoldsTicker => _tick is not null;

    /// <summary>
    /// [S-2506] Watch a source's snapshots at <paramref name="interval"/>. Never calls back from inside
    /// this call: the current value, when there is one, is <see cref="FeedWatch{T}.Value"/>; every
    /// read after this is <paramref name="onSample"/>, on the UI thread. Dispose to let go.
    /// </summary>
    public FeedWatch<T> Watch<T>(SourceId<T> source, TimeSpan interval, Action<T> onSample, string? target = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(onSample);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var key = FeedKey.Create(source.Source, interval, target);
        var feed = Join<T>(key);
        var watch = new FeedWatch<T>(this, feed, onSample, _ticker.CurrentFaultSink);
        feed.Watches.Add(watch);
        return watch;
    }

    /// <summary>
    /// [S-2504] [S-2506] Watch one charted series of a source (<paramref name="instance"/>: a drive, an
    /// adapter; empty for the source's only one). The reading and the history so far are on the watch
    /// at once; each read after this is <paramref name="onUpdate"/>, with the point it added.
    /// </summary>
    public SeriesWatch WatchSeries<T>(SourceId<T> source, TimeSpan interval, string instance, Action<SeriesUpdate> onUpdate, string? target = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(onUpdate);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var key = FeedKey.Create(source.Source, interval, target);
        var feed = Join<T>(key);
        var series = feed.SeriesFor(instance ?? string.Empty);
        var watch = new SeriesWatch(this, feed, series, onUpdate, _ticker.CurrentFaultSink);
        series.Watches.Add(watch);
        return watch;
    }

    private Feed<T> Join<T>(FeedKey key)
    {
        var joined = _feeds.Join(key, out var created);

        if (joined is not Feed<T> feed)
        {
            // A SourceId declared with the wrong snapshot type: a programming error, caught on first use.
            throw new InvalidOperationException($"{key} does not read {typeof(T).Name}");
        }

        if (created)
        {
            feed.Runner.Feeds.Add(feed);

            // A new feed is read now, through the due path, so nothing reads twice: a new cadence
            // resubscribes, which ticks at once; an unchanged one is ticked here. A feed made from
            // inside a delivery (a widget that starts a second watch on a reading) is read at the end
            // of that pass.
            if (_ticking)
            {
                _joinedDuringTick = true;
                Rearbitrate();
            }
            else if (!Rearbitrate())
            {
                OnTick();
            }
        }

        return feed;
    }

    private Feed CreateFeed(FeedKey key)
    {
        if (!_runners.TryGetValue((key.Source, key.Target), out var runner))
        {
            runner = OpenRunner(key.Source, key.Target);
            _runners[(key.Source, key.Target)] = runner;
            _order.Add(runner);
            _order.Sort(static (a, b) => a.Source != b.Source
                ? a.Source.CompareTo(b.Source)
                : string.CompareOrdinal(a.Target, b.Target));
            UpdateEventHooks();
        }

        return runner.NewFeed(key);
    }

    /// <summary>Called by a watch's Dispose. The feed lingers when this was its last holder.</summary>
    internal void Leave(Feed feed)
    {
        if (_disposed)
        {
            return;
        }

        if (!_feeds.Leave(feed.Key, Environment.TickCount64))
        {
            Log.Warn(Scope, $"a watch on {feed.Key} let go of a feed it did not hold");
        }
    }

    /// <summary>
    /// [S-2510] Read a feed's source now, out of cadence (Refresh): a reading for every feed of the
    /// source, and a point in the asking feed's history — a probe asked for is a probe made.
    /// </summary>
    internal void Refresh(Feed feed)
    {
        if (!_disposed && _feeds.TryGet(feed.Key, out var live) && ReferenceEquals(live, feed))
        {
            feed.Pending = true;
            feed.PendingPoint = true;
            feed.Runner.Read(feed.Runner.Feeds, point: false);
        }
    }

    /// <summary>[S-2509] Read every feed of a source now (a system event said its figures changed).</summary>
    private void RefreshSource(SystemSource source)
    {
        foreach (var runner in _order.ToArray())
        {
            if (runner.Source == source && !runner.Closed)
            {
                runner.Read(runner.Feeds, point: false);
            }
        }
    }

    /// <summary>[S-2503] Subscribe the ticker at the cadence the feeds need together. True when it resubscribed (which ticks at once).</summary>
    private bool Rearbitrate()
    {
        var cadence = _feeds.Cadence;

        if (cadence is null)
        {
            _tick?.Dispose();
            _tick = null;
            _cadence = null;
            return false;
        }

        if (_tick is not null && cadence == _cadence)
        {
            return false;
        }

        _tick?.Dispose();
        _cadence = cadence;
        _tick = _ticker.SubscribeUnscoped(cadence.Value, OnTick);
        return true;
    }

    private void OnTick()
    {
        // A resubscription from inside a tick ticks at once; the outer pass already covers it.
        if (_disposed || _ticking)
        {
            return;
        }

        _ticking = true;

        try
        {
            var changed = ExpireFeeds();
            var now = DateTimeOffset.Now;

            ReadDue(now);

            // Only the feeds made during the pass are due again: their first read.
            for (var pass = 0; _joinedDuringTick && pass < 4; pass++)
            {
                _joinedDuringTick = false;
                ReadDue(now);
            }

            _joinedDuringTick = false;

            // Still marked as ticking: a resubscription ticks at once, and this pass has just read.
            if (changed)
            {
                Rearbitrate();
            }
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

    /// <summary>[S-2501] [S-2503] Read each source once for the feeds whose own period moved on, in source order.</summary>
    private void ReadDue(DateTimeOffset now)
    {
        foreach (var runner in _order.ToArray())
        {
            if (runner.Closed)
            {
                continue;
            }

            List<Feed>? due = null;

            foreach (var feed in runner.Feeds)
            {
                if (HubCadence.IsDue(feed.LastPeriod, now, feed.Key.IntervalSeconds, out var period))
                {
                    feed.LastPeriod = period;
                    (due ??= []).Add(feed);
                }
            }

            if (due is not null)
            {
                runner.Read(due, point: true);
            }
        }
    }

    /// <summary>[S-2505] Drop the feeds whose linger ran out, and close a source no feed reads any more.</summary>
    private bool ExpireFeeds()
    {
        var expired = _feeds.Expire(Environment.TickCount64);

        foreach (var (key, feed) in expired)
        {
            var runner = feed.Runner;
            runner.Feeds.Remove(feed);

            if (runner.Feeds.Count == 0)
            {
                CloseRunner(runner);
            }

            Log.Debug(Scope, $"dropped {key} after its linger");
        }

        return expired.Count > 0;
    }

    private void CloseRunner(Runner runner)
    {
        _runners.Remove((runner.Source, runner.Target));
        _order.Remove(runner);
        runner.Close();
        UpdateEventHooks();
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
        _feeds.Clear();

        foreach (var runner in _order.ToArray())
        {
            runner.Close();
        }

        _runners.Clear();
        _order.Clear();
        UpdateEventHooks();
    }
}
