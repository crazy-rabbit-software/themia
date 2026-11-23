using Themia.Core.Widgets;
using Themia.Sdk.Items;
using Themia.Sdk;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Sdk.Devices;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.DeviceBatteries.Tests;

/// <summary>[S-10000]..[S-10006] The Device batteries widget's rules: the fold, the order, the threshold, the rows and the menu.</summary>
public sealed class DeviceBatteriesTests
{
    private static readonly Guid Headset = Guid.Parse("6b1c2d4e-0000-4000-8000-000000000001");
    private static readonly Guid Pad1 = Guid.Parse("6b1c2d4e-0000-4000-8000-000000000002");
    private static readonly Guid Pad2 = Guid.Parse("6b1c2d4e-0000-4000-8000-000000000003");

    private static DeviceBatteryReading Reading(string id, string name, int percent, DeviceBatterySource source = DeviceBatterySource.BluetoothProperty, Guid? container = null, bool? charging = null, DeviceKind kind = DeviceKind.Unknown)
        => new(id, name, kind, percent, source, container, charging);

    [Fact]
    public void S10000_a_clone_is_a_config_of_its_own_with_every_setting()
    {
        var config = new DeviceBatteriesConfig { LowThreshold = 30, ShowCharging = false };
        var clone = Assert.IsType<DeviceBatteriesConfig>(config.Clone());

        Assert.NotSame(config, clone);
        Assert.Equal(30, clone.LowThreshold);
        Assert.False(clone.ShowCharging);
    }

    [Fact]
    public void S10001_rows_are_ordered_lowest_battery_first_then_by_name()
    {
        var rows = DeviceBatteriesRules.Arrange(
            [Reading("a", "Keyboard", 46), Reading("b", "mouse", 12), Reading("c", "Headset", 80), Reading("d", "Apple Pen", 46)],
            showCharging: true, max: 30);

        Assert.Equal(["mouse", "Apple Pen", "Keyboard", "Headset"], rows.Select(r => r.Name));
    }

    [Fact]
    public void S10001_two_identical_reads_give_one_order_and_the_ceiling_holds()
    {
        var readings = Enumerable.Range(0, 40).Select(i => Reading($"id{i}", "Same", 50)).ToList();

        var first = DeviceBatteriesRules.Arrange(readings, true, DeviceBatteriesConfig.MaxDevices);
        var again = DeviceBatteriesRules.Arrange(Enumerable.Reverse(readings), true, DeviceBatteriesConfig.MaxDevices);

        // Same name and no container: one device as far as the fold knows.
        Assert.Single(first);
        Assert.Equal(first, again);

        var many = DeviceBatteriesRules.Arrange(Enumerable.Range(0, 40).Select(i => Reading($"id{i}", $"Device {i:D2}", 50, container: Guid.NewGuid())), true, DeviceBatteriesConfig.MaxDevices);
        Assert.Equal(DeviceBatteriesConfig.MaxDevices, many.Count);
    }

    [Fact]
    public void S10002_a_device_seen_through_two_paths_is_one_row_with_the_most_trusted_level()
    {
        var rows = DeviceBatteriesRules.Arrange(
        [
            Reading("container", "WH-1000XM4", 70, DeviceBatterySource.DeviceContainer, Headset, charging: false),
            Reading("bt", "WH-1000XM4", 80, DeviceBatterySource.BluetoothProperty, Headset, kind: DeviceKind.Audio),
        ], true, 30);

        var row = Assert.Single(rows);
        Assert.Equal(80, row.Percent);
        Assert.Equal(DeviceBatterySource.BluetoothProperty, row.Source);
        Assert.Equal(DeviceKind.Audio, row.Kind);
        Assert.False(row.Charging);
    }

    [Fact]
    public void S10002_a_controller_folds_by_name_where_one_path_has_no_container()
    {
        var rows = DeviceBatteriesRules.Arrange(
        [
            Reading("controller:pad", "Xbox Wireless Controller", 63, DeviceBatterySource.GameController, charging: true, kind: DeviceKind.Gamepad),
            Reading("bt:pad", "xbox wireless controller", 64, DeviceBatterySource.BluetoothProperty, Pad1),
        ], true, 30);

        var row = Assert.Single(rows);
        Assert.Equal(64, row.Percent);
        Assert.True(row.Charging);
        Assert.Equal(DeviceKind.Gamepad, row.Kind);
    }

    [Fact]
    public void S10002_two_containers_are_two_devices_and_nameless_ones_never_fold()
    {
        var twins = DeviceBatteriesRules.Arrange(
            [Reading("1", "Xbox Wireless Controller", 40, container: Pad1), Reading("2", "Xbox Wireless Controller", 90, container: Pad2)], true, 30);
        var nameless = DeviceBatteriesRules.Arrange(
            [Reading("1", " ", 40, DeviceBatterySource.GattBatteryService), Reading("2", "", 90, DeviceBatterySource.GattBatteryService)], true, 30);

        Assert.Equal(2, twins.Count);
        Assert.Equal(2, nameless.Count);
        Assert.All(nameless, row => Assert.Equal(string.Empty, row.Name));
    }

    [Fact]
    public void S10002_a_name_is_one_line_of_at_most_64_characters()
    {
        Assert.Equal("Surface Slim Pen 2", DeviceBatteriesRules.DisplayName("  Surface\nSlim \t Pen 2 "));
        Assert.Equal(DeviceBatteriesRules.MaxName, DeviceBatteriesRules.DisplayName(new string('x', 100)).Length);
        Assert.Equal(string.Empty, DeviceBatteriesRules.DisplayName(null));
    }

