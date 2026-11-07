using Themia.Sdk.Devices;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-4471] How the host's battery source reads what Windows says of a peripheral.</summary>
public sealed class DeviceBatteryRulesTests
{
    [Theory]
    [InlineData("Input.Mouse", DeviceKind.Mouse)]
    [InlineData("Input.Keyboard", DeviceKind.Keyboard)]
    [InlineData("Input.Gaming", DeviceKind.Gamepad)]
    [InlineData("Input.Pen", DeviceKind.Pen)]
    [InlineData("Audio.Headphone", DeviceKind.Audio)]
    [InlineData("Phone", DeviceKind.Phone)]
    public void S4471_a_category_names_the_kind_part_by_part(string category, DeviceKind kind)
        => Assert.Equal(kind, DeviceKinds.FromCategories([category]));

    [Fact]
    public void S4471_a_more_specific_category_wins_over_audio_and_none_is_no_answer()
    {
        Assert.Equal(DeviceKind.Gamepad, DeviceKinds.FromCategories(["Audio.Headset", "Input.Gaming"]));
        Assert.Equal(DeviceKind.Audio, DeviceKinds.FromCategories(["Communication", "Audio.Headset"]));
        Assert.Null(DeviceKinds.FromCategories(["Other", "Openwork", "", null]));
        Assert.Null(DeviceKinds.FromCategories(null));
    }

    [Fact]
    public void S4471_charging_is_a_zero_or_one_and_anything_else_no_answer()
    {
        Assert.True(DeviceBatteryRules.ParseCharging((byte)1));
        Assert.False(DeviceBatteryRules.ParseCharging((byte)0));
        Assert.True(DeviceBatteryRules.ParseCharging(true));
        Assert.Null(DeviceBatteryRules.ParseCharging((byte)2));
        Assert.Null(DeviceBatteryRules.ParseCharging("1"));
        Assert.Null(DeviceBatteryRules.ParseCharging(null));
    }

    [Theory]
    [InlineData(500, 1000, 50)]
    [InlineData(1, 3, 33)]
    [InlineData(2, 3, 67)]
    [InlineData(1200, 1000, 100)]
    [InlineData(0, 1000, 0)]
    public void S4471_a_controllers_capacity_is_a_rounded_percentage(int remaining, int full, int percent)
        => Assert.Equal(percent, DeviceBatteryRules.Percent(remaining, full));

    [Fact]
    public void S4471_a_report_without_both_capacities_is_no_reading()
    {
        Assert.Null(DeviceBatteryRules.Percent(null, 1000));
        Assert.Null(DeviceBatteryRules.Percent(500, null));
        Assert.Null(DeviceBatteryRules.Percent(500, 0));
        Assert.Null(DeviceBatteryRules.Percent(-1, 1000));
    }

    [Fact]
    public void S4471_a_container_id_is_windows_braced_guid_and_the_empty_one_is_none()
    {
        var id = Guid.NewGuid();

        Assert.Equal(id, DeviceBatteryRules.ParseContainer(id.ToString("B")));
        Assert.Equal(id, DeviceBatteryRules.ParseContainer(id));
        Assert.Null(DeviceBatteryRules.ParseContainer(Guid.Empty));
        Assert.Null(DeviceBatteryRules.ParseContainer(Guid.Empty.ToString("B")));
        Assert.Null(DeviceBatteryRules.ParseContainer("BTHENUM\\DEV_8099E7CDCB65"));
        Assert.Null(DeviceBatteryRules.ParseContainer(null));
    }

    [Fact]
    public void S3252_a_battery_property_outside_0_to_100_or_not_a_whole_number_is_no_reading()
    {
        Assert.Equal(80, DeviceBatteryRules.ParsePercent((byte)80));
        Assert.Null(DeviceBatteryRules.ParsePercent((byte)255));
        Assert.Null(DeviceBatteryRules.ParsePercent(50.0));
        Assert.Null(DeviceBatteryRules.ParsePercent(-1));
    }
}
