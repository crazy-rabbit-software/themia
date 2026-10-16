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
    DiskSpace,
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

/// <summary>
/// [S-2502] One feed of the hub: a source and the thing it is aimed at (a ping host; empty for every
/// other source). Every widget asking for the same key shares one feed, one read per due tick and one
/// history. The cadence is not part of it: there is one ([S-2503]).
/// </summary>
/// <param name="Source">The source.</param>
/// <param name="Target">What the source reads, normalised (a host, lower case); empty when it reads the machine.</param>
public readonly record struct FeedKey(SystemSource Source, string Target)
{
    /// <summary>
    /// The key for a source and target: the target trimmed and lower-cased (a host name is not
    /// case-sensitive, so "Example.com" and "example.com " are one prober).
    /// </summary>
    public static FeedKey Create(SystemSource source, string? target = null) => new(source, Normalise(target));

    /// <summary>A target as keys compare it.</summary>
    public static string Normalise(string? target)
        => string.IsNullOrWhiteSpace(target) ? string.Empty : target.Trim().ToLowerInvariant();

    /// <summary>How far apart this feed's reads (and history points) are ([S-2512]).</summary>
    public int PeriodSeconds => SystemCadence.PeriodSeconds(Source);

    public override string ToString() => Target.Length == 0 ? Source.ToString() : $"{Source}({Target})";
}
