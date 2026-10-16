using Themia.Core.Geometry;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-840]/[S-841]/[S-31a] The grid is laid out on the whole monitor; the work area only limits where
/// new geometry may go. Physical monitor 1920x1080 with a 48px taskbar on each edge in turn.
/// </summary>
public class WorkAreaGeometryTests
{
    private static readonly PixelRect Monitor = new(0, 0, 1920, 1080);

    private static ScreenMetrics WithTaskbar(PixelRect workArea, double rasterScale = 1.0)
        => new(Monitor.Width / rasterScale, Monitor.Height / rasterScale) { Allowed = GridGeometry.AllowedArea(Monitor, workArea) };

    private static readonly PixelRect BottomTaskbar = new(0, 0, 1920, 1032);
    private static readonly PixelRect TopTaskbar = new(0, 48, 1920, 1032);
    private static readonly PixelRect LeftTaskbar = new(48, 0, 1872, 1080);
    private static readonly PixelRect RightTaskbar = new(0, 0, 1872, 1080);

    private const double Eps = 1e-9;

    public static TheoryData<string, PixelRect> Edges => new()
    {
        { "bottom", BottomTaskbar },
        { "top", TopTaskbar },
        { "left", LeftTaskbar },
        { "right", RightTaskbar },
    };

    [Theory]
    [MemberData(nameof(Edges))]
    public void S841_the_allowed_area_is_the_work_area_as_a_fraction_of_the_whole_monitor(string edge, PixelRect work)
    {
        var allowed = GridGeometry.AllowedArea(Monitor, work);

        Assert.Equal(work.X / 1920, allowed.X, 9);
        Assert.Equal(work.Y / 1080, allowed.Y, 9);
        Assert.Equal(work.Width / 1920, allowed.W, 9);
        Assert.Equal(work.Height / 1080, allowed.H, 9);
        Assert.False(string.IsNullOrEmpty(edge));
    }

    [Fact]
    public void S841_a_monitor_off_the_origin_resolves_relative_to_its_own_corner()
    {
        var second = new PixelRect(-2560, -200, 2560, 1440);
        var work = new PixelRect(-2560, -200, 2560, 1392);

        var allowed = GridGeometry.AllowedArea(second, work);

        Assert.Equal(new FracRect(0, 0, 1, 1392.0 / 1440), allowed);
    }

    [Fact]
    public void S841_an_auto_hide_taskbar_leaves_the_whole_screen_allowed()
    {
        Assert.Equal(FracRect.Full, GridGeometry.AllowedArea(Monitor, Monitor));
    }

    [Fact]
    public void S841_a_nonsense_work_area_allows_the_whole_screen_rather_than_nothing()
    {
        Assert.Equal(FracRect.Full, GridGeometry.AllowedArea(Monitor, new PixelRect(5000, 5000, 100, 100)));
        Assert.Equal(FracRect.Full, GridGeometry.AllowedArea(new PixelRect(0, 0, 0, 0), BottomTaskbar));
        Assert.Equal(FracRect.Full, new ScreenMetrics(1920, 1080) { Allowed = new FracRect(double.NaN, 0, 1, 1) }.Allowed);
        Assert.Equal(FracRect.Full, new ScreenMetrics(1920, 1080).Allowed);
    }

