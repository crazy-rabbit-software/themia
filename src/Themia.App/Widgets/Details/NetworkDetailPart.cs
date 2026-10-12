using Themia.App.Services.SystemData;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Widgets.Details;

/// <summary>
/// [S-2415]..[S-2419] Download and upload, this session's totals, the addresses and every adapter,
/// from the network meter's own hub feed, so a meter and a details widget on one desktop share one
/// read. The addresses are read before the throughput on a tick ([S-2501]), so the throughput's read
/// presents a sheet of one tick.
/// </summary>
internal sealed class NetworkDetailPart : DetailPart
{
    private readonly WatchSlot<ThroughputSample?> _throughput = new();
    private readonly WatchSlot<IReadOnlyDictionary<string, string>> _addresses = new();
    private readonly WatchSlot<PublicAddressReading> _public = new();
    private IDetailHost? _host;

    public override WidgetKind Kind => WidgetKind.NetworkDetails;

    public override double HeroCharacters => NetworkDetailRules.HeroCharacters;

    /// <summary>The name and the rates on one line, the address under them.</summary>
    public override double ListRowHeight => Fluent.RowHeight + Fluent.Space1;

    public override void Watch(SystemHub hub, WidgetConfig config, IDetailHost host)
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

    public override void Detach()
    {
        _throughput.Release();
        _public.Release();
        _addresses.Release();
        _host = null;
    }
}
