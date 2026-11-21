using Themia.Core.Geometry;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-2240]..[S-2243] Cards that touch become one surface: which corners stay round, which sides draw.</summary>
public class CardJoinsTests
{
    private const double Scale = 2; // the cells' raster scale: 200 %

    private static DipRect? Box(double x, double y, double w, double h) => new DipRect(x, y, w, h);

    private static CardOutline O(string round, string draws) => new(
        round.Contains("TL", StringComparison.Ordinal), round.Contains("TR", StringComparison.Ordinal),
        round.Contains("BR", StringComparison.Ordinal), round.Contains("BL", StringComparison.Ordinal),
        draws.Contains('L', StringComparison.Ordinal), draws.Contains('T', StringComparison.Ordinal),
        draws.Contains('R', StringComparison.Ordinal), draws.Contains('B', StringComparison.Ordinal));

    [Fact]
    public void S2250_a_layer_inside_the_clip_is_round_all_round_or_square_never_one_radius_a_corner()
    {
        // A free card: the card's radius on every corner.
        Assert.True(CardOutline.Free.AllRound);
        Assert.Equal(8, CardJoins.InsideRadius(CardOutline.Free, 8));

        // Every card of a joined panel that keeps some of its corners: square, the surface's clip rounds it.
        var row = CardJoins.Outlines([Box(0, 0, 100, 50), Box(100, 0, 100, 50), Box(200, 0, 100, 50)], Scale);
        Assert.All(row, o => Assert.False(o.AllRound));
        Assert.All(row, o => Assert.Equal(0, CardJoins.InsideRadius(o, 8)));
        Assert.Equal(0, CardJoins.InsideRadius(O("TL", "LT"), 8));
        Assert.Equal(0, CardJoins.InsideRadius(O("TL TR BL", "LTR"), 8));
        Assert.Equal(0, CardJoins.InsideRadius(O(string.Empty, string.Empty), 8));

        // Mirrored, the same: which corners are round does not matter, only whether all are.
        Assert.Equal(8, CardJoins.InsideRadius(CardOutline.Free.Mirrored(), 8));
        Assert.Equal(0, CardJoins.InsideRadius(O("TL BL", "LTB").Mirrored(), 8));

        // No radius, or none that is a length: square.
        Assert.Equal(0, CardJoins.InsideRadius(CardOutline.Free, 0));
        Assert.Equal(0, CardJoins.InsideRadius(CardOutline.Free, -4));
        Assert.Equal(0, CardJoins.InsideRadius(CardOutline.Free, double.NaN));
    }

    [Fact]
    public void S2240_a_card_alone_is_free()
    {
        Assert.Equal([CardOutline.Free], CardJoins.Outlines([Box(0, 0, 100, 50)], Scale));
    }

    [Fact]
    public void S2240_S11893_cells_are_judged_on_the_device_lattice()
    {
        // Until 2026-11-21: edges within half a device pixel met (0.25 DIP at 200 %). Now each edge is its device pixel.
        Assert.NotNull(CardJoins.Meeting(new DipRect(0, 0, 100, 50), new DipRect(100.2, 0, 80, 50), 2));
        Assert.Null(CardJoins.Meeting(new DipRect(0, 0, 100, 50), new DipRect(100.5, 0, 80, 50), 2));
        Assert.NotNull(CardJoins.Meeting(new DipRect(0, 0, 100, 50), new DipRect(100.4, 0, 80, 50), double.NaN));
    }

    /// <summary>[S-2240] Any two cards that meet join: there are no groups.</summary>
    private static CardOutline[] Group(params DipRect?[] boxes) => CardJoins.Outlines(boxes, Scale);

    [Fact]
    public void S2240_regression_ungrouped_cards_that_touch_join()
    {
        // Owner, 2026-11-17: "the widgets should connect when the edges of them touch, right now they only connect
        // when the edges touch and they are grouped together". Three cards edge to edge, no group of any kind.
        DipRect?[] boxes = [Box(0, 0, 100, 50), Box(0, 50, 100, 50), Box(100, 0, 50, 100)];
        var o = CardJoins.Outlines(boxes, Scale);
        Assert.False(o[0].DrawsBottom);
        Assert.False(o[1].DrawsTop);
        Assert.False(o[0].DrawsRight);
        Assert.False(o[2].DrawsLeft);
        Assert.False(o[0].RoundBottomLeft || o[1].RoundTopLeft);
    }

    [Fact]
    public void S2241_a_row_rounds_only_its_far_left_and_far_right()
    {
        var o = Group(Box(0, 0, 50, 80), Box(50, 0, 50, 80));

        Assert.Equal(O("TL BL", "LTB"), o[0]);
        Assert.Equal(O("TR BR", "TRB"), o[1]);
    }

    [Fact]
    public void S2241_a_two_by_two_grid_is_one_panel()
    {
        var o = Group(Box(0, 0, 50, 50), Box(50, 0, 50, 50), Box(0, 50, 50, 50), Box(50, 50, 50, 50));

        Assert.Equal(O("TL", "LT"), o[0]);
        Assert.Equal(O("TR", "TR"), o[1]);
        Assert.Equal(O("BL", "LB"), o[2]);
        Assert.Equal(O("BR", "RB"), o[3]);
    }

