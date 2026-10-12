using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-4650]..[S-4672] Drives as Explorer names and describes them, the network drive's state, the removable list and the pick.</summary>
public sealed class DriveRulesTests
{
    private const ulong Gb = 1024UL * 1024 * 1024;

    private static DriveEntry Local(string root, string label = "", ulong total = 1000 * Gb, ulong free = 400 * Gb) =>
        new(root, VolumeKind.Fixed, DriveState.Ready, label, "NTFS", total, free);

    [Fact]
    public void S4651_a_drive_is_named_as_this_pc_names_it()
    {
        Assert.Equal("System (C:)", DriveRules.Title(Locale.En, Local("C:\\", "System")));
        Assert.Equal("Local Disk (D:)", DriveRules.Title(Locale.En, Local("D:\\")));
        Assert.Equal("USB Drive (E:)", DriveRules.Title(Locale.En, new DriveEntry("E:\\", VolumeKind.Removable, DriveState.Ready)));
        Assert.Equal("CD Drive (F:)", DriveRules.Title(Locale.En, new DriveEntry("F:\\", VolumeKind.Optical, DriveState.NoMedia)));
        Assert.Equal("media (\\\\nas) (Z:)", DriveRules.Title(Locale.En, new DriveEntry("Z:\\", VolumeKind.Network, DriveState.Disconnected, Share: "\\\\nas\\media")));
        Assert.Equal("Network Drive (Y:)", DriveRules.Title(Locale.En, new DriveEntry("Y:\\", VolumeKind.Network, DriveState.Pending)));
    }

    [Theory]
    [InlineData("\\\\server\\share", "share (\\\\server)")]
    [InlineData("\\\\server\\share\\", "share (\\\\server)")]
    [InlineData("\\\\server\\a\\deep", "deep (\\\\server\\a)")]
    [InlineData("\\\\server", "\\\\server")]
    [InlineData("", "")]
    public void S4651_a_share_is_named_share_then_server(string share, string expected) => Assert.Equal(expected, DriveRules.ShareLabel(share));

    [Fact]
    public void S4652_the_description_is_explorers_type_and_the_file_system()
    {
        Assert.Equal("Local Disk · NTFS", DriveRules.Description(Locale.En, Local("C:\\")));
        Assert.Equal("Removable Disk · exFAT", DriveRules.Description(Locale.En, new DriveEntry("E:\\", VolumeKind.Removable, DriveState.Ready, FileSystem: "exFAT")));
        Assert.Equal("Disconnected network drive", DriveRules.Description(Locale.En, new DriveEntry("Z:\\", VolumeKind.Network, DriveState.Disconnected, FileSystem: "NTFS")));
        Assert.Equal("Network Drive", DriveRules.Description(Locale.En, new DriveEntry("Z:\\", VolumeKind.Network, DriveState.Pending)));
    }

    [Fact]
    public void S4653_the_space_line_and_the_bars_colour()
    {
        var drive = Local("C:\\", total: 1000 * Gb, free: 400 * Gb);
        Assert.Contains("free of", DriveRules.SpaceLine(Locale.En, drive), StringComparison.Ordinal);
        Assert.Equal(0.6, drive.UsedFraction!.Value, 3);
        Assert.Equal(MeterPressure.Normal, DriveRules.Pressure(drive));
        Assert.Equal(MeterPressure.Critical, DriveRules.Pressure(Local("C:\\", free: 50 * Gb)));

        Assert.Equal("Disconnected", DriveRules.SpaceLine(Locale.En, new DriveEntry("Z:\\", VolumeKind.Network, DriveState.Disconnected)));
        Assert.Equal("Connecting…", DriveRules.SpaceLine(Locale.En, new DriveEntry("Z:\\", VolumeKind.Network, DriveState.Pending)));
        Assert.Equal("No media", DriveRules.SpaceLine(Locale.En, new DriveEntry("F:\\", VolumeKind.Optical, DriveState.NoMedia)));
        Assert.Null(new DriveEntry("Z:\\", VolumeKind.Network, DriveState.Disconnected).UsedFraction);
    }

    [Fact]
    public void S4654_a_remembered_connection_not_made_is_disconnected_without_asking_the_share()
    {
        var asked = false;
        Assert.Equal(DriveState.Disconnected, DriveRules.NetworkState(DriveRules.ConnectionUnavailable, () => asked = true));
        Assert.False(asked);

        Assert.Equal(DriveState.Ready, DriveRules.NetworkState(0, () => true));
        Assert.Equal(DriveState.Disconnected, DriveRules.NetworkState(0, () => false));
    }

