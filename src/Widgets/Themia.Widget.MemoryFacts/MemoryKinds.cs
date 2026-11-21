using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Widget.MemoryFacts;

[assembly: ThemiaWidgetFamily]
[assembly: ThemiaWidget("memoryInUse", typeof(MemoryInUseModule))]
[assembly: ThemiaWidget("memoryAvailable", typeof(MemoryAvailableModule))]
[assembly: ThemiaWidget("memoryCommitted", typeof(MemoryCommittedModule))]
[assembly: ThemiaWidget("memoryCached", typeof(MemoryCachedModule))]
[assembly: ThemiaWidget("memoryPagedPool", typeof(MemoryPagedPoolModule))]
[assembly: ThemiaWidget("memoryNonPagedPool", typeof(MemoryNonPagedPoolModule))]
[assembly: ThemiaWidget("memoryPageFile", typeof(MemoryPageFileModule))]

namespace Themia.Widget.MemoryFacts;

/// <summary>
/// [S-14630] [S-14604] The family's kinds: the memory's facts, one widget a fact (owner, 2026-11-20: "split all the other widgets
/// that are alike to the weather and air facts into individual metrics too"), in the removed card's order: each one's
/// id, the metric's key, its glyph, its history (its trends), its row's name, its name and description on the add page,
/// its search words and its config.
/// </summary>
internal static class MemoryKinds
{
    public static IReadOnlyList<MetricKind> All { get; } = Graphed(MetricKind.Opening(
    [
        new("memoryInUse", "inUse", Glyphs.Memory, MetricHistory.Series, Str.Details_Memory_InUse.Local(), MemoryFactsStr.MemoryFacts_MemoryInUse_Label.Local(), MemoryFactsStr.MemoryFacts_MemoryInUse_Desc.Local(),
            ["memory", "ram", "in use", "used"], () => new MemoryInUseConfig()),
        new("memoryAvailable", "available", Glyphs.Memory, MetricHistory.Readings, Str.Details_Memory_Available.Local(), MemoryFactsStr.MemoryFacts_MemoryAvailable_Label.Local(), MemoryFactsStr.MemoryFacts_MemoryAvailable_Desc.Local(),
            ["memory", "ram", "available", "free"], () => new MemoryAvailableConfig()),
        new("memoryCommitted", "committed", Glyphs.Memory, MetricHistory.Readings, Str.Details_Memory_Committed.Local(), MemoryFactsStr.MemoryFacts_MemoryCommitted_Label.Local(), MemoryFactsStr.MemoryFacts_MemoryCommitted_Desc.Local(),
            ["memory", "committed", "commit charge", "virtual memory"], () => new MemoryCommittedConfig()),
        new("memoryCached", "cached", Glyphs.Memory, MetricHistory.Readings, Str.Details_Memory_Cached.Local(), MemoryFactsStr.MemoryFacts_MemoryCached_Label.Local(), MemoryFactsStr.MemoryFacts_MemoryCached_Desc.Local(),
            ["memory", "cached", "cache", "standby"], () => new MemoryCachedConfig()),
        new("memoryPagedPool", "pagedPool", Glyphs.Memory, MetricHistory.Readings, Str.Details_Memory_PagedPool.Local(), MemoryFactsStr.MemoryFacts_MemoryPagedPool_Label.Local(), MemoryFactsStr.MemoryFacts_MemoryPagedPool_Desc.Local(),
            ["memory", "paged pool", "kernel"], () => new MemoryPagedPoolConfig()),
        new("memoryNonPagedPool", "nonPagedPool", Glyphs.Memory, MetricHistory.Readings, Str.Details_Memory_NonPagedPool.Local(), MemoryFactsStr.MemoryFacts_MemoryNonPagedPool_Label.Local(), MemoryFactsStr.MemoryFacts_MemoryNonPagedPool_Desc.Local(),
            ["memory", "non-paged pool", "nonpaged", "kernel"], () => new MemoryNonPagedPoolConfig()),
        new("memoryPageFile", "pageFile", Glyphs.Disk, MetricHistory.Readings, Str.Details_Memory_PageFile.Local(), MemoryFactsStr.MemoryFacts_MemoryPageFile_Label.Local(), MemoryFactsStr.MemoryFacts_MemoryPageFile_Desc.Local(),
            ["memory", "page file", "pagefile", "swap", "virtual memory"], () => new MemoryPageFileConfig()),
    ], MetricOpens.TaskManager));

    /// <summary>
    /// [S-14710] [S-14713] Each series' graph (the hub's history: Area or Line, a span of the meters') and the history widget
    /// it stands in for, a placed one replaced in place with its graph shown.
    /// </summary>
    private static IReadOnlyList<MetricKind> Graphed(IReadOnlyList<MetricKind> kinds) => [.. kinds.Select(kind => kind.Key switch
    {
        "inUse" => kind.Graphed(MetricGraph.Hub(), new MergeFrom("memoryHistory")),
        _ => kind,
    })];

    /// <summary>The kind showing the metric <paramref name="key"/>.</summary>
    public static MetricKind Of(string key) => All.Single(k => k.Key == key);

    /// <summary>Its place in the family's order: its add-page rank.</summary>
    public static int Rank(string key) => All.ToList().FindIndex(k => k.Key == key);
}

// [S-14604] Each kind is its module and its config (a config's type is how it knows its id, [S-6902]); everything
// else is the family's (MemoryMetricModule, MemoryMetricConfig, the view) over the single metric's base.

public sealed class MemoryInUseModule() : MemoryMetricModule("inUse");

public sealed class MemoryAvailableModule() : MemoryMetricModule("available");

public sealed class MemoryCommittedModule() : MemoryMetricModule("committed");

public sealed class MemoryCachedModule() : MemoryMetricModule("cached");

public sealed class MemoryPagedPoolModule() : MemoryMetricModule("pagedPool");

public sealed class MemoryNonPagedPoolModule() : MemoryMetricModule("nonPagedPool");

public sealed class MemoryPageFileModule() : MemoryMetricModule("pageFile");

public sealed class MemoryInUseConfig() : MemoryMetricConfig("inUse");

public sealed class MemoryAvailableConfig() : MemoryMetricConfig("available");

public sealed class MemoryCommittedConfig() : MemoryMetricConfig("committed");

public sealed class MemoryCachedConfig() : MemoryMetricConfig("cached");

public sealed class MemoryPagedPoolConfig() : MemoryMetricConfig("pagedPool");

public sealed class MemoryNonPagedPoolConfig() : MemoryMetricConfig("nonPagedPool");

public sealed class MemoryPageFileConfig() : MemoryMetricConfig("pageFile");
