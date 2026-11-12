using Themia.Core.SystemData;

namespace Themia.Core.Model;

/// <summary>
/// [S-1190] Throughput across the active adapters: the reading alone, download over upload. Its
/// history is the Network history atom's, its adapters the Network adapters atom's.
/// </summary>
public sealed class NetworkConfig : ReadingConfig, IReadsSystem
{
    /// <summary>[S-2513] The throughput's series, the history's too.</summary>
    public SystemInterests SystemWants(string systemRoot) => SystemInterests.Chart(SystemSource.Network);

    public override WidgetConfig Clone() => new NetworkConfig();
}
