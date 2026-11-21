using System.Text.Json.Serialization;
using Themia.Core.SystemData;
using Themia.Core.Widgets;

namespace Themia.Core.Model;

/// <summary>
/// [S-1190] Throughput across the active adapters: the reading alone, download over upload; [S-14960] or one adapter's,
/// its Adapter. Its history is the Network history atom's, its adapters the Network adapters atom's.
/// </summary>
public sealed class NetworkConfig : ReadingConfig, IReadsSystem, IHoldsAdapter
{
    /// <summary>[S-14960] The adapter's id; null for Automatic (every counted adapter together).</summary>
    public string? Adapter { get; set; }

    /// <summary>[S-14960] Its name as last seen, for when it is not connected.</summary>
    public string? AdapterName { get; set; }

    /// <summary>[S-2513] The throughput's series, the history's too; [S-14963] the chosen adapter's own.</summary>
    public SystemInterests SystemWants(string systemRoot) => SystemInterests.Chart(SystemSource.Network, NetworkAdapterRules.Normalise(Adapter) ?? string.Empty);

    public override WidgetConfig Clone() => new NetworkConfig { Adapter = Adapter, AdapterName = AdapterName };

    public override IReadOnlyList<string> Repair() => [.. base.Repair(), .. NetworkAdapterRules.Repair(this, Kind.Value)];
}
