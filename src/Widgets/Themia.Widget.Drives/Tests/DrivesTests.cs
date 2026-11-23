using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Tests;
using Themia.Core.Widgets;
using Themia.Sdk.Items;
using Themia.Sdk;
using Themia.Sdk.Devices;
using Xunit;

namespace Themia.Widget.Drives.Tests;

/// <summary>
/// [S-16650]..[S-16656] The drive lists, one per kind (the PC's drives, the USB sticks, the network drives), each the device
/// list's one shape: its drives filtered and in the user's order, the block gap in its sizing; a placed Removable drives
/// widget becoming the USB sticks' list in place.
/// </summary>
public sealed class DrivesTests
{
    private const ulong Gb = 1024UL * 1024 * 1024;

    private static readonly DriveEntry System = new("C:\\", VolumeKind.Fixed, DriveState.Ready, "System", "NTFS", 931 * Gb, 419 * Gb);
    private static readonly DriveEntry Stick = new("E:\\", VolumeKind.Removable, DriveState.Ready, "STICK", "exFAT", 58 * Gb, 31 * Gb);
    private static readonly DriveEntry External = new("G:\\", VolumeKind.Fixed, DriveState.Ready, "Backup", "NTFS", 1863 * Gb, 640 * Gb, Hotplug: true);
    private static readonly DriveEntry Reader = new("F:\\", VolumeKind.Removable, DriveState.NoMedia);
    private static readonly DriveEntry Disc = new("H:\\", VolumeKind.Optical, DriveState.Ready, "HOLIDAY", "UDF", 4 * Gb, 0);
    private static readonly DriveEntry Share = new("Z:\\", VolumeKind.Network, DriveState.Disconnected, Share: @"\\nas\media");
    private static readonly DriveEntry Projects = new("W:\\", VolumeKind.Network, DriveState.Ready, string.Empty, "NTFS", 100 * Gb, 50 * Gb, @"\\server\projects");

    private static readonly DriveEntry[] Walk = [System, Stick, Reader, External, Disc, Share, Projects];

    private static string Roots(IEnumerable<DriveEntry> drives) => string.Join(' ', drives.Select(d => d.Root[..1]));

    [Fact]
    public void S16650_each_list_shows_its_kind_by_letter_an_empty_reader_left_out()
    {
        Assert.Equal("C H", Roots(DrivesRules.Arrange(Walk, DriveListKinds.Drives, new DrivesConfig())));
        Assert.Equal("E G", Roots(DrivesRules.Arrange(Walk, DriveListKinds.UsbSticks, new UsbSticksConfig())));
        Assert.Equal("W Z", Roots(DrivesRules.Arrange(Walk, DriveListKinds.NetworkDrives, new NetworkDrivesConfig())));
    }

    [Fact]
    public void S16651_S16652_drives_are_hidden_and_put_in_the_users_order()
    {
        var config = new NetworkDrivesConfig { Order = ["Z:\\"] };
        Assert.Equal("Z W", Roots(DrivesRules.Arrange(Walk, DriveListKinds.NetworkDrives, config)));

        config.Hidden = ["z:\\"];
        Assert.Equal("W", Roots(DrivesRules.Arrange(Walk, DriveListKinds.NetworkDrives, config)));
    }

    [Fact]
    public void S16654_its_sizing_is_its_rows_of_device_items_and_the_gap_between()
    {
        foreach (var id in new[] { "drives", "usbSticks", "networkDrives" })
        {
            var kind = ModuleCatalog.Of(new WidgetId(id));
            Assert.Equal((300.0, 240.0), (kind.SizingOf().Natural.Width, kind.SizingOf().Natural.Height));
            Assert.Equal(80, kind.SizingOf().Min.Height);
        }

        Assert.Equal(180, ModuleCatalog.Of(new WidgetId("usbSticks")).SizingOf(new UsbSticksConfig { Rows = 2, Gap = 2 }).Natural.Height);
    }

    [Fact]
    public void S16655_its_config_is_repaired_and_copied_whole_as_its_own_kind()
    {
        var config = new UsbSticksConfig { Hidden = ["E:\\", "nonsense", "e:"], Order = ["Z:\\"], Rows = 40, Gap = 1 };
        Assert.Equal(3, config.Repair().Count);
        Assert.Equal(["E:\\"], config.Hidden);
        Assert.Equal(20, config.Rows);

        var copy = Assert.IsType<UsbSticksConfig>(config.Clone());
        Assert.Equal(config.Hidden, copy.Hidden);
        Assert.Equal(config.Order, copy.Order);
        Assert.Equal((config.Gap, config.Rows), (copy.Gap, copy.Rows));
        Assert.NotSame(config.Hidden, copy.Hidden);
    }

    [Fact]
    public void S16656_a_placed_removable_drives_widget_becomes_the_usb_sticks_list_in_place()
    {
        var config = Assert.IsType<UsbSticksConfig>(ConfigJson.Read("RemovableDrives", """{ "kind": "removableDrives", "rows": 5 }"""));

        Assert.Equal((5, 0), (config.Rows, config.Gap));
        Assert.Equal("E G", Roots(DrivesRules.Arrange(Walk, DriveListKinds.UsbSticks, config)));
    }

    [Fact]
    public void S16650_a_shown_drive_is_a_device_item()
    {
        var items = DrivesRules.Arrange(Walk, DriveListKinds.NetworkDrives, new NetworkDrivesConfig()).Select(d => DeviceItems.Drive(Locale.En, d)).ToList();
        Assert.Equal(["projects (\\\\server) (W:)", "media (\\\\nas) (Z:)"], items.Select(i => i.Title));
        Assert.True(items[^1].Absent);
    }
}
