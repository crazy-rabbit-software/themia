using Themia.Core.Model;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>Where the card's header strip stands, [S-401] [S-402] [S-404]..[S-408].</summary>
public sealed class HeaderStripTests
{
    // ---- [S-404] the strip's cell and the content box ----------------------------------------

    [Theory]
    [InlineData(HeaderPlacement.Top)]
    [InlineData(HeaderPlacement.Bottom)]
    [InlineData(HeaderPlacement.Left)]
    [InlineData(HeaderPlacement.Right)]
    [InlineData(HeaderPlacement.Hidden)]
    public void S404_the_strip_and_the_body_tile_the_grid_and_the_view_is_in_the_body(HeaderPlacement placement)
    {
        foreach (var rtl in new[] { false, true })
        {
            var edge = HeaderStrip.Edge(placement, rtl);
            var covered = new int[3, 3];
            Cover(covered, HeaderStrip.BodyCell(edge));
            if (edge != HeaderEdge.None)
            {
                Cover(covered, HeaderStrip.StripCell(edge));
            }

            for (var row = 0; row < 3; row++)
            {
                for (var column = 0; column < 3; column++)
                {
                    Assert.Equal(1, covered[row, column]);
                }
            }

            var body = HeaderStrip.BodyCell(edge);
            var view = HeaderStrip.ViewCell;
            Assert.InRange(view.Row, body.Row, body.Row + body.RowSpan - 1);
            Assert.InRange(view.Column, body.Column, body.Column + body.ColumnSpan - 1);
        }
    }

    [Fact]
    public void S404_a_side_strip_takes_a_whole_column_so_the_content_loses_width_only()
    {
        var start = HeaderStrip.StripCell(HeaderEdge.Start);
        var end = HeaderStrip.StripCell(HeaderEdge.End);

        Assert.Equal(new GridCell(0, 0, 3, 1), start);
        Assert.Equal(new GridCell(0, 2, 3, 1), end);

        // The view's row is the star one, the same as with no header at all: no height is taken.
        Assert.Equal(1, HeaderStrip.ViewCell.Row);
        Assert.NotEqual(HeaderStrip.ViewCell.Column, start.Column);
        Assert.NotEqual(HeaderStrip.ViewCell.Column, end.Column);
    }

    [Fact]
    public void S404_a_top_strip_takes_a_whole_row_so_the_content_loses_height_only()
    {
        Assert.Equal(new GridCell(0, 0, 1, 3), HeaderStrip.StripCell(HeaderEdge.Top));
        Assert.Equal(new GridCell(2, 0, 1, 3), HeaderStrip.StripCell(HeaderEdge.Bottom));
    }

    // ---- [S-402] [S-404] shedding --------------------------------------------------------------

    [Fact]
    public void S402_a_top_header_sheds_on_a_short_card_a_side_one_on_a_narrow_card()
    {
        // Wide and short: too short for a top strip, plenty for one down the side.
        Assert.True(HeaderStrip.Sheds(HeaderPlacement.Top, 400, 80));
        Assert.False(HeaderStrip.Sheds(HeaderPlacement.Left, 400, 80));

        // Narrow and tall: the other way round.
        Assert.False(HeaderStrip.Sheds(HeaderPlacement.Bottom, 80, 400));
        Assert.True(HeaderStrip.Sheds(HeaderPlacement.Right, 80, 400));
    }

    [Fact]
    public void S402_a_card_not_measured_yet_keeps_its_header()
    {
        Assert.False(HeaderStrip.Sheds(HeaderPlacement.Top, 0, 0));
        Assert.False(HeaderStrip.Sheds(HeaderPlacement.Left, 0, 0));
    }

    [Theory]
    [InlineData(HeaderPlacement.Left)]
    [InlineData(HeaderPlacement.Right)]
    public void S409_a_side_header_sheds_below_160_dips_so_an_xs_card_keeps_its_content(HeaderPlacement side)
    {
        // An XS card (120–159 wide) would keep only ~80–120 DIPs beside a strip: text wraps per
        // letter and a button clips, so the strip goes and the content has the whole card.
        Assert.True(HeaderStrip.Sheds(side, 120, 400));
        Assert.True(HeaderStrip.Sheds(side, 159, 400));
        Assert.False(HeaderStrip.Sheds(side, 160, 400));
        Assert.Equal(160, HeaderStrip.SideShedsBelow);
    }

