using System.Text.Json.Serialization;

namespace Themia.Core.Model;

/// <summary>
/// [S-1190] Throughput across the active adapters: download as the reading, download and upload as
/// the history, nothing else ([S-1762]).
/// </summary>
public sealed class NetworkConfig : GaugeConfig
{
    public NetworkConfig()
        : base(graphSpanSeconds: 60)
    {
    }

    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.Network;

    public override WidgetConfig Clone()
    {
        var copy = new NetworkConfig();
        CopyTo(copy);
        return copy;
    }
}
