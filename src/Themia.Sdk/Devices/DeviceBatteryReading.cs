namespace Themia.Sdk.Devices;

/// <summary>
/// [S-4470] Where Windows holds a peripheral's battery reading. A device may be seen through more than
/// one (a Bluetooth headset whose container also carries the level, a controller on Bluetooth), so a
/// widget folds them by <see cref="DeviceBatteryReading.Container"/> and then by name. Ordered by trust:
/// the lower member is what Windows itself shows, and wins a fold.
/// </summary>
public enum DeviceBatterySource
{
    /// <summary>[S-3252] <c>DEVPKEY_Bluetooth_Battery</c> on a device node: the level Bluetooth settings show.</summary>
    BluetoothProperty = 0,

    /// <summary>
    /// <c>System.Devices.BatteryLife</c> on a device container: Windows' devices list, set by Windows or
    /// a vendor driver for any bus (a USB receiver's mouse where its driver reports one).
    /// </summary>
    DeviceContainer = 1,

    /// <summary>The GATT Battery Service of a connected Bluetooth LE device, read by Themia itself.</summary>
    GattBatteryService = 2,

    /// <summary>A game controller's own battery report (<c>Windows.Gaming.Input</c>: Xbox controllers, wired, wireless adapter or Bluetooth).</summary>
    GameController = 3,
}

/// <summary>[S-4470] One peripheral's battery as Windows reported it, before any fold or order.</summary>
/// <param name="Id">The device's id on its path: never shown, stable while it stays connected.</param>
/// <param name="Name">The device's own name, as Windows has it (raw: a widget normalises it).</param>
/// <param name="Kind">What it is, for its icon: Windows' category where it gives one, else the name's guess.</param>
/// <param name="Percent">0..100: a reading out of range is no reading and never reaches here.</param>
/// <param name="Source">Where the reading came from.</param>
/// <param name="Container">The device container (one per physical device), where Windows names one.</param>
/// <param name="Charging">True or false where the source says; null where it does not.</param>
public readonly record struct DeviceBatteryReading(
    string Id,
    string Name,
    DeviceKind Kind,
    int Percent,
    DeviceBatterySource Source,
    Guid? Container = null,
    bool? Charging = null);

/// <summary>[S-4470] Whether the host's battery source has read anything yet.</summary>
public enum DeviceBatteryStatus
{
    /// <summary>Before the first read is in.</summary>
    Loading = 0,

    Ok = 1,

    /// <summary>The read itself failed (logged); no readings.</summary>
    Failed = 2,
}

/// <summary>[S-4470] What the last read found: every reading of every path, unfolded and unordered.</summary>
public sealed record DeviceBatterySnapshot(DeviceBatteryStatus Status, IReadOnlyList<DeviceBatteryReading> Readings)
{
    public static DeviceBatterySnapshot Loading { get; } = new(DeviceBatteryStatus.Loading, []);
}