    [Fact]
    public void S4660_removables_are_sticks_cards_discs_and_external_disks_never_an_empty_reader()
    {
        var drives = new[]
        {
            Local("C:\\"),
            new DriveEntry("G:\\", VolumeKind.Removable, DriveState.Ready, "STICK", "FAT32", 16 * Gb, 8 * Gb),
            new DriveEntry("F:\\", VolumeKind.Removable, DriveState.NoMedia),
            new DriveEntry("E:\\", VolumeKind.Fixed, DriveState.Ready, "Backup", "NTFS", 2000 * Gb, 100 * Gb, Hotplug: true),
            new DriveEntry("H:\\", VolumeKind.Optical, DriveState.Ready, "DVD", "UDF", 4 * Gb, 0),
            new DriveEntry("Z:\\", VolumeKind.Network, DriveState.Ready, Share: "\\\\nas\\media"),
        };

        Assert.Equal(["E:\\", "G:\\", "H:\\"], DriveRules.Removables(drives).Select(d => d.Root));
    }

    [Fact]
    public void S4670_the_pick_is_the_named_drive_the_system_drive_or_a_missing_one()
    {
        var drives = new[] { Local("C:\\", "System"), Local("D:\\", "Data") };

        Assert.Equal("Data", DriveRules.Pick(drives, "d:", "C:\\").Label);
        Assert.Equal("System", DriveRules.Pick(drives, "", "C:\\").Label);
        Assert.Equal("System", DriveRules.Pick(drives, "not a drive", "C:\\").Label);

        var gone = DriveRules.Pick(drives, "Q:\\", "C:\\");
        Assert.Equal(DriveState.Missing, gone.State);
        Assert.Equal("Q:\\", gone.Root);
        Assert.Equal("Not connected", DriveRules.SpaceLine(Locale.En, gone));
    }

    [Fact]
    public void S4656_walks_that_differ_only_below_a_megabyte_are_the_same()
    {
        var a = new[] { Local("C:\\", free: 400 * Gb) };
        var b = new[] { Local("C:\\", free: (400 * Gb) + 4096) };
        var c = new[] { Local("C:\\", free: 300 * Gb) };

        Assert.True(DriveRules.SameAs(a, b));
        Assert.False(DriveRules.SameAs(a, c));
        Assert.False(DriveRules.SameAs(a, []));
    }

    [Fact]
    public void S4655_the_meter_and_disk_space_list_the_local_drives_of_the_one_walk()
    {
        var walk = new[]
        {
            Local("C:\\", "System", free: 100 * Gb),
            new DriveEntry("E:\\", VolumeKind.Removable, DriveState.Ready, "STICK", "exFAT", 64 * Gb, 10 * Gb),
            new DriveEntry("F:\\", VolumeKind.Removable, DriveState.NoMedia),
            new DriveEntry("G:\\", VolumeKind.Optical, DriveState.Ready, "DISC", "UDF", 4 * Gb, 0),
            new DriveEntry("R:\\", VolumeKind.RamDisk, DriveState.Ready, "", "NTFS", 8 * Gb, 8 * Gb),
            new DriveEntry("Z:\\", VolumeKind.Network, DriveState.Ready, "", "", 2000 * Gb, 5 * Gb, "\\\\nas\\media"),
        };

        var volumes = DriveRules.LocalVolumes(walk);
        Assert.Equal([new DiskVolume("C:\\", "System", false), new DiskVolume("E:\\", "STICK", true)], volumes);

        var space = DriveRules.LocalSpace(walk);
        Assert.Equal(["C:\\", "E:\\"], space.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(new DriveSpace(volumes[0], 1000 * Gb, 100 * Gb), space["c:\\"]);
        Assert.Equal(new DriveSpace(volumes[1], 64 * Gb, 10 * Gb), space["E:\\"]);

        Assert.Empty(DriveRules.LocalVolumes([]));
        Assert.Empty(DriveRules.LocalSpace([]));
    }

    [Fact]
    public void S4670_the_drive_config_keeps_a_root_and_repairs_anything_else()
    {
        var config = (DriveConfig)ConfigJson.Read("drive", """{ "kind": "drive", "drive": "d:" }""");
        Assert.Equal("D:\\", config.Drive);

        var bad = new DriveConfig { Drive = "C:\\Users" };
        Assert.NotEmpty(bad.Repair());
        Assert.Equal(string.Empty, bad.Drive);
    }
}
