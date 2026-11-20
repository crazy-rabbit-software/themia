using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Widget.NetworkFacts;

[assembly: ThemiaWidgetFamily]
[assembly: ThemiaWidget("networkLocalAddress", typeof(NetworkLocalAddressModule))]
[assembly: ThemiaWidget("networkReceived", typeof(NetworkReceivedModule))]
[assembly: ThemiaWidget("networkSent", typeof(NetworkSentModule))]
[assembly: ThemiaWidget("networkLinkSpeed", typeof(NetworkLinkSpeedModule))]
[assembly: ThemiaWidget("networkPublicAddress", typeof(NetworkPublicAddressModule))]
[assembly: ThemiaWidget("networkDownload", typeof(NetworkDownloadModule))]
[assembly: ThemiaWidget("networkUpload", typeof(NetworkUploadModule))]

namespace Themia.Widget.NetworkFacts;

/// <summary>
/// [S-14640] [S-14604] The family's kinds: the connection's facts, one widget a fact (owner, 2026-11-20: "split all the other widgets
/// that are alike to the weather and air facts into individual metrics too"), in the removed card's order: each one's
/// id, the metric's key, its glyph, its history (its trends), its row's name, its name and description on the add page,
/// its search words and its config; each stands in for its row of a placed <c>networkFacts</c> card ([S-14606]).
/// </summary>
internal static class NetworkKinds
{
    public static IReadOnlyList<MetricKind> All { get; } = MetricKind.SplitOf("networkFacts",
    [
        new("networkLocalAddress", "localAddress", Glyphs.Network, MetricHistory.None, Str.Details_Network_LocalAddress.Local(), NetworkFactsStr.NetworkFacts_NetworkLocalAddress_Label.Local(), NetworkFactsStr.NetworkFacts_NetworkLocalAddress_Desc.Local(),
            ["ip address", "local address", "ipv4", "ipv6", "lan"], () => new NetworkLocalAddressConfig()),
        new("networkReceived", "received", Glyphs.Download, MetricHistory.None, Str.Details_Network_Received.Local(), NetworkFactsStr.NetworkFacts_NetworkReceived_Label.Local(), NetworkFactsStr.NetworkFacts_NetworkReceived_Desc.Local(),
            ["received", "downloaded", "traffic", "data"], () => new NetworkReceivedConfig()),
        new("networkSent", "sent", Glyphs.Upload, MetricHistory.None, Str.Details_Network_Sent.Local(), NetworkFactsStr.NetworkFacts_NetworkSent_Label.Local(), NetworkFactsStr.NetworkFacts_NetworkSent_Desc.Local(),
            ["sent", "uploaded", "traffic", "data"], () => new NetworkSentConfig()),
        new("networkLinkSpeed", "linkSpeed", Glyphs.Signal, MetricHistory.None, Str.Details_Network_LinkSpeed.Local(), NetworkFactsStr.NetworkFacts_NetworkLinkSpeed_Label.Local(), NetworkFactsStr.NetworkFacts_NetworkLinkSpeed_Desc.Local(),
            ["link speed", "connection speed", "mbps", "gbps", "wifi", "ethernet"], () => new NetworkLinkSpeedConfig()),
        new("networkPublicAddress", "publicAddress", Glyphs.Globe, MetricHistory.None, Str.Details_Network_PublicAddress.Local(), NetworkFactsStr.NetworkFacts_NetworkPublicAddress_Label.Local(), NetworkFactsStr.NetworkFacts_NetworkPublicAddress_Desc.Local(),
            ["public ip", "public address", "external ip", "wan"], () => new NetworkPublicAddressConfig()),
        new("networkDownload", "download", Glyphs.Download, MetricHistory.Series, Str.Details_Network_Download.Local(), NetworkFactsStr.NetworkFacts_NetworkDownload_Label.Local(), NetworkFactsStr.NetworkFacts_NetworkDownload_Desc.Local(),
            ["download", "download speed", "bandwidth", "throughput", "network"], () => new NetworkDownloadConfig()),
        new("networkUpload", "upload", Glyphs.Upload, MetricHistory.Series, Str.Details_Network_Upload.Local(), NetworkFactsStr.NetworkFacts_NetworkUpload_Label.Local(), NetworkFactsStr.NetworkFacts_NetworkUpload_Desc.Local(),
            ["upload", "upload speed", "bandwidth", "throughput", "network"], () => new NetworkUploadConfig()),
    ], ["localAddress", "received", "sent", "linkSpeed"]);

    /// <summary>The kind showing the metric <paramref name="key"/>.</summary>
    public static MetricKind Of(string key) => All.Single(k => k.Key == key);

    /// <summary>Its place in the family's order: its add-page rank.</summary>
    public static int Rank(string key) => All.ToList().FindIndex(k => k.Key == key);
}

// [S-14604] Each kind is its module and its config (a config's type is how it knows its id, [S-6902]); everything
// else is the family's (NetworkMetricModule, NetworkMetricConfig, the view) over the single metric's base.

public sealed class NetworkLocalAddressModule() : NetworkMetricModule("localAddress");

public sealed class NetworkReceivedModule() : NetworkMetricModule("received");

public sealed class NetworkSentModule() : NetworkMetricModule("sent");

public sealed class NetworkLinkSpeedModule() : NetworkMetricModule("linkSpeed");

public sealed class NetworkPublicAddressModule() : NetworkMetricModule("publicAddress");

public sealed class NetworkDownloadModule() : NetworkMetricModule("download");

public sealed class NetworkUploadModule() : NetworkMetricModule("upload");

public sealed class NetworkLocalAddressConfig() : NetworkMetricConfig("localAddress");

public sealed class NetworkReceivedConfig() : NetworkMetricConfig("received");

public sealed class NetworkSentConfig() : NetworkMetricConfig("sent");

public sealed class NetworkLinkSpeedConfig() : NetworkMetricConfig("linkSpeed");

public sealed class NetworkPublicAddressConfig() : NetworkMetricConfig("publicAddress");

public sealed class NetworkDownloadConfig() : NetworkMetricConfig("download");

public sealed class NetworkUploadConfig() : NetworkMetricConfig("upload");