    [Fact]
    public void S409_a_top_header_keeps_its_own_threshold()
    {
        Assert.False(HeaderStrip.Sheds(HeaderPlacement.Top, 120, 96));
        Assert.True(HeaderStrip.Sheds(HeaderPlacement.Top, 400, 95));
        Assert.Equal(96, HeaderStrip.FlatShedsBelow);
    }

    // ---- [S-405] the turn -------------------------------------------------------------------------

    [Fact]
    public void S405_left_turns_counter_clockwise_and_right_clockwise()
    {
        Assert.Equal(HeaderTurn.CounterClockwise, HeaderStrip.Turn(HeaderPlacement.Left));
        Assert.Equal(HeaderTurn.Clockwise, HeaderStrip.Turn(HeaderPlacement.Right));
        Assert.Equal(HeaderTurn.None, HeaderStrip.Turn(HeaderPlacement.Top));
        Assert.Equal(HeaderTurn.None, HeaderStrip.Turn(HeaderPlacement.Bottom));
    }

    [Fact]
    public void S405_on_the_left_the_title_reads_bottom_to_top_with_its_top_facing_out()
    {
        const double length = 300, thickness = 40;
        var r = HeaderStrip.Rotation(HeaderTurn.CounterClockwise, length, thickness);

        // The header's start (its icon) at the strip's bottom, its end at the top.
        AssertNear((0, length), Map(r, 0, 0));
        AssertNear((0, 0), Map(r, length, 0));

        // The header's top edge is the strip's left edge, the card's outer one.
        AssertNear((0, length / 2), Map(r, length / 2, 0));
        AssertNear((thickness, length / 2), Map(r, length / 2, thickness));
        AssertInside(r, length, thickness);
    }

    [Fact]
    public void S405_on_the_right_the_title_reads_top_to_bottom_with_its_top_facing_out()
    {
        const double length = 300, thickness = 40;
        var r = HeaderStrip.Rotation(HeaderTurn.Clockwise, length, thickness);

        AssertNear((thickness, 0), Map(r, 0, 0));
        AssertNear((thickness, length), Map(r, length, 0));

        // The header's top edge is the strip's right edge.
        AssertNear((thickness, length / 2), Map(r, length / 2, 0));
        AssertNear((0, length / 2), Map(r, length / 2, thickness));
        AssertInside(r, length, thickness);
    }

    // ---- [S-406] physical edges ----------------------------------------------------------------

    [Fact]
    public void S406_left_and_right_are_physical_edges_in_a_right_to_left_language()
    {
        Assert.Equal(HeaderEdge.Start, HeaderStrip.Edge(HeaderPlacement.Left, rightToLeft: false));
        Assert.Equal(HeaderEdge.End, HeaderStrip.Edge(HeaderPlacement.Right, rightToLeft: false));

        // The card's flow mirrors right to left, so the physical left is the flow's end there.
        Assert.Equal(HeaderEdge.End, HeaderStrip.Edge(HeaderPlacement.Left, rightToLeft: true));
        Assert.Equal(HeaderEdge.Start, HeaderStrip.Edge(HeaderPlacement.Right, rightToLeft: true));

        // Top and bottom do not mirror.
        Assert.Equal(HeaderEdge.Top, HeaderStrip.Edge(HeaderPlacement.Top, rightToLeft: true));
        Assert.Equal(HeaderEdge.Bottom, HeaderStrip.Edge(HeaderPlacement.Bottom, rightToLeft: true));
        Assert.Equal(HeaderEdge.None, HeaderStrip.Edge(HeaderPlacement.Hidden, rightToLeft: true));
    }

    [Fact]
    public void S406_the_turn_does_not_mirror()
    {
        // The strip is drawn left to right whatever the language, so the turn is the physical one.
        Assert.Equal(HeaderTurn.CounterClockwise, HeaderStrip.Turn(HeaderPlacement.Left));
    }

    // ---- [S-407] corners and the stroke's sides --------------------------------------------------

    [Theory]
    [InlineData(HeaderEdge.Top)]
    [InlineData(HeaderEdge.Bottom)]
    [InlineData(HeaderEdge.Start)]
    [InlineData(HeaderEdge.End)]
    public void S407_the_strip_and_the_body_share_the_cards_four_corners(HeaderEdge edge)
    {
        var strip = HeaderStrip.StripCorners(edge);
        var body = HeaderStrip.BodyCorners(edge);

        Assert.True(strip.TopStart ^ body.TopStart);
        Assert.True(strip.TopEnd ^ body.TopEnd);
        Assert.True(strip.BottomEnd ^ body.BottomEnd);
        Assert.True(strip.BottomStart ^ body.BottomStart);
        Assert.Equal(2, Count(strip));
    }

