using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Themia.App.Services;
using Themia.App.Services.SystemData;
using Themia.App.Shell;
using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.App.Widgets.Details;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.SystemData;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;

namespace Themia.Widget.PingFacts;

/// <summary>
/// [S-14650] One kind of the ping facts family: one statistic of a host's latest pings as a single metric
/// (<see cref="MetricModule"/>), at home in its family (<see cref="AddFolderId.PingFacts"/>). SPEC.md beside this file.
/// </summary>
public abstract class PingMetricModule(string key)
    : MetricModule(PingKinds.Of(key), PingFactsJsonContext.Default, AddFolderId.PingFacts, PingKinds.Rank(key))
{
    /// <summary>[S-2427] The windows offered, in probes; one not among them shows as the default.</summary>
    internal static readonly int[] Windows = [10, 30, 60, 120, 300];

    /// <summary>[S-2426] [S-2427] The host ([S-109]), then how many of the latest pings the statistics cover.</summary>
    protected override void SourceLines(WidgetMenuLines menu, WidgetConfig config)
    {
        ArgumentNullException.ThrowIfNull(menu);
        var ping = (PingMetricConfig)config;
        menu.Lines.Add(PingHostLine.Line(menu, ping.Host, value => ping.Host = value));
        var current = Windows.Contains(ping.EffectiveWindow) ? ping.EffectiveWindow : 60;
        menu.Lines.Add(menu.Choice(Glyphs.Hours, PingFactsStr.Details_Settings_Ping_Window_Label.Text(),
            [.. Windows.Select(w => (w, PingFactsStr.Details_Settings_Ping_Window_Probes.Format(("count", LocaleFormat.Integer(w)))))],
            current, value => ping.Window = value));
    }

    public override FrameworkElement CreateView(IWidgetHost host)
        => new SystemMetricView(host, Kind, new PingMetricPart(Kind.Key));
}

/// <summary>[S-2426] [S-14650] What a ping statistic card remembers: its host, its window and its trend; its statistic is its kind's.</summary>
public abstract class PingMetricConfig(string key) : MetricConfig, IReadsSystem, IPingHost
{
    public const int MinWindow = 5;

    public const int MaxWindow = SeriesHistory.MaxPingWindow;

    /// <summary>The statistic its kind shows.</summary>
    [JsonIgnore]
    public string Key { get; } = key;

    /// <inheritdoc />
    protected override MetricKind Metric => PingKinds.Of(Key);

    /// <summary>Host or address to probe. Empty falls back to <see cref="HostAddress.DefaultPingHost"/>.</summary>
    public string Host { get; set; } = HostAddress.DefaultPingHost;

    /// <summary>[S-2427] How many of the latest probes the statistic covers.</summary>
    public int Window { get; set; } = 60;

    [JsonIgnore]
    public string EffectiveHost => string.IsNullOrWhiteSpace(Host) ? HostAddress.DefaultPingHost : Host.Trim();

    [JsonIgnore]
    public int EffectiveWindow => Math.Clamp(Window, MinWindow, MaxWindow);

    /// <summary>[S-2513] The statistics' window is the prober's history: a series like the meter's.</summary>
    public SystemInterests SystemWants(string systemRoot) => SystemInterests.Chart(FeedKey.Create(SystemSource.Ping, EffectiveHost));

    public override WidgetConfig Clone()
    {
        var copy = (PingMetricConfig)PingKinds.Of(Key).Create();
        CopyTo(copy);
        copy.Host = Host;
        copy.Window = Window;
        return copy;
    }

    public override IReadOnlyList<string> Repair()
    {
        var notes = new List<string>(base.Repair());
        Host ??= HostAddress.DefaultPingHost;
        if (EffectiveWindow != Window)
        {
            notes.Add($"{Kind}: window {Window} is outside {MinWindow}..{MaxWindow}; using {EffectiveWindow}");
            Window = EffectiveWindow;
        }

        return notes;
    }
}

