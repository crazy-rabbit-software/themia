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
}

/// <summary>
/// [S-2502] One feed of the hub: a source, the thing it is aimed at (a ping host; empty for every
/// other source) and the cadence it is read at. Every widget asking for the same key shares one feed,
/// one read per due tick and one history.
/// </summary>
/// <param name="Source">The source.</param>
/// <param name="Target">What the source reads, normalised (a host, lower case); empty when it reads the machine.</param>
/// <param name="IntervalSeconds">The cadence, whole seconds, at least one (<see cref="HubCadence.Seconds"/>).</param>
public readonly record struct FeedKey(SystemSource Source, string Target, int IntervalSeconds)
{
    /// <summary>The cadence as a span.</summary>
    public TimeSpan Interval => TimeSpan.FromSeconds(IntervalSeconds);

    /// <summary>
    /// The key for a source, target and interval: the target trimmed and lower-cased (a host name is
    /// not case-sensitive, so "Example.com" and "example.com " are one prober), the interval in whole
    /// seconds (<see cref="HubCadence.Seconds"/>).
    /// </summary>
    public static FeedKey Create(SystemSource source, TimeSpan interval, string? target = null)
        => new(source, Normalise(target), HubCadence.Seconds(interval));

    /// <summary>A target as keys compare it.</summary>
    public static string Normalise(string? target)
        => string.IsNullOrWhiteSpace(target) ? string.Empty : target.Trim().ToLowerInvariant();

    public override string ToString()
        => Target.Length == 0 ? $"{Source}@{IntervalSeconds}s" : $"{Source}({Target})@{IntervalSeconds}s";
}
