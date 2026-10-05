using Themia.App.Services;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Widgets.Meters;

/// <summary>[S-1130] Physical memory in use, coloured by how full it is.</summary>
internal sealed class MemoryMeter : MeterPart
{
    public override WidgetKind Kind => WidgetKind.Memory;

    public override MeterPressure PressureOf(MeterReading reading)
        => MeterPressures.For(PressureScale.Usage, reading.Available ? reading.Fraction : null);

    public override void Sample(IMeterHost host)
    {
        if (MemoryCounters.Read() is not { } snapshot || MemoryFigures.UsedFraction(snapshot) is not { } fraction)
        {
            host.Present(MeterReading.Unavailable(Str.WidgetsB_Meters_Unavailable.Text()));
            return;
        }

        host.Present(new MeterReading(fraction, Formatting.Percent(fraction), true));
    }

}
