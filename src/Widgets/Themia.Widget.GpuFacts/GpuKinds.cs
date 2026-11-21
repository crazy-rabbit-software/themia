using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Widget.GpuFacts;

[assembly: ThemiaWidgetFamily]
[assembly: ThemiaWidget("gpuDedicatedMemory", typeof(GpuDedicatedMemoryModule))]
[assembly: ThemiaWidget("gpuSharedMemory", typeof(GpuSharedMemoryModule))]
[assembly: ThemiaWidget("gpuTemperature", typeof(GpuTemperatureModule))]
[assembly: ThemiaWidget("gpuFan", typeof(GpuFanModule))]
[assembly: ThemiaWidget("gpuClock", typeof(GpuClockModule))]
[assembly: ThemiaWidget("gpuMemoryClock", typeof(GpuMemoryClockModule))]
[assembly: ThemiaWidget("gpuLoad", typeof(GpuLoadModule))]

namespace Themia.Widget.GpuFacts;

/// <summary>
/// [S-14620] [S-14604] The family's kinds: one graphics adapter's facts, one widget a fact (owner, 2026-11-20: "split all the other widgets
/// that are alike to the weather and air facts into individual metrics too"), in the removed card's order: each one's
/// id, the metric's key, its glyph, its history (its trends), its row's name, its name and description on the add page,
/// its search words and its config.
/// </summary>
internal static class GpuKinds
{
    public static IReadOnlyList<MetricKind> All { get; } = Graphed(MetricKind.Opening(
    [
        new("gpuDedicatedMemory", "dedicated", Glyphs.Memory, MetricHistory.Readings, Str.Details_Gpu_Dedicated.Local(), GpuFactsStr.GpuFacts_GpuDedicatedMemory_Label.Local(), GpuFactsStr.GpuFacts_GpuDedicatedMemory_Desc.Local(),
            ["gpu", "graphics", "video memory", "vram", "dedicated memory"], () => new GpuDedicatedMemoryConfig()),
        new("gpuSharedMemory", "shared", Glyphs.Memory, MetricHistory.Readings, Str.Details_Gpu_Shared.Local(), GpuFactsStr.GpuFacts_GpuSharedMemory_Label.Local(), GpuFactsStr.GpuFacts_GpuSharedMemory_Desc.Local(),
            ["gpu", "graphics", "shared memory", "integrated"], () => new GpuSharedMemoryConfig()),
        new("gpuTemperature", "temperature", Glyphs.Thermometer, MetricHistory.Readings, Str.Details_Gpu_Temperature.Local(), GpuFactsStr.GpuFacts_GpuTemperature_Label.Local(), GpuFactsStr.GpuFacts_GpuTemperature_Desc.Local(),
            ["gpu", "graphics", "temperature", "heat", "thermal"], () => new GpuTemperatureConfig()),
        new("gpuFan", "fan", Glyphs.Wind, MetricHistory.Readings, Str.Details_Gpu_Fan.Local(), GpuFactsStr.GpuFacts_GpuFan_Label.Local(), GpuFactsStr.GpuFacts_GpuFan_Desc.Local(),
            ["gpu", "graphics", "fan", "rpm", "cooling"], () => new GpuFanConfig()),
        new("gpuClock", "clock", Glyphs.Gauge, MetricHistory.Readings, Str.Details_Gpu_Clock.Local(), GpuFactsStr.GpuFacts_GpuClock_Label.Local(), GpuFactsStr.GpuFacts_GpuClock_Desc.Local(),
            ["gpu", "graphics", "clock", "core clock", "mhz"], () => new GpuClockConfig()),
        new("gpuMemoryClock", "memoryClock", Glyphs.Gauge, MetricHistory.Readings, Str.Details_Gpu_MemoryClock.Local(), GpuFactsStr.GpuFacts_GpuMemoryClock_Label.Local(), GpuFactsStr.GpuFacts_GpuMemoryClock_Desc.Local(),
            ["gpu", "graphics", "memory clock", "mhz"], () => new GpuMemoryClockConfig()),
        new("gpuLoad", "load", Glyphs.Gpu, MetricHistory.Series, Str.Details_Gpu_Load.Local(), GpuFactsStr.GpuFacts_GpuLoad_Label.Local(), GpuFactsStr.GpuFacts_GpuLoad_Desc.Local(),
            ["gpu", "graphics", "load", "usage", "utilization"], () => new GpuLoadConfig()),
    ], MetricOpens.TaskManager));

    /// <summary>
    /// [S-14710] [S-14713] Each series' graph (the hub's history: Area or Line, a span of the meters') and the history widget
    /// it stands in for, a placed one replaced in place with its graph shown.
    /// </summary>
    private static IReadOnlyList<MetricKind> Graphed(IReadOnlyList<MetricKind> kinds) => [.. kinds.Select(kind => kind.Key switch
    {
        "load" => kind.Graphed(MetricGraph.Hub(), new MergeFrom("gpuHistory")),
        _ => kind,
    })];

    /// <summary>The kind showing the metric <paramref name="key"/>.</summary>
    public static MetricKind Of(string key) => All.Single(k => k.Key == key);

    /// <summary>Its place in the family's order: its add-page rank.</summary>
    public static int Rank(string key) => All.ToList().FindIndex(k => k.Key == key);
}

// [S-14604] Each kind is its module and its config (a config's type is how it knows its id, [S-6902]); everything
// else is the family's (GpuMetricModule, GpuMetricConfig, the view) over the single metric's base.

public sealed class GpuDedicatedMemoryModule() : GpuMetricModule("dedicated");

public sealed class GpuSharedMemoryModule() : GpuMetricModule("shared");

public sealed class GpuTemperatureModule() : GpuMetricModule("temperature");

public sealed class GpuFanModule() : GpuMetricModule("fan");

public sealed class GpuClockModule() : GpuMetricModule("clock");

public sealed class GpuMemoryClockModule() : GpuMetricModule("memoryClock");

public sealed class GpuLoadModule() : GpuMetricModule("load");

public sealed class GpuDedicatedMemoryConfig() : GpuMetricConfig("dedicated");

public sealed class GpuSharedMemoryConfig() : GpuMetricConfig("shared");

public sealed class GpuTemperatureConfig() : GpuMetricConfig("temperature");

public sealed class GpuFanConfig() : GpuMetricConfig("fan");

public sealed class GpuClockConfig() : GpuMetricConfig("clock");

public sealed class GpuMemoryClockConfig() : GpuMetricConfig("memoryClock");

public sealed class GpuLoadConfig() : GpuMetricConfig("load");
