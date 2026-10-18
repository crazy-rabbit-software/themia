using Themia.App.Services;
using Themia.App.Services.SystemData;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.App.Widgets.Meters;

/// <summary>[S-1130] Physical memory in use, coloured by how full it is; its styles [S-6315].</summary>
internal sealed class MemoryMeter : MeterPart
{
    public override WidgetId Kind => MemoryConfig.Descriptor.Id;

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

    /// <summary>[S-6304] The widest line: both sizes at their longest ("1023.9 GB").</summary>
    public override double ItemLineCharacters
        => Str.Styles_Memory_Used.Format(("size", "1023.9 GB")).Length + Str.Styles_Memory_Free.Format(("size", "1023.9 GB")).Length;

    /// <summary>Ten of sixteen gigabytes in use.</summary>
    public override IReadOnlyList<MeterItem> SampleItems => Items(new MemorySnapshot(16UL << 30, 6UL << 30));

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
                Str.Styles_Memory_Used.Format(("size", Formatting.Bytes(used))),
                Str.Styles_Memory_Free.Format(("size", Formatting.Bytes(snapshot.Total - used)))),
        ];
    }
}
