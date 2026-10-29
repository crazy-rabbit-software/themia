using Themia.Core.Localization;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Widget.Bluetooth.Tests;

/// <summary>
/// The Bluetooth list's ladder and the signal/address rules behind its new columns. [S-1254..S-1257]
/// </summary>
public class BluetoothLadderTests
{
    // Size-tier slots (card less a ~44 px header).
    private static readonly WidgetArea Xs = new(120, 76);
    private static readonly WidgetArea M = new(320, 196);
    private static readonly WidgetArea Wide = new(480, 116);
    private static readonly WidgetArea Tall = new(200, 356);
    private static readonly WidgetArea L = new(560, 376);
    private static readonly WidgetArea Xl = new(800, 516);

    private static BluetoothRowPlan Plan(WidgetArea area, int count) => BluetoothLayout.Plan(area, count);

    [Fact]
    public void S1254_XS_is_one_line_rows_without_the_battery()
    {
        var plan = Plan(Xs, 10);

        Assert.Equal(1, plan.Lines);
        Assert.False(plan.BatteryValue);
        Assert.Equal(1, plan.VisibleRows);
    }

    [Fact]
    public void S3253_the_battery_is_the_rows_value_from_a_180_column()
    {
        Assert.True(Plan(M, 10).BatteryValue);
        Assert.True(Plan(Tall, 10).BatteryValue);
        Assert.True(Plan(Wide, 10).BatteryValue);
        Assert.False(Plan(new WidgetArea(179, 300), 10).BatteryValue);
    }

    [Fact]
    public void S1255_rows_have_two_lines_from_a_regular_tile_both_ways()
    {
        // [S-6712] One line while the area is compact either way, two from regular both ways.
        Assert.Equal(1, Plan(Wide, 10).Lines);
        Assert.Equal(2, Plan(M, 10).Lines);
        Assert.Equal(2, Plan(Tall, 10).Lines);
        Assert.Equal(2, Plan(L, 10).Lines);
    }

    [Fact]
    public void S1254_The_list_takes_every_whole_row_of_the_area_less_the_bottom_inset()
    {
        Assert.Equal(3, Plan(Wide, 20).VisibleRows);
        Assert.Equal(3, Plan(M, 20).VisibleRows);
        Assert.Equal(7, Plan(L, 20).VisibleRows);
        Assert.Equal(10, Plan(Xl, 20).VisibleRows);
    }

    [Fact]
    public void S1254_XL_splits_a_long_list_into_two_columns_and_keeps_a_short_one_whole()
    {
        Assert.Equal(2, Plan(Xl, 14).Columns);
        Assert.Equal(1, Plan(Xl, 3).Columns);
        Assert.Equal(1, Plan(L, 14).Columns);
    }

    [Fact]
    public void S1254_An_unmeasured_area_starts_minimal()
    {
        var plan = BluetoothLayout.Plan(default, 5);

        Assert.Equal(1, plan.Lines);
        Assert.False(plan.BatteryValue);
        Assert.Equal(1, plan.Columns);
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

        var merged = Assert.Single(BluetoothRules.Arrange([classic, le], connectedOnly: false, maxCount: 10, locale: Locale.En));

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
