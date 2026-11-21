using Themia.Core.Localization;
using Themia.Sdk.Devices;
using Themia.Widget.Bluetooth;
namespace Themia.Core.Widgets;

/// <summary>
/// The rules the Bluetooth widget draws by: what a device is called, what it is, and in what order.
/// <para>
/// Pure, and kept out of the enumeration code, because the enumeration needs a radio and a machine
/// to run on and these are the parts that are easy to get quietly wrong — a device whose name is
/// empty, two radios reporting the same headset, a battery percentage of 255 from a device that
/// means "I do not know".
/// </para>
/// </summary>
public static class BluetoothRules
{
    /// <summary>
    /// Guess what a device is from its name.
    /// <para>
    /// A guess, and deliberately only used to pick an icon. Windows exposes a class-of-device for
    /// Classic devices and an appearance value for LE ones, but neither is populated for every
    /// paired device, and a list where half the rows have an icon and half do not looks broken in a
    /// way that a slightly wrong icon does not.
    /// </para>
    /// </summary>
    public static BluetoothDeviceKind Classify(string? name) => (BluetoothDeviceKind)DeviceKinds.FromName(name);

    /// <summary>
    /// What to call a device. A paired device with no name is common enough — some LE peripherals
    /// simply do not advertise one — and an empty row is indistinguishable from a rendering bug.
    /// <para>
    /// Collapsed to a single line: the name comes off the radio, so it is not this widget's to
    /// trust, and a row is one line tall whatever arrives in it.
    /// </para>
    /// </summary>
    public static string DisplayName(string? raw, Locale locale)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return BluetoothStr.WidgetsA_Bluetooth_UnknownDevice.Text(locale);
        }

        var words = raw.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (words.Length == 0)
        {
            return BluetoothStr.WidgetsA_Bluetooth_UnknownDevice.Text(locale);
        }

        var collapsed = string.Join(' ', words);
        return collapsed.Length <= 64 ? collapsed : collapsed[..64];
    }

    /// <summary>
    /// The battery column, or null when the device does not report one — which is most of them.
    /// <para>
    /// 255 is the Battery Service's own "unknown", and anything above 100 is a device answering
    /// out of spec. Both become no column rather than a percentage nobody should act on.
    /// </para>
    /// </summary>
    public static string? BatteryLabel(int? percent, Locale locale)
        => percent is >= 0 and <= 100 ? LocaleFormat.Percent(locale, percent.Value) : null;

    /// <summary>[S-3254] At or below this the battery is drawn in the critical colour, as the tray does.</summary>
    public const int LowBattery = 20;

    /// <summary>[S-3254] Whether a reported battery is low. No reading is never low.</summary>
    public static bool IsLowBattery(int? percent) => percent is >= 0 and <= LowBattery;

    /// <summary>
    /// [S-1256] The signal column, or null. Real RSSI sits between about -100 and -20 dBm; zero is
    /// what a stack reports for "no reading", and anything outside the plausible range is a device or
    /// driver answering out of spec. Both become no column rather than a number nobody should read.
    /// </summary>
    public static string? SignalLabel(int? dbm)
        => dbm is >= -127 and <= -1 ? $"{dbm} dBm" : null;

    /// <summary>
    /// [S-1256] Signal strength as 0..4 bars, the way Windows draws Wi-Fi: -60 dBm and better is
    /// full, every 10 dB below drops a bar. Null when there is no plausible reading.
    /// </summary>
    public static int? SignalBars(int? dbm)
    {
        if (SignalLabel(dbm) is null || dbm is not { } value)
        {
            return null;
        }

        return value switch
        {
            >= -60 => 4,
            >= -70 => 3,
            >= -80 => 2,
            >= -90 => 1,
            _ => 0,
        };
    }

    /// <summary>[S-1255] What a device is, in words, for a two-line row's second line.</summary>
    public static string KindLabel(BluetoothDeviceKind kind, Locale locale) => (kind switch
    {
        BluetoothDeviceKind.Audio => BluetoothStr.WidgetsA_Bluetooth_Kind_Audio,
        BluetoothDeviceKind.Mouse => BluetoothStr.WidgetsA_Bluetooth_Kind_Mouse,
        BluetoothDeviceKind.Keyboard => BluetoothStr.WidgetsA_Bluetooth_Kind_Keyboard,
        BluetoothDeviceKind.Phone => BluetoothStr.WidgetsA_Bluetooth_Kind_Phone,
        BluetoothDeviceKind.Gamepad => BluetoothStr.WidgetsA_Bluetooth_Kind_Controller,
        BluetoothDeviceKind.Pen => BluetoothStr.WidgetsA_Bluetooth_Kind_Pen,
        BluetoothDeviceKind.Watch => BluetoothStr.WidgetsA_Bluetooth_Kind_Watch,
        _ => BluetoothStr.WidgetsA_Bluetooth_Kind_Device,
    }).Text(locale);

    /// <summary>
    /// [S-1255] A two-line row's second line: what the device is, then each enabled fact the device
    /// actually reported ("Audio · Connected · 80%"). Nothing reported is left out, never "--".
    /// </summary>
    public static string DetailLine(BluetoothDeviceEntry device, bool showConnection, bool showBattery, bool showSignal, Locale locale)
    {
        var parts = new List<string>(4) { KindLabel(device.Kind, locale) };

        if (showConnection)
        {
            parts.Add(ConnectionLabel(device.Connected, locale));
        }

        if (showBattery && BatteryLabel(device.BatteryPercent, locale) is { } battery)
        {
            parts.Add(battery);
        }

        if (showSignal && SignalLabel(device.SignalDbm) is { } signal)
        {
            parts.Add(signal);
        }

        return string.Join(" · ", parts);
    }

    /// <summary>The connection state, in words. Carried by text as well as by the dot beside it.</summary>
    public static string ConnectionLabel(bool connected, Locale locale)
        => (connected ? BluetoothStr.WidgetsA_Bluetooth_Connected : BluetoothStr.WidgetsA_Bluetooth_Paired).Text(locale);

    /// <summary>The sentence a tile with no rows shows. Every state names itself.</summary>
    public static string StatusMessage(BluetoothStatus status, Locale locale) => (status switch
    {
        BluetoothStatus.Loading => BluetoothStr.WidgetsA_Bluetooth_Status_Loading,
        BluetoothStatus.NoRadio => BluetoothStr.WidgetsA_Bluetooth_Status_NoRadio,
        BluetoothStatus.RadioOff => BluetoothStr.WidgetsA_Bluetooth_Status_RadioOff,
        BluetoothStatus.PermissionDenied => BluetoothStr.WidgetsA_Bluetooth_Status_PermissionDenied,
        BluetoothStatus.Failed => BluetoothStr.WidgetsA_Bluetooth_Status_Failed,
        _ => BluetoothStr.WidgetsA_Bluetooth_Status_None,
    }).Text(locale);

    /// <summary>
    /// Merge, filter and order what the two enumerations returned.
    /// <para>
    /// The merge is the part that matters: a headset that supports both Classic and LE is enumerated
    /// twice, under one name, and showing it twice makes the list look like it is double-counting.
    /// The LE entry is the one that carries a GATT battery reading, so where the two disagree the
    /// richer answer wins rather than whichever happened to be enumerated first.
    /// </para>
    /// <para>
    /// Connected devices first, then by name, so the order is stable between polls — a list that
    /// reshuffles every thirty seconds cannot be read.
    /// </para>
    /// </summary>
    public static List<BluetoothDeviceEntry> Arrange(
        IEnumerable<BluetoothDeviceEntry> devices,
        bool connectedOnly,
        Locale locale)
    {
        ArgumentNullException.ThrowIfNull(devices);

        var merged = new List<BluetoothDeviceEntry>();

        foreach (var device in devices)
        {
            var name = DisplayName(device.Name, locale);
            var normalised = device with { Name = name, Kind = Classify(name) };

            // Nameless devices are not merged with each other: they all normalise to the same
            // string, and folding four unnamed peripherals into one row would hide three devices.
            var index = string.IsNullOrWhiteSpace(device.Name)
                ? -1
                : merged.FindIndex(existing => existing.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

            if (index < 0)
            {
                merged.Add(normalised);
                continue;
            }

            var current = merged[index];

            merged[index] = current with
            {
                Connected = current.Connected || normalised.Connected,
                BatteryPercent = normalised.BatteryPercent ?? current.BatteryPercent,
                SignalDbm = normalised.SignalDbm ?? current.SignalDbm,
            };
        }

        if (connectedOnly)
        {
            merged.RemoveAll(device => !device.Connected);
        }

        merged.Sort(static (left, right) =>
        {
            if (left.Connected != right.Connected)
            {
                return left.Connected ? -1 : 1;
            }

            return string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
        });


        return merged;
    }
}
