using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Widget.Bluetooth;
using Xunit;

namespace Themia.Widget.Bluetooth.Tests;

/// <summary>
/// The Bluetooth list's rows ([S-11310]) and the signal/address rules behind its second line. [S-1255..S-1257]
/// </summary>
public class BluetoothLadderTests
{
    [Fact]
    public void S11310_S16660_its_rows_of_device_items_are_its_natural_height_and_every_cell_between()
    {
        var kind = Themia.Sdk.ModuleCatalog.Of(new Themia.Sdk.WidgetId("bluetooth"));
        var sizing = kind.Sizing(new BluetoothConfig());

        Assert.Equal(3 * 60, sizing.Natural.Height);
        Assert.Equal(20, KitClamp(40));
        Assert.Equal((220d, Themia.Sdk.SizeStretch.Ceiling), (sizing.Min.Width, sizing.Max.Width)); // relaxed, [S-12411]: no practical Max

        // [S-14500] One item at least, every cell taller too; [S-16661] the gap between two.
        Assert.Equal(60, sizing.Min.Height);
        Assert.Equal((2 * 60) + 10, kind.Sizing(new BluetoothConfig { Rows = 2, Gap = 1 }).Natural.Height);
    }

    private static int KitClamp(int rows)
    {
        var config = new BluetoothConfig { Rows = rows };
        config.Repair();
        return config.Rows;
    }

    [Fact]
    public void S16660_a_device_is_an_item_its_battery_the_reading_and_the_bar()
    {
        var config = new BluetoothConfig();
        var pen = BluetoothItems.Item(new BluetoothDeviceEntry("Slim Pen", "pen", true, BluetoothDeviceKind.Pen, 18), config.Shows, Locale.En);
        Assert.Equal(("Slim Pen", "18", "Pen · Connected", Themia.Sdk.Kit.ColourRole.Critical), (pen.Title, pen.End!.Value, pen.Subtitle, pen.Bar!.Role));
        Assert.Equal(0.18, pen.Bar!.To, 6);

        var phone = BluetoothItems.Item(new BluetoothDeviceEntry("Pixel", "phone", false, BluetoothDeviceKind.Phone, null), config.Shows, Locale.En);
        Assert.True(phone.Absent);
        // [S-16670] No battery is no percentage and no bar: not drawn, never greyed out.
        Assert.Equal(("Phone · Paired", (Themia.Sdk.Items.ItemBar?)null, Themia.Sdk.Kit.ColourRole.TextSecondary), (phone.Subtitle, phone.Bar, phone.Lead!.Role));

        var noBattery = BluetoothItems.Item(new BluetoothDeviceEntry("Pixel", "phone", true, BluetoothDeviceKind.Phone, 50), new BluetoothConfig { HiddenParts = [Themia.Sdk.Items.ItemPart.End, Themia.Sdk.Items.ItemPart.Bar] }.Shows, Locale.En);
        Assert.Null(noBattery.End);
        Assert.Null(noBattery.Bar);

        var off = BluetoothItems.Empty(BluetoothStatus.RadioOff, Locale.En);
        Assert.True(off.Absent);
        Assert.Equal(BluetoothRules.StatusMessage(BluetoothStatus.RadioOff, Locale.En), off.Subtitle);
    }

    [Theory]
    [InlineData(-45, "-45 dBm", 4)]
    [InlineData(-60, "-60 dBm", 4)]
    [InlineData(-61, "-61 dBm", 3)]
    [InlineData(-85, "-85 dBm", 1)]
    [InlineData(-99, "-99 dBm", 0)]
    public void S1256_Signal_reads_as_dBm_and_bars(int dbm, string label, int bars)
    {
        Assert.Equal(label, BluetoothRules.SignalLabel(dbm));
        Assert.Equal(bars, BluetoothRules.SignalBars(dbm));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-128)]
    [InlineData(20)]
    public void S1256_No_reading_or_an_implausible_one_is_no_column(int? dbm)
    {
        Assert.Null(BluetoothRules.SignalLabel(dbm));
        Assert.Null(BluetoothRules.SignalBars(dbm));
    }

    [Fact]
    public void S1256_Addresses_parse_from_the_property_and_the_endpoint_id()
    {
        Assert.Equal(0xC0288D123456UL, BluetoothReadings.ParseAddress("c0:28:8d:12:34:56"));
        Assert.Equal(0xC0288D123456UL, BluetoothReadings.ParseAddress("C0288D123456"));
        Assert.Null(BluetoothReadings.ParseAddress("c0:28:8d:12:34"));
        Assert.Null(BluetoothReadings.ParseAddress("zz:28:8d:12:34:56"));
        Assert.Null(BluetoothReadings.ParseAddress(null));

        Assert.Equal(0xC0288D123456UL, BluetoothReadings.AddressFromId("BluetoothLE#BluetoothLE00:1a:7d:da:71:13-c0:28:8d:12:34:56"));
        Assert.Null(BluetoothReadings.AddressFromId("BluetoothLE#nothing-"));
        Assert.Null(BluetoothReadings.AddressFromId("fixture:Surface Pen"));
    }

    [Fact]
    public void S1256_A_dual_mode_device_keeps_the_LE_entrys_signal()
    {
        var classic = new BluetoothDeviceEntry("Headset", "a", true, BluetoothDeviceKind.Audio, null);
        var le = new BluetoothDeviceEntry("Headset", "b", true, BluetoothDeviceKind.Audio, 70, -62);

        var merged = Assert.Single(BluetoothRules.Arrange([classic, le], connectedOnly: false, locale: Locale.En));

        Assert.Equal(-62, merged.SignalDbm);
        Assert.Equal(70, merged.BatteryPercent);
    }

    [Fact]
    public void S1255_The_second_line_lists_only_what_was_reported_and_enabled()
    {
        var device = new BluetoothDeviceEntry("Mouse", "id", true, BluetoothDeviceKind.Mouse, 45, null);

        Assert.Equal("Mouse · Connected · 45%", BluetoothRules.DetailLine(device, true, true, true, Locale.En));
        Assert.Equal("Mouse", BluetoothRules.DetailLine(device, false, false, true, Locale.En));
    }
}
