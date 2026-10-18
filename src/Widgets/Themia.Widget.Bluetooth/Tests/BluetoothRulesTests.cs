using Themia.Core.Localization;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Widget.Bluetooth.Tests;

/// <summary>
/// The rules a Bluetooth list is drawn by. [S-240] [S-241] [S-242] [S-243]
/// <para>
/// The merge is the one with teeth: a headset that speaks both Classic and LE is enumerated twice
/// under one name, and a list that double-counts the only device on it reads as broken.
/// </para>
/// </summary>
public class BluetoothRulesTests
{
    private static BluetoothDeviceEntry Device(
        string name,
        bool connected = false,
        int? battery = null,
        string id = "x") => new(name, id, connected, BluetoothDeviceKind.Unknown, battery);

    [Theory]
    [InlineData("WH-1000XM4 Headphones", BluetoothDeviceKind.Audio)]
    [InlineData("Galaxy Buds Pro", BluetoothDeviceKind.Audio)]
    [InlineData("MX Master 3 Mouse", BluetoothDeviceKind.Mouse)]
    [InlineData("Keychron K2", BluetoothDeviceKind.Keyboard)]
    [InlineData("Pixel 8", BluetoothDeviceKind.Phone)]
    [InlineData("Xbox Wireless Controller", BluetoothDeviceKind.Gamepad)]
    [InlineData("Surface Slim Pen 2", BluetoothDeviceKind.Pen)]
    [InlineData("Galaxy Watch6", BluetoothDeviceKind.Watch)]
    [InlineData("HC-05", BluetoothDeviceKind.Unknown)]
    public void S240_A_device_gets_its_icon_from_its_name(string name, BluetoothDeviceKind expected)
        => Assert.Equal(expected, BluetoothRules.Classify(name));

    [Fact]
    public void S240_Classification_ignores_case_and_a_missing_name()
    {
        Assert.Equal(BluetoothDeviceKind.Audio, BluetoothRules.Classify("AIRPODS PRO"));
        Assert.Equal(BluetoothDeviceKind.Unknown, BluetoothRules.Classify(null));
        Assert.Equal(BluetoothDeviceKind.Unknown, BluetoothRules.Classify("   "));
    }

    [Fact]
    public void S241_A_device_that_advertises_no_name_still_gets_a_readable_row()
    {
        Assert.Equal("Unknown device", BluetoothRules.DisplayName(null, Locale.En));
        Assert.Equal("Unknown device", BluetoothRules.DisplayName("  \t ", Locale.En));
    }

    [Fact]
    public void S241_A_name_off_the_radio_is_collapsed_to_one_line_and_capped()
    {
        // The name is somebody else's string. A row is one line tall whatever arrives in it.
        Assert.Equal("Joe's Buds", BluetoothRules.DisplayName("  Joe's\r\n  Buds ", Locale.En));
        Assert.Equal(64, BluetoothRules.DisplayName(new string('n', 200), Locale.En).Length);
    }

    [Fact]
    public void S242_A_battery_the_device_will_not_report_is_no_column_at_all()
    {
        Assert.Null(BluetoothRules.BatteryLabel(null, Locale.En));

        // 255 is the Battery Service's own "unknown", and anything past 100 is out of spec.
        Assert.Null(BluetoothRules.BatteryLabel(255, Locale.En));
        Assert.Null(BluetoothRules.BatteryLabel(-1, Locale.En));
        Assert.Equal("72%", BluetoothRules.BatteryLabel(72, Locale.En));
        Assert.Equal("0%", BluetoothRules.BatteryLabel(0, Locale.En));
    }

    [Fact]
    public void S243_The_same_device_seen_by_both_radios_is_one_row()
    {
        var arranged = BluetoothRules.Arrange(
            [
                Device("WH-1000XM4", connected: true, id: "classic"),
                Device("WH-1000XM4", connected: false, battery: 85, id: "le"),
            ],
            connectedOnly: false,
            maxCount: 10, locale: Locale.En);

        var only = Assert.Single(arranged);

        // The connection came from the Classic entry and the battery from the LE one: the merge
        // keeps the richer answer from each rather than whichever was enumerated first.
        Assert.True(only.Connected);
        Assert.Equal(85, only.BatteryPercent);
    }

