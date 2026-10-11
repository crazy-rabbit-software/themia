using Themia.Core.Settings;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-1556]..[S-1559] The eyedropper's pure rules: pixel to hex, displays and DPI, the loupe.</summary>
public sealed class ScreenPickTests
{
    private static readonly LoupeMetrics Metrics = new(Cells: 11, CellDips: 8, OffsetDips: 20, GapDips: 4, LabelHeightDips: 28);

    /// <summary>A 1080p display at 100 % on the left, a 4K one at 150 % to its right, 200 px lower.</summary>
    private static readonly ScreenMonitor Left = new(new PixelBox(0, 0, 1920, 1080), 96);
    private static readonly ScreenMonitor Right = new(new PixelBox(1920, 200, 3840, 2160), 144);
    private static readonly ScreenMonitor[] Mixed = [Left, Right];

    [Fact]
    public void S1556_a_pixel_reads_as_its_hex()
    {
        var capture = new PixelBox(-10, 5, 2, 2);

        // Top-down BGRA: (0,0) red, (1,0) green, (0,1) blue, (1,1) 0x123456.
        byte[] bits = [0, 0, 255, 0, 0, 255, 0, 0, 255, 0, 0, 0, 0x56, 0x34, 0x12, 0];

        Assert.Equal("#FF0000", ScreenPick.HexAt(bits, capture, -10, 5));
        Assert.Equal("#00FF00", ScreenPick.HexAt(bits, capture, -9, 5));
        Assert.Equal("#0000FF", ScreenPick.HexAt(bits, capture, -10, 6));
        Assert.Equal("#123456", ScreenPick.HexAt(bits, capture, -9, 6));
    }

    [Theory]
    [InlineData(-11, 5)]
    [InlineData(-8, 5)]
    [InlineData(-10, 4)]
    [InlineData(-10, 7)]
    public void S1556_a_point_outside_the_capture_has_no_colour(int x, int y)
    {
        var capture = new PixelBox(-10, 5, 2, 2);
        var bits = new byte[16];

        Assert.Null(ScreenPick.HexAt(bits, capture, x, y));
    }

    [Fact]
    public void S1556_a_buffer_shorter_than_the_capture_is_refused_not_overrun()
    {
        var capture = new PixelBox(0, 0, 4, 4);

        Assert.Null(ScreenPick.HexAt(new byte[8], capture, 3, 3));
    }

    [Fact]
    public void S1558_the_capture_covers_every_display_including_ones_left_of_the_primary()
    {
        ScreenMonitor[] monitors = [Left, new(new PixelBox(-1280, -300, 1280, 1024), 120), Right];

        Assert.Equal(PixelBox.FromEdges(-1280, -300, 5760, 2360), ScreenPick.VirtualScreen(monitors));
        Assert.True(ScreenPick.VirtualScreen([]).IsEmpty);
    }

    [Fact]
    public void S1558_the_pointer_is_on_the_display_that_holds_it()
    {
        Assert.Equal(Left, ScreenPick.MonitorAt(Mixed, 1919, 0));
        Assert.Equal(Right, ScreenPick.MonitorAt(Mixed, 1920, 200));
    }

    [Fact]
    public void S1558_a_point_in_a_gap_between_displays_belongs_to_the_nearest()
    {
        // Below the 1080p display, left of the 4K one: the gap the bounding box leaves.
        Assert.Equal(Left, ScreenPick.MonitorAt(Mixed, 100, 1500));
        Assert.Equal(Right, ScreenPick.MonitorAt(Mixed, 1900, 2300));
        Assert.Null(ScreenPick.MonitorAt([], 0, 0));
    }

    [Fact]
    public void S1557_the_loupe_sits_below_right_of_the_pointer_and_frames_the_pixel()
    {
        var layout = ScreenPick.Layout(500, 400, Left, Metrics);

        Assert.Equal(new PixelBox(520, 420, 88, 88), layout.Loupe);
        Assert.Equal(8, layout.CellPx);
        Assert.Equal(new PixelBox(520 + (5 * 8), 420 + (5 * 8), 8, 8), layout.Centre);
        Assert.Equal(new PixelBox(520, 420 + 88 + 4, 88, 28), layout.Label);
        Assert.Equal(PixelBox.FromEdges(520, 420, 608, 540), layout.Bounds);
    }

