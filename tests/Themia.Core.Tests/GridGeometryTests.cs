using Themia.Core.Geometry;
using Xunit;

namespace Themia.Core.Tests;

public class GridGeometryTests
{
    private static readonly ScreenMetrics Screen = new(1920, 1080);

    [Fact]
    public void S09_S6608_a_widget_hanging_over_the_corner_stays_where_it_is_and_one_pushed_away_comes_back_in_reach()
    {
        var overhanging = new FracRect(0.9, 0.9, 0.3, 0.3);
        Assert.Equal(overhanging, GridGeometry.Clamp(overhanging, Screen));

        var clamped = GridGeometry.Clamp(new FracRect(5, 5, 0.3, 0.3), Screen);

        Assert.Equal(1 - (GridGeometry.MinVisiblePx / Screen.Width), clamped.X, 9);
        Assert.Equal(1 - (GridGeometry.MinVisiblePx / Screen.Height), clamped.Y, 9);
        Assert.Equal(0.3, clamped.W, 9);
    }

    [Fact]
    public void S09_clamp_collapses_non_finite_input_instead_of_storing_nan()
    {
        var poisoned = new FracRect(double.NaN, 0.2, double.PositiveInfinity, 0.2);

        var clamped = GridGeometry.Clamp(poisoned, Screen);

        Assert.True(clamped.IsFinite);
        Assert.True(clamped.W > 0);
        Assert.True(clamped.H > 0);
    }

    [Fact]
    public void S08_clamp_lifts_a_sub_pixel_floor_widget_back_to_the_minimum()
    {
        var tiny = new FracRect(0.5, 0.5, 0.0001, 0.0001);

        var clamped = GridGeometry.Clamp(tiny, Screen);

        Assert.True(clamped.W * Screen.Width >= GridGeometry.MinWidgetPx - 0.001);
        Assert.True(clamped.H * Screen.Height >= GridGeometry.MinWidgetPx - 0.001);
    }

    [Fact]
    public void S06_snap_rounds_onto_the_grid_and_is_idempotent()
    {
        var raw = new FracRect(0.123, 0.456, 0.2345, 0.3456);

        var once = GridGeometry.Snap(raw, Screen);
        var twice = GridGeometry.Snap(once, Screen);

        Assert.Equal(once, twice);
    }

    [Fact]
    public void S06_settle_without_snap_keeps_free_fractional_positioning()
    {
        var raw = new FracRect(0.1234, 0.5678, 0.2, 0.2);

        var free = GridGeometry.Settle(raw, Screen, snapEnabled: false);

        Assert.Equal(0.1234, free.X, 6);
        Assert.Equal(0.5678, free.Y, 6);
    }

    [Theory]
    [InlineData(ResizeHandle.Left)]
    [InlineData(ResizeHandle.Right)]
    [InlineData(ResizeHandle.Top)]
    [InlineData(ResizeHandle.Bottom)]
    public void S31_a_side_handle_moves_only_its_own_edge(ResizeHandle handle)
    {
        var start = new FracRect(0.3, 0.3, 0.3, 0.3);

        var resized = GridGeometry.Resize(start, handle, 0.05, 0.05, Screen);

        if (!GridGeometry.MovesLeft(handle))
        {
            Assert.Equal(start.X, resized.X, 9);
        }

        if (!GridGeometry.MovesTop(handle))
        {
            Assert.Equal(start.Y, resized.Y, 9);
        }

        if (!GridGeometry.MovesRight(handle))
        {
            Assert.Equal(start.Right, resized.Right, 9);
        }

        if (!GridGeometry.MovesBottom(handle))
        {
            Assert.Equal(start.Bottom, resized.Bottom, 9);
        }
    }

    [Fact]
    public void S31_dragging_an_edge_past_its_opposite_stops_at_the_floor_instead_of_inverting()
    {
        var start = new FracRect(0.3, 0.3, 0.2, 0.2);

        // Drag the left edge far past the right edge.
        var resized = GridGeometry.Resize(start, ResizeHandle.Left, 5.0, 0, Screen);

        Assert.True(resized.W > 0);
        Assert.Equal(start.Right, resized.Right, 9);
        Assert.True(resized.W * Screen.Width >= GridGeometry.MinWidgetPx - 0.001);
    }

