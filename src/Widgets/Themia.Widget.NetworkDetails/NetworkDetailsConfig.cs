using System.Text.Json.Serialization;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.SystemData;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Core.Model;

/// <summary>[S-2415] Down and up, the session's totals, the addresses and every adapter.</summary>
public sealed class NetworkDetailsConfig : DetailConfig, IReadsSystem
{
    /// <summary>
    /// [S-2417] Ask a public service for this connection's public address. Off by default: it is a
    /// request to somebody else's server, and the user should choose it.
    /// </summary>
    public bool ShowPublicAddress { get; set; }

    /// <summary>[S-2513] The addresses and the throughput; [S-2417] the public lookup only while the setting is on: it asks a third party.</summary>
    public SystemInterests SystemWants(string systemRoot) => ShowPublicAddress
        ? SystemInterests.Read(SystemSource.LocalAddresses, SystemSource.PublicAddress, SystemSource.Network)
        : SystemInterests.Read(SystemSource.LocalAddresses, SystemSource.Network);

    public override WidgetConfig Clone()
    {
        var copy = new NetworkDetailsConfig { ShowPublicAddress = ShowPublicAddress };
        return copy;
    }
}
