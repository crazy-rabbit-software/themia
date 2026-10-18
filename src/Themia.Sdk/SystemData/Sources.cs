using Themia.Core.Model;
using Themia.Core.SystemData;
using Themia.Core.Widgets;

namespace Themia.App.Services.SystemData;

/// <summary>[S-2408] [S-2409] The machine-wide counts and memory figures, and the page files.</summary>
public readonly record struct PerformanceSnapshot(PerformanceFigures? Figures, PagefileUsage? Pagefile);

/// <summary>[S-1161] [S-1162] The volumes as of the last walk, and the I/O counters' rates (null without counters).</summary>
public sealed record DiskSnapshot(IReadOnlyList<DiskVolume> Volumes, DiskActivity? Activity);

/// <summary>
/// [S-6310] [S-6318] Each logical processor's load, 0..1, in processor order (null for a core with
/// no reading), and every core's history as the source keeps it: empty unless a Core graphs widget
/// wants it ([S-2513]). Read on the UI thread only, as every hub snapshot is.
/// </summary>
public sealed record CoreLoads(IReadOnlyList<double?> Loads, CoreHistory History);

/// <summary>[S-2500] Every source the hub reads, by the snapshot it hands back.</summary>
public static class Sources
{
    public static readonly SourceId<MeterReading> Cpu = new(SystemSource.Cpu);
    public static readonly SourceId<MemorySnapshot?> Memory = new(SystemSource.Memory);
    public static readonly SourceId<PerformanceSnapshot> Performance = new(SystemSource.Performance);
    public static readonly SourceId<CpuFigures> Processor = new(SystemSource.Processor);

    /// <summary>[S-6310] Each logical processor's load and, for the core graphs, their history ([S-6318]).</summary>
    public static readonly SourceId<CoreLoads> CpuCores = new(SystemSource.CpuCores);

    public static readonly SourceId<IReadOnlyList<ProcessSample>?> Processes = new(SystemSource.Processes);
    public static readonly SourceId<DiskSnapshot> DiskActivity = new(SystemSource.DiskActivity);
    public static readonly SourceId<IReadOnlyDictionary<string, string>> LocalAddresses = new(SystemSource.LocalAddresses);
    public static readonly SourceId<PublicAddressReading> PublicAddress = new(SystemSource.PublicAddress);
    public static readonly SourceId<ThroughputSample?> Network = new(SystemSource.Network);
    public static readonly SourceId<BatteryPower?> Battery = new(SystemSource.Battery);
    public static readonly SourceId<BatteryPack?> BatteryPack = new(SystemSource.BatteryPack);
    public static readonly SourceId<GpuSnapshot> Gpu = new(SystemSource.Gpu);

    /// <summary>[S-2508] Probes of one host; the host is the feed's target.</summary>
    public static readonly SourceId<PingOutcome> Ping = new(SystemSource.Ping);

    /// <summary>The disk series of every drive together; any other disk instance is a drive root.</summary>
    public const string AllDrives = SystemInterests.AllDrives;

    /// <summary>[S-1124] The lowest top the network chart may have: 1 KB/s, so idle chatter stays a flat line (the original's floor).</summary>
    public const double NetworkChartFloor = 1024;
}
