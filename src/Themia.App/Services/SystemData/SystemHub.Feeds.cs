using Themia.Core.Diagnostics;
using Themia.Core.SystemData;

namespace Themia.App.Services.SystemData;

/// <summary>[S-2504] One read of a series: the reading to show, and the point it added to the history (null when none).</summary>
internal readonly record struct SeriesUpdate(MeterReading Reading, SeriesPoint? Point);

internal sealed partial class SystemHub
{
    /// <summary>One feed: a source at a cadence, its newest snapshot, its watchers and its charted series.</summary>
    internal abstract class Feed(FeedKey key, Runner runner)
    {
        public FeedKey Key { get; } = key;

        public Runner Runner { get; } = runner;

        /// <summary>The period index of the last due read ([S-2503]); none yet at first.</summary>
        public long LastPeriod { get; set; } = long.MinValue;

        /// <summary>[S-2507] A read was asked for and its answer has not been delivered yet.</summary>
        public bool Pending { get; set; }

        /// <summary>Whether that answer is a cadence read, which adds a point to the history.</summary>
        public bool PendingPoint { get; set; }
    }

    internal sealed class Feed<T>(FeedKey key, Runner<T> runner) : Feed(key, runner)
    {
        public new Runner<T> Runner { get; } = runner;

        public bool HasValue { get; set; }

        public T Value { get; set; } = default!;

        public List<FeedWatch<T>> Watches { get; } = [];

        public Dictionary<string, Series> Series { get; } = new(StringComparer.Ordinal);

        /// <summary>
        /// The series for an instance, made on first ask. It lives as long as its feed (instances are
        /// drives and adapters: a handful), so flipping a meter between two drives keeps both histories.
        /// A new series gets the newest snapshot's reading at once, without a point.
        /// </summary>
        public Series SeriesFor(string instance)
        {
            if (Series.TryGetValue(instance, out var series))
            {
                return series;
            }

            series = new Series(instance);
            Series[instance] = series;

            if (HasValue)
            {
                series.Latest = Runner.Project(Value, instance).Reading;
            }

            return series;
        }
    }

    /// <summary>[S-2504] One charted series: its history ring and newest reading, kept here so a view built again reads the same.</summary>
    internal sealed class Series(string instance)
    {
        public string Instance { get; } = instance;

        public SampleRing<SeriesPoint> Ring { get; } = new(SeriesHistory.Capacity);

        public MeterReading? Latest { get; set; }

        public List<SeriesWatch> Watches { get; } = [];
    }

    /// <summary>One open source and the feeds that read it.</summary>
    internal abstract class Runner(SystemSource source, string target)
    {
        public SystemSource Source { get; } = source;

        public string Target { get; } = target;

        public List<Feed> Feeds { get; } = [];

        public bool Closed { get; protected set; }

        public abstract Feed NewFeed(FeedKey key);

        /// <summary>Read once for <paramref name="feeds"/>; <paramref name="point"/> when it is their cadence read.</summary>
        public abstract void Read(IReadOnlyList<Feed> feeds, bool point);

        public abstract void Close();
    }

    internal sealed class Runner<T>(SystemSource source, string target, HubSource<T> reader) : Runner(source, target)
    {
        // The token source is both the cancel handle and the "a read is out" flag ([S-2507]).
        private CancellationTokenSource? _inFlight;
        private string? _fault;

        public override Feed NewFeed(FeedKey key) => new Feed<T>(key, this);

        public MeterSample Project(T value, string instance)
        {
            try
            {
                return reader.Project(value, instance);
            }
            catch (Exception ex)
            {
                Log.Error(Scope, $"could not project {Source} series '{instance}'", ex);
                return new MeterSample(Failed(), null);
            }
        }

        public override void Read(IReadOnlyList<Feed> feeds, bool point)
        {
            if (Closed)
            {
                return;
            }

            foreach (var feed in feeds)
            {
                feed.Pending = true;
                feed.PendingPoint |= point;
            }

            if (reader.IsAsync)
            {
                // [S-2507] One read at a time: a tick while one is out is served by its answer.
                if (_inFlight is null)
                {
                    _ = ReadAsync();
                }

                return;
            }

            T value;

            try
            {
                value = reader.Read();
                Note(null);
            }
            catch (Exception ex)
            {
                Note(ex);
                ClearPending();
                return;
            }

            Deliver(value);
        }

        private async Task ReadAsync()
        {
            var read = new CancellationTokenSource();
            _inFlight = read;

            try
            {
                var value = await reader.ReadAsync(read.Token).ConfigureAwait(true);

                // An answer for a source that has been closed belongs to nobody.
                if (Closed || !ReferenceEquals(_inFlight, read))
                {
                    return;
                }

                Note(null);
                Deliver(value);
            }
            catch (OperationCanceledException)
            {
                // ignore: cancelled by Close; nobody is left to show it to
            }
            catch (Exception ex)
            {
                // Fire-and-forget from a tick: an escape here would go unobserved.
                Note(ex);
                ClearPending();
            }
            finally
            {
                // The task that made the source owns it on every path: Close only cancels.
                if (ReferenceEquals(_inFlight, read))
                {
                    _inFlight = null;
                }

                read.Dispose();
            }
        }

