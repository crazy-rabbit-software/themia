using Themia.Core.Model;

namespace Themia.Core.SystemData;

/// <summary>[S-2504] One charted series a widget shows: a feed and an instance in it (a drive root or <see cref="SystemInterests.AllDrives"/>, an adapter's position; empty for the feed's only one).</summary>
public readonly record struct SeriesInterest(FeedKey Key, string Instance);

/// <summary>
/// [S-2513] What the system hub keeps reading: the feeds and charted series of every system widget in
/// every view, shown or not (as <c>SyncInterests</c> does for accounts and the web). A
/// view switched to finds its charts with their history and its details already read; nothing
/// starts when it is first looked at. The cost is bounded by what the views hold, and the hub's
/// ticker stops with the desktop. A widget's view watches through the same rule (<see cref="For"/>).
/// The host collects every view's (<c>SystemInterests.From</c>, Core's).
/// </summary>
public sealed record SystemInterests(IReadOnlyList<FeedKey> Feeds, IReadOnlyList<SeriesInterest> Series)
{
    /// <summary>The disk series of every drive together; any other disk instance is a drive root.</summary>
    public const string AllDrives = "*";

    public static SystemInterests None { get; } = new([], []);

    /// <summary>
    /// [S-6318] The longest Core graphs span in any view, in seconds (0: no widget shows the style):
    /// how much of every processor's history the hub's <c>CpuCores</c> source keeps
    /// (<see cref="CoreHistory.CapacityFor"/>), never an hour by default.
    /// </summary>
    public int CoreHistorySeconds { get; init; }

    /// <summary>
    /// [S-2513] What one widget reads: every feed its view watches and every series it charts (a
    /// series' feed is in <see cref="Feeds"/> too), as its config says (<see cref="IReadsSystem"/>).
    /// Nothing for a widget that is not a system one.
    /// </summary>
    /// <param name="systemRoot">The drive Windows runs from (<c>C:\</c>): a disk meter with no drive chosen reads it.</param>
    public static SystemInterests For(WidgetConfig config, string systemRoot)
    {
        ArgumentNullException.ThrowIfNull(config);
        return config is IReadsSystem reads ? reads.SystemWants(systemRoot) : None;
    }

    /// <summary>Whether two sets want the same feeds and series.</summary>
    public bool SameAs(SystemInterests other)
        => other is not null && Feeds.SequenceEqual(other.Feeds) && Series.SequenceEqual(other.Series) && CoreHistorySeconds == other.CoreHistorySeconds;

    /// <summary>One charted series of a source (and its feed): a meter's wants.</summary>
    public static SystemInterests Chart(SystemSource source, string instance = "") => Chart(FeedKey.Create(source), instance);

    /// <summary>One charted series of a feed (a ping host's).</summary>
    public static SystemInterests Chart(FeedKey key, string instance = "") => new([key], [new SeriesInterest(key, instance)]);

    /// <summary>The feeds of the sources, nothing charted: a details widget's wants.</summary>
    public static SystemInterests Read(params SystemSource[] sources) => new([.. sources.Select(s => FeedKey.Create(s))], []);
}

/// <summary>
/// [S-2513] What a system widget's config wants the hub to read: the feeds its view watches and the
/// series it charts, so the host's <see cref="SystemInterests.For"/> asks the config rather than
/// switching over widget types.
/// </summary>
public interface IReadsSystem
{
    /// <param name="systemRoot">The drive Windows runs from (<c>C:\</c>).</param>
    SystemInterests SystemWants(string systemRoot);
}
