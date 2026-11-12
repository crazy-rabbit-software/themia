using Themia.App.Services;
using Themia.App.Services.SystemData;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.SystemData;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.App.Widgets.Meters;


/// <summary>
/// [S-430] [S-11000] What every GPU atom's meter shares (the reading, the history, the engines): one
/// adapter's load from the hub's series for it ([S-2504]), its pressure ([S-432]), its name as the
/// header's label and whether it is there at all, from the GPU feed's snapshots, watched with the
/// series. One path, so the three agree on the adapter, its name and its absence.
/// </summary>
public abstract class GpuPart : MeterPart
{
    /// <summary>[S-6322] The engine types every adapter has, in the order a row of each is drawn.</summary>
    private static readonly GpuEngineKind[] Engines = [GpuEngineKind.ThreeD, GpuEngineKind.Copy, GpuEngineKind.VideoEncode, GpuEngineKind.VideoDecode];

    private int _index;
    private IFeedWatch<GpuSnapshot>? _snapshots;

    /// <summary>[S-2014] A click on the card opens Task Manager.</summary>
    public override CardOpen? Opens(WidgetConfig config) => CardOpen.TaskManager;

    /// <summary>[S-432] The load's pressure: high from 70 %, critical from 90 %.</summary>
    public override MeterPressure PressureOf(MeterReading reading)
        => reading is { Available: true, Value: { } percent } ? Pressure(percent) : MeterPressure.Normal;

    private static MeterPressure Pressure(double percent) => GpuRules.LoadPressure(percent) switch
    {
        GpuPressure.Critical => MeterPressure.Critical,
        GpuPressure.High => MeterPressure.Elevated,
        _ => MeterPressure.Normal,
    };

    public override void ApplyConfig(WidgetConfig config, IMeterHost host)
        => _index = Math.Max(0, (config as IGpuAdapter)?.Adapter ?? 0);

    /// <summary>[S-430] [S-433] The adapters: the chosen one names the card; none at its place draws the meter absent.</summary>
    public override void Watch(ISystemFeeds hub, IMeterHost host)
    {
        ArgumentNullException.ThrowIfNull(hub);
        ArgumentNullException.ThrowIfNull(host);
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
            host.SetAbsent(snapshot.Adapters.Count == 0 ? Str.Details_Gpu_NoAdapter.Text() : Str.Details_Gpu_AdapterMissing.Text());
            return;
        }

        host.SetAbsent(null);
        host.SetAbout(GpuRules.ShortName(snapshot.Readings[_index].Name));
    }

    /// <summary>[S-6322] [S-11006] The four engine types, in their order, as the newest snapshot has them ("--" for one it lacks).</summary>
    public override MeterItemsWatch? WatchItems(ISystemFeeds hub, Action<IReadOnlyList<MeterItem>> onItems)
        => MeterItemsWatch.Of(hub, Sources.Gpu, snapshot => Items(_index < snapshot.Readings.Count ? snapshot.Readings[_index].Engines : []), onItems);

    /// <summary>[S-6322] A row per engine type, in the one order, each its load or "--".</summary>
    public static IReadOnlyList<MeterItem> Items(IReadOnlyList<GpuEngineLoad> engines)
    {
        ArgumentNullException.ThrowIfNull(engines);
        return [.. Engines.Select(kind => engines.Where(e => e.Kind == kind).Select(e => (GpuEngineLoad?)e).FirstOrDefault() is { } e
            ?new MeterItem(e.Percent / 100, Pressure(e.Percent), Label(kind), Formatting.Percent(e.Percent / 100))
            : new MeterItem(null, MeterPressure.Normal, Label(kind), "--"))];
    }

    /// <summary>[S-1940] The engines of an adapter that is not there: each "--", no bar.</summary>
    public override IReadOnlyList<MeterItem> AbsentItems => Items([]);

    private static string Label(GpuEngineKind kind) => kind switch
    {
        GpuEngineKind.ThreeD => Str.Details_Gpu_Engine3d.Text(),
        GpuEngineKind.Copy => Str.Details_Gpu_EngineCopy.Text(),
        GpuEngineKind.VideoEncode => Str.Details_Gpu_EngineVideoEncode.Text(),
        _ => Str.Details_Gpu_EngineVideoDecode.Text(),
    };

#if DEBUG
    /// <summary>[S-1940] A PC with no adapter to read.</summary>
    public override MeterReading? AbsentReading => MeterReading.Missing(Str.Details_Gpu_NoAdapter.Text());
#endif
}
