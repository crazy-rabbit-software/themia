using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.Disk.Tests;

/// <summary>[S-1168] The disk meter's options on its properties menu: Appearance, the drive, the span under Style.</summary>
public sealed class DiskMenuTests
{
    private static readonly DriveEntry[] Drives =
    [
        new("C:\\", VolumeKind.Fixed, DriveState.Ready, "System"),
        new("E:\\", VolumeKind.Removable, DriveState.Ready, "Stick"),
        new("Z:\\", VolumeKind.Network, DriveState.Ready, Share: "\\\\nas\\media"),
    ];

    private int _saves;

    private WidgetMenuLines Build(DiskConfig disk)
    {
        var menu = new WidgetMenuLines(() => _saves++, _ => { });
        DiskMenu.Lines(menu, disk, Drives);
        return menu;
    }

    private ItemMenuEntry Drive(DiskConfig disk) => Assert.Single(Build(disk).Lines);

    [Fact]
    public void S1168_the_first_level_is_appearance_then_the_drive_and_the_span_is_under_style()
    {
        var menu = Build(new DiskConfig());

        Assert.Equal(DiskStr.Shell_Meters_Drive_Label.Text(), Assert.Single(menu.Lines).Text);
        Assert.Equal(Str.WidgetsB_Settings_Meters_ChartSpan_Label.Text(), Assert.Single(menu.StyleLines).Text);
    }

    [Fact]
    public void S1168_the_drive_offers_the_system_drive_all_drives_and_the_local_volumes_and_holds_the_system_drive_by_default()
    {
        var options = Drive(new DiskConfig()).Children!;
        var system = DiskRules.Instance(DiskRules.Root(SystemDrive.Root) ?? "C:\\");

        Assert.Equal(
            [Str.Shell_Meters_SystemDrive.Format(("drive", system)), Str.WidgetsB_Disk_AllDrives.Text(), .. DriveRules.LocalVolumes(Drives).Select(DiskRules.Title)],
            options.Select(o => o.Text));
        Assert.Equal(2, DriveRules.LocalVolumes(Drives).Count);
        Assert.True(options[0].Checked);
    }

    [Fact]
    public void S1168_a_removed_drive_stays_offered_as_not_connected_and_held()
    {
        var options = Drive(new DiskConfig { Drive = "F:\\" }).Children!;

        var last = options[^1];
        Assert.Equal(Str.WidgetsB_Settings_Disk_NotConnectedDrive.Format(("drive", "F:")), last.Text);
        Assert.True(last.Checked);
        Assert.Single(options, o => o.Checked == true);
    }

    [Fact]
    public void S1168_all_drives_sets_the_scope_and_a_drive_sets_it_back_each_stored_once()
    {
        var disk = new DiskConfig { Drive = "e:\\" };
        var options = Drive(disk).Children!;
        Assert.True(options[3].Checked);

        options[3].Action!();
        Assert.Equal(0, _saves);

        options[1].Action!();
        Assert.Equal((DiskScope.AllDrives, string.Empty), (disk.Scope, disk.Drive));
        Assert.Equal(1, _saves);

        Drive(disk).Children![2].Action!();
        Assert.Equal((DiskScope.OneDrive, "C:\\"), (disk.Scope, disk.Drive));
        Assert.Equal(2, _saves);
    }
}
