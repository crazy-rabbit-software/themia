using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Tests;
using Themia.Core.Widgets;
using Themia.Sdk.Items;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.Drive.Tests;

/// <summary>[S-4671] [S-4672] [S-6274] The drive card on the family's blocks: its Status row, the Bar and Ring ladders.</summary>
public sealed class DriveTests
{
    private const ulong Gb = 1024UL * 1024 * 1024;


    [Fact]
    public void S4671_a_drive_without_figures_is_one_status_row()
    {

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
        // [S-16637] [S-17180] The bar style is the device item itself: one item tall (60, its bar standing at its end), 220..520 wide at least.
        var bar = DriveFace.Scaled(DriveStyle.Bar);
        Assert.Equal(60, bar.Natural.Height);
        Assert.True(bar.FixedHeight);
        Assert.True(bar.Min.Width >= 220 && bar.Natural.Width >= 300 && bar.Max.Width >= 520);

        var ring = DriveFace.Sizing(DriveStyle.Ring);
        Assert.Empty(ring.Problems());
        Assert.Equal(104, DriveFace.PlannedRing);
        Assert.True(ChartRules.Hole(DriveFace.PlannedRing).Width >= MeterSizing.ReadingWidth(DriveFace.Worst));
    }

    [Theory]
    [InlineData(DriveStyle.Ring, 14)]
    public void S15749_the_drive_scales_down_as_one_block_to_its_line_at_9(DriveStyle style, double line)
    {
        // WR-7: the share and its line stay together; WR-14: down to where the smallest text is still read. The least card
        // is the content box at the line's 9 in the inset, which does not scale; nothing gives way.
        var sizing = DriveFace.Scaled(style);
        var natural = DriveFace.Sizing(style).Natural;
        var k = 9 / line;
        // [S-15759] The bar's never narrower than its widest line beside its icon at that scale.
        var least = style == DriveStyle.Bar ? Math.Max(Math.Ceiling(((natural.Width - 24) * k) + 24), Math.Ceiling(((DriveFace.WidestLine(Locale.En) + 12 + Themia.Sdk.Items.ItemLayout.ReadingWidth) * k) + 24)) : Math.Ceiling(((natural.Width - 24) * k) + 24);
        Assert.Equal(least, sizing.Min.Width);
        Assert.Equal(Math.Ceiling(((natural.Height - 24) * k) + 24), sizing.Min.Height);
        Assert.Empty(sizing.Problems());
    }

    [Fact]
    public void S15750_the_space_line_is_held_to_its_widest_forms()
    {
        var templates = DriveFace.SpaceTemplates(Locale.En);
        Assert.Contains("888 GB free of 888 GB", templates);
        Assert.Equal(6, templates.Count);
        Assert.All(templates, t => Assert.DoesNotContain(t, c => char.IsDigit(c) && c != '8'));
    }
}
