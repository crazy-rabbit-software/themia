using Themia.Core.Geometry;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-2240]..[S-2243] Cards that touch become one surface: which corners stay round, which sides draw.</summary>
public class CardJoinsTests
{
    private const double Tol = 0.25; // half a device pixel at 200 %

    private static PixelRect? Box(double x, double y, double w, double h) => new PixelRect(x, y, w, h);

    private static CardOutline O(string round, string draws) => new(
        round.Contains("TL", StringComparison.Ordinal), round.Contains("TR", StringComparison.Ordinal),
        round.Contains("BR", StringComparison.Ordinal), round.Contains("BL", StringComparison.Ordinal),
        draws.Contains('L', StringComparison.Ordinal), draws.Contains('T', StringComparison.Ordinal),
        draws.Contains('R', StringComparison.Ordinal), draws.Contains('B', StringComparison.Ordinal));

    [Fact]
    public void S2240_a_card_alone_is_free()
    {
        Assert.Equal([CardOutline.Free], CardJoins.Outlines([Box(0, 0, 100, 50)], Tol));
    }

    [Fact]
    public void S2240_the_tolerance_is_half_a_device_pixel()
    {
        Assert.Equal(0.25, CardJoins.Tolerance(2));
        Assert.Equal(0.5, CardJoins.Tolerance(1));
        Assert.Equal(0.5, CardJoins.Tolerance(double.NaN));
    }

    [Fact]
    public void S2241_a_column_of_equal_cards_rounds_only_its_top_and_bottom()
    {
        var o = CardJoins.Outlines([Box(0, 0, 100, 50), Box(0, 50, 100, 50), Box(0, 100, 100, 50)], Tol);

        Assert.Equal(O("TL TR", "LTRB"), o[0]);
        Assert.Equal(O("", "LRB"), o[1]);
        Assert.Equal(O("BL BR", "LRB"), o[2]);
    }

    [Fact]
    public void S2241_a_row_rounds_only_its_far_left_and_far_right()
    {
        var o = CardJoins.Outlines([Box(0, 0, 50, 80), Box(50, 0, 50, 80)], Tol);

        Assert.Equal(O("TL BL", "LTRB"), o[0]);
        Assert.Equal(O("TR BR", "TRB"), o[1]);
    }

    [Fact]
    public void S2241_a_two_by_two_grid_is_one_panel()
    {
        var o = CardJoins.Outlines([Box(0, 0, 50, 50), Box(50, 0, 50, 50), Box(0, 50, 50, 50), Box(50, 50, 50, 50)], Tol);

        Assert.Equal(O("TL", "LTRB"), o[0]);
        Assert.Equal(O("TR", "TRB"), o[1]);
        Assert.Equal(O("BL", "LRB"), o[2]);
        Assert.Equal(O("BR", "RB"), o[3]);
    }

    [Fact]
    public void S2242_a_t_junction_draws_one_line_under_the_wide_card()
    {
        // A wide card over two narrow ones side by side.
        var o = CardJoins.Outlines([Box(0, 0, 100, 40), Box(0, 40, 50, 40), Box(50, 40, 50, 40)], Tol);

        Assert.Equal(O("TL TR", "LTRB"), o[0]);
        Assert.Equal(O("BL", "LRB"), o[1]);
        Assert.Equal(O("BR", "RB"), o[2]);
    }

    [Fact]
    public void S2241_a_narrow_card_on_a_wide_one_squares_only_what_it_covers()
    {
        // Narrow on top, inset from both ends of the wide card under it.
        var o = CardJoins.Outlines([Box(20, 0, 40, 30), Box(0, 30, 100, 50)], Tol);

        // The narrow card: its bottom corners sit on the wide card; its bottom side is wholly
        // covered and the wide card's is not, so the narrow one leaves its line to the wide one.
        Assert.Equal(O("TL TR", "LTR"), o[0]);
        Assert.Equal(O("TL TR BL BR", "LTRB"), o[1]);
    }

    [Fact]
    public void S2241_a_narrow_card_flush_left_on_a_wide_one_squares_the_corner_it_reaches()
    {
        var o = CardJoins.Outlines([Box(0, 0, 40, 30), Box(0, 30, 100, 50)], Tol);

        Assert.Equal(O("TL TR", "LTR"), o[0]);
        Assert.Equal(O("TR BL BR", "LTRB"), o[1]);
    }

    [Fact]
    public void S2242_staggered_cards_each_draw_their_own_line()
    {
        // Neither side is wholly covered: both draw.
        var o = CardJoins.Outlines([Box(0, 0, 60, 30), Box(30, 30, 60, 30)], Tol);

        Assert.Equal(O("TL TR BL", "LTRB"), o[0]);
        Assert.Equal(O("TR BL BR", "LTRB"), o[1]);
    }

    [Fact]
    public void S2242_the_upper_card_leaves_its_line_when_every_neighbour_under_it_draws()
    {
        // The upper card's bottom is wholly covered by two cards that each reach past it, so
        // neither of theirs is: the upper card leaves its line out.
        var o = CardJoins.Outlines([Box(20, 0, 60, 30), Box(0, 30, 50, 30), Box(50, 30, 50, 30)], Tol);

        Assert.False(o[0].DrawsBottom);
        Assert.True(o[1].DrawsTop);
        Assert.True(o[2].DrawsTop);
    }

