using Themia.App.Services;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Widgets.Details;

/// <summary>
/// [S-2415]..[S-2419] Download and upload, this session's totals, the addresses and every adapter,
/// from the network meter's own shared reader (<see cref="NetworkThroughput"/>), so a meter and a
/// details widget on one desktop share one sample.
/// </summary>
internal sealed class NetworkDetailPart : DetailPart
{
    private NetworkDetailsConfig _config = new();
    private IDisposable? _addresses;
    private IDisposable? _public;
    private bool _attached;

    public override WidgetKind Kind => WidgetKind.NetworkDetails;

    public override double HeroCharacters => NetworkDetailRules.HeroCharacters;

    /// <summary>The name and the rates on one line, the address under them.</summary>
    public override double ListRowHeight => Fluent.RowHeight + Fluent.Space1;

    public override bool ApplyConfig(WidgetConfig config, IDetailHost host)
    {
        var next = config as NetworkDetailsConfig ?? new NetworkDetailsConfig();
        var changed = next.ShowPublicAddress != _config.ShowPublicAddress;
        _config = next;

        if (_attached)
        {
            HoldPublic();
        }

        return changed;
    }

    public override void Attach()
    {
        _attached = true;
        _addresses ??= LocalAddresses.Acquire();
        HoldPublic();
    }

    /// <summary>[S-2417] The public lookup runs only while the setting is on.</summary>
    private void HoldPublic()
    {
        if (_config.ShowPublicAddress)
        {
            _public ??= PublicAddress.Acquire();
        }
        else
        {
            _public?.Dispose();
            _public = null;
        }
    }

    public override void Sample(IDetailHost host)
    {
        var publicAddress = _public is null ? new PublicAddressReading(PublicAddressState.Off) : PublicAddress.Current();
        host.Present(NetworkDetailRules.Sheet(Strings.Current, NetworkThroughput.Read(), LocalAddresses.Current(), publicAddress));
    }

    public override void Detach()
    {
        _attached = false;
        _public?.Dispose();
        _public = null;
        _addresses?.Dispose();
        _addresses = null;
    }
}
