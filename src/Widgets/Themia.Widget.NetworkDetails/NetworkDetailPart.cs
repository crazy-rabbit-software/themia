using Themia.App.Services.SystemData;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Widget.NetworkDetails;

namespace Themia.App.Widgets.Details;

/// <summary>
/// [S-2415]..[S-2419] Download and upload, this session's totals, the addresses and every adapter,
/// from the network meter's own hub feed, so a meter and a details widget on one desktop share one
/// read. The addresses are read before the throughput on a tick ([S-2501]), so the throughput's read
/// presents a sheet of one tick.
/// </summary>
internal sealed class NetworkDetailPart : DetailPart
{
    /// <inheritdoc />
    public override CardOpen? Opens(WidgetConfig config) => NetworkDetailsModule.Opens;

    private readonly WatchSlot<ThroughputSample?> _throughput = new();
    private readonly WatchSlot<IReadOnlyDictionary<string, string>> _addresses = new();
    private readonly WatchSlot<PublicAddressReading> _public = new();
    private IDetailHost? _host;

    public override WidgetId Kind => new WidgetId("networkDetails");

    public override string HeroWorst => NetworkDetailRules.HeroWorst;

    public override void Watch(ISystemFeeds hub, WidgetConfig config, IDetailHost host)
    {
        _host = host;
        var network = config as NetworkDetailsConfig ?? new NetworkDetailsConfig();
        _addresses.Hold(hub, Sources.LocalAddresses, static _ => { });

        // [S-2417] The public lookup runs only while the setting is on.
        if (network.ShowPublicAddress)
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

#if DEBUG
    /// <summary>[S-1917] Two fixed adapters, so the renders are one picture on every machine.</summary>
    public override DetailSheet? SampleSheet()
    {
        var wifi = new InterfaceRate("wifi", "Wi-Fi", 2.4 * 1024 * 1024, 320 * 1024, 812L << 20, 96L << 20, AdapterMedium.Wireless, LinkBitsPerSecond: 866_000_000);
        var wired = new InterfaceRate("lan", "Ethernet", 12 * 1024, 3 * 1024, 41L << 20, 9L << 20, AdapterMedium.Ethernet, LinkBitsPerSecond: 1_000_000_000);
        var sample = new ThroughputSample(wifi.Down + wired.Down, wifi.Up + wired.Up, [wifi, wired], 853L << 20, 105L << 20);
        var addresses = new Dictionary<string, string> { ["wifi"] = "192.168.1.174", ["lan"] = "10.0.0.12" };
        return NetworkDetailRules.Sheet(Strings.Current, sample, addresses, new PublicAddressReading(PublicAddressState.Off));
    }
#endif

    public override void Detach()
    {
        _throughput.Release();
        _public.Release();
        _addresses.Release();
        _host = null;
    }
}
