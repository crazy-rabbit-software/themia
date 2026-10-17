using Microsoft.UI.Dispatching;
using Themia.Core.Diagnostics;
using Themia.Core.Time;

namespace Themia.App.Services;

/// <summary>
/// One timer for the whole desktop.
/// <para>
/// A widget layer is mostly idle, so what actually costs power is how often the process wakes.
/// Every clock shares this ticker rather than owning a timer: the cadence is the finest any
/// subscriber asked for, it drops to once a minute the moment nothing needs seconds, it stops
/// entirely when nothing is subscribed or the surface is hidden, and each wake is re-aligned to the
/// next real boundary so the displayed time never drifts behind the system clock.
/// </para>
/// </summary>
public sealed class Ticker : IDisposable
{
    private const string Scope = "ticker";

    private readonly DispatcherQueueTimer _timer;
    private readonly List<Subscription> _subscriptions = [];

    private bool _paused;
    private bool _disposed;

    public Ticker(DispatcherQueue dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        _timer = dispatcher.CreateTimer();
        _timer.IsRepeating = false;
        _timer.Tick += OnTick;
    }

    /// <summary>The cadence the timer is currently running at. Exposed for diagnostics.</summary>
    public TimeSpan CurrentCadence { get; private set; } = TimeSpan.FromMinutes(1);

    /// <summary>How many subscriptions are live. Exposed for diagnostics: the switch check ([S-1883]).</summary>
    public int SubscriptionCount => _subscriptions.Count;

    /// <summary>Subscribe at a cadence. Dispose the result to unsubscribe; the timer stops when the last one goes.</summary>
    public IDisposable Subscribe(TimeSpan interval, Action onTick)
    {
        ArgumentNullException.ThrowIfNull(onTick);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var subscription = new Subscription(this, TickSchedule.Normalise(interval), onTick, _faultSink);
        _subscriptions.Add(subscription);
        subscription.Fire();
        Reschedule();
        return subscription;
    }

    /// <summary>
    /// [S-2506] Subscribe outside any fault scope: for a shared service (the system hub) that is
    /// subscribed from inside whichever widget's Attach happens to need it first, and must not report
    /// its own failures to that widget's card. Such a subscriber catches its own failures.
    /// </summary>
    public IDisposable SubscribeUnscoped(TimeSpan interval, Action onTick)
    {
        ArgumentNullException.ThrowIfNull(onTick);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var subscription = new Subscription(this, TickSchedule.Normalise(interval), onTick, null);
        _subscriptions.Add(subscription);
        subscription.Fire();
        Reschedule();
        return subscription;
    }

    /// <summary>
    /// [S-891] [S-2506] Where failures of a subscription made now would be reported: the card whose
    /// Attach or ApplyConfig is running, or null. The system hub keeps it per watch, so a widget whose
    /// callback throws gets its own error card.
    /// </summary>
    public Action<Exception>? CurrentFaultSink => _faultSink;

    /// <summary>
    /// [S-891] Route tick failures of the subscriptions made while the result is held to
    /// <paramref name="onFault"/>, as well as to the log. A widget card holds one around its view's
    /// Attach and ApplyConfig, which is where views subscribe, so a view that throws on a tick is
    /// replaced by its error card instead of failing silently every second. UI thread only (the
    /// ticker is); scopes nest.
    /// </summary>
    public IDisposable CatchFaults(Action<Exception> onFault)
    {
        ArgumentNullException.ThrowIfNull(onFault);
        return new FaultScope(this, onFault);
    }

    private Action<Exception>? _faultSink;

    private sealed class FaultScope : IDisposable
    {
        private readonly Ticker _owner;
        private readonly Action<Exception>? _previous;
        private bool _disposed;

        public FaultScope(Ticker owner, Action<Exception> sink)
        {
            _owner = owner;
            _previous = owner._faultSink;
            owner._faultSink = sink;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _owner._faultSink = _previous;
            }
        }
    }

    /// <summary>
    /// Stop waking while the surface cannot be seen (every widget window hidden, or the session
    /// locked). Resuming ticks immediately so nothing shows a stale time for up to a minute.
    /// </summary>
    public void SetPaused(bool paused)
    {
        if (_paused == paused || _disposed)
        {
            return;
        }

        _paused = paused;

        if (paused)
        {
            _timer.Stop();
            return;
        }

        foreach (var subscription in _subscriptions.ToArray())
        {
            subscription.Fire();
        }

        Reschedule();
    }

    private void Remove(Subscription subscription)
    {
        _subscriptions.Remove(subscription);
        Reschedule();
    }

    private void Reschedule()
    {
        if (_disposed || _paused)
        {
            return;
        }

        if (_subscriptions.Count == 0)
        {
            _timer.Stop();
            return;
        }

        var finest = TimeSpan.FromMinutes(1);
        foreach (var subscription in _subscriptions)
        {
            if (subscription.Interval < finest)
            {
                finest = subscription.Interval;
            }
        }

        CurrentCadence = finest;
        _timer.Interval = TickSchedule.DelayToNextBoundary(DateTimeOffset.Now, finest);
        _timer.Start();
    }

    private void OnTick(DispatcherQueueTimer sender, object args)
    {
        if (_disposed || _paused)
        {
            return;
        }

        var now = DateTimeOffset.Now;
        foreach (var subscription in _subscriptions.ToArray())
        {
            // A minute subscriber sharing a one-second timer must still only run on the minute.
            if (subscription.ShouldFire(now))
            {
                subscription.Fire();
            }
        }

        Reschedule();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _timer.Tick -= OnTick;
        _timer.Stop();
        _subscriptions.Clear();
    }

    private sealed class Subscription(Ticker owner, TimeSpan interval, Action onTick, Action<Exception>? onFault) : IDisposable
    {
        private long _lastPeriod = -1;
        private bool _disposed;

        public TimeSpan Interval { get; } = interval;

        public bool ShouldFire(DateTimeOffset now)
        {
            var period = TickSchedule.PeriodIndex(now, Interval);
            if (period == _lastPeriod)
            {
                return false;
            }

            _lastPeriod = period;
            return true;
        }

        public void Fire()
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                onTick();
            }
            catch (Exception ex)
            {
                // One misbehaving widget must not stop the clock for every other widget.
                Log.Error(Scope, "a tick subscriber threw", ex);
                ReportFault(ex);
            }
        }

        private void ReportFault(Exception error)
        {
            if (onFault is null)
            {
                return;
            }

            try
            {
                onFault(error);
            }
            catch (Exception ex)
            {
                Log.Error(Scope, "reporting a tick failure threw", ex);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            owner.Remove(this);
        }
    }
}