        /// <summary>A failing source is logged when it starts failing and when it recovers, never once per tick.</summary>
        private void Note(Exception? error)
        {
            var fault = error?.Message;

            if (fault == _fault)
            {
                return;
            }

            _fault = fault;

            if (error is null)
            {
                Log.Info(Scope, $"{Source} reads again");
            }
            else
            {
                Log.Error(Scope, $"could not read {Source}", error);
            }
        }

        private void ClearPending()
        {
            foreach (var feed in Feeds)
            {
                feed.Pending = false;
                feed.PendingPoint = false;
            }
        }

        /// <summary>One snapshot to every feed that asked: its series (a point each on a cadence read), then its watchers.</summary>
        private void Deliver(T value)
        {
            foreach (var feed in Feeds.ToArray())
            {
                if (!feed.Pending || feed is not Feed<T> typed)
                {
                    continue;
                }

                var point = feed.PendingPoint;
                feed.Pending = false;
                feed.PendingPoint = false;
                typed.HasValue = true;
                typed.Value = value;

                foreach (var series in typed.Series.Values)
                {
                    var sample = Project(value, series.Instance);
                    var added = point ? sample.Point : null;

                    series.Latest = sample.Reading;

                    if (added is { } p)
                    {
                        series.Ring.Add(p);
                    }

                    foreach (var watch in series.Watches.ToArray())
                    {
                        watch.Notify(new SeriesUpdate(sample.Reading, added));
                    }
                }

                foreach (var watch in typed.Watches.ToArray())
                {
                    watch.Notify(value);
                }
            }
        }

        public override void Close()
        {
            if (Closed)
            {
                return;
            }

            Closed = true;
            var read = _inFlight;
            _inFlight = null;

            // Cancel only: ReadAsync's finally disposes it, on the continuation this releases.
            read?.Cancel();

            try
            {
                reader.Close();
            }
            catch (Exception ex)
            {
                Log.Error(Scope, $"closing {Source} failed", ex);
            }
        }
    }
}

/// <summary>[S-2506] One widget's hold on a feed's snapshots. Dispose to let go; twice is once.</summary>
internal sealed class FeedWatch<T> : IDisposable
{
    private readonly SystemHub _hub;
    private readonly SystemHub.Feed<T> _feed;
    private readonly Action<T> _onSample;
    private readonly Action<Exception>? _faults;
    private bool _disposed;

    internal FeedWatch(SystemHub hub, SystemHub.Feed<T> feed, Action<T> onSample, Action<Exception>? faults)
    {
        _hub = hub;
        _feed = feed;
        _onSample = onSample;
        _faults = faults;
    }

    public FeedKey Key => _feed.Key;

    /// <summary>Whether the feed has been read yet.</summary>
    public bool HasValue => _feed.HasValue;

    /// <summary>The newest snapshot; meaningful only when <see cref="HasValue"/>.</summary>
    public T Value => _feed.Value;

    /// <summary>[S-2510] Read now (a Refresh command): a new reading, no chart point.</summary>
    public void Refresh()
    {
        if (!_disposed)
        {
            _hub.Refresh(_feed);
        }
    }

    internal void Notify(T value)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            _onSample(value);
        }
        catch (Exception ex)
        {
            SystemHub.ReportFault(ex, _faults, _feed.Key);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _feed.Watches.Remove(this);
        _hub.Leave(_feed);
    }
}

/// <summary>[S-2504] [S-2506] One widget's hold on a charted series. Dispose to let go; twice is once.</summary>
internal sealed class SeriesWatch : IDisposable
{
    private readonly SystemHub _hub;
    private readonly SystemHub.Feed _feed;
    private readonly SystemHub.Series _series;
    private readonly Action<SeriesUpdate> _onUpdate;
    private readonly Action<Exception>? _faults;
    private bool _disposed;

    internal SeriesWatch(SystemHub hub, SystemHub.Feed feed, SystemHub.Series series, Action<SeriesUpdate> onUpdate, Action<Exception>? faults)
    {
        _hub = hub;
        _feed = feed;
        _series = series;
        _onUpdate = onUpdate;
        _faults = faults;
    }

    public FeedKey Key => _feed.Key;

    public string Instance => _series.Instance;

    /// <summary>The newest reading, or null before the first read has come back.</summary>
    public MeterReading? Latest => _series.Latest;

    /// <summary>How many points the history holds.</summary>
    public int Count => _series.Ring.Count;

    /// <summary>The newest <paramref name="max"/> points, oldest first (a copy).</summary>
    public SeriesPoint[] History(int max) => _series.Ring.Newest(max);

    /// <summary>[S-2510] Read now: a probe unless one is out; a new reading, no chart point.</summary>
    public void Refresh()
    {
        if (!_disposed)
        {
            _hub.Refresh(_feed);
        }
    }

    internal void Notify(SeriesUpdate update)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            _onUpdate(update);
        }
        catch (Exception ex)
        {
            SystemHub.ReportFault(ex, _faults, _feed.Key);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _series.Watches.Remove(this);
        _hub.Leave(_feed);
    }
}
