using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk.Devices;
using Themia.Sdk.Items;
using Themia.Sdk.Kit;

namespace Themia.Widget.Battery;

/// <summary>
/// [S-17182] This PC's battery as a device item (owner, 2026-11-23: "battery widget should get the same base container as teh
/// bluetooth device one and should also have the percentage progress as well"): the battery glyph filled to the nearest tenth
/// (the bolt while charging, [S-1187]), its name, its state and time as the line ("Charging · Full in 1h 2m", "On battery ·
/// 3h 10m left"), its charge the reading with its bar (Critical at or below <see cref="Low"/>, never Caution for being full,
/// [S-16632]). No battery, or one Windows will not read, has no percentage and no bar, its line saying why ([S-16670]).
/// </summary>
public static class BatteryItems
{
    /// <summary>The one item's id: this PC has one battery (several are one pack, [S-2423]).</summary>
    public const string Id = "battery";

    /// <summary>[S-17182] The charge the bar turns Critical at: Windows' battery saver comes on at 20 % by default.</summary>
    public const int Low = 20;

    /// <summary>[S-1187] Segoe Fluent Icons: Battery0..Battery9, Battery10.</summary>
    private static readonly string[] Levels =
        ["", "", "", "", "", "", "", "", "", "", ""];

    /// <summary>[S-1187] BatteryCharging0..8, BatteryCharging9 (also for a full charging battery: the font has no charging 10).</summary>
    private static readonly string[] ChargingLevels =
        ["", "", "", "", "", "", "", "", "", "", ""];

    /// <summary>[S-1187] The battery glyph a charge draws as, the bolt while charging; no charge known: the half-full one ([S-1941]).</summary>
    public static string Glyph(int? percent, bool charging) => (charging ? ChargingLevels : Levels)[BatteryRules.Level(percent ?? 50)];

    /// <summary>
    /// [S-17182] The item's line: its state in words ([S-2425]) and, while charging, the time to full where the pack's rate
    /// gives it, or on battery the time left (Windows' own estimate, else the pack's).
    /// </summary>
    public static string Line(Locale locale, BatteryPower power, BatteryPack? pack)
    {
        var status = BatteryDetailRules.Status(locale, power);
        if (power.Charging && pack is { } charging && BatteryDetailRules.TimeToFull(charging) is { } full)
        {
            return $"{status} · {BatteryStr.Battery_Item_FullIn.Format(locale, ("time", BatteryDetailRules.Hours(locale, full)))}";
        }

        if (!power.OnMains && (power.Remaining ?? (pack is { } draining ? BatteryDetailRules.TimeToEmpty(draining) : null)) is { } left)
        {
            return $"{status} · {BatteryStr.Battery_Item_Left.Format(locale, ("time", BatteryDetailRules.Hours(locale, left)))}";
        }

        return status;
    }

    /// <summary>
    /// [S-17182] The battery as an item: <paramref name="read"/> false is the hub not read yet ("Loading…"); a read of null is
    /// Windows not saying; no battery says so ([S-1940]); else the charge, its bar and the line.
    /// </summary>
    public static Item Item(Locale locale, bool read, BatteryPower? power, BatteryPack? pack)
    {
        var name = BatteryStr.Widget_Battery_Label.Text(locale);
        if (!read || power is not { } shown || !shown.HasBattery || shown.Percent is not { } percent)
        {
            var why = !read ? Strings.Get(locale, Str.Common_Loading)
                : power is not { } known ? Strings.Get(locale, Str.Shell_Meter_Unavailable)
                : !known.HasBattery ? Strings.Get(locale, Str.WidgetsB_Battery_None)
                : BatteryDetailRules.Status(locale, known);
            return new Item(Id, name, new ItemLead(Glyph(null, false), ColourRole.TextSecondary), why, Tooltip: $"{name} · {why}", Muted: power is { HasBattery: false });
        }

        var line = Line(locale, shown, pack);
        return new Item(
            Id,
            name,
            new ItemLead(Glyph(percent, shown.Charging), ColourRole.TextPrimary),
            line,
            ItemEnd.Percent(locale, percent / 100.0),
            new ItemBar(percent / 100.0, 0, DeviceItems.BatteryRole(percent, Low)),
            Tooltip: $"{name} · {LocaleFormat.Percent(locale, percent)} · {line}");
    }
}
