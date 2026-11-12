using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Widget.Bluetooth.Tests;

/// <summary>
/// The Bluetooth list's rows ([S-11310]) and the signal/address rules behind its second line. [S-1255..S-1257]
/// </summary>
public class BluetoothLadderTests
{
    [Fact]
    public void S11310_the_rows_and_lines_are_the_configs_four_one_line_rows_for_a_new_list()
    {
        Assert.Equal(new BluetoothRowPlan(1, 4), BluetoothLayout.Plan(new BluetoothConfig()));
        Assert.Equal(new BluetoothRowPlan(2, 20), BluetoothLayout.Plan(new BluetoothConfig { Rows = 40, Lines = 2 }));
    }

    [Fact]
    public void S11310_the_height_follows_the_rows_220_to_520_wide_and_dragging_sets_them()
    {
        var kind = Themia.Sdk.ModuleCatalog.Of(new Themia.Sdk.WidgetId("bluetooth"));
        var config = new BluetoothConfig();
        var sizing = kind.Sizing(config);

        Assert.Equal(36 + (4 * 28) + 12, sizing.Natural.Height);
        Assert.Equal(36 + (4 * 40) + 12, kind.Sizing(new BluetoothConfig { Lines = 2 }).Natural.Height);
        Assert.Equal((220d, 520d), (sizing.Min.Width, sizing.Max.Width));

        var six = Assert.IsType<BluetoothConfig>(kind.SizeSets!(config, sizing.Natural with { Height = 36 + (6 * 28) + 12 }));
        Assert.Equal(6, six.Rows);
        Assert.Equal(4, config.Rows);
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
        Assert.Equal(0xC0288D123456UL, BluetoothRules.ParseAddress("c0:28:8d:12:34:56"));
        Assert.Equal(0xC0288D123456UL, BluetoothRules.ParseAddress("C0288D123456"));
        Assert.Null(BluetoothRules.ParseAddress("c0:28:8d:12:34"));
        Assert.Null(BluetoothRules.ParseAddress("zz:28:8d:12:34:56"));
        Assert.Null(BluetoothRules.ParseAddress(null));

        Assert.Equal(0xC0288D123456UL, BluetoothRules.AddressFromId("BluetoothLE#BluetoothLE00:1a:7d:da:71:13-c0:28:8d:12:34:56"));
        Assert.Null(BluetoothRules.AddressFromId("BluetoothLE#nothing-"));
        Assert.Null(BluetoothRules.AddressFromId("fixture:Surface Pen"));
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
        Assert.Equal("2 connected", BluetoothRules.ConnectedSummary(2, Locale.En));
        Assert.Null(BluetoothRules.ConnectedSummary(0, Locale.En));
    }
}
