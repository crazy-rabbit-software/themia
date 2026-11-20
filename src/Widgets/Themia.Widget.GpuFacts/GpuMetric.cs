using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Themia.App.Services;
using Themia.App.Services.SystemData;
using Themia.App.Shell;
using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.App.Widgets.Details;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.SystemData;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;

namespace Themia.Widget.GpuFacts;

/// <summary>
/// [S-14620] One kind of the GPU facts family: one of an adapter's facts as a single metric (<see cref="MetricModule"/>),
/// at home in its family (<see cref="AddFolderId.GpuFacts"/>); its adapter is the GPU atoms' shared line ([S-433]).
/// SPEC.md beside this file.
/// </summary>
public abstract class GpuMetricModule(string key)
    : MetricModule(GpuKinds.Of(key), GpuFactsJsonContext.Default, AddFolderId.GpuFacts, GpuKinds.Rank(key))
{
    /// <summary>[S-2431] The adapter, offered with two or more ([S-433]).</summary>
    protected override void SourceLines(WidgetMenuLines menu, WidgetConfig config) => Lines(menu, (GpuMetricConfig)config, GpuAdapters.List());

    internal static void Lines(WidgetMenuLines menu, GpuMetricConfig config, IReadOnlyList<GpuAdapter> adapters)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);
        if (GpuAdapterLine.Line(menu, config.Adapter, value => config.Adapter = value, adapters) is { } adapter)
        {
            menu.Lines.Add(adapter);
        }
    }

    public override FrameworkElement CreateView(IWidgetHost host)
        => new SystemMetricView(host, Kind, new GpuMetricPart(Kind.Key));
}

/// <summary>[S-14620] What a GPU fact card remembers: its adapter and its trend; its fact is its kind's.</summary>
public abstract class GpuMetricConfig(string key) : MetricConfig, IReadsSystem, IGpuAdapter
{
    /// <summary>The fact its kind shows.</summary>
    [JsonIgnore]
    public string Key { get; } = key;

    /// <summary>[S-2431] The adapter, as its position in DXGI's list (the GPU reading's: the primary display's first).</summary>
    public int Adapter { get; set; }

    /// <inheritdoc />
    protected override MetricHistory KindHistory => GpuKinds.Of(Key).History;

    /// <summary>[S-2513] Every adapter's sample; [S-14603] the adapter's load series while its line is drawn.</summary>
    public SystemInterests SystemWants(string systemRoot)
        => MetricRules.Effective(this) == MetricTrend.Line ? GpuInterests.Of(Adapter) : SystemInterests.Read(SystemSource.Gpu);

    public override WidgetConfig Clone()
    {
        var copy = (GpuMetricConfig)GpuKinds.Of(Key).Create();
        CopyTo(copy);
        copy.Adapter = Adapter;
        return copy;
    }

    public override IReadOnlyList<string> Repair()
        => [.. base.Repair(), .. GpuInterests.Repair(Kind, Adapter, value => Adapter = value)];
}

/// <summary>
/// [S-2430]..[S-2432] [S-14620] One adapter's figures for one fact, from the GPU meter's own hub feed (one query a due
/// tick for every GPU widget); the load's line is the adapter's series ([S-14603]). An adapter that is gone shows the first.
/// </summary>
internal sealed partial class GpuMetricPart(string key) : SystemMetricPart
{
    private readonly WatchSlot<GpuSnapshot> _gpu = new();
    private ISeriesWatch? _series;
    private GpuMetricConfig? _config;
    private ISystemMetricHost? _host;

    public override void Watch(ISystemFeeds hub, WidgetConfig config, ISystemMetricHost host)
    {
        _host = host;
        _config = config as GpuMetricConfig;

        // Another adapter is the same snapshot, picked again: shown at once.
        _gpu.Hold(hub, Sources.Gpu, _ =>
        {
            NextReading();
            Show();
        });

        var series = _config is { } own && MetricRules.Effective(own) == MetricTrend.Line ? SystemInterests.For(own, SystemDrive.Root).Series.FirstOrDefault() : (SeriesInterest?)null;
        if (series is not { } wanted)
        {
            _series?.Dispose();
            _series = null;
        }
        else if (_series is null || _series.Interest != wanted)
        {
            _series?.Dispose();
            _series = hub.WatchSeries(wanted, _ => Show());
        }

        Show();
    }

    private void Show()
    {
        if (_host is not { } host || !_gpu.HasValue)
        {
            return;
        }

        var snapshot = _gpu.Value;
        if (snapshot.Details.Count == 0)
        {
            host.Present(MetricRules.Pick(GpuDetailRules.Absent(Strings.Current, GpuFactsStr.Details_Gpu_None.Text()), key));
            return;
        }

        // [S-2431] An adapter that is gone (a removed eGPU) shows the first one rather than nothing.
        var adapter = _config?.Adapter ?? 0;
        var detail = snapshot.Details[adapter < snapshot.Details.Count ? adapter : 0];
        var shown = MetricRules.Pick(GpuDetailRules.Sheet(Strings.Current, detail), key) with { Reading = LastReading };
        // [S-14703] The load's history is a share, written as one.
        host.Present(_series is { } series ? shown with { Series = [.. series.History(MetricRules.LinePoints).Select(p => (double?)p.First)], Figure = Formatting.Percent } : shown);
    }

    public override void Detach()
    {
        _series?.Dispose();
        _series = null;
        _gpu.Release();
        _host = null;
    }
}

/// <summary>[S-6903] The family's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(GpuDedicatedMemoryConfig))]
[JsonSerializable(typeof(GpuSharedMemoryConfig))]
[JsonSerializable(typeof(GpuTemperatureConfig))]
[JsonSerializable(typeof(GpuFanConfig))]
[JsonSerializable(typeof(GpuClockConfig))]
[JsonSerializable(typeof(GpuMemoryClockConfig))]
[JsonSerializable(typeof(GpuLoadConfig))]
internal sealed partial class GpuFactsJsonContext : JsonSerializerContext;
