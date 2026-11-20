using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Widget.BatteryFacts;

[assembly: ThemiaWidgetFamily]
[assembly: ThemiaWidget("batteryStatus", typeof(BatteryStatusModule))]
[assembly: ThemiaWidget("batteryTime", typeof(BatteryTimeModule))]
[assembly: ThemiaWidget("batteryHealth", typeof(BatteryHealthModule))]
[assembly: ThemiaWidget("batteryFullCapacity", typeof(BatteryFullCapacityModule))]
[assembly: ThemiaWidget("batteryDesignCapacity", typeof(BatteryDesignCapacityModule))]
[assembly: ThemiaWidget("batteryCycles", typeof(BatteryCyclesModule))]
[assembly: ThemiaWidget("batteryRate", typeof(BatteryRateModule))]
[assembly: ThemiaWidget("batteryVoltage", typeof(BatteryVoltageModule))]
[assembly: ThemiaWidget("batteryTemperature", typeof(BatteryTemperatureModule))]
[assembly: ThemiaWidget("batteryChemistry", typeof(BatteryChemistryModule))]
[assembly: ThemiaWidget("batteryManufacturer", typeof(BatteryManufacturerModule))]

namespace Themia.Widget.BatteryFacts;

/// <summary>
/// [S-14660] [S-14604] The family's kinds: what the battery itself reports, one widget a fact (owner, 2026-11-20: "split all the other widgets
/// that are alike to the weather and air facts into individual metrics too"), in the removed card's order: each one's
/// id, the metric's key, its glyph, its history (its trends), its row's name, its name and description on the add page,
/// its search words and its config; each stands in for its row of a placed <c>batteryFacts</c> card ([S-14606]).
/// </summary>
internal static class BatteryKinds
{
    public static IReadOnlyList<MetricKind> All { get; } = MetricKind.Opening(MetricKind.SplitOf("batteryFacts",
    [
        new("batteryStatus", "status", Glyphs.Battery, MetricHistory.None, Str.Details_Battery_Status.Local(), BatteryFactsStr.BatteryFacts_BatteryStatus_Label.Local(), BatteryFactsStr.BatteryFacts_BatteryStatus_Desc.Local(),
            ["battery", "status", "charging", "plugged in"], () => new BatteryStatusConfig()),
        new("batteryTime", "time", Glyphs.Hours, MetricHistory.Readings, Str.Details_Battery_TimeLeft.Local(), BatteryFactsStr.BatteryFacts_BatteryTime_Label.Local(), BatteryFactsStr.BatteryFacts_BatteryTime_Desc.Local(),
            ["battery", "time left", "remaining", "full in", "runtime"], () => new BatteryTimeConfig()),
        new("batteryHealth", "health", Glyphs.Battery, MetricHistory.None, Str.Details_Battery_Health.Local(), BatteryFactsStr.BatteryFacts_BatteryHealth_Label.Local(), BatteryFactsStr.BatteryFacts_BatteryHealth_Desc.Local(),
            ["battery", "health", "wear", "capacity"], () => new BatteryHealthConfig()),
        new("batteryFullCapacity", "fullCapacity", Glyphs.Battery, MetricHistory.None, Str.Details_Battery_FullCapacity.Local(), BatteryFactsStr.BatteryFacts_BatteryFullCapacity_Label.Local(), BatteryFactsStr.BatteryFacts_BatteryFullCapacity_Desc.Local(),
            ["battery", "full charge capacity", "capacity", "wh"], () => new BatteryFullCapacityConfig()),
        new("batteryDesignCapacity", "designCapacity", Glyphs.Battery, MetricHistory.None, Str.Details_Battery_DesignCapacity.Local(), BatteryFactsStr.BatteryFacts_BatteryDesignCapacity_Label.Local(), BatteryFactsStr.BatteryFacts_BatteryDesignCapacity_Desc.Local(),
            ["battery", "design capacity", "capacity", "wh"], () => new BatteryDesignCapacityConfig()),
        new("batteryCycles", "cycles", Glyphs.Rotate, MetricHistory.None, Str.Details_Battery_Cycles.Local(), BatteryFactsStr.BatteryFacts_BatteryCycles_Label.Local(), BatteryFactsStr.BatteryFacts_BatteryCycles_Desc.Local(),
            ["battery", "cycle count", "cycles", "wear"], () => new BatteryCyclesConfig()),
        new("batteryRate", "rate", Glyphs.ChargingBolt, MetricHistory.Readings, Str.Details_Battery_DischargeRate.Local(), BatteryFactsStr.BatteryFacts_BatteryRate_Label.Local(), BatteryFactsStr.BatteryFacts_BatteryRate_Desc.Local(),
            ["battery", "charge rate", "discharge rate", "watts", "power draw"], () => new BatteryRateConfig()),
        new("batteryVoltage", "voltage", Glyphs.ChargingBolt, MetricHistory.Readings, Str.Details_Battery_Voltage.Local(), BatteryFactsStr.BatteryFacts_BatteryVoltage_Label.Local(), BatteryFactsStr.BatteryFacts_BatteryVoltage_Desc.Local(),
            ["battery", "voltage", "volts"], () => new BatteryVoltageConfig()),
        new("batteryTemperature", "temperature", Glyphs.Thermometer, MetricHistory.Readings, Str.Details_Battery_Temperature.Local(), BatteryFactsStr.BatteryFacts_BatteryTemperature_Label.Local(), BatteryFactsStr.BatteryFacts_BatteryTemperature_Desc.Local(),
            ["battery", "temperature", "heat"], () => new BatteryTemperatureConfig()),
        new("batteryChemistry", "chemistry", Glyphs.Battery, MetricHistory.None, Str.Details_Battery_Chemistry.Local(), BatteryFactsStr.BatteryFacts_BatteryChemistry_Label.Local(), BatteryFactsStr.BatteryFacts_BatteryChemistry_Desc.Local(),
            ["battery", "chemistry", "lithium", "li-ion"], () => new BatteryChemistryConfig()),
        new("batteryManufacturer", "manufacturer", Glyphs.Tag, MetricHistory.None, Str.Details_Battery_Manufacturer.Local(), BatteryFactsStr.BatteryFacts_BatteryManufacturer_Label.Local(), BatteryFactsStr.BatteryFacts_BatteryManufacturer_Desc.Local(),
            ["battery", "manufacturer", "maker", "vendor"], () => new BatteryManufacturerConfig()),
    ], ["status", "time", "health", "fullCapacity"]), MetricOpens.Battery);

