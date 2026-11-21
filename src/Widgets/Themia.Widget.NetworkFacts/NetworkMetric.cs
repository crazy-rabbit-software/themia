using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Themia.App.Services;
using Themia.App.Services.SystemData;
using Themia.App.Widgets;
using Themia.App.Widgets.Details;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.SystemData;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;

namespace Themia.Widget.NetworkFacts;

/// <summary>
/// [S-14640] One kind of the network facts family: one of the connection's facts as a single metric
/// (<see cref="MetricModule"/>), at home in its family (<see cref="AddFolderId.NetworkFacts"/>). SPEC.md beside this file.
/// </summary>
public abstract class NetworkMetricModule(string key)
    : MetricModule(NetworkKinds.Of(key), NetworkFactsJsonContext.Default, AddFolderId.NetworkFacts, NetworkKinds.Rank(key))
{
    /// <summary>[S-2417] The public address asks a third party: whom, and how often, is said on its page.</summary>
    protected override void SourceLines(WidgetMenuLines menu, WidgetConfig config)
    {
        ArgumentNullException.ThrowIfNull(menu);
        if (config is NetworkMetricConfig { ShowsPublicAddress: true })
        {
            menu.Lines.Add(WidgetMenuLines.Note(NetworkFactsStr.Details_Settings_Network_Public_Desc.Format(("minutes", (long)NetworkDetailRules.PublicAddressLifetime.TotalMinutes))));
        }
    }

    public override FrameworkElement CreateView(IWidgetHost host)
        => new SystemMetricView(host, Kind, new NetworkMetricPart(Kind.Key));
}

/// <summary>[S-14640] What a network fact card remembers: its trend; its fact is its kind's.</summary>
public abstract class NetworkMetricConfig(string key) : MetricConfig, IReadsSystem
{
    /// <summary>The fact its kind shows.</summary>
    [JsonIgnore]
    public string Key { get; } = key;

    /// <inheritdoc />
    protected override MetricKind Metric => NetworkKinds.Of(Key);

    /// <summary>
    /// [S-2417] Whether it asks a public service for this connection's public address: only the public address's own card
    /// does, a widget the user adds for it (it is a request to somebody else's server).
    /// </summary>
    [JsonIgnore]
    public bool ShowsPublicAddress => Key == NetworkDetailRules.PublicAddressKey;

    /// <summary>
    /// [S-2513] The addresses and the throughput; [S-2417] the public lookup only for the public address's card;
    /// [S-14603] the throughput's series while a rate's line is drawn.
    /// </summary>
    public SystemInterests SystemWants(string systemRoot)
    {
        SystemSource[] feeds = ShowsPublicAddress
            ? [SystemSource.LocalAddresses, SystemSource.PublicAddress, SystemSource.Network]
            : [SystemSource.LocalAddresses, SystemSource.Network];
        var read = SystemInterests.Read(feeds);
        return MetricRules.Effective(this) == MetricTrend.Line ? read with { Series = SystemInterests.Chart(SystemSource.Network).Series } : read;
    }

    public override WidgetConfig Clone()
    {
        var copy = (NetworkMetricConfig)NetworkKinds.Of(Key).Create();
        CopyTo(copy);
        return copy;
    }
}

/// <summary>
/// [S-2415]..[S-2419] [S-14640] One of the connection's facts from the network meter's own hub feed (one read for a meter
/// and every card); the addresses are read before the throughput on a tick ([S-2501]), so the throughput's read is a
/// new reading of one tick. A rate's line is the throughput's series, download first, upload second ([S-14603]).
/// </summary>
internal sealed class NetworkMetricPart(string key) : SystemMetricPart
{
    private readonly WatchSlot<ThroughputSample?> _throughput = new();
    private readonly WatchSlot<IReadOnlyDictionary<string, string>> _addresses = new();
    private readonly WatchSlot<PublicAddressReading> _public = new();
    private ISeriesWatch? _series;
    private WidgetConfig? _config;
    private ISystemMetricHost? _host;

    public override void Watch(ISystemFeeds hub, WidgetConfig config, ISystemMetricHost host)
    {
        _host = host;
        _config = config;
        var own = config as NetworkMetricConfig;
        _addresses.Hold(hub, Sources.LocalAddresses, static _ => { });

        // [S-2417] The public lookup runs only for the public address's card.
        if (own is { ShowsPublicAddress: true })
        {
            _public.Hold(hub, Sources.PublicAddress, _ => Show());
        }
        else
        {
            _public.Release();
        }

        var series = own is not null ? SystemInterests.For(own, SystemDrive.Root).Series.FirstOrDefault() : default;
        if (series == default)
        {
            _series?.Dispose();
            _series = null;
        }
        else if (_series is null || _series.Interest != series)
        {
            _series?.Dispose();
            _series = hub.WatchSeries(series, _ => Show());
        }

        _throughput.Hold(hub, Sources.Network, _ =>
        {
            NextReading();
            Show();
        });
        Show();
    }

    private void Show()
    {
        if (_host is not { } host || !_throughput.HasValue)
        {
            return;
        }

        var addresses = _addresses.HasValue ? _addresses.Value : new Dictionary<string, string>();
        var publicAddress = _public.Current is null
            ? new PublicAddressReading(PublicAddressState.Off)
            : _public.HasValue ? _public.Value : new PublicAddressReading(PublicAddressState.Checking);

        var shown = MetricRules.Pick(NetworkDetailRules.Sheet(Strings.Current, _throughput.Value, addresses, publicAddress), key) with { Reading = LastReading };
        if (_series is { } series)
        {
            var upload = key == "upload";
            // [S-14703] The throughput's history in bytes a second, written as a rate.
            shown = _config is { } config ? Graphed(shown, series, config, p => upload ? p.Second : p.First, Formatting.Rate) : shown;
        }

        host.Present(shown);
    }

    public override void Detach()
    {
        _series?.Dispose();
        _series = null;
        _throughput.Release();
        _public.Release();
        _addresses.Release();
        _host = null;
    }
}

/// <summary>[S-6903] The family's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(NetworkLocalAddressConfig))]
[JsonSerializable(typeof(NetworkReceivedConfig))]
[JsonSerializable(typeof(NetworkSentConfig))]
[JsonSerializable(typeof(NetworkLinkSpeedConfig))]
[JsonSerializable(typeof(NetworkPublicAddressConfig))]
[JsonSerializable(typeof(NetworkDownloadConfig))]
[JsonSerializable(typeof(NetworkUploadConfig))]
internal sealed partial class NetworkFactsJsonContext : JsonSerializerContext;
