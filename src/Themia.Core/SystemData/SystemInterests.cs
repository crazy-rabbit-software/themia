using System.Globalization;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.Core.SystemData;

/// <summary>[S-2504] One charted series a widget shows: a feed and an instance in it (a drive root or <see cref="SystemInterests.AllDrives"/>, an adapter's position; empty for the feed's only one).</summary>
public readonly record struct SeriesInterest(FeedKey Key, string Instance);

/// <summary>
/// [S-2513] What the system hub keeps reading: the feeds and charted series of every system widget in
/// every view, shown or not (as <see cref="Sync.SyncInterests"/> does for accounts and the web). A
/// view switched to finds its charts with their history and its details already read; nothing
/// starts when it is first looked at. The cost is bounded by what the views hold, and the hub's
/// ticker stops with the desktop. A widget's view watches through the same rule (<see cref="For"/>).
/// </summary>
public sealed record SystemInterests(IReadOnlyList<FeedKey> Feeds, IReadOnlyList<SeriesInterest> Series)
{
    /// <summary>The disk series of every drive together; any other disk instance is a drive root.</summary>
    public const string AllDrives = "*";

    public static SystemInterests None { get; } = new([], []);

    /// <summary>
    /// Every view's widgets, deduplicated and in read order, so two states that want the same thing
    /// compare equal (<see cref="SameAs"/>) and the hub hears only of real changes.
    /// </summary>
    /// <param name="systemRoot">The drive Windows runs from (<c>C:\</c>): a disk meter with no drive chosen reads it.</param>
    public static SystemInterests From(AppState state, string systemRoot)
    {
        ArgumentNullException.ThrowIfNull(state);

        var feeds = new HashSet<FeedKey>();
        var series = new HashSet<SeriesInterest>();

        foreach (var widget in state.Views.SelectMany(v => v.Widgets))
        {
            if (widget.Config is { } config)
            {
                var wants = For(config, systemRoot);
                feeds.UnionWith(wants.Feeds);
                series.UnionWith(wants.Series);
            }
        }

        return new SystemInterests(
            [.. feeds.OrderBy(k => k.Source).ThenBy(k => k.Target, StringComparer.Ordinal)],
            [.. series.OrderBy(s => s.Key.Source).ThenBy(s => s.Key.Target, StringComparer.Ordinal).ThenBy(s => s.Instance, StringComparer.Ordinal)]);
    }

    /// <summary>
    /// [S-2513] What one widget reads: every feed its view watches and every series it charts (a
    /// series' feed is in <see cref="Feeds"/> too). Nothing for a widget that is not a system one.
    /// </summary>
    public static SystemInterests For(WidgetConfig config, string systemRoot)
    {
        ArgumentNullException.ThrowIfNull(config);

        switch (config)
        {
            case CpuConfig { ShowsCores: true }:
                // [S-6310] The core styles read each processor too, beside the total and its history.
                return new SystemInterests([FeedKey.Create(SystemSource.Cpu), FeedKey.Create(SystemSource.CpuCores)], [new SeriesInterest(FeedKey.Create(SystemSource.Cpu), string.Empty)]);
            case CpuConfig:
                return Chart(SystemSource.Cpu);
            case MemoryConfig:
                return Chart(SystemSource.Memory);
            case NetworkConfig:
                return Chart(SystemSource.Network);
            case BatteryConfig:
                return Chart(SystemSource.Battery);
            case DiskConfig disk:
                return Chart(SystemSource.DiskActivity, DiskInstance(disk, systemRoot));
            case PingConfig ping:
                return Chart(FeedKey.Create(SystemSource.Ping, ping.EffectiveHost));
            case GpuConfig gpu:
                var key = FeedKey.Create(SystemSource.Gpu);
                return new SystemInterests(
                    [key],
                    [.. GpuRules.ResolveAdapters(gpu.Adapters).Select(i => new SeriesInterest(key, i.ToString(CultureInfo.InvariantCulture)))]);
            case CpuDetailsConfig:
                return Read(SystemSource.Cpu, SystemSource.Performance, SystemSource.Processor);
            case MemoryDetailsConfig:
                return Read(SystemSource.Performance);
            case TopProcessesConfig:
                return Read(SystemSource.Processes);
            case NetworkDetailsConfig network:
                // [S-2417] The public lookup only while the setting is on: it asks a third party.
                return network.ShowPublicAddress
                    ? Read(SystemSource.LocalAddresses, SystemSource.PublicAddress, SystemSource.Network)
                    : Read(SystemSource.LocalAddresses, SystemSource.Network);
            case DiskSpaceConfig:
                return Read(SystemSource.DiskSpace);
            case BatteryDetailsConfig:
                return Read(SystemSource.Battery, SystemSource.BatteryPack);
            case PingDetailsConfig pingDetails:
                // The statistics' window is the prober's history: a series like the meter's.
                return Chart(FeedKey.Create(SystemSource.Ping, pingDetails.EffectiveHost));
            case GpuDetailsConfig:
                return Read(SystemSource.Gpu);
            default:
                return None;
        }
    }

    /// <summary>[S-1160] [S-1822] The disk meter's series: every drive together, or its drive (the system drive when none is chosen).</summary>
    public static string DiskInstance(DiskConfig disk, string systemRoot)
    {
        ArgumentNullException.ThrowIfNull(disk);
        return disk.Scope == DiskScope.AllDrives ? AllDrives : DiskRules.Root(disk.Drive) ?? systemRoot;
    }

    /// <summary>Whether two sets want the same feeds and series.</summary>
    public bool SameAs(SystemInterests other)
        => other is not null && Feeds.SequenceEqual(other.Feeds) && Series.SequenceEqual(other.Series);

    private static SystemInterests Chart(SystemSource source, string instance = "") => Chart(FeedKey.Create(source), instance);

    private static SystemInterests Chart(FeedKey key, string instance = "") => new([key], [new SeriesInterest(key, instance)]);

    private static SystemInterests Read(params SystemSource[] sources) => new([.. sources.Select(s => FeedKey.Create(s))], []);
}
