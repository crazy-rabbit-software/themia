using Themia.Sdk.Devices;

namespace Themia.Core.Widgets;

/// <summary>
/// What a paired device is, as far as its icon is concerned. A closed set rather than the original's
/// free-form string: the glyph table has to cover every case at compile time, and a typo in a
/// category name is otherwise a device that silently draws the generic icon.
/// </summary>
public enum BluetoothDeviceKind
{
    Unknown = 0,
    Audio = 1,
    Mouse = 2,
    Keyboard = 3,
    Phone = 4,
    Gamepad = 5,
    Pen = 6,
    Watch = 7,
}

/// <summary>Why a Bluetooth list has no rows. Each one wants a different sentence, and none of them is blank.</summary>
public enum BluetoothStatus
{
    /// <summary>Before the first enumeration comes back.</summary>
    Loading = 0,

    Ok = 1,

    /// <summary>This machine has no Bluetooth radio at all. Not an error — a fact about the hardware.</summary>
    NoRadio = 2,

    /// <summary>There is a radio and it is switched off.</summary>
    RadioOff = 3,

    /// <summary>Windows refused the app access to the radios.</summary>
    PermissionDenied = 4,

    /// <summary>The enumeration itself failed. The only one of these that is a fault.</summary>
    Failed = 5,
}

/// <summary>One paired device, as the widget draws it.</summary>
/// <param name="Name">The device's own name. Attacker-influenced in principle, so it is displayed and nothing more.</param>
/// <param name="Id">The Windows device-interface id. Used to re-read battery, never shown.</param>
/// <param name="Connected">Whether it is connected right now.</param>
/// <param name="Kind">Which glyph it gets.</param>
/// <param name="BatteryPercent">
/// 0..100 where Windows reports it for a connected device ([S-3252]: the device property, else the
/// GATT Battery Service), else null.
/// </param>
/// <param name="SignalDbm">
/// [S-1256] Received signal strength in dBm where the radio reported one (an LE device seen
/// advertising, or the association endpoint's own reading), else null. Most paired Classic devices
/// never have one.
/// </param>
public readonly record struct BluetoothDeviceEntry(
    string Name,
    string Id,
    bool Connected,
    BluetoothDeviceKind Kind,
    int? BatteryPercent,
    int? SignalDbm = null);

/// <summary>[S-4474] One pass over the paired Bluetooth devices: why it found nothing when it found nothing, and what it found.</summary>
/// <param name="Status">The state to draw. Only <see cref="BluetoothStatus.Ok"/> has rows.</param>
/// <param name="Devices">
/// What the two enumerations returned, as they returned it (their names raw, off the radio). Unmerged and unordered on
/// purpose: folding the dual-mode duplicates and choosing the order are the widget's pure rules.
/// </param>
public readonly record struct BluetoothSnapshot(BluetoothStatus Status, IReadOnlyList<BluetoothDeviceEntry> Devices)
{
    public static BluetoothSnapshot Of(BluetoothStatus status) => new(status, []);
}

/// <summary>
/// [S-4474] [S-3252] [S-1256] The host's rules over a pass of the paired devices, pure so they are tested without a radio:
/// a connected device's battery is the host's one battery inventory's reading of its container
/// (<see cref="Batteries"/>, [S-4470]), and an LE advertisement's signal is matched to a device by its address.
/// </summary>
public static class BluetoothReadings
{
    /// <summary>
    /// [S-3252] [S-4470] Each device container's battery from the one battery inventory's readings: the most trusted source's
    /// (<see cref="DeviceBatterySource"/>: the Bluetooth property first), so the Bluetooth list and Device batteries agree.
    /// </summary>
    public static Dictionary<Guid, int> Batteries(IEnumerable<DeviceBatteryReading> readings)
    {
        ArgumentNullException.ThrowIfNull(readings);
        var best = new Dictionary<Guid, (int Percent, DeviceBatterySource Source)>();
        foreach (var reading in readings)
        {
            if (reading.Container is not { } container || container == Guid.Empty || reading.Percent is < 0 or > 100)
            {
                continue;
            }

            if (!best.TryGetValue(container, out var held) || reading.Source < held.Source)
            {
                best[container] = (reading.Percent, reading.Source);
            }
        }

        return best.ToDictionary(kv => kv.Key, kv => kv.Value.Percent);
    }

    /// <summary>
    /// [S-3252] Give each connected device its container's battery. A paired but disconnected device
    /// keeps none: the property holds the last value Windows saw, and a headset in its case at "80%"
    /// from yesterday is a number nobody should act on. Where the property has nothing, a reading the
    /// entry already holds stays.
    /// </summary>
    /// <param name="containers">The device container of each entry, by index; null where unknown.</param>
    public static List<BluetoothDeviceEntry> WithBattery(
        IReadOnlyList<BluetoothDeviceEntry> devices,
        IReadOnlyList<Guid?> containers,
        IReadOnlyDictionary<Guid, int> batteries)
    {
        ArgumentNullException.ThrowIfNull(devices);
        ArgumentNullException.ThrowIfNull(containers);
        ArgumentNullException.ThrowIfNull(batteries);

        var result = new List<BluetoothDeviceEntry>(devices.Count);

        for (var i = 0; i < devices.Count; i++)
        {
            var device = devices[i];

            if (!device.Connected)
            {
                result.Add(device with { BatteryPercent = null });
                continue;
            }

            var container = i < containers.Count ? containers[i] : null;
            result.Add(container is { } id && batteries.TryGetValue(id, out var percent)
                ? device with { BatteryPercent = percent }
                : device);
        }

        return result;
    }

    /// <summary>
    /// [S-1256] A Bluetooth address as the 48-bit number an advertisement carries, from the
    /// association endpoint's "aa:bb:cc:dd:ee:ff" (or bare hex), or null for anything else.
    /// </summary>
    public static ulong? ParseAddress(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var hex = text.Trim().Replace(":", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal);

        if (hex.Length != 12)
        {
            return null;
        }

        return ulong.TryParse(hex, System.Globalization.NumberStyles.AllowHexSpecifier, System.Globalization.CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    /// <summary>
    /// [S-1256] The device's own address from an association-endpoint id
    /// (<c>BluetoothLE#BluetoothLE&lt;radio&gt;-&lt;device&gt;</c>): the part after the last dash.
    /// The fallback for when the address property was not returned.
    /// </summary>
    public static ulong? AddressFromId(string? id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        var dash = id.LastIndexOf('-');
        return dash < 0 || dash == id.Length - 1 ? null : ParseAddress(id[(dash + 1)..]);
    }

}
