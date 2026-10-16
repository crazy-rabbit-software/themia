using Themia.Core.Geometry;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-6608]..[S-6610] Widgets may hang over any edge of the display, but always keep the visible
/// minimum on its work area; the work area's edges stay a magnet while the snap aid is on; the
/// properties panel anchors to what shows.
/// </summary>
public class EdgeOverhangTests
{
    private static readonly ScreenMetrics Screen = new(1920, 1080);

    private const double Eps = 1e-9;

    private static double KeepX(ScreenMetrics s) => GridGeometry.MinVisiblePx / s.Width;

    private static double KeepY(ScreenMetrics s) => GridGeometry.MinVisiblePx / s.Height;

    /// <summary>The overlap with the allowed area on each axis, in DIPs.</summary>
    private static (double X, double Y) Visible(FracRect r, ScreenMetrics s)
    {
        var a = s.Allowed;
        return (
            (Math.Min(r.Right, a.Right) - Math.Max(r.X, a.X)) * s.Width,
            (Math.Min(r.Bottom, a.Bottom) - Math.Max(r.Y, a.Y)) * s.Height);
    }

    public static TheoryData<string, FracRect, FracRect> Edges => new()
    {
        // Pushed far past each edge, and where it settles: the visible minimum left on the screen.
        { "left", new FracRect(-3, 0.4, 0.2, 0.2), new FracRect((32.0 / 1920) - 0.2, 0.4, 0.2, 0.2) },
        { "right", new FracRect(3, 0.4, 0.2, 0.2), new FracRect(1 - (32.0 / 1920), 0.4, 0.2, 0.2) },
        { "top", new FracRect(0.4, -3, 0.2, 0.2), new FracRect(0.4, (32.0 / 1080) - 0.2, 0.2, 0.2) },
        { "bottom", new FracRect(0.4, 3, 0.2, 0.2), new FracRect(0.4, 1 - (32.0 / 1080), 0.2, 0.2) },
    };

    [Theory]
    [MemberData(nameof(Edges))]
    public void S6608_a_widget_pushed_past_an_edge_keeps_exactly_the_visible_minimum(string edge, FracRect pushed, FracRect expected)
    {
        var settled = GridGeometry.Clamp(pushed, Screen);

        Assert.Equal(expected.X, settled.X, 9);
        Assert.Equal(expected.Y, settled.Y, 9);
        Assert.Equal(expected.W, settled.W, 9);
        Assert.Equal(expected.H, settled.H, 9);
        Assert.False(string.IsNullOrEmpty(edge));
    }

    [Theory]
    [MemberData(nameof(Edges))]
    public void S6608_a_widget_over_an_edge_by_less_than_the_limit_stays_where_it_is(string edge, FracRect pushed, FracRect expected)
    {
        // Halfway between where it would have been clamped before (flush) and the limit.
        var flushX = Math.Clamp(expected.X, 0, 1 - expected.W);
        var flushY = Math.Clamp(expected.Y, 0, 1 - expected.H);
        var halfway = expected with { X = (expected.X + flushX) / 2, Y = (expected.Y + flushY) / 2 };

        Assert.Equal(halfway, GridGeometry.Clamp(halfway, Screen));
        Assert.Equal(halfway, GridGeometry.Settle(halfway, Screen, snapEnabled: false));
        Assert.NotEqual(pushed, expected);
        Assert.False(string.IsNullOrEmpty(edge));
    }

    [Theory]
    [InlineData(-5.0, -5.0)]
    [InlineData(5.0, -5.0)]
    [InlineData(-5.0, 5.0)]
    [InlineData(5.0, 5.0)]
    public void S6608_a_widget_pushed_past_a_corner_keeps_a_visible_minimum_square_in_it(double x, double y)
    {
        var settled = GridGeometry.Settle(new FracRect(x, y, 0.3, 0.3), Screen, snapEnabled: false);

        var (vx, vy) = Visible(settled, Screen);
        Assert.Equal(GridGeometry.MinVisiblePx, vx, 6);
        Assert.Equal(GridGeometry.MinVisiblePx, vy, 6);
    }

    [Fact]
    public void S6608_a_floor_sized_widget_is_never_cut_at_all()
    {
        var (minW, minH) = GridGeometry.MinFraction(Screen);

        var settled = GridGeometry.Clamp(new FracRect(-1, 2, minW, minH), Screen);

        Assert.Equal(0, settled.X, 9);
        Assert.Equal(1 - minH, settled.Y, 9);
    }