    [Fact]
    public void S2242_the_upper_card_keeps_its_line_when_a_neighbour_left_its_own_out()
    {
        // Under the upper card: one card reaching past it on the left, one wholly under it. The
        // second leaves its top out, so the upper card must draw its bottom.
        var o = CardJoins.Outlines([Box(20, 0, 60, 30), Box(0, 30, 50, 30), Box(50, 30, 30, 30)], Tol);

        Assert.True(o[0].DrawsBottom);
        Assert.False(o[2].DrawsTop);
    }

    [Fact]
    public void S2240_a_bare_card_between_two_cards_joins_nothing()
    {
        var o = CardJoins.Outlines([Box(0, 0, 100, 50), null, Box(0, 100, 100, 50)], Tol);

        Assert.All(o, x => Assert.Equal(CardOutline.Free, x));
    }

    [Fact]
    public void S2240_a_gap_above_the_tolerance_joins_nothing_and_one_within_it_joins()
    {
        Assert.All(CardJoins.Outlines([Box(0, 0, 100, 50), Box(0, 50.3, 100, 50)], Tol), x => Assert.Equal(CardOutline.Free, x));

        var close = CardJoins.Outlines([Box(0, 0, 100, 50), Box(0, 50.2, 100, 50)], Tol);
        Assert.False(close[0].RoundBottomLeft);
        Assert.False(close[1].DrawsTop);
    }

    [Fact]
    public void S2240_cards_meeting_only_at_a_corner_do_not_join()
    {
        Assert.All(CardJoins.Outlines([Box(0, 0, 50, 50), Box(50, 50, 50, 50)], Tol), x => Assert.Equal(CardOutline.Free, x));
        Assert.All(CardJoins.Outlines([Box(0, 0, 50, 50), Box(50, 49.9, 50, 50)], Tol), x => Assert.Equal(CardOutline.Free, x));
    }

    [Fact]
    public void S2240_different_radii_join_by_the_same_geometry()
    {
        // The rule takes no radius: a card of radius 16 on one of radius 0 joins as any two do, and
        // each draws its round corners at its own radius.
        var o = CardJoins.Outlines([Box(0, 0, 100, 50), Box(0, 50, 100, 50)], Tol);
        var upper = o[0].Stroke(100, 50, 1, 16);
        var lower = o[1].Stroke(100, 50, 1, 0);

        Assert.Contains(upper[0].Segments, s => s.ArcRadius == 15.5);
        Assert.DoesNotContain(lower.SelectMany(f => f.Segments), s => s.ArcRadius > 0);
    }

    [Fact]
    public void S2241_right_to_left_mirrors_the_outline()
    {
        var o = new CardOutline(true, false, false, true, true, true, false, true);
        Assert.Equal(new CardOutline(false, true, true, false, false, true, true, true), o.Mirrored());
        Assert.Equal(o, o.Mirrored().Mirrored());
    }

    [Fact]
    public void S2243_a_free_card_strokes_one_closed_rounded_figure_half_the_stroke_inside()
    {
        var f = Assert.Single(CardOutline.Free.Stroke(100, 50, 2, 8));

        Assert.True(f.Closed);
        Assert.Equal((8, 1), (f.StartX, f.StartY));
        Assert.Equal(4, f.Segments.Count(s => s.ArcRadius == 7));
        Assert.Equal(new OutlineSegment(8, 1, 7), f.Segments[^1]);
    }

    [Fact]
    public void S2243_a_side_left_out_breaks_the_path_and_its_neighbours_run_to_the_edge()
    {
        // The lower card of a column: top not drawn, upper corners square, lower ones round.
        var f = Assert.Single(O("BL BR", "LRB").Stroke(100, 50, 1, 8));

        Assert.False(f.Closed);
        Assert.Equal((99.5, 0), (f.StartX, f.StartY));
        Assert.Equal(new OutlineSegment(0.5, 0, 0), f.Segments[^1]);
        Assert.Equal(2, f.Segments.Count(s => s.ArcRadius > 0));
    }

    [Fact]
    public void S2245_the_hit_region_rounds_the_round_corners_in_device_pixels_and_squares_the_joined()
    {
        var lower = O("BL BR", "LRB");

        Assert.Equal(new HitCorners(0, 0, 12, 12), CardFrame.Corners(lower, drawsSurface: true, radiusDip: 8, scale: 1.5));
        Assert.Equal(new HitCorners(16, 16, 16, 16), CardFrame.Corners(CardOutline.Free, drawsSurface: true, radiusDip: 8, scale: 2));
    }

    [Fact]
    public void S2245_a_bare_card_or_a_radius_of_zero_takes_its_whole_rect()
    {
        Assert.Equal(HitCorners.Square, CardFrame.Corners(CardOutline.Free, drawsSurface: false, radiusDip: 8, scale: 1));
        Assert.Equal(HitCorners.Square, CardFrame.Corners(CardOutline.Free, drawsSurface: true, radiusDip: 0, scale: 1));
    }

    [Fact]
    public void S2243_two_sides_left_out_apart_are_two_figures_and_none_drawn_is_none()
    {
        Assert.Equal(2, O("", "LR").Stroke(100, 50, 1, 8).Count);
        Assert.Empty(O("", "").Stroke(100, 50, 1, 8));
        Assert.Empty(CardOutline.Free.Stroke(100, 50, 0, 8));
    }
}