    [Fact]
    public void S2241_a_t_junction_squares_its_inner_corners()
    {
        // A wide member over two narrow ones side by side.
        var o = Group(Box(0, 0, 100, 40), Box(0, 40, 50, 40), Box(50, 40, 50, 40));

        Assert.Equal(O("TL TR", "LTR"), o[0]);
        Assert.Equal(O("BL", "LB"), o[1]);
        Assert.Equal(O("BR", "RB"), o[2]);
    }

    [Fact]
    public void S2241_a_narrow_member_flush_left_on_a_wide_one_squares_the_corner_it_reaches()
    {
        var o = Group(Box(0, 0, 40, 30), Box(0, 30, 100, 50));

        Assert.Equal(O("TL TR", "LTR"), o[0]);
        Assert.Equal(O("TR BL BR", "LTRB") with { Seams = [new OutlineSeam(CardSide.Top, 0, 0.4)] }, o[1]);
    }

    [Fact]
    public void S2241_staggered_members_each_draw_the_stretch_their_mate_leaves_open()
    {
        var o = Group(Box(0, 0, 60, 30), Box(30, 30, 60, 30));

        Assert.Equal(O("TL TR BL", "LTRB") with { Seams = [new OutlineSeam(CardSide.Bottom, 0.5, 1)] }, o[0]);
        Assert.Equal(O("TR BL BR", "LTRB") with { Seams = [new OutlineSeam(CardSide.Top, 0, 0.5)] }, o[1]);
    }

    [Fact]
    public void S2240_a_bare_card_between_two_members_joins_nothing()
    {
        var o = Group(Box(0, 0, 100, 50), null, Box(0, 100, 100, 50));

        Assert.All(o, x => Assert.Equal(CardOutline.Free, x));
    }

    [Fact]
    public void S2240_a_gap_above_the_tolerance_joins_nothing_and_one_within_it_joins()
    {
        Assert.All(Group(Box(0, 0, 100, 50), Box(0, 50.3, 100, 50)), x => Assert.Equal(CardOutline.Free, x));

        var close = Group(Box(0, 0, 100, 50), Box(0, 50.2, 100, 50));
        Assert.False(close[0].RoundBottomLeft);
        Assert.False(close[1].DrawsTop);
    }

    [Fact]
    public void S2240_members_meeting_only_at_a_corner_do_not_join()
    {
        Assert.All(Group(Box(0, 0, 50, 50), Box(50, 50, 50, 50)), x => Assert.Equal(CardOutline.Free, x));
        Assert.All(Group(Box(0, 0, 50, 50), Box(50, 49.9, 50, 50)), x => Assert.Equal(CardOutline.Free, x));
    }