    [Fact]
    public void S841_the_allowed_fraction_is_the_same_at_any_display_scale()
    {
        Assert.Equal(WithTaskbar(BottomTaskbar).Allowed, WithTaskbar(BottomTaskbar, rasterScale: 1.5).Allowed);
        Assert.Equal(1280, WithTaskbar(BottomTaskbar, rasterScale: 1.5).Width, 9);
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    [InlineData(1.5)]
    public void S840_a_taskbar_change_never_moves_or_resizes_a_placed_widget(double rasterScale)
    {
        // Rendering resolves against the whole monitor: the same fraction is the same pixels however
        // big the taskbar is, auto-hidden or on any edge.
        var widget = new FracRect(0.25, 0.9, 0.2, 0.08);
        var expected = GridGeometry.ToPixels(widget, WithTaskbar(Monitor, rasterScale));

        foreach (var work in new[] { BottomTaskbar, TopTaskbar, LeftTaskbar, RightTaskbar, new PixelRect(0, 0, 1920, 900) })
        {
            Assert.Equal(expected, GridGeometry.ToPixels(widget, WithTaskbar(work, rasterScale)));
        }
    }

    [Theory]
    [MemberData(nameof(Edges))]
    public void S6608_a_widget_dragged_onto_any_taskbar_keeps_the_visible_minimum_off_it(string edge, PixelRect work)
    {
        var screen = WithTaskbar(work);
        var a = screen.Allowed;

        // Sizes on the 10px grid, so snapping leaves them alone and only the position is in question.
        const double w = 200.0 / 1920, h = 100.0 / 1080;

        foreach (var pushed in new[]
                 {
                     new FracRect(0.4, 1.5, w, h), new FracRect(0.4, -1.5, w, h),
                     new FracRect(-1.5, 0.4, w, h), new FracRect(1.5, 0.4, w, h),
                 })
        {
            var settled = GridGeometry.Settle(pushed, screen, snapEnabled: true);

            var overlapX = (Math.Min(settled.Right, a.Right) - Math.Max(settled.X, a.X)) * 1920;
            var overlapY = (Math.Min(settled.Bottom, a.Bottom) - Math.Max(settled.Y, a.Y)) * 1080;
            Assert.True(overlapX >= GridGeometry.MinVisiblePx - 1e-6, edge);
            Assert.True(overlapY >= GridGeometry.MinVisiblePx - 1e-6, edge);
            Assert.Equal(pushed.W, settled.W, 6);
            Assert.Equal(pushed.H, settled.H, 6);
        }
    }

    [Fact]
    public void S6609_a_widget_dragged_just_past_a_bottom_taskbar_lands_flush_on_its_top_edge()
    {
        var screen = WithTaskbar(BottomTaskbar);
        const double h = 100.0 / 1080;

        // 5 DIPs over the taskbar: inside the magnet.
        var settled = GridGeometry.Settle(new FracRect(0.4, (1037.0 / 1080) - h, 0.1, h), screen, snapEnabled: true);

        Assert.Equal(1032.0 / 1080, settled.Bottom, 9);
    }

    [Fact]
    public void S11_S6608_placing_a_widget_on_the_taskbar_keeps_the_visible_minimum_above_it()
    {
        var screen = WithTaskbar(BottomTaskbar);

        var placed = GridGeometry.PlaceAt(0.5, 1.0, 200, 150, screen, snapEnabled: true);

        Assert.Equal(screen.Allowed.Bottom - (GridGeometry.MinVisiblePx / 1080), placed.Y, 9);
        Assert.Equal(150.0 / 1080, placed.H, 9);
    }

    [Fact]
    public void S841_a_widget_bigger_than_the_work_area_shrinks_to_it()
    {
        var screen = WithTaskbar(BottomTaskbar);

        var clamped = GridGeometry.Clamp(new FracRect(0, 0, 1, 1), screen);

        Assert.Equal(FracRect.FromEdges(0, 0, 1, 1032.0 / 1080), clamped);
    }

    [Fact]
    public void S841_an_allowed_area_narrower_than_the_floor_still_yields_an_on_screen_floor_sized_widget()
    {
        var screen = new ScreenMetrics(1920, 1080) { Allowed = new FracRect(0.99, 0, 0.005, 1) };

        var clamped = GridGeometry.Clamp(new FracRect(0.5, 0.5, 0.0001, 0.2), screen);

        Assert.True(clamped.W * 1920 >= GridGeometry.MinWidgetPx - 0.001);
        Assert.True(clamped.Right <= 1 + Eps);
        Assert.True(clamped.X >= 0);
    }

    [Fact]
    public void S09_S841_non_finite_input_collapses_into_the_allowed_area()
    {
        var screen = WithTaskbar(TopTaskbar);

        var clamped = GridGeometry.Clamp(new FracRect(double.NaN, double.NaN, 0.1, 0.1), screen);

        Assert.True(clamped.IsFinite);
        Assert.Equal(screen.Allowed.Y, clamped.Y, 9);
    }

    [Fact]
    public void S840_load_validation_does_not_move_a_widget_the_taskbar_has_grown_over()
    {
        // Load clamps against the whole screen only: "no auto-move" for a widget now under the taskbar.
        var underTaskbar = new FracRect(0.3, 0.95, 0.2, 0.05);

        Assert.Equal(underTaskbar, GridGeometry.Clamp(underTaskbar, new ScreenMetrics(1920, 1080)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void S31a_S6608_dragging_the_bottom_edge_past_the_taskbar_moves_only_that_edge(bool snap)
    {
        var screen = WithTaskbar(BottomTaskbar);
        var start = new FracRect(0.2, 0.7, 0.2, 0.15);

        var dragged = GridGeometry.Resize(start, ResizeHandle.Bottom, 0, 0.2, screen);
        var settled = GridGeometry.Settle(dragged, screen, snap, ResizeHandle.Bottom);

        Assert.Equal(start.Y, settled.Y, 12);
        Assert.Equal(1.05, settled.Bottom, 2);
        Assert.True(settled.Bottom > screen.Allowed.Bottom);
        Assert.Equal(start.X, settled.X, 12);
        Assert.Equal(start.W, settled.W, 12);
    }

    [Theory]
    [MemberData(nameof(Edges))]
    public void S31a_S6608_a_resize_toward_any_edge_keeps_the_anchored_corner_and_grows_no_bigger_than_the_work_area(string edge, PixelRect work)
    {
        var screen = WithTaskbar(work);
        var a = screen.Allowed;
        var start = new FracRect(0.4, 0.4, 0.2, 0.2);

        foreach (var (handle, dx, dy) in new[]
                 {
                     (ResizeHandle.TopLeft, -1.0, -1.0), (ResizeHandle.BottomRight, 1.0, 1.0),
                 })
        {
            var settled = GridGeometry.Settle(GridGeometry.Resize(start, handle, dx, dy, screen), screen, true, handle);

            Assert.True(settled.W <= a.W + Eps && settled.H <= a.H + Eps, edge);
            if (handle == ResizeHandle.TopLeft)
            {
                Assert.Equal(start.Right, settled.Right, 12);
                Assert.Equal(start.Bottom, settled.Bottom, 12);
            }
            else
            {
                Assert.Equal(start.X, settled.X, 12);
                Assert.Equal(start.Y, settled.Y, 12);
            }
        }
    }

    [Fact]
    public void S31a_snapping_a_resize_never_moves_the_anchored_edge()
    {
        var screen = new ScreenMetrics(1920, 1080);

        // An off-grid widget (placed with Alt, or clamped flush to a taskbar) resized by its right edge.
        var start = new FracRect(0.1234, 0.5678, 0.2, 0.2);

        var settled = GridGeometry.Settle(
            GridGeometry.Resize(start, ResizeHandle.Right, 0.0131, 0, screen), screen, snapEnabled: true, ResizeHandle.Right);

        Assert.Equal(start.X, settled.X, 12);
        Assert.Equal(start.Y, settled.Y, 12);
        Assert.Equal(start.H, settled.H, 12);

        var (cols, _) = GridGeometry.GridDims(screen);
        Assert.Equal(Math.Round(settled.Right * cols), settled.Right * cols, 6);
    }

    [Fact]
    public void S31a_S08_a_snapped_resize_keeps_the_floor()
    {
        var screen = new ScreenMetrics(1920, 1080);
        var start = new FracRect(0.5, 0.5, 0.2, 0.2);

        var settled = GridGeometry.Settle(
            GridGeometry.Resize(start, ResizeHandle.Left, 0.5, 0, screen), screen, snapEnabled: true, ResizeHandle.Left);

        Assert.True(settled.W * 1920 >= GridGeometry.MinWidgetPx - 0.001);
        Assert.Equal(start.Right, settled.Right, 12);
    }

    [Fact]
    public void S841_allowed_pixels_is_the_work_area_in_the_screens_own_dips()
    {
        var screen = WithTaskbar(TopTaskbar, rasterScale: 1.5);

        var allowed = GridGeometry.AllowedPixels(screen);

        Assert.Equal(32, allowed.Y, 9);
        Assert.Equal(1280, allowed.Width, 9);
        Assert.Equal(688, allowed.Height, 9);
    }

    [Fact]
    public void S851_intersect_clips_a_widget_to_the_work_area_and_drops_one_wholly_outside()
    {
        var work = new PixelRect(0, 0, 1920, 1032);

        Assert.Equal(new PixelRect(100, 1000, 200, 32), GridGeometry.Intersect(new PixelRect(100, 1000, 200, 60), work));
        Assert.Null(GridGeometry.Intersect(new PixelRect(100, 1040, 200, 30), work));
        Assert.Null(GridGeometry.Intersect(new PixelRect(1920, 0, 10, 10), work));
    }
}
