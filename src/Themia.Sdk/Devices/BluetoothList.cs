using Themia.Core.Widgets;

namespace Themia.Sdk.Devices;

/// <summary>
/// [S-16660] The paired Bluetooth devices a page lists (a Bluetooth list's checklist, a Bluetooth device's choice): the
/// host's last pass over the radios, installed by the host once at start, as the drive list is.
/// </summary>
public static class BluetoothList
{
    private static Func<BluetoothSnapshot>? _now;

    /// <summary>The host's last pass (Loading before the first, or with no host installed: a page lists none then).</summary>
    public static BluetoothSnapshot Now() => Volatile.Read(ref _now)?.Invoke() ?? BluetoothSnapshot.Of(BluetoothStatus.Loading);

    /// <summary>The host's Bluetooth service. Once per process.</summary>
    internal static void Install(Func<BluetoothSnapshot> now)
    {
        ArgumentNullException.ThrowIfNull(now);
        if (Interlocked.CompareExchange(ref _now, now, null) is not null)
        {
            throw new InvalidOperationException("the Bluetooth list is installed once per process");
        }
    }
}
