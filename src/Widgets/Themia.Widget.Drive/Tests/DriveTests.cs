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

    // The view's area: the card less its header (36 of a card's height).
    private static WidgetArea Area(double width, double cardHeight) => new(width, cardHeight - FamilySpace.ContentTop);

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
    public void S4672_the_share_is_28_and_40_its_line_and_the_facts_come_with_room()
    {
        // XS: the 28 line, its Caption line under and the bar fill the 72 of content; no fact row is left.
        var xs = DriveFace.Bar(Area(120, 120));
        Assert.Equal(28, xs.Step.FontSize);
        Assert.True(xs.Under);
        Assert.Equal(0, xs.Facts);

        var s = DriveFace.Bar(Area(200, 150));
        Assert.True(s.Under);
        Assert.False(s.Side);
        Assert.Equal(1, s.Facts);

        var m = DriveFace.Bar(Area(320, 240));
        Assert.Equal(28, m.Step.FontSize);
        Assert.True(m.Under);
        Assert.False(m.Side);
        Assert.Equal(36 + 16 + 12 + 8, m.RowsTop);
        Assert.Equal(4, m.Facts);

        var wide = DriveFace.Bar(Area(480, 160));
        Assert.True(wide.Side);
        Assert.Equal(192, wide.Column);
        Assert.Equal(0, wide.RowsTop);
        Assert.Equal(4, wide.Facts);

        var xl = DriveFace.Bar(Area(800, 560));
        Assert.Equal(40, xl.Step.FontSize);
        Assert.Equal(DriveFace.FactCount, xl.Facts);
    }

    [Fact]
    public void S6274_the_ring_takes_its_column_with_the_space_line_under_it_and_the_facts_beside()
    {
        var xs = DriveFace.Ring(Area(120, 120));
        Assert.Equal(72, xs.Ring);
        Assert.False(xs.Line);

        var m = DriveFace.Ring(Area(320, 240));
        Assert.Equal(192 - 8 - 20, m.Ring);
        Assert.True(m.Line);
        Assert.Equal(0, m.Facts);

        var wide = DriveFace.Ring(Area(480, 160));
        Assert.True(wide.Side);
        Assert.Equal(112 - 28, wide.Ring);
        Assert.Equal(4, wide.Facts);

        Assert.Equal(0, DriveFace.Ring(new WidgetArea(60, 40)).Ring);
    }
}
