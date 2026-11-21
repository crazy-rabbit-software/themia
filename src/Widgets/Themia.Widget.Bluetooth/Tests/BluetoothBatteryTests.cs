using Themia.Core.Widgets;
using Themia.Sdk.Devices;
using Xunit;

namespace Themia.Widget.Bluetooth.Tests;

/// <summary>
/// [S-3252]..[S-3254] Where a paired device's battery comes from (the host's one battery inventory, by container,
/// [S-4470]) and how the row shows it.
/// </summary>
public class BluetoothBatteryTests
{
    private static readonly Guid Headset = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Mouse = new("22222222-2222-2222-2222-222222222222");

    private static BluetoothDeviceEntry Device(string name, bool connected, int? battery = null)
        => new(name, name, connected, BluetoothDeviceKind.Unknown, battery);

    [Fact]
    public void S3252_S4470_a_container_takes_the_battery_inventorys_most_trusted_reading()
    {
        var map = BluetoothReadings.Batteries(
        [
            new("gatt", "Headset", DeviceKind.Audio, 40, DeviceBatterySource.GattBatteryService, Headset),
            new("bt", "Headset", DeviceKind.Audio, 70, DeviceBatterySource.BluetoothProperty, Headset),
            new("pad", "Controller", DeviceKind.Gamepad, 90, DeviceBatterySource.GameController),
            new("bad", "Mouse", DeviceKind.Mouse, 255, DeviceBatterySource.DeviceContainer, Mouse),
        ]);

        Assert.Equal(70, map[Headset]);
        Assert.False(map.ContainsKey(Mouse));
        Assert.Single(map);
    }

    [Fact]
    public void S3252_only_a_connected_device_gets_its_containers_battery()
    {
        var batteries = new Dictionary<Guid, int> { [Headset] = 70, [Mouse] = 15 };

        var result = BluetoothReadings.WithBattery(
            [Device("Headset", connected: true), Device("Mouse", connected: false), Device("Pen", connected: true)],
            [Headset, Mouse, null],
            batteries);

        Assert.Equal(70, result[0].BatteryPercent);
        Assert.Null(result[1].BatteryPercent);
        Assert.Null(result[2].BatteryPercent);
    }

    [Fact]
    public void S3252_a_reading_already_in_hand_is_kept_where_the_property_has_none()
    {
        var result = BluetoothReadings.WithBattery([Device("Pen", connected: true, battery: 33)], [Mouse], new Dictionary<Guid, int>());

        Assert.Equal(33, result[0].BatteryPercent);
    }

    [Theory]
    [InlineData(20, true)]
    [InlineData(5, true)]
    [InlineData(21, false)]
    [InlineData(null, false)]
    [InlineData(255, false)]
    public void S3254_twenty_percent_and_below_is_low(int? percent, bool low)
        => Assert.Equal(low, BluetoothRules.IsLowBattery(percent));
}
