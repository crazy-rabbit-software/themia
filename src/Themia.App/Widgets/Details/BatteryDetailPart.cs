using Themia.App.Services;
using Themia.App.Services.SystemData;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Widgets.Details;

/// <summary>
/// [S-2423]..[S-2425] The battery's charge and status (the battery meter's own hub feed) and what the
/// battery itself reports through the battery class driver (<see cref="BatteryDevice"/>): health,
/// capacities, cycles, rate, voltage, temperature, chemistry and maker. A desktop shows the empty state.
/// The device is asked only while Windows says there is a battery: a desktop costs one status call.
/// </summary>
internal sealed class BatteryDetailPart : DetailPart
{
    private readonly WatchSlot<BatteryPower?> _power = new();
    private readonly WatchSlot<BatteryPack?> _pack = new();
    private SystemHub? _hub;
    private IDetailHost? _host;

    public override WidgetKind Kind => WidgetKind.BatteryDetails;

    public override double HeroCharacters => BatteryDetailRules.HeroCharacters;

    public override void Watch(SystemHub hub, WidgetConfig config, IDetailHost host)
    {
        _hub = hub;
        _host = host;
        _power.Hold(hub, Sources.Battery, _ => Show());
        Show();
    }

    private void Show()
    {
        if (_host is not { } host || _hub is not { } hub || !_power.HasValue)
        {
            return;
        }

        if (_power.Value is not { } power)
        {
            _pack.Release();
            host.Present(DetailSheet.Waiting with { Note = Str.Shell_Meter_Unavailable.Text() });
            return;
        }

        if (power.HasBattery)
        {
            // Read in the same pass when the battery first shows up ([S-2506]); presents again then.
            _pack.Hold(hub, Sources.BatteryPack, _ => Show());
        }
        else
        {
            _pack.Release();
        }

        host.Present(BatteryDetailRules.Sheet(Strings.Current, power, power.HasBattery && _pack.HasValue ? _pack.Value : null));
    }

    public override void Detach()
    {
        _pack.Release();
        _power.Release();
        _hub = null;
        _host = null;
    }
}
