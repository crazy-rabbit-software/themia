using Themia.Core.Geometry;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-3733] Where a submenu of the properties menu stands: clear of the widgets and of the levels above it.</summary>
public sealed class SubmenuPlacementTests
{
    private static readonly PixelRect Area = new(0, 0, 1920, 1032);

    private const double W = 300;
    private const double H = 200;

    /// <summary>The panel, 300 wide, standing to the right of a widget.</summary>
    private static readonly PixelRect Panel = new(708, 300, 300, 400);

    private static readonly PixelRect Widget = new(400, 300, 300, 200);

    [Fact]
    public void S3733_with_room_it_stands_on_the_end_side_lapping_the_parent_level_with_its_line()
    {
        var spot = SubmenuPlacement.Place(Panel, 420, W, H, Area, [Widget], [Panel]);

        Assert.Equal(SubmenuSide.End, spot.Side);
        Assert.Equal(Panel.X + Panel.Width - SubmenuPlacement.Lap, spot.X);
        Assert.Equal(420, spot.Y);
        Assert.Equal(0, spot.Covered);
    }

    [Fact]
    public void S3733_right_to_left_the_end_side_is_the_left()
    {
        var panel = new PixelRect(900, 300, 300, 400);
        var widget = new PixelRect(1208, 300, 300, 200);

        var spot = SubmenuPlacement.Place(panel, 420, W, H, Area, [widget], [panel], rightToLeft: true);

        Assert.Equal(SubmenuSide.End, spot.Side);
        Assert.Equal(panel.X - W + SubmenuPlacement.Lap, spot.X);
    }

    [Fact]
    public void S3733_at_the_screens_right_edge_it_flips_to_the_start_side()
    {
        var panel = new PixelRect(1610, 300, 300, 400);
        var widget = new PixelRect(1000, 300, 300, 200);

        var spot = SubmenuPlacement.Place(panel, 420, W, H, Area, [widget], [panel]);

        Assert.Equal(SubmenuSide.Start, spot.Side);
        Assert.Equal(panel.X - W + SubmenuPlacement.Lap, spot.X);
        Assert.Equal(0, spot.Covered);
    }

    [Fact]
    public void S3733_where_the_end_side_would_cover_the_widget_it_takes_the_start_side()
    {
        // The panel on the widget's left (no room on its right): the end side is over the widget.
        var widget = new PixelRect(1300, 200, 600, 600);
        var panel = new PixelRect(992, 300, 300, 400);

        var spot = SubmenuPlacement.Place(panel, 420, W, H, Area, [widget], [panel]);

        Assert.Equal(SubmenuSide.Start, spot.Side);
        Assert.Equal(0, spot.Covered);
    }

    [Fact]
    public void S3733_a_second_level_clears_the_panel_and_the_first_submenu()
    {
        var first = new PixelRect(1004, 400, 300, 300);

        var spot = SubmenuPlacement.Place(first, 500, W, H, Area, [Widget], [first, Panel]);

        Assert.Equal(SubmenuSide.End, spot.Side);
        Assert.Equal(first.X + first.Width - SubmenuPlacement.Lap, spot.X);
        Assert.Equal(0, SubmenuPlacement.Covered(new PixelRect(spot.X, spot.Y, W, H), [Panel, Widget]));
    }

    [Fact]
    public void S3733_near_the_bottom_it_is_slid_up_inside_the_area()
    {
        var spot = SubmenuPlacement.Place(Panel, 950, W, H, Area, [Widget], [Panel]);

        Assert.Equal(SubmenuSide.End, spot.Side);
        Assert.Equal(Area.Height - SubmenuPlacement.Gap - H, spot.Y);
    }

    [Fact]
    public void S3733_with_neither_side_free_at_the_line_it_slides_clear_of_what_is_in_the_way()
    {
        // Widgets on both sides of the panel at the line's height, free under them.
        var left = new PixelRect(400, 300, 300, 300);
        var right = new PixelRect(1012, 300, 600, 300);

        var spot = SubmenuPlacement.Place(Panel, 400, W, H, Area, [left, right], [Panel]);

        Assert.Equal(0, spot.Covered);
        Assert.Equal(600 + SubmenuPlacement.Gap, spot.Y);
    }

    [Fact]
    public void S3733_hemmed_in_between_the_panel_and_the_screens_edge_it_stands_beyond_the_panel()
    {
        // A 1280-wide area: the widget, the panel beside it and the first submenu beside the panel up
        // to the edge. Neither side of that submenu is free; beyond the panel, under the widget, is.
        var area = new PixelRect(0, 0, 1280, 800);
        var widget = new PixelRect(200, 160, 300, 200);
        var panel = new PixelRect(508, 40, 300, 760);
        var first = new PixelRect(804, 40, 300, 750);

        var spot = SubmenuPlacement.Place(first, 380, W, H, area, [widget], [first, panel]);

        Assert.Equal(SubmenuSide.Beyond, spot.Side);
        Assert.Equal(0, spot.Covered);
        Assert.Equal(panel.X - W + SubmenuPlacement.Lap, spot.X);
    }

    [Fact]
    public void S3733_with_nowhere_free_it_takes_the_least_covered_place()
    {
        // A selection filling the work area: anywhere covers it. The end side, pressed in at the
        // screen's edge, would cover the panel too; the start side covers only the selection.
        var everything = new PixelRect(0, 0, 1920, 1032);
        var panel = new PixelRect(1600, 40, 300, 400);

        var spot = SubmenuPlacement.Place(panel, 100, W, H, Area, [everything], [panel]);

        Assert.True(spot.Covered > 0);
        Assert.Equal(W * H, spot.Covered, 3);
        Assert.Equal(0, SubmenuPlacement.Covered(new PixelRect(spot.X, spot.Y, W, H), [new PixelRect(panel.X + SubmenuPlacement.Lap, panel.Y, panel.Width - (2 * SubmenuPlacement.Lap), panel.Height)]));
    }

    [Fact]
    public void S3733_a_submenu_taller_than_the_area_starts_at_its_top()
    {
        var spot = SubmenuPlacement.Place(Panel, 400, W, 2000, Area, [], [Panel]);

        Assert.Equal(SubmenuPlacement.Gap, spot.Y);
    }

    [Fact]
    public void S3733_it_never_leaves_the_area_sideways()
    {
        var narrow = new PixelRect(0, 0, 500, 1032);
        var panel = new PixelRect(100, 300, 300, 400);

        var spot = SubmenuPlacement.Place(panel, 400, W, H, narrow, [], [panel]);

        Assert.InRange(spot.X, narrow.X, narrow.X + narrow.Width - W);
    }
}
