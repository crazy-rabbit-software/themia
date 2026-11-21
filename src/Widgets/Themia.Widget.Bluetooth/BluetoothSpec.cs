using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;

namespace Themia.Widget.Bluetooth;

/// <summary>
/// [S-1254] [S-11310] The paired Bluetooth devices on the kit ([S-7074]): rows of the rich look, the device's kind glyph
/// as the lead (primary connected, secondary paired, [S-3961]), its name, its battery as the value (a reading, [S-7072];
/// Critical at or below 20 %, [S-3254]), its kind, connection and signal on the second line where the row has two
/// ([S-1255]). Read-only: nothing here connects, disconnects or unpairs.
/// </summary>
public static class BluetoothSpec
{
    public static KitSpec Spec { get; } = new(
        KitRowLook.Rich,
        KitShow.Lead | KitShow.Subtitle | KitShow.When,
        static () => BluetoothStr.WidgetsA_Bluetooth_UnknownDevice.Text(),
        static () => BluetoothStr.WidgetsA_Bluetooth_Status_None.Text(),
        KitTime.None)
    {
        Readings = true,
    };

    /// <summary>[S-1254] A device's lead key: its kind and whether it is connected (its glyph's role).</summary>
    public static string LeadKey(BluetoothDeviceEntry device) => $"{device.Kind}:{(device.Connected ? "on" : "off")}";

    /// <summary>
    /// [S-1254] A device as a kit item: its endpoint id the id, its name, its battery as the value where the config shows
    /// it, the detail line the subtitle, everything it knows in words as the tooltip (for a reader who cannot see the
    /// icon's colour).
    /// </summary>
    public static KitItem Item(BluetoothDeviceEntry device, Themia.Core.Model.BluetoothConfig config, Locale locale)
    {
        ArgumentNullException.ThrowIfNull(config);

        var battery = config.ShowBattery ? BluetoothRules.BatteryLabel(device.BatteryPercent, locale) : null;
        return new KitItem(
            device.Id,
            device.Name,
            Subtitle: BluetoothRules.DetailLine(device, config.DrawsConnection, config.ShowBattery && battery is null, config.ShowSignal, locale),
            Lead: new KitLead(KitLeadKind.Icon, LeadKey(device)),
            Tone: battery is not null && BluetoothRules.IsLowBattery(device.BatteryPercent) ? KitTone.Critical : KitTone.Neutral,
            Tooltip: $"{device.Name} · {BluetoothRules.DetailLine(device, true, true, true, locale)}",
            Value: battery);
    }

    /// <summary>[S-1259] A device's menu: Refresh, the radios read again now.</summary>
    public static IReadOnlyList<KitAction> Actions(KitItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return [new KitAction(KitActionKind.Refresh, Str.Common_Refresh.Text())];
    }
}
