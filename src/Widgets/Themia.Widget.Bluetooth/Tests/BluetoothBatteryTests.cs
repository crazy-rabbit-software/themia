using Themia.Core.Widgets;
using Xunit;

namespace Themia.Widget.Bluetooth.Tests;

/// <summary>
/// [S-3252]..[S-3254] Where a paired device's battery comes from (the Windows device property, by
/// container) and how the row shows it.
/// </summary>
public class BluetoothBatteryTests
{
    private static readonly Guid Headset = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Mouse = new("22222222-2222-2222-2222-222222222222");

    private static BluetoothDeviceEntry Device(string name, bool connected, int? battery = null)
        => new(name, name, connected, BluetoothDeviceKind.Unknown, battery);

    [Fact]
    public void S3252_the_property_is_a_byte_percentage()
    {
        Assert.Equal(80, BluetoothRules.ParseBattery((byte)80));
        Assert.Equal(0, BluetoothRules.ParseBattery((byte)0));
        Assert.Equal(100, BluetoothRules.ParseBattery(100));
        Assert.Equal(55, BluetoothRules.ParseBattery(55u));
    }

    [Fact]
    public void S3252_an_unknown_or_out_of_spec_reading_is_no_battery()
    {
        Assert.Null(BluetoothRules.ParseBattery(null));
        Assert.Null(BluetoothRules.ParseBattery((byte)255));
        Assert.Null(BluetoothRules.ParseBattery(101));
        Assert.Null(BluetoothRules.ParseBattery(-1));
        Assert.Null(BluetoothRules.ParseBattery("80"));
        Assert.Null(BluetoothRules.ParseBattery(80.0));
    }

    [Fact]
    public void S3252_a_container_takes_its_highest_valid_reading()
    {
        var map = BluetoothRules.BatteryByContainer(
        [
            (Headset, (byte)40),
            (Headset, (byte)70),
            (Headset, (byte)255),
            (Mouse, null),
            (null, (byte)90),
        ]);

        Assert.Equal(70, map[Headset]);
        Assert.False(map.ContainsKey(Mouse));
        Assert.Single(map);
    }

    [Fact]
    public void S3252_only_a_connected_device_gets_its_containers_battery()
    {
        var batteries = new Dictionary<Guid, int> { [Headset] = 70, [Mouse] = 15 };

        var result = BluetoothRules.WithBattery(
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
        var result = BluetoothRules.WithBattery([Device("Pen", connected: true, battery: 33)], [Mouse], new Dictionary<Guid, int>());

        Assert.Equal(33, result[0].BatteryPercent);
    }

    [Fact]
    public void S3253_a_small_row_carries_the_battery_percentage()
    {
        var small = new WidgetArea(200, 116);
        var plan = BluetoothLayout.Plan(small, small.Height - 14, 4);

        Assert.True(plan.BatteryColumn);
        Assert.False(plan.StatusWord);
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
