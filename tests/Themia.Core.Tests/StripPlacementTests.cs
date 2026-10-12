using Themia.Core.Geometry;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>Where the selected widgets' properties strip stands ([S-3708], [S-3709]).</summary>
public sealed class StripPlacementTests
{
    private static readonly PixelRect Area = new(0, 0, 1920, 1032);

    private const double W = 400;
    private const double H = 41;

    [Fact]
    public void S3708_it_stands_above_the_widget_centred_a_gap_off_its_top_edge()
    {
        var target = new PixelRect(700, 400, 300, 200);

        var spot = StripPlacement.Place(target, W, H, Area, []);

        Assert.Equal(StripSide.Above, spot.Side);
        Assert.Equal(650, spot.X);
        Assert.Equal(400 - StripPlacement.Gap - H, spot.Y);
    }

    [Fact]
    public void S3708_with_no_room_above_it_flips_below()
    {
        var target = new PixelRect(700, 20, 300, 200);

        var spot = StripPlacement.Place(target, W, H, Area, []);

        Assert.Equal(StripSide.Below, spot.Side);
        Assert.Equal(220 + StripPlacement.Gap, spot.Y);
    }

    [Fact]
    public void S3708_exactly_enough_room_above_is_room()
    {
        var target = new PixelRect(700, H + StripPlacement.Gap, 300, 200);

        Assert.Equal(StripSide.Above, StripPlacement.Place(target, W, H, Area, []).Side);
        Assert.Equal(StripSide.Below, StripPlacement.Place(target with { Y = target.Y - 1 }, W, H, Area, []).Side);
    }

    [Fact]
    public void S3708_it_flips_below_rather_than_land_on_the_edit_bar()
    {
        var editBar = new PixelRect(760, 8, 400, 42);
        var target = new PixelRect(800, 70, 300, 200);

        var spot = StripPlacement.Place(target, W, H, Area, [editBar]);

        Assert.Equal(StripSide.Below, spot.Side);

        // Off to the side of the bar, above is clear again.
        Assert.Equal(StripSide.Above, StripPlacement.Place(target with { X = 1400 }, W, H, Area, [editBar]).Side);
    }

    [Fact]
    public void S3708_it_slides_along_to_stay_inside_the_work_area()
    {
        var left = StripPlacement.Place(new PixelRect(0, 400, 100, 100), W, H, Area, []);
        var right = StripPlacement.Place(new PixelRect(1850, 400, 70, 100), W, H, Area, []);

        Assert.Equal(0, left.X);
        Assert.Equal(1920 - W, right.X);
    }

    [Fact]
    public void S3708_a_strip_wider_than_the_area_keeps_its_start_edge_in()
    {
        var narrow = new PixelRect(100, 0, 300, 1000);

        Assert.Equal(100, StripPlacement.Place(new PixelRect(150, 400, 50, 50), W, H, narrow, []).X);
    }

    [Fact]
    public void S3708_a_widget_the_full_height_of_the_area_has_it_over_its_edge_inside_the_area()
    {
        var tall = new PixelRect(700, 0, 300, 1032);

        var spot = StripPlacement.Place(tall, W, H, Area, []);

        // Ties go below, away from the edit bar at the top.
        Assert.Equal(StripSide.Over, spot.Side);
        Assert.Equal(1032 - H, spot.Y);

        var nearlyTall = new PixelRect(700, 30, 300, 1002);
        var over = StripPlacement.Place(nearlyTall, W, H, Area, []);
        Assert.Equal(StripSide.Over, over.Side);
        Assert.Equal(0, over.Y);
    }

    [Fact]
    public void S3708_the_work_area_offset_is_respected()
    {
        // A taskbar at the top: the work area starts 48 DIPs down.
        var area = new PixelRect(0, 48, 1920, 1032);
        var target = new PixelRect(700, 70, 300, 200);

        Assert.Equal(StripSide.Below, StripPlacement.Place(target, W, H, area, []).Side);
    }

    [Fact]
    public void S3709_the_anchor_is_the_bounding_box_of_the_selection()
    {
        var box = StripPlacement.Bounds([new PixelRect(100, 200, 50, 50), new PixelRect(400, 100, 100, 60), new PixelRect(0, 0, 0, 0)]);

        Assert.Equal(new PixelRect(100, 100, 400, 150), box);
        Assert.Null(StripPlacement.Bounds([]));
    }
}
