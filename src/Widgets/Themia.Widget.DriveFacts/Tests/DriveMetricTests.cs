using System.Text.Json;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.DriveFacts.Tests;

/// <summary>[S-14670] A drive's facts, a single metric each: the drive, the figures, a drive gone.</summary>
public sealed class DriveMetricTests
{
    private const ulong Gb = 1024UL * 1024 * 1024;

    [Fact]
    public void S14670_a_drive_with_figures_has_its_fact_and_one_without_keeps_its_row_dimmed()
    {
        var ready = new DriveEntry("C:\\", VolumeKind.Fixed, DriveState.Ready, "System", "NTFS", 931 * Gb, 419 * Gb);
        Assert.Equal(new MetricValue("419 GB", 419d * Gb), MetricRules.Pick(DriveRules.Sheet(Locale.En, ready), "free").Value);
        Assert.True(MetricRules.Pick(DriveRules.Sheet(Locale.En, ready), "type").Value.Available);

        var gone = DriveRules.Sheet(Locale.En, new DriveEntry("Q:\\", VolumeKind.Unknown, DriveState.Missing));
        Assert.False(MetricRules.Pick(gone, "used").Value.Available);
    }

    [Fact]
    public void S14670_its_drive_and_trend_survive_a_save_and_a_bad_drive_is_the_system_drive()
    {
        var json = JsonSerializer.Serialize<WidgetConfig>(new DriveFreeConfig { Drive = "D:\\", Trend = MetricTrend.Arrow }, StateJson.WidgetConfig);
        var back = (DriveFreeConfig)((DriveFreeConfig)JsonSerializer.Deserialize(json, StateJson.WidgetConfig)!).Clone();
        Assert.Equal(("D:\\", MetricTrend.Arrow), (back.Drive, back.Trend));

        var bad = new DriveUsedConfig { Drive = "nope" };
        Assert.NotEmpty(bad.Repair());
        Assert.Equal(string.Empty, bad.Drive);
    }

    [Fact]
    public void S14670_its_source_line_is_the_drive_and_only_a_figure_that_moves_has_a_trend()
    {
        var menu = new WidgetMenuLines(() => { });
        DriveMetricModule.Lines(menu, new DriveFreeConfig(), []);
        Assert.Equal([Str.Details_Disk_Drive.Text()], menu.Lines.Select(l => l.Text));
        Assert.Equal(MetricHistory.Readings, new DriveFreeConfig().History);
        Assert.Equal(MetricHistory.None, new DriveFileSystemConfig().History);
    }
}
