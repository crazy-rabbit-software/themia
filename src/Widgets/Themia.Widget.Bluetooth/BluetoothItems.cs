using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk.Devices;
using Themia.Sdk.Kit;

namespace Themia.Widget.Bluetooth;

/// <summary>[S-16660] What a Bluetooth item says besides its name: its battery, its connection word, its signal.</summary>
public readonly record struct BluetoothShows(bool Battery, bool Connection, bool Signal);

/// <summary>
/// [S-16660] The paired Bluetooth devices on the device family's item ([S-16630]): the kind's glyph (primary connected,
/// secondary paired, [S-3961]), the name, the battery the reading with its bar (Critical at or below 20 %, [S-3254]), and
/// the kind, connection and signal as its line ([S-1255]). Read-only: nothing here connects, disconnects or unpairs.
/// </summary>
public static class BluetoothItems
{
    /// <summary>[S-3252] [S-16672] Whether a battery is read: its percentage or its bar is shown.</summary>
    public static bool ReadsBattery(IReadOnlyCollection<DevicePart> hidden)
        => !(hidden ?? []).Contains(DevicePart.Reading) || !(hidden ?? []).Contains(DevicePart.Bar);

    /// <summary>[S-16660] The glyph of the list and the device widget: the Bluetooth mark.</summary>
    public static string Glyph => Glyphs.Bluetooth;

    /// <summary>
    /// [S-16660] A device as a device item: its endpoint id the id, its name, its battery as the reading where the config
    /// shows it ("--" for a device that reports none, WR-10; none drawn with the battery off), the detail line its line,
    /// everything it knows in words as the tooltip (for a reader who cannot see the icon's colour).
    /// </summary>
    public static DeviceItem Item(BluetoothDeviceEntry device, BluetoothShows config, Locale locale)
    {
        var percent = device.BatteryPercent is >= 0 and <= 100 ? device.BatteryPercent : null;
        DeviceReading? reading = config.Battery && percent is { } p ? DeviceReading.Percent(locale, p / 100.0) : null;
        var line = BluetoothRules.DetailLine(device, config.Connection, showBattery: false, config.Signal, locale);
        return new DeviceItem(
            device.Id,
            device.Name.Length > 0 ? device.Name : BluetoothStr.WidgetsA_Bluetooth_UnknownDevice.Text(locale),
            new DeviceIcon(DeviceItems.Glyph((DeviceKind)device.Kind), device.Connected ? ColourRole.TextPrimary : ColourRole.TextSecondary),
            reading,
            line,
            config.Battery && percent is { } fill ? fill / 100.0 : null,
            BluetoothRules.IsLowBattery(percent) ? ColourRole.Critical : ColourRole.Accent,
            $"{device.Name} · {BluetoothRules.DetailLine(device, true, true, true, locale)}");
    }

    /// <summary>
    /// [S-16662] The device a Bluetooth device widget shows from a pass arranged by <see cref="BluetoothRules.Arrange"/>: the
    /// one named while it is paired; with none named, the first (a connected one where there is one); null where the named
    /// one is paired no more or there is none.
    /// </summary>
    public static BluetoothDeviceEntry? Pick(IReadOnlyList<BluetoothDeviceEntry> arranged, string? chosen)
    {
        ArgumentNullException.ThrowIfNull(arranged);
        return string.IsNullOrEmpty(chosen)
            ? arranged.Count > 0 ? arranged[0] : null
            : arranged.FirstOrDefault(d => string.Equals(d.Id, chosen, StringComparison.OrdinalIgnoreCase)) is { Id: not null } found ? found : null;
    }

    /// <summary>
    /// [S-16660] [S-244] WR-10: a list with no device keeps the face, one item: the Bluetooth glyph, "--", the empty bar and
    /// which empty list it is (no radio, the radio off, no access, a failed read, nothing paired, reading).
    /// </summary>
    public static DeviceItem Empty(BluetoothStatus status, Locale locale)
    {
        var why = BluetoothRules.StatusMessage(status, locale);
        var problem = status is BluetoothStatus.Failed or BluetoothStatus.PermissionDenied;
        return new DeviceItem(string.Empty, string.Empty, new DeviceIcon(Glyphs.Bluetooth, problem ? ColourRole.Caution : ColourRole.TextSecondary), null, why, null, ColourRole.Accent, why);
    }

    /// <summary>[S-1259] A device's menu: Refresh, the radios read again now (read-only: nothing connects or unpairs).</summary>
    public static IReadOnlyList<Themia.App.Widgets.ItemMenuEntry> Menu(Action refresh)
        => [new Themia.App.Widgets.ItemMenuEntry(Glyphs.Refresh, Str.Common_Refresh.Text(), refresh)];
}
