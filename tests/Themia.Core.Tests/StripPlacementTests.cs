using Themia.Core.Geometry;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>Where the selected widgets' properties panel stands ([S-3708], [S-3709]).</summary>
public sealed class StripPlacementTests
{
    private static readonly PixelRect Area = new(0, 0, 1920, 1032);

    private const double W = 280;
    private const double H = 240;

    [Fact]
    public void S3708_it_stands_to_the_right_of_the_widget_a_gap_off_its_edge_level_with_its_top()
    {
        var target = new PixelRect(700, 400, 300, 200);

        var spot = StripPlacement.Place(target, W, H, Area, []);

        Assert.Equal(StripSide.Right, spot.Side);
        Assert.Equal(1000 + StripPlacement.Gap, spot.X);
        Assert.Equal(400, spot.Y);
    }

    [Fact]
    public void S3708_with_no_room_on_the_right_it_stands_on_the_left()
    {
        var target = new PixelRect(1500, 400, 300, 200);

        var spot = StripPlacement.Place(target, W, H, Area, []);

        Assert.Equal(StripSide.Left, spot.Side);
        Assert.Equal(1500 - StripPlacement.Gap - W, spot.X);
        Assert.Equal(400, spot.Y);
    }

    [Fact]
    public void S3708_exactly_enough_room_on_the_right_is_room()
    {
        var target = new PixelRect(1920 - W - StripPlacement.Gap - 300, 400, 300, 200);

        Assert.Equal(StripSide.Right, StripPlacement.Place(target, W, H, Area, []).Side);
        Assert.Equal(StripSide.Left, StripPlacement.Place(target with { X = target.X + 1 }, W, H, Area, []).Side);
    }

    [Fact]
    public void S3708_right_to_left_mirrors_the_sides()
    {
        var target = new PixelRect(700, 400, 300, 200);

        var spot = StripPlacement.Place(target, W, H, Area, [], rightToLeft: true);
        Assert.Equal(StripSide.Left, spot.Side);
        Assert.Equal(700 - StripPlacement.Gap - W, spot.X);

        // No room on the left: the right, as left to right falls back to the left.
        Assert.Equal(StripSide.Right, StripPlacement.Place(target with { X = 100 }, W, H, Area, [], rightToLeft: true).Side);
    }

    [Fact]
    public void S3708_beside_a_widget_low_on_the_screen_it_slides_up_to_stay_inside_the_area()
    {
        var target = new PixelRect(700, 950, 300, 82);

        var spot = StripPlacement.Place(target, W, H, Area, []);

        Assert.Equal(StripSide.Right, spot.Side);
        Assert.Equal(1032 - H, spot.Y);
    }

    [Fact]
    public void S3708_beside_the_edit_bar_it_steps_down_under_the_bar_rather_than_cover_it()
    {
        var editBar = new PixelRect(760, 8, 400, 42);
        var target = new PixelRect(500, 10, 300, 200);

        var spot = StripPlacement.Place(target, W, H, Area, [editBar]);

        Assert.Equal(StripSide.Right, spot.Side);
        Assert.Equal(50 + StripPlacement.Gap, spot.Y);
    }

    [Fact]
    public void S3708_a_widget_spanning_the_width_has_it_below_then_above()
    {
        var wide = new PixelRect(0, 300, 1920, 200);

        var below = StripPlacement.Place(wide, W, H, Area, []);
        Assert.Equal(StripSide.Below, below.Side);
        Assert.Equal(500 + StripPlacement.Gap, below.Y);
        Assert.Equal(0, below.X);

        var low = StripPlacement.Place(wide with { Y = 700 }, W, H, Area, []);
        Assert.Equal(StripSide.Above, low.Side);
        Assert.Equal(700 - StripPlacement.Gap - H, low.Y);
    }

    [Fact]
    public void S3708_below_or_above_its_start_edge_lines_up_with_the_widgets()
    {
        var wide = new PixelRect(40, 300, 1860, 200);

        Assert.Equal(40, StripPlacement.Place(wide, W, H, Area, []).X);
        Assert.Equal(40 + 1860 - W, StripPlacement.Place(wide, W, H, Area, [], rightToLeft: true).X);
    }

    [Fact]
    public void S3708_a_widget_filling_the_area_has_it_over_its_top_end_corner_inside_the_area()
    {
        var spot = StripPlacement.Place(Area, W, H, Area, []);

        Assert.Equal(StripSide.Over, spot.Side);
        Assert.Equal(1920 - StripPlacement.Gap - W, spot.X);
        Assert.Equal(StripPlacement.Gap, spot.Y);

        var mirrored = StripPlacement.Place(Area, W, H, Area, [], rightToLeft: true);
        Assert.Equal(StripPlacement.Gap, mirrored.X);
    }

    [Fact]
    public void S3708_it_never_covers_the_widget_while_a_side_has_room()
    {
        foreach (var target in new[] { new PixelRect(10, 10, 200, 200), new PixelRect(1700, 800, 200, 200), new PixelRect(900, 0, 400, 1032) })
        {
            var spot = StripPlacement.Place(target, W, H, Area, []);
            var panel = new PixelRect(spot.X, spot.Y, W, H);

            Assert.NotEqual(StripSide.Over, spot.Side);
            Assert.False(panel.X < target.X + target.Width && target.X < panel.X + W && panel.Y < target.Y + target.Height && target.Y < panel.Y + H);
            Assert.True(panel.X >= 0 && panel.X + W <= 1920 && panel.Y >= 0 && panel.Y + H <= 1032);
        }
    }

    [Fact]
    public void S3708_the_work_area_offset_is_respected()
    {
        // A taskbar at the top: the work area starts 48 DIPs down.
        var area = new PixelRect(0, 48, 1920, 1032);
        var target = new PixelRect(700, 20, 300, 200);

        var spot = StripPlacement.Place(target, W, H, area, []);
        Assert.Equal(StripSide.Right, spot.Side);
        Assert.Equal(48, spot.Y);
    }

    [Fact]
    public void S3709_the_anchor_is_the_bounding_box_of_the_selection()
    {
        var box = StripPlacement.Bounds([new PixelRect(100, 200, 50, 50), new PixelRect(400, 100, 100, 60), new PixelRect(0, 0, 0, 0)]);

        Assert.Equal(new PixelRect(100, 100, 400, 150), box);
        Assert.Null(StripPlacement.Bounds([]));
    }
}
