namespace Themia.Core.SystemData;

/// <summary>
/// [S-2501] Where a system figure comes from. The order is the order the hub reads them in on one
/// tick, so a widget combining several (CPU details: load, counts, clock) sees one tick's figures:
/// whatever another source is combined with comes before it.
/// </summary>
public enum SystemSource
{
    Cpu,
    Memory,
    Performance,
    Processor,
    Processes,
    DiskActivity,
    LocalAddresses,
    PublicAddress,
    Network,
    Battery,
    BatteryPack,
    Gpu,
    Ping,

    /// <summary>[S-6310] Each logical processor's load (the CPU meter's core styles).</summary>
    CpuCores,
}
