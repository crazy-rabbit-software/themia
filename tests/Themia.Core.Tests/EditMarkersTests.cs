using Themia.Core.Geometry;
using Xunit;

namespace Themia.Core.Tests;

public class EditMarkersTests
{
    private static PixelRect Absolute(EditMarker marker, PixelRect bar)
        => new(marker.Hit.X + bar.X, marker.Hit.Y + bar.Y, bar.Width, bar.Height);

    private static bool Contains(PixelRect outer, PixelRect inner)
        => inner.X >= outer.X - 1e-9 && inner.Y >= outer.Y - 1e-9
            && inner.X + inner.Width <= outer.X + outer.Width + 1e-9
            && inner.Y + inner.Height <= outer.Y + outer.Height + 1e-9;

    private static bool Overlap(PixelRect a, PixelRect b)
        => a.X < b.X + b.Width - 1e-9 && b.X < a.X + a.Width - 1e-9
            && a.Y < b.Y + b.Height - 1e-9 && b.Y < a.Y + a.Height - 1e-9;

    [Fact]
    public void S31b_draws_eight_markers_one_per_handle()
    {
        var markers = EditMarkers.For(280, 180, 1);

        Assert.Equal(Enum.GetValues<ResizeHandle>().Order(), markers.Select(m => m.Handle).Order());
    }

    [Fact]
    public void S31b_a_corner_is_a_square_cornered_bracket_flush_inside_the_cell()
    {
        var markers = EditMarkers.For(280, 180, 1);
        var bottomRight = markers.Single(m => m.Handle == ResizeHandle.BottomRight);
        var bars = bottomRight.Bars.Select(b => Absolute(bottomRight, b)).ToList();

        Assert.Equal(2, bars.Count);
        Assert.Contains(bars, b => b == PixelRect.FromEdges(280 - EditMarkers.Arm, 180 - EditMarkers.Thickness, 280, 180));
        Assert.Contains(bars, b => b == PixelRect.FromEdges(280 - EditMarkers.Thickness, 180 - EditMarkers.Arm, 280, 180));
    }

    [Fact]
    public void S31b_an_edge_marker_is_a_short_bar_centred_on_its_edge()
    {
        var markers = EditMarkers.For(280, 180, 1);
        var top = markers.Single(m => m.Handle == ResizeHandle.Top);
        var left = markers.Single(m => m.Handle == ResizeHandle.Left);

        Assert.Equal(PixelRect.FromEdges(132, 0, 148, 3), Absolute(top, Assert.Single(top.Bars)));
        Assert.Equal(PixelRect.FromEdges(0, 82, 3, 98), Absolute(left, Assert.Single(left.Bars)));
    }

    [Fact]
    public void S31b_no_marker_runs_outside_the_cell_bounds()
    {
        foreach (var marker in EditMarkers.For(200, 120, 1.5))
        {
            foreach (var bar in marker.Bars)
            {
                Assert.True(Contains(new PixelRect(0, 0, 200, 120), Absolute(marker, bar)), $"{marker.Handle}");
            }
        }
    }

    [Fact]
    public void S31b_every_hit_box_keeps_the_old_grip_and_covers_its_ink()
    {
        const double w = 280, h = 180, g = EditMarkers.GripSize / 2;
        var points = new Dictionary<ResizeHandle, (double X, double Y)>
        {
            [ResizeHandle.TopLeft] = (0, 0), [ResizeHandle.Top] = (w / 2, 0), [ResizeHandle.TopRight] = (w, 0),
            [ResizeHandle.Left] = (0, h / 2), [ResizeHandle.Right] = (w, h / 2),
            [ResizeHandle.BottomLeft] = (0, h), [ResizeHandle.Bottom] = (w / 2, h), [ResizeHandle.BottomRight] = (w, h),
        };

        foreach (var marker in EditMarkers.For(w, h, 1))
        {
            var (x, y) = points[marker.Handle];
            Assert.True(Contains(marker.Hit, PixelRect.FromEdges(x - g, y - g, x + g, y + g)), $"{marker.Handle} grip");
            foreach (var bar in marker.Bars)
            {
                Assert.True(Contains(marker.Hit, Absolute(marker, bar)), $"{marker.Handle} ink");
            }
        }
    }

    [Theory]
    [InlineData(32, 32)]
    [InlineData(64, 40)]
    [InlineData(280, 180)]
    public void S31b_hit_boxes_never_overlap_so_a_press_finds_one_handle(double w, double h)
    {
        var markers = EditMarkers.For(w, h, 1.25);

        for (var i = 0; i < markers.Count; i++)
        {
            for (var j = i + 1; j < markers.Count; j++)
            {
                Assert.False(Overlap(markers[i].Hit, markers[j].Hit), $"{markers[i].Handle} / {markers[j].Handle}");
            }
        }
    }

    [Fact]
    public void S31b_a_small_widget_shrinks_its_marks_and_keeps_a_gap_between_them()
    {
        var markers = EditMarkers.For(32, 32, 1);
        var corner = markers.Single(m => m.Handle == ResizeHandle.TopLeft);
        var edge = markers.Single(m => m.Handle == ResizeHandle.Top);

        var arm = corner.Bars.Select(b => Absolute(corner, b)).Max(b => b.X + b.Width);
        var bar = Absolute(edge, Assert.Single(edge.Bars));

        Assert.True(arm < EditMarkers.Arm);
        Assert.True(arm >= EditMarkers.Thickness);
        Assert.True(bar.X - arm >= EditMarkers.MinGap - 1e-9, $"gap {bar.X - arm}");
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    [InlineData(1.5)]
    [InlineData(1.75)]
    [InlineData(2.0)]
    public void S31b_every_edge_lands_on_a_device_pixel(double scale)
    {
        foreach (var marker in EditMarkers.For(213.4, 127.7, scale))
        {
            foreach (var bar in marker.Bars.Select(b => Absolute(marker, b)).Concat(marker.Bars.Select(b => EditMarkers.Haloed(Absolute(marker, b), scale))))
            {
                foreach (var edge in new[] { bar.X, bar.Y, bar.X + bar.Width, bar.Y + bar.Height })
                {
                    var px = edge * scale;
                    Assert.True(Math.Abs(px - Math.Round(px)) < 1e-6, $"{marker.Handle} {edge} at {scale}");
                }
            }
        }
    }

    [Fact]
    public void S31b_the_halo_grows_a_bar_by_whole_device_pixel()
    {
        var bar = new PixelRect(10, 10, 14, 3);

        Assert.Equal(new PixelRect(9, 9, 16, 5), EditMarkers.Haloed(bar, 1));
        Assert.Equal(new PixelRect(9.2, 9.2, 15.6, 4.6), EditMarkers.Haloed(bar, 1.25)); // one device pixel: 0.8 DIPs
    }

    [Fact]
    public void S31b_a_degenerate_size_or_scale_draws_nothing_wild()
    {
        foreach (var marker in EditMarkers.For(double.NaN, -5, 0))
        {
            Assert.All(marker.Bars, b => Assert.True(double.IsFinite(b.X) && double.IsFinite(b.Width) && b.Width >= 0));
        }
    }
}
