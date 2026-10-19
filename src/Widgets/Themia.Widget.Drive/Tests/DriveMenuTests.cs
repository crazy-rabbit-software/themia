using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.Drive.Tests;

/// <summary>[S-4674] The Drive card's options on its properties menu: Appearance, then which drive.</summary>
public sealed class DriveMenuTests
{
    private static readonly DriveEntry[] Drives =
    [
        new("C:\\", VolumeKind.Fixed, DriveState.Ready, "System"),
        new("E:\\", VolumeKind.Removable, DriveState.Ready),
        new("Z:\\", VolumeKind.Network, DriveState.Disconnected, Share: "\\\\nas\\media"),
    ];

    private int _saves;

    private ItemMenuEntry Drive(DriveConfig config)
    {
        var menu = new WidgetMenuLines(() => _saves++, _ => { });
        DriveMenu.Lines(menu, config, Drives);
        Assert.Equal(0, menu.CardAt);
        return Assert.Single(menu.Lines);
    }

    [Fact]
    public void S4674_the_drive_offers_the_system_drive_then_every_drive_shares_included()
    {
        var line = Drive(new DriveConfig());
        var system = DiskRules.Instance(DiskRules.Root(SystemDrive.Root) ?? "C:\\");

        Assert.Equal(DriveStr.Drives_Settings_Drive_Label.Text(), line.Text);
        Assert.Equal(
            [Str.Shell_Meters_SystemDrive.Format(("drive", system)), .. Drives.Select(d => DriveRules.Title(Strings.Current, d))],
            line.Children!.Select(o => o.Text));
        Assert.True(line.Children![0].Checked);
    }

    [Fact]
    public void S4674_a_drive_that_is_gone_stays_offered_as_not_connected()
    {
        var options = Drive(new DriveConfig { Drive = "G:\\" }).Children!;

        Assert.Equal(Str.WidgetsB_Settings_Disk_NotConnectedDrive.Format(("drive", "G:")), options[^1].Text);
        Assert.Single(options, o => o.Checked == true);
        Assert.True(options[^1].Checked);
    }

    [Fact]
    public void S4674_a_pick_is_stored_once_and_the_current_drive_stores_nothing()
    {
        var config = new DriveConfig { Drive = "z:\\" };
        var options = Drive(config).Children!;

        options[3].Action!();
        Assert.Equal(0, _saves);

        options[2].Action!();
        Assert.Equal("E:\\", config.Drive);
        Assert.Equal(1, _saves);
    }
}
