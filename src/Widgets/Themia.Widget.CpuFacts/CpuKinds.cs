using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Widget.CpuFacts;

[assembly: ThemiaWidgetFamily]
[assembly: ThemiaWidget("cpuLoad", typeof(CpuLoadModule))]
[assembly: ThemiaWidget("cpuSpeed", typeof(CpuSpeedModule))]
[assembly: ThemiaWidget("cpuBaseSpeed", typeof(CpuBaseSpeedModule))]
[assembly: ThemiaWidget("cpuCoreCount", typeof(CpuCoreCountModule))]
[assembly: ThemiaWidget("cpuLogical", typeof(CpuLogicalModule))]
[assembly: ThemiaWidget("cpuProcesses", typeof(CpuProcessesModule))]
[assembly: ThemiaWidget("cpuThreads", typeof(CpuThreadsModule))]
[assembly: ThemiaWidget("cpuHandles", typeof(CpuHandlesModule))]
[assembly: ThemiaWidget("cpuUptime", typeof(CpuUptimeModule))]
[assembly: ThemiaWidget("cpuTemperature", typeof(CpuTemperatureModule))]

namespace Themia.Widget.CpuFacts;

/// <summary>
/// [S-14610] [S-14604] The family's kinds: the processor's facts, one widget a fact (owner, 2026-11-20: "split all the other widgets
/// that are alike to the weather and air facts into individual metrics too"), in the removed card's order: each one's
/// id, the metric's key, its glyph, its history (its trends), its row's name, its name and description on the add page,
/// its search words and its config.
/// </summary>
internal static class CpuKinds
{
    public static IReadOnlyList<MetricKind> All { get; } = MetricKind.Opening(
    [
        // [S-14714] The load, the CPU history's metric: the hub's series, its graph standing in for the history ([S-14713]).
        new MetricKind("cpuLoad", "load", Glyphs.Cpu, MetricHistory.Series, CpuFactsStr.CpuFacts_CpuLoad_Label.Local(), CpuFactsStr.CpuFacts_CpuLoad_Label.Local(), CpuFactsStr.CpuFacts_CpuLoad_Desc.Local(),
            ["cpu", "processor", "load", "usage", "busy", "graph", "history", "task manager"], () => new CpuLoadConfig())
            .Graphed(MetricGraph.Hub(), new MergeFrom("cpuHistory")),
        new("cpuSpeed", "speed", Glyphs.Gauge, MetricHistory.Readings, Str.Details_Cpu_Speed.Local(), CpuFactsStr.CpuFacts_CpuSpeed_Label.Local(), CpuFactsStr.CpuFacts_CpuSpeed_Desc.Local(),
            ["cpu", "processor", "clock speed", "frequency", "ghz", "speed"], () => new CpuSpeedConfig()),
        new("cpuBaseSpeed", "baseSpeed", Glyphs.Cpu, MetricHistory.None, Str.Details_Cpu_BaseSpeed.Local(), CpuFactsStr.CpuFacts_CpuBaseSpeed_Label.Local(), CpuFactsStr.CpuFacts_CpuBaseSpeed_Desc.Local(),
            ["cpu", "processor", "base speed", "base clock", "ghz"], () => new CpuBaseSpeedConfig()),
        new("cpuCoreCount", "cores", Glyphs.Cpu, MetricHistory.None, Str.Details_Cpu_Cores.Local(), CpuFactsStr.CpuFacts_CpuCoreCount_Label.Local(), CpuFactsStr.CpuFacts_CpuCoreCount_Desc.Local(),
            ["cpu", "processor", "cores", "physical cores"], () => new CpuCoreCountConfig()),
        new("cpuLogical", "logical", Glyphs.Cpu, MetricHistory.None, Str.Details_Cpu_Logical.Local(), CpuFactsStr.CpuFacts_CpuLogical_Label.Local(), CpuFactsStr.CpuFacts_CpuLogical_Desc.Local(),
            ["cpu", "processor", "logical processors", "threads", "hyperthreading", "smt"], () => new CpuLogicalConfig()),
        new("cpuProcesses", "processes", Glyphs.Apps, MetricHistory.Readings, Str.Details_Cpu_Processes.Local(), CpuFactsStr.CpuFacts_CpuProcesses_Label.Local(), CpuFactsStr.CpuFacts_CpuProcesses_Desc.Local(),
            ["processes", "programs", "running", "task manager"], () => new CpuProcessesConfig()),
        new("cpuThreads", "threads", Glyphs.Program, MetricHistory.Readings, Str.Details_Cpu_Threads.Local(), CpuFactsStr.CpuFacts_CpuThreads_Label.Local(), CpuFactsStr.CpuFacts_CpuThreads_Desc.Local(),
            ["threads", "running", "task manager"], () => new CpuThreadsConfig()),
        new("cpuHandles", "handles", Glyphs.Tag, MetricHistory.Readings, Str.Details_Cpu_Handles.Local(), CpuFactsStr.CpuFacts_CpuHandles_Label.Local(), CpuFactsStr.CpuFacts_CpuHandles_Desc.Local(),
            ["handles", "kernel", "task manager"], () => new CpuHandlesConfig()),
        new("cpuUptime", "uptime", Glyphs.Hours, MetricHistory.None, Str.Details_Cpu_Uptime.Local(), CpuFactsStr.CpuFacts_CpuUptime_Label.Local(), CpuFactsStr.CpuFacts_CpuUptime_Desc.Local(),
            ["uptime", "up time", "since boot", "running time"], () => new CpuUptimeConfig()),
        new("cpuTemperature", "temperature", Glyphs.Thermometer, MetricHistory.Readings, Str.Details_Cpu_Temperature.Local(), CpuFactsStr.CpuFacts_CpuTemperature_Label.Local(), CpuFactsStr.CpuFacts_CpuTemperature_Desc.Local(),
            ["cpu", "processor", "temperature", "heat", "thermal", "celsius"], () => new CpuTemperatureConfig()),
    ], MetricOpens.TaskManager);