    [Fact]
    public void S243_Nameless_devices_are_not_folded_into_each_other()
    {
        // They all normalise to the same string. Merging on that would hide three devices behind
        // one row and report the wrong count.
        var arranged = BluetoothRules.Arrange(
            [Device(string.Empty, id: "a"), Device(string.Empty, id: "b"), Device("   ", id: "c")],
            connectedOnly: false,
            maxCount: 10, locale: Locale.En);

        Assert.Equal(3, arranged.Count);
    }

    [Fact]
    public void S243_Connected_devices_come_first_and_the_rest_are_alphabetical()
    {
        var arranged = BluetoothRules.Arrange(
            [
                Device("Zeta Keyboard"),
                Device("Alpha Mouse"),
                Device("Omega Buds", connected: true),
            ],
            connectedOnly: false,
            maxCount: 10, locale: Locale.En);

        Assert.Equal(["Omega Buds", "Alpha Mouse", "Zeta Keyboard"], arranged.Select(d => d.Name));
    }

    [Fact]
    public void S243_The_order_does_not_change_between_two_identical_polls()
    {
        // A list that reshuffles every thirty seconds cannot be read.
        BluetoothDeviceEntry[] devices =
        [
            Device("Beta", connected: true),
            Device("Alpha", connected: true),
            Device("Gamma"),
        ];

        var first = BluetoothRules.Arrange(devices, connectedOnly: false, maxCount: 10, locale: Locale.En);
        var second = BluetoothRules.Arrange(devices.Reverse(), connectedOnly: false, maxCount: 10, locale: Locale.En);

        Assert.Equal(first.Select(d => d.Name), second.Select(d => d.Name));
    }

    [Fact]
    public void S243_Connected_only_drops_the_rest_and_the_row_count_is_honoured()
    {
        var connectedOnly = BluetoothRules.Arrange(
            [Device("A", connected: true), Device("B")],
            connectedOnly: true,
            maxCount: 10, locale: Locale.En);

        Assert.Equal(["A"], connectedOnly.Select(d => d.Name));

        var capped = BluetoothRules.Arrange(
            [Device("A"), Device("B"), Device("C")],
            connectedOnly: false,
            maxCount: 2, locale: Locale.En);

        Assert.Equal(2, capped.Count);
    }

    [Fact]
    public void S244_Every_empty_list_says_which_empty_list_it_is()
    {
        // A blank tile is a defect: four of these mean four different things to do about it.
        Assert.Equal("This PC has no Bluetooth radio", BluetoothRules.StatusMessage(BluetoothStatus.NoRadio, Locale.En));
        Assert.Equal("Bluetooth is turned off", BluetoothRules.StatusMessage(BluetoothStatus.RadioOff, Locale.En));
        Assert.Equal("No paired devices", BluetoothRules.StatusMessage(BluetoothStatus.Ok, Locale.En));
        Assert.Equal("Reading paired devices", BluetoothRules.StatusMessage(BluetoothStatus.Loading, Locale.En));

        Assert.Equal(
            "Windows has not granted access to the radios",
            BluetoothRules.StatusMessage(BluetoothStatus.PermissionDenied, Locale.En));

        Assert.Equal("Could not read the paired devices", BluetoothRules.StatusMessage(BluetoothStatus.Failed, Locale.En));
    }

    [Fact]
    public void S242_The_connection_state_is_carried_by_words_as_well_as_by_a_dot()
    {
        Assert.Equal("Connected", BluetoothRules.ConnectionLabel(true, Locale.En));
        Assert.Equal("Paired", BluetoothRules.ConnectionLabel(false, Locale.En));
    }
}
