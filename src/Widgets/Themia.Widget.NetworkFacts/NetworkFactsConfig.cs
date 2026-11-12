using System.Text.Json.Serialization;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.SystemData;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Core.Model;

/// <summary>[S-2415] Down and up, the session's totals, the addresses and every adapter.</summary>
public sealed class NetworkFactsConfig : FactsConfig, IReadsSystem
{
    public NetworkFactsConfig()
        : base(NetworkDetailRules.Facts)
    {
    }

    /// <summary>
    /// [S-2417] [S-11020] Whether it asks a public service for this connection's public address: only
    /// while the pick shows it. Never a default: it is a request to somebody else's server, and the user
    /// should choose it.
    /// </summary>
    [JsonIgnore]
    public bool ShowsPublicAddress => Facts.Contains(NetworkDetailRules.PublicAddressKey);

    /// <summary>[S-2513] The addresses and the throughput; [S-2417] the public lookup only while the setting is on: it asks a third party.</summary>
    public SystemInterests SystemWants(string systemRoot) => ShowsPublicAddress
        ? SystemInterests.Read(SystemSource.LocalAddresses, SystemSource.PublicAddress, SystemSource.Network)
        : SystemInterests.Read(SystemSource.LocalAddresses, SystemSource.Network);

    public override WidgetConfig Clone()
    {
        var copy = new NetworkFactsConfig();
        CopyTo(copy);
        return copy;
    }
}
