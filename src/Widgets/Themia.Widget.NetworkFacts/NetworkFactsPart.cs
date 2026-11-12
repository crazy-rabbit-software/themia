using Themia.App.Services.SystemData;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Widget.NetworkFacts;

namespace Themia.App.Widgets.Details;

/// <summary>
/// [S-2415]..[S-2419] Download and upload, this session's totals, the addresses and every adapter,
/// from the network meter's own hub feed, so a meter and a details widget on one desktop share one
/// read. The addresses are read before the throughput on a tick ([S-2501]), so the throughput's read
/// presents a sheet of one tick.
/// </summary>
internal sealed class NetworkFactsPart : DetailPart
{
    /// <inheritdoc />
    public override CardOpen? Opens(WidgetConfig config) => NetworkFactsModule.Opens;

    private readonly WatchSlot<ThroughputSample?> _throughput = new();
    private readonly WatchSlot<IReadOnlyDictionary<string, string>> _addresses = new();
    private readonly WatchSlot<PublicAddressReading> _public = new();
    private IDetailHost? _host;

    public override WidgetId Kind => new WidgetId("networkFacts");


    /// <summary>[S-11020] Fact rows alone, as the config picks them.</summary>
    public override DetailAtom Atom => DetailAtom.Facts;

    public override IReadOnlyList<FactOption> FactOptions => NetworkDetailRules.Facts;

    public override void Watch(ISystemFeeds hub, WidgetConfig config, IDetailHost host)
    {
        _host = host;
        var network = config as NetworkFactsConfig ?? new NetworkFactsConfig();
        _addresses.Hold(hub, Sources.LocalAddresses, static _ => { });

        // [S-2417] The public lookup runs only while the setting is on.
        if (network.ShowsPublicAddress)
        {
            _public.Hold(hub, Sources.PublicAddress, static _ => { });
        }
        else
        {
            _public.Release();
        }

        _throughput.Hold(hub, Sources.Network, _ => Show());
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

        host.Present(NetworkDetailRules.Sheet(Strings.Current, _throughput.Value, addresses, publicAddress));
    }

    public override void Detach()
    {
        _throughput.Release();
        _public.Release();
        _addresses.Release();
        _host = null;
    }
}