    [Fact]
    public void S6608_a_widget_larger_than_the_screen_shrinks_to_it_and_stays_in_reach()
    {
        var settled = GridGeometry.Clamp(new FracRect(-2, 0.5, 3, 4), Screen);

        Assert.Equal(1, settled.W, 9);
        Assert.Equal(1, settled.H, 9);
        Assert.Equal(KeepX(Screen) - 1, settled.X, 9);
        Assert.Equal(0.5, settled.Y, 9);
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    [InlineData(1.5)]
    [InlineData(2.0)]
    public void S6608_the_visible_minimum_is_the_same_dips_at_any_display_scale(double rasterScale)
    {
        // A 2560 × 1440 panel at each scale: fewer DIPs, the same 32 of them on screen.
        var screen = new ScreenMetrics(2560 / rasterScale, 1440 / rasterScale);

        var settled = GridGeometry.Clamp(new FracRect(5, -5, 0.2, 0.2), screen);

        var (vx, vy) = Visible(settled, screen);
        Assert.Equal(GridGeometry.MinVisiblePx, vx, 6);
        Assert.Equal(GridGeometry.MinVisiblePx, vy, 6);
    }

    [Fact]
    public void S6608_the_minimum_is_kept_on_the_work_area_so_nothing_hides_under_the_taskbar()
    {
        var screen = Screen with { Allowed = new FracRect(0, 0, 1, 1032.0 / 1080) };

        var settled = GridGeometry.Clamp(new FracRect(0.4, 0.99, 0.2, 0.2), screen);

        Assert.Equal((1032.0 - 32) / 1080, settled.Y, 9);
    }

    [Fact]
    public void S6608_an_allowed_area_narrower_than_the_minimum_keeps_what_it_can_hold()
    {
        var screen = Screen with { Allowed = new FracRect(0.99, 0, 0.005, 1) };

        var settled = GridGeometry.Clamp(new FracRect(0.5, 0.5, 0.0001, 0.2), screen);

        var (vx, _) = Visible(settled, screen);
        Assert.Equal(0.005 * 1920, vx, 6);
    }

    [Theory]
    [InlineData(ResizeHandle.Left, 0.9, 0)]
    [InlineData(ResizeHandle.Right, -0.9, 0)]
    [InlineData(ResizeHandle.Top, 0, 0.9)]
    [InlineData(ResizeHandle.Bottom, 0, -0.9)]
    public void S6608_a_resize_cannot_drag_a_widget_out_of_reach(ResizeHandle handle, double dx, double dy)
    {
        // A wide widget hanging over the opposite edge; its moved edge is dragged off the screen.
        var start = handle switch
        {
            ResizeHandle.Left => new FracRect(0.7, 0.4, 0.6, 0.2),
            ResizeHandle.Right => new FracRect(-0.3, 0.4, 0.6, 0.2),
            ResizeHandle.Top => new FracRect(0.4, 0.7, 0.2, 0.6),
            _ => new FracRect(0.4, -0.3, 0.2, 0.6),
        };

        foreach (var snap in new[] { true, false })
        {
            var settled = GridGeometry.Settle(GridGeometry.Resize(start, handle, dx, dy, Screen), Screen, snap, handle);

            var (vx, vy) = Visible(settled, Screen);
            Assert.True(vx >= GridGeometry.MinVisiblePx - 1e-6);
            Assert.True(vy >= GridGeometry.MinVisiblePx - 1e-6);
            Assert.True(settled.W * 1920 >= GridGeometry.MinWidgetPx - 1e-6);
            Assert.True(settled.H * 1080 >= GridGeometry.MinWidgetPx - 1e-6);
        }
    }

    [Fact]
    public void S6608_a_resize_may_carry_an_edge_past_the_screen_without_moving_the_anchored_one()
    {
        var start = new FracRect(0.1, 0.1, 0.2, 0.2);

        var settled = GridGeometry.Settle(GridGeometry.Resize(start, ResizeHandle.TopLeft, -0.2, -0.2, Screen), Screen, false, ResizeHandle.TopLeft);

        Assert.Equal(-0.1, settled.X, 9);
        Assert.Equal(-0.1, settled.Y, 9);
        Assert.Equal(start.Right, settled.Right, 12);
        Assert.Equal(start.Bottom, settled.Bottom, 12);
    }

    // ---- [S-6609] the edge magnet ----------------------------------------------------------------

    [Theory]
    [InlineData(-6.0)]
    [InlineData(-1.0)]
    [InlineData(4.0)]
    [InlineData(7.5)]
    public void S6609_a_drag_within_the_magnet_of_an_edge_lands_flush_on_it(double offsetPx)
    {
        var w = 200.0 / 1920;

        var left = GridGeometry.Settle(new FracRect(offsetPx / 1920, 0.4, w, 0.1), Screen, snapEnabled: true);
        var right = GridGeometry.Settle(new FracRect(1 - w - (offsetPx / 1920), 0.4, w, 0.1), Screen, snapEnabled: true);

        Assert.Equal(0, left.X, 12);
        Assert.Equal(1, right.Right, 9);
    }

    [Fact]
    public void S6609_past_the_magnet_the_drag_goes_over_the_edge()
    {
        var settled = GridGeometry.Settle(new FracRect(-30.0 / 1920, 0.4, 200.0 / 1920, 0.1), Screen, snapEnabled: true);

        Assert.Equal(-30.0 / 1920, settled.X, 9);
    }

    [Fact]
    public void S6609_without_the_snap_aid_there_is_no_magnet()
    {
        var settled = GridGeometry.Settle(new FracRect(-4.0 / 1920, 0.4, 0.1, 0.1), Screen, snapEnabled: false);

        Assert.Equal(-4.0 / 1920, settled.X, 12);
    }

    [Fact]
    public void S6609_a_resized_edge_within_the_magnet_lands_flush_on_a_taskbar_off_the_grid()
    {
        // 1032 is not a line of the 10-DIP grid; the magnet still puts the edge exactly on it.
        var screen = Screen with { Allowed = new FracRect(0, 0, 1, 1032.0 / 1080) };
        var start = new FracRect(0.2, 0.7, 0.2, 0.15);
        var dy = (1035.0 / 1080) - start.Bottom;

        var settled = GridGeometry.Settle(GridGeometry.Resize(start, ResizeHandle.Bottom, 0, dy, screen), screen, true, ResizeHandle.Bottom);

        Assert.Equal(1032.0 / 1080, settled.Bottom, 12);
        Assert.Equal(start.Y, settled.Y, 12);
    }

    [Fact]
    public void S6609_a_block_lands_flush_when_its_box_edge_is_within_the_magnet()
    {
        var screen = new ScreenMetrics(1000, 1000);
        FracRect[] start = [new(0.5, 0.5, 0.1, 0.1), new(0.65, 0.5, 0.1, 0.1)];

        // The block's right edge (0.75) dragged to 1.005: 5 DIPs over, inside the magnet.
        var delta = BlockGeometry.Move(start, 0.255, 0, screen, snapEnabled: true);

        Assert.Equal(1, BlockGeometry.Apply(start[1], delta, screen).Right, 9);
        Assert.Equal(0.15, BlockGeometry.Apply(start[1], delta, screen).X - BlockGeometry.Apply(start[0], delta, screen).X, 9);
    }

    // ---- [S-6610] the properties panel ----------------------------------------------------------

    [Fact]
    public void S6610_the_panel_stands_beside_the_visible_part_of_a_selection_hanging_off_the_left()
    {
        var area = new PixelRect(0, 0, 1920, 1032);
        var target = new PixelRect(-400, -100, 432, 300);

        var spot = StripPlacement.Place(target, 280, 120, area, []);

        Assert.Equal(StripSide.Right, spot.Side);
        Assert.Equal(32 + StripPlacement.Gap, spot.X);
        Assert.Equal((200 - 120) / 2.0, spot.Y, 9);
    }

    [Fact]
    public void S6610_the_panel_stays_inside_the_area_for_a_selection_hanging_off_any_edge()
    {
        var area = new PixelRect(0, 0, 1920, 1032);

        foreach (var target in new[]
                 {
                     new PixelRect(1888, 400, 600, 300), new PixelRect(700, 1000, 300, 600),
                     new PixelRect(700, -568, 300, 600), new PixelRect(-1500, -1000, 3500, 2050),
                 })
        {
            var spot = StripPlacement.Place(target, 280, 120, area, []);

            Assert.InRange(spot.X, area.X - Eps, area.Width - 280 + Eps);
            Assert.InRange(spot.Y, area.Y - Eps, area.Height - 120 + Eps);
        }
    }
}
