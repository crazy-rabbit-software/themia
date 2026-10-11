using Themia.App.Services;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Widgets.Details;

/// <summary>
/// [S-2423]..[S-2425] The battery's charge and status (<c>GetSystemPowerStatus</c>, the meter's
/// source) and what the battery itself reports through the battery class driver
/// (<see cref="BatteryDevice"/>): health, capacities, cycles, rate, voltage, temperature, chemistry
/// and maker. A desktop shows the empty state.
/// </summary>
internal sealed class BatteryDetailPart : DetailPart
{
    public override WidgetKind Kind => WidgetKind.BatteryDetails;

    public override double HeroCharacters => BatteryDetailRules.HeroCharacters;

    public override void Sample(IDetailHost host)
    {
        if (SystemMetrics.Battery() is not { } power)
        {
            host.Present(DetailSheet.Waiting with { Note = Str.Shell_Meter_Unavailable.Text() });
            return;
        }

        // The device is only asked when there is a battery: a desktop costs one status call.
        host.Present(BatteryDetailRules.Sheet(Strings.Current, power, power.HasBattery ? BatteryDevice.Read() : null));
    }
}