    /// <summary>The kind showing the metric <paramref name="key"/>.</summary>
    public static MetricKind Of(string key) => All.Single(k => k.Key == key);

    /// <summary>Its place in the family's order: its add-page rank.</summary>
    public static int Rank(string key) => All.ToList().FindIndex(k => k.Key == key);
}

// [S-14604] Each kind is its module and its config (a config's type is how it knows its id, [S-6902]); everything
// else is the family's (BatteryMetricModule, BatteryMetricConfig, the view) over the single metric's base.

public sealed class BatteryStatusModule() : BatteryMetricModule("status");

public sealed class BatteryTimeModule() : BatteryMetricModule("time");

public sealed class BatteryHealthModule() : BatteryMetricModule("health");

public sealed class BatteryFullCapacityModule() : BatteryMetricModule("fullCapacity");

public sealed class BatteryDesignCapacityModule() : BatteryMetricModule("designCapacity");

public sealed class BatteryCyclesModule() : BatteryMetricModule("cycles");

public sealed class BatteryRateModule() : BatteryMetricModule("rate");

public sealed class BatteryVoltageModule() : BatteryMetricModule("voltage");

public sealed class BatteryTemperatureModule() : BatteryMetricModule("temperature");

public sealed class BatteryChemistryModule() : BatteryMetricModule("chemistry");

public sealed class BatteryManufacturerModule() : BatteryMetricModule("manufacturer");

public sealed class BatteryStatusConfig() : BatteryMetricConfig("status");

public sealed class BatteryTimeConfig() : BatteryMetricConfig("time");

public sealed class BatteryHealthConfig() : BatteryMetricConfig("health");

public sealed class BatteryFullCapacityConfig() : BatteryMetricConfig("fullCapacity");

public sealed class BatteryDesignCapacityConfig() : BatteryMetricConfig("designCapacity");

public sealed class BatteryCyclesConfig() : BatteryMetricConfig("cycles");

public sealed class BatteryRateConfig() : BatteryMetricConfig("rate");

public sealed class BatteryVoltageConfig() : BatteryMetricConfig("voltage");

public sealed class BatteryTemperatureConfig() : BatteryMetricConfig("temperature");

public sealed class BatteryChemistryConfig() : BatteryMetricConfig("chemistry");

public sealed class BatteryManufacturerConfig() : BatteryMetricConfig("manufacturer");