    [Fact]
    public void S31_a_corner_handle_anchors_the_opposite_corner()
    {
        var start = new FracRect(0.2, 0.2, 0.3, 0.3);

        var resized = GridGeometry.Resize(start, ResizeHandle.TopLeft, 0.05, 0.05, Screen);

        Assert.Equal(start.Right, resized.Right, 9);
        Assert.Equal(start.Bottom, resized.Bottom, 9);
        Assert.Equal(0.25, resized.X, 9);
        Assert.Equal(0.25, resized.Y, 9);
    }

    [Fact]
    public void S02_the_same_fraction_is_proportionally_larger_on_a_larger_screen()
    {
        var rect = new FracRect(0.25, 0.25, 0.5, 0.5);
        var small = new ScreenMetrics(1280, 720);
        var large = new ScreenMetrics(3840, 2160);

        var onSmall = GridGeometry.ToPixels(rect, small);
        var onLarge = GridGeometry.ToPixels(rect, large);

        Assert.Equal(onLarge.Width / onSmall.Width, large.Width / small.Width, 3);
        Assert.Equal(onLarge.X / onSmall.X, large.Width / small.Width, 3);
    }

    [Fact]
    public void S10_a_default_pixel_size_lands_the_same_physical_size_on_any_monitor()
    {
        var small = new ScreenMetrics(1280, 720);
        var large = new ScreenMetrics(3840, 2160);

        var onSmall = GridGeometry.PlaceAt(0.5, 0.5, 320, 160, small, snapEnabled: false);
        var onLarge = GridGeometry.PlaceAt(0.5, 0.5, 320, 160, large, snapEnabled: false);

        Assert.Equal(320, onSmall.W * small.Width, 1);
        Assert.Equal(320, onLarge.W * large.Width, 1);
    }

    [Fact]
    public void S04_neighbouring_widgets_meet_exactly_with_no_gap()
    {
        var left = new FracRect(0.0, 0.0, 0.5, 1.0);
        var right = new FracRect(0.5, 0.0, 0.5, 1.0);

        var l = GridGeometry.ToPixels(left, Screen);
        var r = GridGeometry.ToPixels(right, Screen);

        Assert.Equal(l.X + l.Width, r.X, 9);
    }

    [Fact]
    public void S04_the_cell_is_the_same_whatever_the_gap()
    {
        // The whole point: the gap is padding inside the cell, so moving the gap slider must not
        // move or resize the widget's geometry.
        var rect = new FracRect(0.25, 0.25, 0.5, 0.5);

        var cell = GridGeometry.ToPixels(rect, Screen);

        Assert.Equal(0.25 * Screen.Width, cell.X, 6);
        Assert.Equal(0.5 * Screen.Width, cell.Width, 6);
    }

    [Fact]
    public void S04_the_gap_insets_the_painted_surface_by_half_on_every_side()
    {
        var cell = GridGeometry.ToPixels(new FracRect(0.25, 0.25, 0.5, 0.5), Screen);

        var painted = GridGeometry.Inset(cell, gapPx: 16);

        Assert.Equal(cell.X + 8, painted.X, 9);
        Assert.Equal(cell.Y + 8, painted.Y, 9);
        Assert.Equal(cell.Width - 16, painted.Width, 9);
        Assert.Equal(cell.Height - 16, painted.Height, 9);
    }

    [Fact]
    public void S04_two_neighbours_show_a_full_gap_between_their_painted_surfaces()
    {
        var left = GridGeometry.Inset(GridGeometry.ToPixels(new FracRect(0, 0, 0.5, 1), Screen), 16);
        var right = GridGeometry.Inset(GridGeometry.ToPixels(new FracRect(0.5, 0, 0.5, 1), Screen), 16);

        Assert.Equal(16, right.X - (left.X + left.Width), 9);
    }

