using Themia.App.Services;
using Themia.App.Services.SystemData;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Widgets.Meters;

/// <summary>[S-1140] Processor load across all cores, coloured by how busy the machine is; its styles [S-6311].</summary>
internal sealed class CpuMeter : MeterPart
{
    public override WidgetKind Kind => WidgetKind.Cpu;

    public override MeterPressure PressureOf(MeterReading reading)
        => MeterPressures.For(PressureScale.Usage, reading.Available ? reading.Fraction : null);

    public override MeterLook Look(WidgetConfig config) => (config as CpuConfig)?.Style switch
    {
        CpuStyle.Cores => MeterLook.Columns,
        CpuStyle.Grid => MeterLook.Tiles,
        CpuStyle.Gauge => MeterLook.Gauge,
        CpuStyle.CoreGraphs => MeterLook.Graphs,
        _ => MeterLook.Graph,
    };

    /// <summary>[S-6310] Each logical processor, from the hub's one per-core read; [S-6318] with each one's history.</summary>
    public override MeterItemsWatch? WatchItems(SystemHub hub, Action<IReadOnlyList<MeterItem>> onItems)
        => MeterItemsWatch.Of(
            hub,
            Sources.CpuCores,
            cores => Items(cores.Loads),
            onItems,
            static (cores, core, max) => cores.History.Newest(core, max),
            static cores => cores.History.Added);

    /// <summary>Sixteen cores, some idle, some busy, one pegged: every shade of the grid at once.</summary>
    public override IReadOnlyList<MeterItem> SampleItems => Items(
        [0.12, 0.64, 0.05, 0.31, 0.92, 0.18, 0.44, 0.02, 0.73, 0.27, 0.58, 0.09, 0.36, 1.0, 0.21, 0.48]);

    private static IReadOnlyList<MeterItem> Items(IReadOnlyList<double?> loads)
        => [.. loads.Select(load => new MeterItem(load, MeterPressures.For(PressureScale.Usage, load)))];
}