/// <summary>
/// [S-2426]..[S-2429] [S-14650] One statistic over the latest probes of one host. [S-2508] The probes are the hub's
/// prober for the host, the one a ping meter on that host charts; the window is the prober's history, so the figure
/// survives a view switch. Each probe back is a new reading; the line is the statistic over the window ending at each of
/// the latest probes ([S-14603]).
/// </summary>
internal sealed class PingMetricPart(string key) : SystemMetricPart
{
    private PingMetricConfig? _config;
    private ISeriesWatch? _watch;
    private SeriesInterest? _series;
    private ISystemMetricHost? _host;

    /// <inheritdoc />
    public override Action? Refresh => () => _watch?.Refresh();

    public override void Watch(ISystemFeeds hub, WidgetConfig config, ISystemMetricHost host)
    {
        _host = host;
        _config = config as PingMetricConfig;
        if (_config is not { } own)
        {
            return;
        }

        // A different host is a different prober, with its own window; a different window is the same probes counted again.
        var series = SystemInterests.For(own, SystemDrive.Root).Series[0];
        if (_watch is null || series != _series)
        {
            _watch?.Dispose();
            _series = series;
            _watch = hub.WatchSeries(series, _ =>
            {
                NextReading();
                Show();
            });
        }

        Show();
    }

    /// <summary>[S-2427] [S-2428] The statistic over the newest probes the window holds, and its line.</summary>
    private void Show()
    {
        if (_host is not { } host || _watch is not { } watch || _config is not { } config)
        {
            return;
        }

        var window = config.EffectiveWindow;
        // [S-14711] The line's points: the span's probes, each the statistic over the window ending there.
        var slots = MetricGraphs.Points(config, watch.Interest.Key.PeriodSeconds);
        var points = watch.History(window + slots - 1);
        var stats = Stats(points, points.Length, window);
        var status = PingStats.Status(watch.Count > 0, stats.Last is not null);
        var shown = MetricRules.Pick(PingDetailRules.Sheet(Strings.Current, stats, status), key) with { Reading = LastReading };

        if (PingKinds.Of(key).History == MetricHistory.Series)
        {
            var first = Math.Max(1, points.Length - slots + 1);
            // [S-14703] Written as the reading is: a share for the loss, milliseconds for the rest.
            shown = shown with
            {
                Series = [.. Enumerable.Range(first, points.Length - first + 1).Select(end => Figure(Stats(points, end, window)))],
                Figure = key == "loss" ? Formatting.Percent : Formatting.Latency,
                Slots = slots,
                Added = watch.Added,
            };
        }

        host.Present(shown);
    }

    /// <summary>[S-2428] The statistics of the <paramref name="window"/> probes ending before <paramref name="end"/>.</summary>
    private static PingWindowStats Stats(SeriesPoint[] points, int end, int window)
    {
        var stats = new PingWindow(window);
        for (var i = Math.Max(0, end - window); i < end; i++)
        {
            stats.Add(points[i].Answer);
        }

        return stats.Stats();
    }

    /// <summary>The kind's statistic as a number, for its line.</summary>
    private double? Figure(PingWindowStats stats) => key switch
    {
        "average" => stats.Average,
        "minimum" => stats.Min,
        "maximum" => stats.Max,
        "jitter" => stats.Jitter,
        "loss" => stats.Loss,
        _ => null,
    };

    public override void Detach()
    {
        _watch?.Dispose();
        _watch = null;
        _series = null;
        _host = null;
    }
}

/// <summary>[S-6903] The family's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(PingAverageConfig))]
[JsonSerializable(typeof(PingMinimumConfig))]
[JsonSerializable(typeof(PingMaximumConfig))]
[JsonSerializable(typeof(PingJitterConfig))]
[JsonSerializable(typeof(PingLossConfig))]
[JsonSerializable(typeof(PingAnsweredConfig))]
internal sealed partial class PingFactsJsonContext : JsonSerializerContext;
