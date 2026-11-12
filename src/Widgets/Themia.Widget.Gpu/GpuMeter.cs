using Themia.App.Services;
using Themia.App.Services.SystemData;
using Themia.Core.Model;
using Themia.Core.SystemData;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Widget.Gpu;

namespace Themia.App.Widgets.Meters;

/// <summary>
/// [S-430] One graphics adapter's load, a meter as the CPU's and memory's are (the shared
/// <see cref="MeterView"/>): the reading over its history, a gauge, or a row per engine type
/// ([S-6320]). The load and its history are the hub's series for the chosen adapter ([S-2504]); its
/// name (the card's title) and whether it is there at all come from the GPU feed's snapshots, watched
/// with the series.
/// </summary>
internal sealed class GpuMeter : MeterPart
{
    /// <inheritdoc />
    public override CardOpen? Opens(WidgetConfig config) => GpuModule.Opens;

    /// <summary>[S-1940] The engines every adapter has, drawn for one that is not there: "--", no bar.</summary>
    private static readonly GpuEngineKind[] StandIn = [GpuEngineKind.ThreeD, GpuEngineKind.Copy, GpuEngineKind.VideoEncode, GpuEngineKind.VideoDecode];

    private int _index;
    private IFeedWatch<GpuSnapshot>? _snapshots;

    public override WidgetId Kind => new WidgetId("gpu");

    /// <summary>[S-432] The load's pressure: high from 70 %, critical from 90 %.</summary>
    public override MeterPressure PressureOf(MeterReading reading)
        => reading is { Available: true, Value: { } percent } ? GpuRules.LoadPressure(percent) switch
        {
            GpuPressure.Critical => MeterPressure.Critical,
            GpuPressure.High => MeterPressure.Elevated,
            _ => MeterPressure.Normal,
        } : MeterPressure.Normal;

    public override MeterLook Look(WidgetConfig config) => (config as GpuConfig)?.Style switch
    {
        GpuStyle.Engines => MeterLook.Rows,
        GpuStyle.Gauge => MeterLook.Gauge,
        _ => MeterLook.Graph,
    };

    public override void ApplyConfig(WidgetConfig config, IMeterHost host)
        => _index = Math.Max(0, (config as GpuConfig)?.Adapter ?? 0);

    /// <summary>[S-430] [S-433] The adapters: the chosen one names the card; none at its place draws the meter absent.</summary>
    public override void Watch(ISystemFeeds hub, IMeterHost host)
    {
        _snapshots?.Dispose();
        _snapshots = hub.Watch(Sources.Gpu, snapshot => Name(snapshot, host));
        if (_snapshots.HasValue)
        {
            Name(_snapshots.Value, host);
        }
    }

    public override void Unwatch()
    {
        _snapshots?.Dispose();
        _snapshots = null;
    }

    private void Name(GpuSnapshot snapshot, IMeterHost host)
    {
        // [S-433] [S-1940] No adapter at all, or not the chosen one (an eGPU unplugged): the same tile
        // in the disabled look, the reason on hover — never an empty state in its place.
        if (snapshot.Adapters.Count == 0 || _index >= snapshot.Readings.Count)
        {
            host.SetAbout(null);
            host.SetAbsent(snapshot.Adapters.Count == 0 ? GpuStr.WidgetsB_Gpu_NoAdapter.Text() : GpuStr.WidgetsB_Gpu_AdapterMissing.Text());
            return;
        }

        host.SetAbsent(null);
        host.SetAbout(GpuRules.ShortName(snapshot.Readings[_index].Name));
    }

    /// <summary>[S-6322] The adapter's engine types as the newest snapshot has them.</summary>
    public override MeterItemsWatch? WatchItems(ISystemFeeds hub, Action<IReadOnlyList<MeterItem>> onItems)
        => MeterItemsWatch.Of(hub, Sources.Gpu, snapshot => _index < snapshot.Readings.Count ? Items(snapshot.Readings[_index].Engines) : [], onItems);

    private static IReadOnlyList<MeterItem> Items(IReadOnlyList<GpuEngineLoad> engines)
        => [.. engines.Select(e => new MeterItem(
            e.Percent / 100,
            GpuRules.LoadPressure(e.Percent) switch
            {
                GpuPressure.Critical => MeterPressure.Critical,
                GpuPressure.High => MeterPressure.Elevated,
                _ => MeterPressure.Normal,
            },
            Label(e.Kind),
            Formatting.Percent(e.Percent / 100)))];

    public override IReadOnlyList<MeterItem> AbsentItems => [.. StandIn.Select(kind => new MeterItem(null, MeterPressure.Normal, Label(kind), "--"))];

    private static string Label(GpuEngineKind kind) => kind switch
    {
        GpuEngineKind.ThreeD => GpuStr.Styles_Gpu_Engine3d.Text(),
        GpuEngineKind.Copy => GpuStr.Styles_Gpu_EngineCopy.Text(),
        GpuEngineKind.VideoEncode => GpuStr.Styles_Gpu_EngineVideoEncode.Text(),
        _ => GpuStr.Styles_Gpu_EngineVideoDecode.Text(),
    };

#if DEBUG
    /// <summary>[S-1940] A PC with no adapter to read.</summary>
    public override MeterReading? AbsentReading => MeterReading.Missing(GpuStr.WidgetsB_Gpu_NoAdapter.Text());
#endif
}
