using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Themia.App.Services;
using Themia.App.Services.SystemData;
using Themia.App.Widgets;
using Themia.App.Widgets.Details;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.SystemData;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;

namespace Themia.Widget.CpuFacts;

/// <summary>
/// [S-14610] One kind of the CPU facts family: one of the processor's facts as a single metric
/// (<see cref="MetricModule"/>), at home in its family (<see cref="AddFolderId.CpuFacts"/>). SPEC.md beside this file.
/// </summary>
public abstract class CpuMetricModule(string key)
    : MetricModule(CpuKinds.Of(key), CpuFactsJsonContext.Default, AddFolderId.CpuFacts, CpuKinds.Rank(key))
{
    /// <summary>[S-2011] A click on the card opens Task Manager.</summary>
    internal static CardOpen? Opens => CardOpen.TaskManager;

    public override FrameworkElement CreateView(IWidgetHost host)
        => new SystemMetricView(host, Kind.Glyph, Kind.History, () => Kind.Name.Text(), new CpuMetricPart(Kind.Key));
}

/// <summary>[S-14610] What a CPU fact card remembers: its trend; its fact is its kind's. It reads the hub's load, counts and processor feeds.</summary>
public abstract class CpuMetricConfig(string key) : MetricConfig, IReadsSystem
{
    /// <summary>The fact its kind shows.</summary>
    [JsonIgnore]
    public string Key { get; } = key;

    /// <inheritdoc />
    protected override MetricHistory KindHistory => CpuKinds.Of(Key).History;

    /// <summary>[S-2513] The load, the counts and the processor's figures, in the hub's read order ([S-2501]).</summary>
    public SystemInterests SystemWants(string systemRoot) => SystemInterests.Read(SystemSource.Cpu, SystemSource.Performance, SystemSource.Processor);

    public override WidgetConfig Clone()
    {
        var copy = (CpuMetricConfig)CpuKinds.Of(Key).Create();
        CopyTo(copy);
        return copy;
    }
}

/// <summary>
/// [S-2405] [S-14610] The processor's figures for one fact: the load is the CPU meter's own feed and the counts the
/// memory's; the hub reads both before the processor on a tick ([S-2501]), so the processor's read is one tick's figures
/// and a new reading.
/// </summary>
internal sealed class CpuMetricPart(string key) : SystemMetricPart
{
    private readonly WatchSlot<MeterReading> _load = new();
    private readonly WatchSlot<PerformanceSnapshot> _counts = new();
    private readonly WatchSlot<CpuFigures> _processor = new();
    private ISystemMetricHost? _host;

    /// <inheritdoc />
    public override CardOpen? Opens(WidgetConfig config) => CpuMetricModule.Opens;

    public override void Watch(ISystemFeeds hub, WidgetConfig config, ISystemMetricHost host)
    {
        _host = host;
        _load.Hold(hub, Sources.Cpu, static _ => { });
        _counts.Hold(hub, Sources.Performance, static _ => { });
        _processor.Hold(hub, Sources.Processor, _ =>
        {
            NextReading();
            Show();
        });
        Show();
    }

    private void Show()
    {
        if (_host is not { } host || !_processor.HasValue)
        {
            return;
        }

        var load = _load.HasValue && _load.Value.Available ? _load.Value.Fraction : null;
        var counts = _counts.HasValue ? _counts.Value.Figures : null;
        var figures = _processor.Value with
        {
            Load = load,
            Processes = counts?.Processes,
            Threads = counts?.Threads,
            Handles = counts?.Handles,
        };

        host.Present(MetricRules.Pick(CpuDetailRules.Sheet(Strings.Current, figures), key) with { Reading = LastReading });
    }

    public override void Detach()
    {
        _processor.Release();
        _counts.Release();
        _load.Release();
        _host = null;
    }
}

/// <summary>[S-6903] The family's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(CpuSpeedConfig))]
[JsonSerializable(typeof(CpuBaseSpeedConfig))]
[JsonSerializable(typeof(CpuCoreCountConfig))]
[JsonSerializable(typeof(CpuLogicalConfig))]
[JsonSerializable(typeof(CpuProcessesConfig))]
[JsonSerializable(typeof(CpuThreadsConfig))]
[JsonSerializable(typeof(CpuHandlesConfig))]
[JsonSerializable(typeof(CpuUptimeConfig))]
[JsonSerializable(typeof(CpuTemperatureConfig))]
internal sealed partial class CpuFactsJsonContext : JsonSerializerContext;
