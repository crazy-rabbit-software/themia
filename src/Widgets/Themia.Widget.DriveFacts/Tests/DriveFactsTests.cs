using System.Text.Json;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.DriveFacts.Tests;

/// <summary>[S-11026] The drive facts atom: its pick, its sheet, its drive and its lines.</summary>
public sealed class DriveFactsTests
{
    private const ulong Gb = 1024UL * 1024 * 1024;

    [Fact]
    public void S11026_a_new_one_shows_used_free_capacity_and_the_file_system()
        => Assert.Equal(["used", "free", "capacity", "fileSystem"], new DriveFactsConfig().Facts);

    [Fact]
    public void S11026_a_drive_with_figures_has_every_fact_and_one_without_keeps_its_rows_dimmed()
    {
        var ready = new DriveEntry("C:\\", VolumeKind.Fixed, DriveState.Ready, "System", "NTFS", 931 * Gb, 419 * Gb);
        var rows = FactsRules.Pick(Locale.En, DriveRules.Sheet(Locale.En, ready), ["free", "type"], DriveRules.Facts);
        Assert.Equal("419 GB", rows[0].Value);
        Assert.True(rows[1].Available);

        var gone = FactsRules.Pick(Locale.En, DriveRules.Sheet(Locale.En, new DriveEntry("Q:\\", VolumeKind.Unknown, DriveState.Missing)), new DriveFactsConfig().Facts, DriveRules.Facts);
        Assert.Equal(4, gone.Count);
        Assert.All(gone, f => Assert.False(f.Available));
    }

    [Fact]
    public void S11026_its_drive_and_pick_survive_a_save_and_a_bad_drive_is_the_system_drive()
    {
        var json = JsonSerializer.Serialize<WidgetConfig>(new DriveFactsConfig { Drive = "D:\\", Facts = ["type"] }, StateJson.WidgetConfig);
        var back = (DriveFactsConfig)((DriveFactsConfig)JsonSerializer.Deserialize(json, StateJson.WidgetConfig)!).Clone();
        Assert.Equal(("D:\\", "type"), (back.Drive, Assert.Single(back.Facts)));

        var bad = new DriveFactsConfig { Drive = "nope" };
        Assert.NotEmpty(bad.Repair());
        Assert.Equal(string.Empty, bad.Drive);
    }

    [Fact]
    public void S11026_its_lines_are_the_drive_then_the_pick()
    {
        var menu = new WidgetMenuLines(() => { }, _ => { });
        DriveFactsModule.Lines(menu, new DriveFactsConfig(), []);
        Assert.Equal([Str.Details_Disk_Drive.Text(), Str.Details_Facts.Text()], menu.Lines.Select(l => l.Text));
    }
}