    [Fact]
    public void S407_a_strip_at_the_start_rounds_its_outer_corners()
    {
        Assert.Equal(new CardCorners(true, false, false, true), HeaderStrip.StripCorners(HeaderEdge.Start));
        Assert.Equal(new CardCorners(false, true, true, false), HeaderStrip.StripCorners(HeaderEdge.End));
        Assert.Equal(new CardCorners(true, true, true, true), HeaderStrip.BodyCorners(HeaderEdge.None));
    }

    [Theory]
    [InlineData(HeaderEdge.Top)]
    [InlineData(HeaderEdge.Bottom)]
    [InlineData(HeaderEdge.Start)]
    [InlineData(HeaderEdge.End)]
    public void S407_the_strip_leaves_only_its_inner_side_off_the_cards_edge_and_the_seam_is_there(HeaderEdge edge)
    {
        var strip = HeaderStrip.StripSides(edge);
        var body = HeaderStrip.BodySides(edge);
        var seam = HeaderStrip.Seam(edge);

        // Between them the strip and the body reach every side of the card.
        Assert.True(strip.Start || body.Start);
        Assert.True(strip.Top || body.Top);
        Assert.True(strip.End || body.End);
        Assert.True(strip.Bottom || body.Bottom);

        // The body bleeds under the strip on exactly the one side it does not reach the edge.
        Assert.Equal(!body.Start, seam.Start);
        Assert.Equal(!body.Top, seam.Top);
        Assert.Equal(!body.End, seam.End);
        Assert.Equal(!body.Bottom, seam.Bottom);
    }

    [Fact]
    public void S407_with_no_strip_the_body_reaches_every_side_and_bleeds_nowhere()
    {
        Assert.Equal(new CardSides(true, true, true, true), HeaderStrip.BodySides(HeaderEdge.None));
        Assert.Equal(new CardSides(false, false, false, false), HeaderStrip.Seam(HeaderEdge.None));
    }

    // ---- [S-408] the trailing part ---------------------------------------------------------------

    [Theory]
    [InlineData(HeaderPlacement.Top, true)]
    [InlineData(HeaderPlacement.Bottom, true)]
    [InlineData(HeaderPlacement.Left, false)]
    [InlineData(HeaderPlacement.Right, false)]
    public void S408_a_side_strip_carries_the_icon_and_title_only(HeaderPlacement placement, bool trailing)
        => Assert.Equal(trailing, HeaderStrip.ShowsTrailing(placement));

    private static void Cover(int[,] grid, GridCell cell)
    {
        for (var row = cell.Row; row < cell.Row + cell.RowSpan; row++)
        {
            for (var column = cell.Column; column < cell.Column + cell.ColumnSpan; column++)
            {
                grid[row, column]++;
            }
        }
    }

    private static int Count(CardCorners c)
        => (c.TopStart ? 1 : 0) + (c.TopEnd ? 1 : 0) + (c.BottomEnd ? 1 : 0) + (c.BottomStart ? 1 : 0);

    /// <summary>Rotate about the origin, then translate: WinUI's CompositeTransform order.</summary>
    private static (double X, double Y) Map(HeaderRotation r, double x, double y)
    {
        var a = r.Degrees * Math.PI / 180;
        return ((x * Math.Cos(a)) - (y * Math.Sin(a)) + r.X, (x * Math.Sin(a)) + (y * Math.Cos(a)) + r.Y);
    }

    /// <summary>The header's four corners land inside the strip, thickness wide and length high.</summary>
    private static void AssertInside(HeaderRotation r, double length, double thickness)
    {
        foreach (var (x, y) in new[] { (0.0, 0.0), (length, 0.0), (0.0, thickness), (length, thickness) })
        {
            var p = Map(r, x, y);
            Assert.InRange(p.X, -1e-9, thickness + 1e-9);
            Assert.InRange(p.Y, -1e-9, length + 1e-9);
        }
    }

    private static void AssertNear((double X, double Y) expected, (double X, double Y) actual)
    {
        Assert.Equal(expected.X, actual.X, 6);
        Assert.Equal(expected.Y, actual.Y, 6);
    }
}