    [Fact]
    public void S10003_S16665_at_or_below_the_threshold_the_bar_is_critical()
    {
        var low = DeviceBatteriesItems.Item(new DeviceBatteryRow("a", "Mouse", DeviceKind.Mouse, 20, null, DeviceBatterySource.DeviceContainer), 20, Locale.En);
        var fine = DeviceBatteriesItems.Item(new DeviceBatteryRow("b", "Mouse", DeviceKind.Mouse, 21, null, DeviceBatterySource.DeviceContainer), 20, Locale.En);

        Assert.Equal(ColourRole.Critical, low.Bar!.Role);
        Assert.Equal(ColourRole.Accent, fine.Bar!.Role);
        Assert.Equal(("20", "%", (double?)0.2), (low.End!.Value, low.End.Unit, (double?)low.Bar!.To));
    }

    [Fact]
    public void S10003_a_threshold_off_the_offered_steps_is_repaired_to_the_default()
    {
        var config = new DeviceBatteriesConfig { LowThreshold = 17 };

        Assert.Equal(DeviceBatteriesConfig.DefaultThreshold, config.EffectiveThreshold);
        Assert.NotEmpty(config.Repair());
        Assert.Equal(DeviceBatteriesConfig.DefaultThreshold, config.LowThreshold);
        Assert.Empty(new DeviceBatteriesConfig { LowThreshold = 30 }.Repair());
    }

    [Fact]
    public void S10004_charging_devices_are_left_out_only_when_switched_off()
    {
        DeviceBatteryReading[] readings = [Reading("a", "Pad", 60, charging: true, container: Pad1), Reading("b", "Mouse", 30, container: Pad2)];

        Assert.Equal(2, DeviceBatteriesRules.Arrange(readings, true, 30).Count);
        Assert.Equal("Mouse", Assert.Single(DeviceBatteriesRules.Arrange(readings, false, 30)).Name);
    }

    [Fact]
    public void S10001_S16665_an_item_is_its_name_level_kind_glyph_and_charging_word()
    {
        var item = DeviceBatteriesItems.Item(new DeviceBatteryRow("pad", "Xbox Wireless Controller", DeviceKind.Gamepad, 64, true, DeviceBatterySource.GameController), 20, Locale.En);

        Assert.Equal("Xbox Wireless Controller", item.Title);
        Assert.Equal(DeviceItems.Glyph(DeviceKind.Gamepad), item.Lead!.Glyph);
        Assert.Equal("Controller · Charging", item.Subtitle);

        var none = DeviceBatteriesItems.Empty(DeviceBatteryStatus.Ok, Locale.En);
        Assert.True(none.Absent);
        Assert.Equal(DeviceBatteriesStr.DeviceBatteries_Empty.Text(Locale.En), none.Subtitle);
    }

    [Fact]
    public void S10001_every_kind_has_its_own_glyph()
    {
        var glyphs = Enum.GetValues<DeviceKind>().Select(DeviceItems.Glyph).ToList();
        Assert.Equal(glyphs.Count, glyphs.Distinct().Count());
    }

    [Fact]
    public void S10005_an_items_menu_opens_bluetooth_and_devices_and_refreshes()
    {
        var opened = 0;
        var refreshed = 0;
        var entries = DeviceBatteriesItems.Menu(() => opened++, () => refreshed++).Where(e => !e.IsSeparator).ToList();

        Assert.Equal([DeviceBatteriesStr.DeviceBatteries_OpenSettings.Text(), Str.Common_Refresh.Text()], entries.Select(e => e.Text));
        entries[0].Action!();
        entries[1].Action!();
        Assert.Equal((1, 1), (opened, refreshed));
    }

    [Fact]
    public void S10006_the_menu_is_the_threshold_and_the_charging_switch_and_saves()
    {
        var saves = 0;
        var config = new DeviceBatteriesConfig();
        var menu = new WidgetMenuLines(() => saves++);
        DeviceBatteriesMenu.Lines(menu, config);

        Assert.Equal(
            [DeviceBatteriesStr.DeviceBatteries_Settings_Low.Text(), DeviceBatteriesStr.DeviceBatteries_Settings_Charging.Text(), Str.Items_Parts.Text(), Str.Items_Gap.Text(), Str.Kit_Rows.Text()],
            menu.Lines.Where(l => !l.IsSeparator).Select(l => l.Text));

        menu.Lines[1].Action!();
        Assert.False(config.ShowCharging);
        Assert.Equal(1, saves);
    }

    [Fact]
    public void S11028_S16665_its_rows_of_device_items_are_its_natural_height_and_every_cell_between()
    {
        var kind = ModuleCatalog.Of(new WidgetId("deviceBatteries"));
        Assert.Equal(3 * 60, kind.SizingOf().Natural.Height);
        Assert.Equal(60, kind.SizingOf().Min.Height);
        Assert.Equal((2 * 60) + 10, kind.SizingOf(new DeviceBatteriesConfig { Rows = 2, Gap = 1 }).Natural.Height);
        Assert.Equal(6, ((DeviceBatteriesConfig)new DeviceBatteriesConfig { Rows = 6 }.Clone()).Rows);
    }
}
