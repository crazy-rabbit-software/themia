using Themia.App.Services;
using Themia.App.Services.SystemData;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.SystemData;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Widget.PingFacts;

namespace Themia.App.Widgets.Details;

/// <summary>
/// [S-2426]..[S-2429] Latency statistics over the latest probes of one host: the newest answer,
/// average, range, jitter and loss. [S-2508] The probes are the hub's prober for the host, the same
/// one a ping meter on that host charts: one probe out at a time, a late answer from a
/// host the user has since changed belongs to the old host's prober ([S-106], [S-107]), and the window
/// is the prober's history, so the statistics survive a view switch.
/// </summary>
internal sealed class PingFactsPart : DetailPart
{
    /// <inheritdoc />
    public override CardOpen? Opens(WidgetConfig config) => PingFactsModule.Opens;

    private PingFactsConfig _config = new();
    private ISeriesWatch? _watch;
    private SeriesInterest? _series;
    private IDetailHost? _host;

    public override WidgetId Kind => new WidgetId("pingFacts");

    public override bool HasHero => false;

    /// <summary>[S-11023] Fact rows alone, as the config picks them.</summary>
    public override DetailAtom? Atom => DetailAtom.Facts;

    public override IReadOnlyList<FactOption> FactOptions => PingDetailRules.Facts;

    public override void Watch(ISystemFeeds hub, WidgetConfig config, IDetailHost host)
    {
        _host = host;
        _config = config as PingFactsConfig ?? new PingFactsConfig();

        // [S-1173] The header's label names the host as the user does ([S-2260]).
        host.SetAbout(_config.EffectiveHost);

        // A different host is a different prober, with its own window; a different window size is
        // the same probes counted again. The series is the one the state asks the hub for ([S-2513]).
        var series = SystemInterests.For(_config, SystemDrive.Root).Series[0];

        if (_watch is null || series != _series)
        {
            _watch?.Dispose();
            _series = series;
            _watch = hub.WatchSeries(series, _ => Show());
        }

        Show();
    }

    public override IReadOnlyList<WidgetAction> ContextActions(IDetailHost host)
        => [new WidgetAction(Glyphs.Refresh, Str.Common_Refresh.Text(), host.SampleNow)];

    public override void Refresh() => _watch?.Refresh();

    /// <summary>[S-2427] [S-2428] The statistics over the newest probes the window holds.</summary>
    private void Show()
    {
        if (_host is not { } host || _watch is not { } watch)
        {
            return;
        }

        var window = new PingWindow(_config.EffectiveWindow);

        foreach (var point in watch.History(_config.EffectiveWindow))
        {
            window.Add(point.Answer);
        }

        var stats = window.Stats();
        var status = PingStats.Status(watch.Count > 0, stats.Last is not null);
        host.Present(PingDetailRules.Sheet(Strings.Current, stats, _config.EffectiveHost, status));
    }

    public override void Detach()
    {
        _watch?.Dispose();
        _watch = null;
        _series = null;
        _host = null;
    }
}
