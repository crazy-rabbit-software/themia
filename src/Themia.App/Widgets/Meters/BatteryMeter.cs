using Themia.App.Services;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Widgets.Meters;

/// <summary>
/// [S-1180] [S-1187] Charge level, led by the battery icon filled to the nearest tenth (with a bolt
/// while charging), coloured by the original's charge scale. Its history is drawn like any other
/// meter's when the graph is on ([S-1126]). A machine with no battery shows an empty state.
/// </summary>
internal sealed class BatteryMeter : MeterPart
{
    // Segoe Fluent Icons: Battery0..Battery9, Battery10; BatteryCharging0..8, BatteryCharging9 (also
    // drawn for a full charging battery — the font has no charging-10 in the same family).
    private static readonly string[] Levels =
        ["", "", "", "", "", "", "", "", "", "", ""];

    private static readonly string[] ChargingLevels =
        ["", "", "", "", "", "", "", "", "", "", ""];

    private bool _charging;
    private int? _percent;

    public override WidgetKind Kind => WidgetKind.Battery;

    public override bool CanBeAbsent => true;

    /// <summary>The icon sits beside the number, about two characters wide at the reading's size.</summary>
    public override double ReadingCharacters => base.ReadingCharacters + 2;

    public override MeterPressure PressureOf(MeterReading reading)
        => MeterPressures.For(PressureScale.Charge, reading.Available ? reading.Fraction : null, _charging);

    /// <summary>[S-1187] The battery glyph, filled to the nearest tenth, with a bolt while charging.</summary>
    public override string? Badge(MeterReading reading)
        => reading.Available && _percent is { } percent
            ? (_charging ? ChargingLevels : Levels)[BatteryRules.Level(percent)]
            : null;

    public override void Sample(IMeterHost host) => Present(host, SystemMetrics.Battery());

    private void Present(IMeterHost host, BatteryPower? reading)
    {
        if (reading is not { } power)
        {
            host.Present(MeterReading.Unavailable(Str.WidgetsB_Meters_Unavailable.Text()));
            return;
        }

        if (!power.HasBattery)
        {
            host.Present(MeterReading.Missing(Str.WidgetsB_Battery_None.Text()));
            return;
        }

        if (power.Percent is not { } percent)
        {
            host.Present(MeterReading.Unavailable(Str.WidgetsB_Meters_Unavailable.Text()));
            return;
        }

        _charging = power.Charging;
        _percent = percent;

        host.Present(new MeterReading(percent / 100.0, LocaleFormat.Percent(Strings.Current, percent), true));
    }
}
