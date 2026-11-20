using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
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

    /// <summary>[S-2001] What a click on the card opens for this config; null for nothing.</summary>
    public virtual CardOpen? Opens(WidgetConfig config) => null;

    /// <summary>The stamp of the reading a hub callback brought: one more than the last.</summary>
    protected long NextReading() => ++_reading;

    /// <summary>The stamp of the newest reading (what a watch presents again at once).</summary>
    protected long LastReading => _reading;

#if DEBUG
    /// <summary>[S-1942] Debug renders: the read <c>--state unavailable</c> shows, a thing not on this machine; null where the kind cannot be absent.</summary>
    public virtual MetricShown? Absent() => null;
#endif
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
    private readonly Func<string> _name;
    private WidgetConfig? _config;
    private bool _attached;

    /// <param name="host">The card's host: its hub.</param>
    /// <param name="glyph">The kind's glyph.</param>
    /// <param name="history">What history the kind's metric has.</param>
    /// <param name="name">The metric's name in the current language.</param>
    /// <param name="part">The family's part for the kind.</param>
    public SystemMetricView(IWidgetHost host, string glyph, MetricHistory history, Func<string> name, SystemMetricPart part)
        : base(host, glyph, history)
    {
        _host = host;
        _name = name ?? throw new ArgumentNullException(nameof(name));
        _part = part ?? throw new ArgumentNullException(nameof(part));
    }

    protected override string MetricName => _name();

    /// <inheritdoc />
    public override CardOpen? Opens => _config is { } config ? _part.Opens(config) : null;

    /// <inheritdoc />
    public override IReadOnlyList<WidgetAction> ContextActions
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
        else
        {
            _config = config;
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
        _config = config;
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
