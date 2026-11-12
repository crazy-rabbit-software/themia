using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Tests;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.Drive.Tests;

/// <summary>[S-4671] [S-4672] [S-6274] The drive card on the family's blocks: its header, its Status row, the Bar and Ring ladders.</summary>
public sealed class DriveTests
{
    private const ulong Gb = 1024UL * 1024 * 1024;


    [Fact]
    public void S4671_a_new_drive_card_has_its_header_and_a_drive_without_figures_is_one_status_row()
    {
        Assert.NotEqual(HeaderPlacement.Hidden, WidgetInstance.DefaultHeader(new WidgetId("drive")));

        var ready = new DriveEntry("C:\\", VolumeKind.Fixed, DriveState.Ready, "System", "NTFS", 931 * Gb, 419 * Gb);
        Assert.Null(DriveFace.Status(Locale.En, ready));

        var gone = DriveFace.Status(Locale.En, new DriveEntry("Z:\\", VolumeKind.Network, DriveState.Disconnected, Share: @"\\nas\media"));
        Assert.Equal(new DriveStatus(StatusKind.Problem, "Disconnected network drive"), gone);
        Assert.Equal(new DriveStatus(StatusKind.Empty, "Connecting…"), DriveFace.Status(Locale.En, new DriveEntry("Y:\\", VolumeKind.Network, DriveState.Pending)));
        Assert.Equal(new DriveStatus(StatusKind.Empty, "Not connected"), DriveFace.Status(Locale.En, new DriveEntry("Q:\\", VolumeKind.Unknown, DriveState.Missing)));
        Assert.Equal(new DriveStatus(StatusKind.Empty, "No media"), DriveFace.Status(Locale.En, new DriveEntry("F:\\", VolumeKind.Optical, DriveState.NoMedia)));
    }

    [Fact]
    public void S4672_the_bar_is_one_height_and_the_ring_its_dial_with_its_line()
    {
        var bar = DriveFace.Sizing(DriveStyle.Bar);
        Assert.Equal(36 + 36 + 16 + 8 + 4 + 12, bar.Natural.Height);
        Assert.True(bar.FixedHeight);
        Assert.Equal((160, 200, 320), (bar.Min.Width, bar.Natural.Width, bar.Max.Width));

        var ring = DriveFace.Sizing(DriveStyle.Ring);
        Assert.Empty(ring.Problems());
        Assert.Equal(104, DriveFace.Ring(ring.Natural.Width - 24, ring.Natural.Height - 36 - 12));
        Assert.True(ChartRules.Hole(DriveFace.Ring(ring.Min.Width - 24, ring.Min.Height - 48)).Width >= MeterSizing.ReadingWidth(DriveFace.Worst));
    }
}