    /// <summary>The kind showing the metric <paramref name="key"/>.</summary>
    public static MetricKind Of(string key) => All.Single(k => k.Key == key);

    /// <summary>Its place in the family's order: its add-page rank.</summary>
    public static int Rank(string key) => All.ToList().FindIndex(k => k.Key == key);
}

// [S-14604] Each kind is its module and its config (a config's type is how it knows its id, [S-6902]); everything
// else is the family's (CpuMetricModule, CpuMetricConfig, the view) over the single metric's base.

public sealed class CpuLoadModule() : CpuMetricModule("load");

public sealed class CpuSpeedModule() : CpuMetricModule("speed");

public sealed class CpuBaseSpeedModule() : CpuMetricModule("baseSpeed");

public sealed class CpuCoreCountModule() : CpuMetricModule("cores");

public sealed class CpuLogicalModule() : CpuMetricModule("logical");

public sealed class CpuProcessesModule() : CpuMetricModule("processes");

public sealed class CpuThreadsModule() : CpuMetricModule("threads");

public sealed class CpuHandlesModule() : CpuMetricModule("handles");

public sealed class CpuUptimeModule() : CpuMetricModule("uptime");

public sealed class CpuTemperatureModule() : CpuMetricModule("temperature");

public sealed class CpuLoadConfig() : CpuMetricConfig("load");

public sealed class CpuSpeedConfig() : CpuMetricConfig("speed");

public sealed class CpuBaseSpeedConfig() : CpuMetricConfig("baseSpeed");

public sealed class CpuCoreCountConfig() : CpuMetricConfig("cores");

public sealed class CpuLogicalConfig() : CpuMetricConfig("logical");

public sealed class CpuProcessesConfig() : CpuMetricConfig("processes");

public sealed class CpuThreadsConfig() : CpuMetricConfig("threads");

public sealed class CpuHandlesConfig() : CpuMetricConfig("handles");

public sealed class CpuUptimeConfig() : CpuMetricConfig("uptime");

public sealed class CpuTemperatureConfig() : CpuMetricConfig("temperature");
