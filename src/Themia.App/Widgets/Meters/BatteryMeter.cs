using Themia.App.Services;
using Themia.App.Services.SystemData;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;

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

    public override WidgetId Kind => BatteryConfig.Descriptor.Id;

    public override bool CanBeAbsent => true;

    /// <summary>[S-1187] The charge glyph leads the reading; the ladder measures it as the readout's glyph ([S-3860]).</summary>
    public override bool HasBadge => true;

    /// <summary>[S-6272] The battery and ring styles draw the charge themselves.</summary>
    public override MeterLook Look(WidgetConfig config) => (config as BatteryConfig)?.Style switch
    {
        BatteryStyle.Battery => MeterLook.Battery,
        BatteryStyle.Ring => MeterLook.Ring,
        _ => MeterLook.Graph,
    };

    /// <summary>64 %, charging.</summary>
    public override MeterReading SampleReading => new(0.64, Formatting.Percent(0.64), true, Charging: true);

    public override MeterPressure PressureOf(MeterReading reading)
        => MeterPressures.For(PressureScale.Charge, reading.Available ? reading.Fraction : null, reading.Charging);

    /// <summary>[S-1187] The battery glyph, filled to the nearest tenth, with a bolt while charging.</summary>
    public override string? Badge(MeterReading reading)
        => reading is { Available: true, Fraction: { } fraction }
            ? (reading.Charging ? ChargingLevels : Levels)[BatteryRules.Level((int)Math.Round(fraction * 100))]
            : null;
}
