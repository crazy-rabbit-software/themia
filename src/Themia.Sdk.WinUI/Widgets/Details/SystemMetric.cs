using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.SystemData;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;

namespace Themia.App.Widgets.Details;

/// <summary>[S-14604] What a system metric's part hands its reads to: the shared <see cref="SystemMetricView"/>.</summary>
public interface ISystemMetricHost
{
    /// <summary>Put one read on the row. Called from the part's hub callbacks, on the UI thread.</summary>
    void Present(MetricShown shown);
}

/// <summary>
/// [S-14604] One system family's half of its single-metric widgets: which hub feeds (and series) the figures come from,
/// the sheet they make (the SDK's detail rules) and the one fact of it the kind shows (<see cref="MetricRules.Pick"/>).
/// <see cref="SystemMetricView"/> owns the face. A part reads nothing itself: it watches the hub's feeds in
/// <see cref="Watch"/> and lets go of them in <see cref="Detach"/> ([S-1881]). A hub callback is a new reading
/// (<see cref="MetricShown.Reading"/>: <see cref="NextReading"/>); what a watch presents at once is the last one again.
/// </summary>
public abstract class SystemMetricPart
{
    private long _reading;

    /// <summary>
    /// Take a config (already the kind's) and hold the feeds it needs, on Attach and on every config change. A feed held
    /// already is kept; what the feeds hold is presented at once.
    /// </summary>
    public abstract void Watch(ISystemFeeds hub, WidgetConfig config, ISystemMetricHost host);

    /// <summary>Let go of every feed. Safe twice.</summary>
    public abstract void Detach();

    /// <summary>[S-2510] Read now: a Refresh line on the card menu where the part offers one.</summary>
    public virtual Action? Refresh => null;

    /// <summary>The stamp of the reading a hub callback brought: one more than the last.</summary>
    protected long NextReading() => ++_reading;

    /// <summary>The stamp of the newest reading (what a watch presents again at once).</summary>
    protected long LastReading => _reading;

    /// <summary>
    /// [S-14710] [S-14711] <paramref name="shown"/> with its graph over the hub's <paramref name="series"/>: the newest points
    /// the config's span holds at the series' period (its kind's graph, [S-14711]), each <paramref name="point"/>, written as
    /// the shown figure is (<paramref name="figure"/>), with the series' absolute count so a fold stays put.
    /// </summary>
    protected static MetricShown Graphed(MetricShown shown, ISeriesWatch series, WidgetConfig config, Func<SeriesPoint, double?> point, Func<double, string> figure)
    {
        ArgumentNullException.ThrowIfNull(shown);
        ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(point);
        var slots = config is IMetricGraph graph ? MetricGraphs.Points(graph, series.Interest.Key.PeriodSeconds) : MetricRules.LinePoints;
        return shown with { Series = [.. series.History(slots).Select(point)], Slots = slots, Added = series.Added, Figure = figure };
    }

#if DEBUG
    /// <summary>[S-1942] Debug renders: the read <c>--state unavailable</c> shows, a thing not on this machine; null where the kind cannot be absent.</summary>
    public virtual MetricShown? Absent() => null;
#endif
}

/// <summary>
/// [S-14710] [S-14714] A metric that is one charted series of the hub, its reading the series' newest and its graph the
/// series' own history (the processor's load, the battery's charge, a drive's activity: what their histories drew,
/// [S-14713]): the series is the config's (<see cref="IReadsSystem"/>), its reading as the meter writes it, its history
/// written by <paramref name="figure"/> (a share, a rate). One part for every such kind: a family declares the kind, never a
/// part of its own. Unavailable (a sensor not there, no battery) its "--", why its tooltip.
/// </summary>
public sealed class SeriesMetricPart(Func<double, string> figure, Func<SeriesPoint, double?>? point = null) : SystemMetricPart
{
    private readonly Func<SeriesPoint, double?> _point = point ?? (p => double.IsFinite(p.First) ? p.First : null);
    private ISeriesWatch? _series;
    private WidgetConfig? _config;
    private ISystemMetricHost? _host;

    /// <inheritdoc />
    public override Action? Refresh => () => _series?.Refresh();

    public override void Watch(ISystemFeeds hub, WidgetConfig config, ISystemMetricHost host)
    {
        ArgumentNullException.ThrowIfNull(hub);
        _host = host;
        _config = config;
        var wanted = SystemInterests.For(config, SystemDrive.Root).Series.FirstOrDefault();
        if (wanted == default)
        {
            _series?.Dispose();
            _series = null;
        }
        else if (_series is null || _series.Interest != wanted)
        {
            _series?.Dispose();
            _series = hub.WatchSeries(wanted, _ =>
            {
                NextReading();
                Show();
            });
        }

        Show();
    }

    private void Show()
    {
        if (_host is not { } host || _config is not { } config)
        {
            return;
        }

        if (_series is not { Latest: { } latest } series)
        {
            host.Present(new MetricShown(MetricValue.Unread) { Why = Str.WidgetsB_Meters_Unavailable.Text() });
            return;
        }

        var value = latest.Available ? new MetricValue(latest.Primary, latest.Value ?? latest.Fraction) : MetricValue.Unread;
        var shown = new MetricShown(value) { Reading = LastReading, Why = latest.Available ? null : latest.Reason };
        host.Present(Graphed(shown, series, config, _point, figure));
    }

    public override void Detach()
    {
        _series?.Dispose();
        _series = null;
        _host = null;
    }
}

/// <summary>
/// [S-14604] The face of every system single-metric widget (the processor's, the graphics', the memory's, the network's,
/// the ping's, the battery's): the one base face (<see cref="MetricView"/>) over the hub, through its family's part.
/// Nothing is watched until Attach, and everything is let go in Detach ([S-1881]).
/// </summary>
public sealed partial class SystemMetricView : MetricView, ISystemMetricHost
{
    private const string LogScope = "metrics";

    private readonly IWidgetHost _host;
    private readonly SystemMetricPart _part;
    private bool _attached;

    /// <param name="host">The card's host: its hub.</param>
    /// <param name="kind">The kind.</param>
    /// <param name="part">The family's part for the kind.</param>
    public SystemMetricView(IWidgetHost host, MetricKind kind, SystemMetricPart part)
        : base(host, kind)
    {
        _host = host;
        _part = part ?? throw new ArgumentNullException(nameof(part));
    }

    /// <inheritdoc />
    protected override IReadOnlyList<WidgetAction> OwnActions
        => _part.Refresh is { } refresh ? [new WidgetAction(Glyphs.Refresh, Str.Common_Refresh.Text(), refresh)] : [];

    /// <inheritdoc />
    void ISystemMetricHost.Present(MetricShown shown)
    {
        if (_attached)
        {
            Present(shown);
        }
    }

    protected override void OnAttach(Ticker ticker, WidgetConfig config)
    {
        _attached = true;
        Watch(config);
    }

    protected override void OnApply(WidgetConfig config)
    {
        if (_attached)
        {
            Watch(config);
        }
    }

    protected override void OnDetach()
    {
        _attached = false;

        // [S-2513] The feeds stay in the hub while the state wants them.
        _part.Detach();
    }

    private void Watch(WidgetConfig config)
    {
        if (_host.System is not { } hub)
        {
            // Only before the host has started: there is nothing to read from yet.
            Log.Warn(LogScope, $"no system hub for {config.Kind}");
            Present(new MetricShown(MetricValue.Unread) { Why = Str.WidgetsB_Meters_Unavailable.Text() });
            return;
        }

        _part.Watch(hub, config, this);
    }
}