    [Fact]
    public void S2240_different_radii_join_by_the_same_geometry()
    {
        // The rule takes no radius: a member of radius 16 on one of radius 0 joins as any two do, and
        // each draws its round corners at its own radius.
        var o = Group(Box(0, 0, 100, 50), Box(0, 50, 100, 50));
        var upper = o[0].Stroke(100, 50, 1, 16);
        var lower = o[1].Stroke(100, 50, 1, 0);

        Assert.Contains(upper.SelectMany(f => f.Segments), s => s.ArcRadius == 15.5);
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
    public void S2243_two_sides_left_out_apart_are_two_figures_and_none_drawn_is_none()
    {
        Assert.Equal(2, O("", "LR").Stroke(100, 50, 1, 8).Count);
        Assert.Empty(O("", "").Stroke(100, 50, 1, 8));
        Assert.Empty(CardOutline.Free.Stroke(100, 50, 0, 8));
    }

    [Fact]
    public void S11870_a_column_of_three_draws_no_line_inside_and_its_outer_border_whole()
    {
        var o = CardJoins.Outlines([Box(0, 0, 100, 50), Box(0, 50, 100, 50), Box(0, 100, 100, 50)], Scale);

        Assert.Equal(O("TL TR", "LTR"), o[0]);
        Assert.Equal(O("", "LR"), o[1]);
        Assert.Equal(O("BL BR", "LRB"), o[2]);
        Assert.All(o, x => Assert.Null(x.Seams));

        // The outer border is one unbroken line down each side: the left and the right run to the
        // card's edge where the inner sides are left out.
        var middle = o[1].Stroke(100, 50, 1, 8);
        Assert.Equal(2, middle.Count);
        Assert.Contains(middle, f => (f.StartX, f.StartY) == (99.5, 0) && f.Segments[^1] == new OutlineSegment(99.5, 50, 0));
        Assert.Contains(middle, f => (f.StartX, f.StartY) == (0.5, 50) && f.Segments[^1] == new OutlineSegment(0.5, 0, 0));
    }

    [Fact]
    public void S11870_a_card_that_joins_nothing_against_a_panel_is_a_card_of_its_own()
    {
        // Two cards side by side, a card that takes no part in joins (a desktop icon: null box) as wide as both under them.
        var o = CardJoins.Outlines([Box(0, 0, 50, 40), Box(50, 0, 50, 40), null], Scale);

        Assert.Equal(O("TL BL", "LTB"), o[0]);
        Assert.Equal(O("TR BR", "TRB"), o[1]);
        Assert.Equal(CardOutline.Free, o[2]);
    }

    [Fact]
    public void S11870_a_side_partly_across_a_neighbour_leaves_out_only_that_stretch()
    {
        // A wide card over a narrow one (x 20..60) and a card that joins nothing (x 60..100, its own set): the wide
        // card draws its bottom only over that one.
        var o = CardJoins.Outlines([Box(0, 0, 100, 40), Box(20, 40, 40, 40), Box(60, 40, 40, 40)], Scale, sets: [0, 0, 1]);

        Assert.True(o[0].DrawsBottom);
        Assert.Equal([new OutlineSeam(CardSide.Bottom, 0.2, 0.6)], o[0].Seams);
        Assert.False(o[1].DrawsTop);
        Assert.Equal(CardOutline.Free, o[2]);

        // A narrow card on a wide one: the wide one draws its top on either side of the narrow one.
        var under = CardJoins.Outlines([Box(20, 0, 40, 30), Box(0, 30, 100, 50)], Scale);
        Assert.Equal(O("TL TR", "LTR"), under[0]);
        Assert.True(under[1].DrawsTop);
        Assert.Equal([new OutlineSeam(CardSide.Top, 0.2, 0.6)], under[1].Seams);
    }

    [Fact]
    public void S11870_a_card_apart_from_the_others_draws_its_whole_border()
    {
        var o = CardJoins.Outlines([Box(0, 0, 100, 50), Box(0, 50, 100, 50), Box(300, 0, 100, 50)], Scale);

        Assert.Equal(CardOutline.Free, o[2]);
    }

    [Fact]
    public void S11870_a_dragged_block_keeps_its_seamless_inside_free_of_the_cards_standing_still()
    {
        // The dragged pair (set 1) over a still card (set 0).
        var o = CardJoins.Outlines([Box(0, 0, 50, 40), Box(50, 0, 50, 40), Box(0, 40, 100, 40)], Scale, sets: [1, 1, 0]);

        Assert.False(o[0].DrawsRight);
        Assert.False(o[1].DrawsLeft);
        Assert.True(o[0].DrawsBottom && o[1].DrawsBottom);
        Assert.Equal(CardOutline.Free, o[2]);
    }

    [Fact]
    public void S11870_right_to_left_mirrors_the_seams()
    {
        var o = new CardOutline(true, true, false, false, true, true, true, true, [new OutlineSeam(CardSide.Bottom, 0.25, 0.5), new OutlineSeam(CardSide.Left, 0.125, 0.5)]);
        var m = o.Mirrored();

        Assert.Equal([new OutlineSeam(CardSide.Bottom, 0.5, 0.75), new OutlineSeam(CardSide.Right, 0.125, 0.5)], m.Seams);
        Assert.Equal(o, m.Mirrored());
        Assert.NotEqual(o, o with { Seams = null });
    }

    [Fact]
    public void S11871_a_seam_breaks_its_side_into_runs_that_reach_into_it_by_the_stroke()
    {
        // A card whose top is drawn but for 20..60 of 100, square on top, round below.
        var o = new CardOutline(false, false, true, true, true, true, true, true, [new OutlineSeam(CardSide.Top, 0.2, 0.6)]);
        var f = Assert.Single(o.Stroke(100, 50, 1, 8));

        Assert.False(f.Closed);

        // From the seam's far end less the stroke, round the right, the bottom and the left, back
        // along the top to the seam's near end plus the stroke.
        Assert.Equal((59, 0.5), (f.StartX, f.StartY));
        Assert.Equal(new OutlineSegment(21, 0.5, 0), f.Segments[^1]);
        Assert.Equal(2, f.Segments.Count(s => s.ArcRadius > 0));
    }

    [Fact]
    public void S11871_seams_at_both_ends_of_a_side_end_its_neighbours_at_the_edge_and_leave_a_run_between()
    {
        // The top drawn only between 0.3 and 0.7: the left and the right run to the top edge, the
        // middle stretch is a figure of its own.
        var o = new CardOutline(false, false, false, false, true, true, true, true, [new OutlineSeam(CardSide.Top, 0, 0.3), new OutlineSeam(CardSide.Top, 0.7, 1)]);
        var figures = o.Stroke(100, 50, 1, 0);

        Assert.Equal(2, figures.Count);
        Assert.Contains(figures, f => (f.StartX, f.StartY) == (29, 0.5) && f.Segments.Single() == new OutlineSegment(71, 0.5, 0));
        Assert.Contains(figures, f => (f.StartX, f.StartY) == (99.5, 0) && f.Segments[^1] == new OutlineSegment(0.5, 0, 0));
    }
}
