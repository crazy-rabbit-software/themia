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

namespace Themia.Widget.MemoryFacts;

/// <summary>
/// [S-14630] One kind of the memory facts family: one of the memory's facts as a single metric
/// (<see cref="MetricModule"/>), at home in its family (<see cref="AddFolderId.MemoryFacts"/>). SPEC.md beside this file.
/// </summary>
public abstract class MemoryMetricModule(string key)
    : MetricModule(MemoryKinds.Of(key), MemoryFactsJsonContext.Default, AddFolderId.MemoryFacts, MemoryKinds.Rank(key))
{
    /// <summary>[S-2013] A click on the card opens Task Manager.</summary>
    internal static CardOpen? Opens => CardOpen.TaskManager;

    public override FrameworkElement CreateView(IWidgetHost host)
        => new SystemMetricView(host, Kind, new MemoryMetricPart(Kind.Key));
}

/// <summary>[S-14630] What a memory fact card remembers: its trend; its fact is its kind's.</summary>
public abstract class MemoryMetricConfig(string key) : MetricConfig, IReadsSystem
{
    /// <summary>The fact its kind shows.</summary>
    [JsonIgnore]
    public string Key { get; } = key;

    /// <inheritdoc />
    protected override MetricHistory KindHistory => MemoryKinds.Of(Key).History;

    /// <summary>[S-2513] The memory figures and the page files; [S-14603] the memory's share series while its line is drawn.</summary>
    public SystemInterests SystemWants(string systemRoot)
    {
        var read = SystemInterests.Read(SystemSource.Performance);
        if (MetricRules.Effective(this) != MetricTrend.Line)
        {
            return read;
        }

        var chart = SystemInterests.Chart(SystemSource.Memory);
        return new SystemInterests([.. read.Feeds, .. chart.Feeds], chart.Series);
    }

    public override WidgetConfig Clone()
    {
        var copy = (MemoryMetricConfig)MemoryKinds.Of(Key).Create();
        CopyTo(copy);
        return copy;
    }
}

/// <summary>
/// [S-2408] [S-14630] Memory in use, available, committed, cached, the pools and the page file, one fact a card, from the
/// hub's performance feed; the line of memory in use is the memory reading's own share series ([S-14603]).
/// </summary>
internal sealed class MemoryMetricPart(string key) : SystemMetricPart
{
    private readonly WatchSlot<PerformanceSnapshot> _figures = new();
    private ISeriesWatch? _series;
    private ISystemMetricHost? _host;

    /// <inheritdoc />
    public override CardOpen? Opens(WidgetConfig config) => MemoryMetricModule.Opens;

    public override void Watch(ISystemFeeds hub, WidgetConfig config, ISystemMetricHost host)
    {
        _host = host;
        _figures.Hold(hub, Sources.Performance, _ =>
        {
            NextReading();
            Show();
        });

        var series = config is MemoryMetricConfig own ? SystemInterests.For(own, SystemDrive.Root).Series.FirstOrDefault() : default;
        if (series == default)
        {
            _series?.Dispose();
            _series = null;
        }
        else if (_series is null || _series.Interest != series)
        {
            _series?.Dispose();
            _series = hub.WatchSeries(series, _ => Show());
        }

        Show();
    }

    private void Show()
    {
        if (_host is not { } host || !_figures.HasValue)
        {
            return;
        }

        var shown = MetricRules.Pick(MemoryDetailRules.Sheet(Strings.Current, _figures.Value.Figures, _figures.Value.Pagefile), key) with { Reading = LastReading };
        host.Present(_series is { } series ? shown with { Series = [.. series.History(MetricRules.LinePoints).Select(p => (double?)p.First)] } : shown);
    }

    public override void Detach()
    {
        _series?.Dispose();
        _series = null;
        _figures.Release();
        _host = null;
    }
}

/// <summary>[S-6903] The family's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(MemoryInUseConfig))]
[JsonSerializable(typeof(MemoryAvailableConfig))]
[JsonSerializable(typeof(MemoryCommittedConfig))]
[JsonSerializable(typeof(MemoryCachedConfig))]
[JsonSerializable(typeof(MemoryPagedPoolConfig))]
[JsonSerializable(typeof(MemoryNonPagedPoolConfig))]
[JsonSerializable(typeof(MemoryPageFileConfig))]
internal sealed partial class MemoryFactsJsonContext : JsonSerializerContext;
