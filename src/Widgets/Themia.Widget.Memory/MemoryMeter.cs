using Themia.App.Services;
using Themia.App.Services.SystemData;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Widget.Memory;

namespace Themia.App.Widgets.Meters;

/// <summary>[S-1130] Physical memory in use, coloured by how full it is; its styles [S-6315].</summary>
internal sealed class MemoryMeter : MeterPart
{
    /// <inheritdoc />
    public override CardOpen? Opens(WidgetConfig config) => MemoryModule.Opens;

    public override WidgetId Kind => new WidgetId("memory");

    /// <inheritdoc />
    public override bool Sized => true;

    public override MeterPressure PressureOf(MeterReading reading)
        => MeterPressures.For(PressureScale.Usage, reading.Available ? reading.Fraction : null);

    public override MeterLook Look(WidgetConfig config) => (config as MemoryConfig)?.Style switch
    {
        MemoryStyle.Bar => MeterLook.Rows,
        MemoryStyle.Gauge => MeterLook.Gauge,
        _ => MeterLook.Graph,
    };

    /// <summary>[S-6316] One bar, used against free, from the snapshot the reading itself comes from.</summary>
    public override MeterItemsWatch? WatchItems(ISystemFeeds hub, Action<IReadOnlyList<MeterItem>> onItems)
        => MeterItemsWatch.Of(hub, Sources.Memory, snapshot => snapshot is { } s ? Items(s) : [], onItems);

    private static IReadOnlyList<MeterItem> Items(MemorySnapshot snapshot)
    {
        // The reading's own figures ([S-1130]), so the bar and the number agree to the byte.
        if (MemoryFigures.UsedFraction(snapshot) is not { } fraction)
        {
            return [];
        }

        var used = MemoryFigures.Used(snapshot);

        return
        [
            new MeterItem(
                fraction,
                MeterPressures.For(PressureScale.Usage, fraction),
                MemoryStr.Styles_Memory_Used.Format(("size", Formatting.Bytes(used))),
                MemoryStr.Styles_Memory_Free.Format(("size", Formatting.Bytes(snapshot.Total - used)))),
        ];
    }
}