    [Fact]
    public void S04_a_gap_wider_than_the_cell_leaves_something_to_click_rather_than_nothing()
    {
        var cell = new PixelRect(100, 100, 20, 20);

        var painted = GridGeometry.Inset(cell, gapPx: 400);

        Assert.True(painted.Width >= 1);
        Assert.True(painted.Height >= 1);
        Assert.True(painted.X >= cell.X);
        Assert.True(painted.X + painted.Width <= cell.X + cell.Width + 0.001);
    }

    [Fact]
    public void S04_a_zero_or_nonsense_gap_leaves_the_cell_untouched()
    {
        var cell = new PixelRect(10, 20, 300, 200);

        Assert.Equal(cell, GridGeometry.Inset(cell, 0));
        Assert.Equal(cell, GridGeometry.Inset(cell, -5));
        Assert.Equal(cell, GridGeometry.Inset(cell, double.NaN));
    }

    [Fact]
    public void S11_placement_is_centred_on_the_drop_point()
    {
        var placed = GridGeometry.PlaceAt(0.5, 0.5, 320, 160, Screen, snapEnabled: false);

        Assert.Equal(0.5, placed.X + (placed.W / 2), 6);
        Assert.Equal(0.5, placed.Y + (placed.H / 2), 6);
    }

    [Fact]
    public void S11_S6608_placement_at_a_screen_corner_hangs_over_it_but_stays_in_reach()
    {
        var placed = GridGeometry.PlaceAt(0.0, 0.0, 320, 160, Screen, snapEnabled: true);

        Assert.True(placed.X < 0);
        Assert.True(placed.Y < 0);
        Assert.True(placed.Right * Screen.Width >= GridGeometry.MinVisiblePx - 0.001);
        Assert.True(placed.Bottom * Screen.Height >= GridGeometry.MinVisiblePx - 0.001);
    }

    [Fact]
    public void S06c_every_fifth_line_from_the_monitor_edge_is_major()
    {
        Assert.Equal(5, GridGeometry.MajorEvery);
        Assert.True(GridGeometry.IsMajorLine(0));
        Assert.True(GridGeometry.IsMajorLine(5));
        Assert.True(GridGeometry.IsMajorLine(190));
        Assert.False(GridGeometry.IsMajorLine(1));
        Assert.False(GridGeometry.IsMajorLine(4));
        Assert.False(GridGeometry.IsMajorLine(6));
    }

    [Theory]
    [InlineData(100.0, 1.0, 1, 100.5)]   // odd width: centred on a pixel
    [InlineData(100.0, 1.0, 2, 100.0)]   // even width: centred on a pixel edge
    [InlineData(100.0, 1.5, 1, 150.5 / 1.5)]
    [InlineData(100.0, 1.5, 4, 150.0 / 1.5)]
    [InlineData(10.2, 2.0, 2, 10.0)]
    [InlineData(10.0, 0.0, 1, 10.5)]     // a bad scale counts as 1
    public void S06c_a_line_covers_whole_physical_pixels(double dip, double scale, int widthPx, double expected)
        => Assert.Equal(expected, GridGeometry.CrispLine(dip, scale, widthPx), 9);

    [Theory]
    [InlineData(1.0, 1, 2)]
    [InlineData(1.25, 1, 2)]
    [InlineData(1.5, 2, 3)]
    [InlineData(2.0, 2, 3)]
    [InlineData(3.0, 3, 4)]
    [InlineData(4.0, 4, 6)]
    [InlineData(0.0, 1, 2)]
    public void S06c_the_major_line_is_wider_than_the_minor(double scale, int minor, int major)
        => Assert.Equal((minor, major), GridGeometry.LineWidthsPx(scale));

    [Fact]
    public void GridDims_never_returns_a_degenerate_grid_for_a_degenerate_screen()
    {
        var (cols, rows) = GridGeometry.GridDims(new ScreenMetrics(0, 0));

        Assert.True(cols >= 1);
        Assert.True(rows >= 1);
    }
}
