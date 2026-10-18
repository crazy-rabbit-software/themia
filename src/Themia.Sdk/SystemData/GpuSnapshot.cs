using Themia.Core.Widgets;

namespace Themia.App.Services;

/// <summary>One graphics adapter as DXGI lists it.</summary>
/// <param name="Index">Its place in DXGI's list: the persisted selection.</param>
/// <param name="Luid">The token its performance counters are filed under.</param>
/// <param name="LuidLow">[S-2432] The LUID's low part, for opening the adapter in the graphics kernel.</param>
/// <param name="LuidHigh">[S-2432] The LUID's high part.</param>
public sealed record GpuAdapter(int Index, string Name, string Luid, ulong DedicatedBytes, ulong SharedBytes, uint LuidLow = 0, int LuidHigh = 0);

/// <summary>One sample of every adapter. Readings line up with <see cref="Adapters"/>.</summary>
/// <param name="HasLoad">
/// False when no adapter has a load figure yet — the first sample after the counters were opened,
/// on a machine with no NVIDIA card. Each reading says so for itself too; the view shows "--" for
/// it rather than a 0% that never happened.
/// </param>
public sealed record GpuSnapshot(IReadOnlyList<GpuAdapter> Adapters, IReadOnlyList<GpuReading> Readings, bool HasLoad)
{
    /// <summary>[S-2430] Each adapter's memory and sensors, in <see cref="Adapters"/> order, for the details widget.</summary>
    public IReadOnlyList<GpuDetail> Details { get; init; } = [];
}