    [Fact]
    public void S1557_the_loupe_is_the_same_size_in_dips_on_a_scaled_display()
    {
        var layout = ScreenPick.Layout(3000, 1000, Right, Metrics);

        Assert.Equal(12, layout.CellPx);
        Assert.Equal(12 * 11, layout.Loupe.Width);
        Assert.Equal(42, layout.Label.Height);
        Assert.Equal(3000 + 30, layout.Loupe.Left);
    }

    [Fact]
    public void S1557_near_the_right_and_bottom_edge_the_loupe_flips_to_stay_on_the_display()
    {
        var layout = ScreenPick.Layout(1910, 1075, Left, Metrics);

        Assert.Equal(1910 - 20 - 88, layout.Loupe.Left);
        Assert.Equal(1075 - 20 - (88 + 4 + 28), layout.Loupe.Top);
        Assert.True(layout.Bounds.Right <= Left.Bounds.Right);
        Assert.True(layout.Bounds.Bottom <= Left.Bounds.Bottom);
    }

    [Fact]
    public void S1557_the_loupe_never_spills_onto_the_next_display()
    {
        // Just left of the 4K display: below-right would cross onto it, so it flips left.
        var layout = ScreenPick.Layout(1900, 10, Left, Metrics);

        Assert.True(layout.Bounds.Right <= 1920);
        Assert.True(layout.Bounds.Left >= 0);
    }

    [Fact]
    public void S1557_a_display_smaller_than_the_loupe_gets_it_at_its_top_left()
    {
        var tiny = new ScreenMonitor(new PixelBox(100, 100, 50, 50), 96);

        var layout = ScreenPick.Layout(120, 120, tiny, Metrics);

        Assert.Equal(100, layout.Loupe.Left);
        Assert.Equal(100, layout.Loupe.Top);
    }

    [Fact]
    public void S1557_an_even_cell_count_is_refused()
        => Assert.Throws<ArgumentOutOfRangeException>(() => ScreenPick.Layout(0, 0, Left, Metrics with { Cells = 10 }));

    [Fact]
    public void S1557_the_loupe_magnifies_the_pixels_around_the_pointer()
    {
        var capture = new PixelBox(-1280, -300, 7040, 2660);
        var layout = ScreenPick.Layout(500, 400, Left, Metrics);

        var source = ScreenPick.Source(capture, 500, 400, layout, Metrics.Cells);

        Assert.NotNull(source);
        Assert.Equal(new PixelBox(500 - 5 + 1280, 400 - 5 + 300, 11, 11), source.Value.Source);
        Assert.Equal(layout.Loupe, source.Value.Destination);
    }

    [Fact]
    public void S1557_at_the_edge_of_the_desktop_only_the_captured_part_is_magnified()
    {
        var capture = new PixelBox(0, 0, 1920, 1080);
        var layout = ScreenPick.Layout(1, 0, Left, Metrics);

        var source = ScreenPick.Source(capture, 1, 0, layout, Metrics.Cells);

        Assert.NotNull(source);

        // Columns -4..5 and rows -5..5 of the 11 are off the desktop on the left and top.
        Assert.Equal(new PixelBox(0, 0, 7, 6), source.Value.Source);
        Assert.Equal(new PixelBox(layout.Loupe.Left + (4 * 8), layout.Loupe.Top + (5 * 8), 7 * 8, 6 * 8), source.Value.Destination);
    }

    [Fact]
    public void S1557_nothing_captured_around_the_pointer_magnifies_nothing()
    {
        var capture = new PixelBox(0, 0, 100, 100);
        var layout = ScreenPick.Layout(500, 500, Left, Metrics);

        Assert.Null(ScreenPick.Source(capture, 500, 500, layout, Metrics.Cells));
    }

    [Theory]
    [InlineData(255, 255, 255, true)]
    [InlineData(0, 0, 0, false)]
    [InlineData(255, 255, 0, true)]
    [InlineData(0, 0, 255, false)]
    public void S1557_the_label_ink_reads_on_the_picked_colour(byte r, byte g, byte b, bool dark)
        => Assert.Equal(dark, ScreenPick.WantsDarkInk(r, g, b));

    [Theory]
    [InlineData(8.0, 96u, 8)]
    [InlineData(8.0, 144u, 12)]
    [InlineData(8.0, 120u, 10)]
    [InlineData(8.0, 0u, 8)]
    [InlineData(0.1, 96u, 1)]
    public void S1558_dips_scale_with_the_display_dpi(double dips, uint dpi, int pixels)
        => Assert.Equal(pixels, ScreenPick.ToPixels(dips, dpi));
}
