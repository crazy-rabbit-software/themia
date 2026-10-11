using Themia.App.Services;
using Themia.App.Services.SystemData;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Widgets.Meters;

/// <summary>[S-1130] Physical memory in use, coloured by how full it is.</summary>
internal sealed class MemoryMeter : MeterPart
{
    public override WidgetKind Kind => WidgetKind.Memory;

    public override MeterPressure PressureOf(MeterReading reading)
        => MeterPressures.For(PressureScale.Usage, reading.Available ? reading.Fraction : null);

    public override MeterSeries Series(WidgetConfig config) => MeterSeries.Of(Sources.Memory, Interval(config));
}
