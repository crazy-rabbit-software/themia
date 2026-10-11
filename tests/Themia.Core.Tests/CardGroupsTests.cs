using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

public class CardGroupsTests
{
    private const double Tol = 5;

    private static GroupCandidate Card(double x, double y, double w, double h, WidgetSurface? surface = WidgetSurface.Card)
        => new(new PixelRect(x, y, w, h), surface);

    private static List<List<int>> Groups(params GroupCandidate[] cards)
        => [.. CardGroups.Find(cards, Tol).Select(g => g.Members.ToList())];

    // -- [S-2100] / [S-2101] who touches -----------------------------------------------------------

    [Fact]
    public void S2101_side_by_side_cells_that_meet_form_one_group()
    {
        var groups = Groups(Card(0, 0, 100, 100), Card(100, 0, 100, 100));

        Assert.Equal([[0, 1]], groups);
    }

    [Fact]
    public void S2101_stacked_cells_that_meet_form_one_group()
    {
        Assert.Equal([[0, 1]], Groups(Card(0, 0, 100, 100), Card(0, 100, 100, 60)));
    }

    [Fact]
    public void S2101_a_partial_shared_edge_is_a_touch()
    {
        // B sits beside A's lower half only.
        Assert.Equal([[0, 1]], Groups(Card(0, 0, 100, 200), Card(100, 120, 80, 200)));
    }

    [Fact]
    public void S2101_touching_only_at_a_corner_is_not_a_touch()
    {
        Assert.Empty(Groups(Card(0, 0, 100, 100), Card(100, 100, 100, 100)));
    }

    [Fact]
    public void S2101_a_shared_run_no_longer_than_the_tolerance_is_a_corner_not_a_touch()
    {
        Assert.Empty(Groups(Card(0, 0, 100, 100), Card(100, 96, 100, 100)));
    }

    [Fact]
    public void S2101_one_empty_cell_between_two_widgets_keeps_them_apart()
    {
        // The snap cell is 10 DIP; a 10 DIP gap is one whole empty cell.
        Assert.Empty(Groups(Card(0, 0, 100, 100), Card(110, 0, 100, 100)));
        Assert.Empty(Groups(Card(0, 0, 100, 100), Card(0, 110, 100, 100)));
    }

    [Fact]
    public void S2101_a_rounding_pixel_either_way_still_touches()
    {
        Assert.Equal([[0, 1]], Groups(Card(0, 0, 100, 100), Card(101, 0, 100, 100)));
        Assert.Equal([[0, 1]], Groups(Card(0, 0, 100, 100), Card(99, 0, 100, 100)));
        Assert.Equal([[0, 1]], Groups(Card(0, 0, 100, 100), Card(104.9, 0, 100, 100)));
        Assert.Empty(Groups(Card(0, 0, 100, 100), Card(105, 0, 100, 100)));
    }

    [Fact]
    public void S2101_tolerance_is_half_a_snap_cell()
    {
        var screen = new ScreenMetrics(1920, 1080);

        Assert.Equal(5, CardGroups.Tolerance(screen), 3);
    }

    [Fact]
    public void S2101_snapped_fractions_resolved_to_pixels_meet_within_the_tolerance()
    {
        // Two widgets snapped against each other on a 2560x1440 surface, stored as fractions.
        var screen = new ScreenMetrics(2560, 1440);
        var (cols, _) = GridGeometry.GridDims(screen);
        var a = GridGeometry.Snap(new FracRect(0.1, 0.1, 37.0 / cols, 0.2), screen);
        var b = GridGeometry.Snap(new FracRect(a.Right, 0.1, 0.13, 0.2), screen);

        var cards = new[]
        {
            new GroupCandidate(GridGeometry.ToPixels(a, screen), WidgetSurface.Card),
            new GroupCandidate(GridGeometry.ToPixels(b, screen), WidgetSurface.Card),
        };

        Assert.Single(CardGroups.Find(cards, CardGroups.Tolerance(screen)));
    }

    [Fact]
    public void S2100_groups_are_transitive()
    {
        // A touches B, B touches C; A and C do not touch.
        var groups = Groups(Card(0, 0, 100, 100), Card(100, 0, 100, 100), Card(200, 0, 100, 100), Card(500, 0, 100, 100), Card(500, 100, 100, 100));

        Assert.Equal([[0, 1, 2], [3, 4]], groups);
    }

    [Fact]
    public void S2100_a_lone_card_is_in_no_group()
    {
        Assert.Empty(Groups(Card(0, 0, 100, 100)));
        Assert.Empty(Groups());
    }

    // -- [S-2102] what may join --------------------------------------------------------------------

    [Fact]
    public void S2102_a_transparent_or_hidden_widget_never_joins()
    {
        Assert.Empty(Groups(Card(0, 0, 100, 100), Card(100, 0, 100, 100, WidgetSurface.Transparent)));
        Assert.Empty(Groups(Card(0, 0, 100, 100), Card(100, 0, 100, 100, null)));
    }

    [Fact]
    public void S2102_a_transparent_widget_does_not_bridge_two_cards()
    {
        Assert.Empty(Groups(Card(0, 0, 100, 100), Card(100, 0, 100, 100, WidgetSurface.Transparent), Card(200, 0, 100, 100)));
    }

    [Fact]
    public void S2102_card_and_translucent_widgets_do_not_merge_but_like_ones_do()
    {
        Assert.Empty(Groups(Card(0, 0, 100, 100), Card(100, 0, 100, 100, WidgetSurface.Translucent)));
        Assert.Equal([[0, 1]], Groups(Card(0, 0, 100, 100, WidgetSurface.Translucent), Card(100, 0, 100, 100, WidgetSurface.Translucent)));
    }

    [Fact]
    public void S2102_overlapping_widgets_keep_their_own_cards()
    {
        // A and B overlap; C touches B. B is out (it overlaps), so C has nobody left to join.
        Assert.Empty(Groups(Card(0, 0, 100, 100), Card(50, 50, 100, 100), Card(150, 50, 100, 100)));
    }

    [Fact]
    public void S2102_overlapping_a_transparent_widget_still_keeps_the_card_out()
    {
        Assert.Empty(Groups(Card(0, 0, 100, 100), Card(100, 0, 100, 100), Card(120, 20, 40, 40, WidgetSurface.Transparent)));
    }

    // -- [S-2103] the outline ----------------------------------------------------------------------

    private static CardOutline Outline(double inset, double convex, double concave, params PixelRect[] cells)
        => CardGroups.Outline(cells, Tol, inset, convex, concave)!;

    [Fact]
    public void S2103_two_cards_side_by_side_make_one_rectangle_with_four_rounded_corners()
    {
        var outline = Outline(4, 8, 4, new PixelRect(0, 0, 100, 100), new PixelRect(100, 0, 100, 100));

        var loop = Assert.Single(outline.Loops);
        Assert.Equal(4, loop.Count);
        Assert.All(loop, c => Assert.True(c.Convex));
        Assert.All(loop, c => Assert.Equal(8, c.Radius));

        // Inset by half the gap on the outside only: no gap between the two members.
        Assert.Equal(4, loop.Min(c => c.X));
        Assert.Equal(196, loop.Max(c => c.X));
        Assert.Equal(4, loop.Min(c => c.Y));
        Assert.Equal(96, loop.Max(c => c.Y));
    }

    [Fact]
    public void S2103_the_outer_loop_runs_clockwise_on_screen()
    {
        var loop = Outline(0, 0, 0, new PixelRect(0, 0, 100, 100), new PixelRect(100, 0, 100, 100)).Loops[0];

        // Shoelace in y-down coordinates: positive means clockwise as drawn.
        var area = 0.0;
        for (var i = 0; i < loop.Count; i++)
        {
            var a = loop[i];
            var b = loop[(i + 1) % loop.Count];
            area += (a.X * b.Y) - (b.X * a.Y);
        }

        Assert.True(area > 0);
    }

    [Fact]
    public void S2103_an_L_shape_has_five_convex_corners_and_one_concave()
    {
        // A wide card over a narrower one at the left: an L.
        var outline = Outline(4, 8, 4, new PixelRect(0, 0, 200, 100), new PixelRect(0, 100, 100, 100));

        var loop = Assert.Single(outline.Loops);
        Assert.Equal(6, loop.Count);
        Assert.Equal(5, loop.Count(c => c.Convex));

        var concave = Assert.Single(loop, c => !c.Convex);

        // The concave corner moves with both of its edges, into the members: away from the empty quadrant.
        Assert.Equal(96, concave.X);
        Assert.Equal(96, concave.Y);
        Assert.Equal(4, concave.Radius);
    }

    [Fact]
    public void S2104_a_concave_corner_with_no_gap_is_sharp()
    {
        var outline = Outline(0, 8, 0, new PixelRect(0, 0, 200, 100), new PixelRect(0, 100, 100, 100));

        var concave = Assert.Single(outline.Loops[0], c => !c.Convex);
        Assert.Equal(0, concave.Radius);
        Assert.Equal(concave.InX, concave.OutX);
        Assert.Equal(concave.InY, concave.OutY);
    }

    [Fact]
    public void S2103_tangent_points_sit_on_the_edges_beside_the_corner()
    {
        var loop = Outline(0, 10, 0, new PixelRect(0, 0, 100, 100), new PixelRect(100, 0, 100, 100)).Loops[0];

        var topRight = Assert.Single(loop, c => c.X == 200 && c.Y == 0);
        Assert.Equal((190.0, 0.0), (topRight.InX, topRight.InY));
        Assert.Equal((200.0, 10.0), (topRight.OutX, topRight.OutY));
    }

    [Fact]
    public void S2103_a_radius_is_capped_at_half_the_shorter_edge_beside_it()
    {
        // A 10 DIP step between two stacked cards: its two corners can only round by 5.
        var loop = Outline(0, 16, 16, new PixelRect(0, 0, 100, 100), new PixelRect(0, 100, 110, 100)).Loops[0];

        var step = loop.Where(c => c.Y == 100).ToList();
        Assert.Equal(2, step.Count);
        Assert.All(step, c => Assert.Equal(5, c.Radius));
    }

    [Fact]
    public void S2103_edges_within_the_tolerance_are_welded_so_no_notch_opens()
    {
        // Unsnapped: B's right edge is 2 DIP short of A's, and they meet 1 DIP apart.
        var loop = Outline(0, 0, 0, new PixelRect(0, 0, 100, 100), new PixelRect(0, 101, 98, 100)).Loops[0];

        Assert.Equal(4, loop.Count);
    }

    [Fact]
    public void S2103_a_ring_around_an_empty_hole_has_an_outer_loop_and_a_hole()
    {
        // Four cards framing a 100x100 hole.
        var outline = Outline(4, 8, 4,
            new PixelRect(0, 0, 300, 100),
            new PixelRect(0, 100, 100, 100),
            new PixelRect(200, 100, 100, 100),
            new PixelRect(0, 200, 300, 100));

        Assert.Equal(2, outline.Loops.Count);
        var hole = outline.Loops.Single(l => l.All(c => !c.Convex));
        Assert.Equal(4, hole.Count);

        // The hole grows by the inset: the gap faces it too.
        Assert.Equal(96, hole.Min(c => c.X));
        Assert.Equal(204, hole.Max(c => c.X));
    }

    [Fact]
    public void S2103_a_staircase_has_a_corner_at_every_step()
    {
        var outline = Outline(4, 8, 4,
            new PixelRect(0, 0, 100, 100),
            new PixelRect(100, 0, 100, 100),
            new PixelRect(100, 100, 100, 100),
            new PixelRect(200, 100, 100, 100));

        var loop = Assert.Single(outline.Loops);
        Assert.Equal(8, loop.Count);
        Assert.Equal(2, loop.Count(c => !c.Convex));
    }

    [Fact]
    public void S2103_members_meeting_only_at_a_corner_inside_a_group_keep_two_rounded_corners()
    {
        // P1 and P2 meet only at (100,200); the top bar and the right column join them.
        var outline = Outline(4, 8, 4,
            new PixelRect(0, 100, 100, 100),
            new PixelRect(100, 200, 100, 100),
            new PixelRect(0, 0, 300, 100),
            new PixelRect(200, 100, 100, 200));

        // The empty middle reaches the outside through the pinch, so it is one loop passing it twice,
        // and both members' corners there are convex and pulled apart by the inset.
        var loop = Assert.Single(outline.Loops);
        Assert.Contains(loop, c => c.Convex && c.X == 96 && c.Y == 196);
        Assert.Contains(loop, c => c.Convex && c.X == 104 && c.Y == 204);
        Assert.False(Covers(outline.Region, 100, 200));
        Assert.True(Covers(outline.Region, 50, 150));
        Assert.True(Covers(outline.Region, 150, 250));
    }

    [Fact]
    public void S2103_an_inset_wider_than_a_member_gives_no_outline()
    {
        Assert.Null(CardGroups.Outline([new PixelRect(0, 0, 40, 100), new PixelRect(40, 0, 40, 30)], Tol, 32, 8, 8));
    }

    // -- [S-2105] the region ----------------------------------------------------------------------

    private static bool Covers(IReadOnlyList<PixelRect> region, double x, double y)
        => region.Any(r => x >= r.X && x <= r.X + r.Width && y >= r.Y && y <= r.Y + r.Height);

    [Fact]
    public void S2105_the_region_covers_the_painted_shape_and_not_the_gap_around_it()
    {
        var outline = Outline(4, 8, 4, new PixelRect(0, 0, 200, 100), new PixelRect(0, 100, 100, 100));

        Assert.True(Covers(outline.Region, 50, 50));
        Assert.True(Covers(outline.Region, 150, 50));
        Assert.True(Covers(outline.Region, 50, 150));

        // The seam between the two members is painted: no gap inside a group.
        Assert.True(Covers(outline.Region, 50, 100));

        // The gap on the outside stays the desktop's, and so does the empty quadrant of the L.
        Assert.False(Covers(outline.Region, 2, 50));
        Assert.False(Covers(outline.Region, 150, 150));
        Assert.False(Covers(outline.Region, 110, 110));
    }

    [Fact]
    public void S2105_the_region_takes_the_fillet_of_a_rounded_concave_corner()
    {
        var outline = Outline(4, 8, 4, new PixelRect(0, 0, 200, 100), new PixelRect(0, 100, 100, 100));

        // Just past the inset concave corner (96,96), inside its 4 DIP fillet; beyond it is the gap.
        Assert.True(Covers(outline.Region, 97, 97));
        Assert.False(Covers(outline.Region, 101, 101));
    }

    [Fact]
    public void S2105_the_region_leaves_a_hole_open()
    {
        var outline = Outline(4, 8, 4,
            new PixelRect(0, 0, 300, 100),
            new PixelRect(0, 100, 100, 100),
            new PixelRect(200, 100, 100, 100),
            new PixelRect(0, 200, 300, 100));

        Assert.False(Covers(outline.Region, 150, 150));
        Assert.True(Covers(outline.Region, 50, 150));
    }

    [Fact]
    public void S2104_the_fillet_never_reaches_past_the_members_cells()
    {
        var cells = new[] { new PixelRect(0, 0, 200, 100), new PixelRect(0, 100, 100, 100) };
        var outline = Outline(4, 8, 4, cells);

        foreach (var r in outline.Region)
        {
            // Every region rect's corners lie inside some member cell.
            foreach (var (x, y) in new[] { (r.X, r.Y), (r.X + r.Width, r.Y), (r.X, r.Y + r.Height), (r.X + r.Width, r.Y + r.Height) })
            {
                Assert.Contains(cells, c => x >= c.X && x <= c.X + c.Width && y >= c.Y && y <= c.Y + c.Height);
            }
        }
    }

    [Fact]
    public void S2107_the_render_fixture_groups_and_outlines_in_every_look()
    {
        var screen = new ScreenMetrics(960, 540);
        var scene = new[]
        {
            (20.0, 20.0, 200.0, 140.0, WidgetSurface.Card), (220, 20, 160, 140, WidgetSurface.Card),
            (20, 190, 240, 130, WidgetSurface.Card), (20, 320, 130, 200, WidgetSurface.Card),
            (420, 20, 180, 140, WidgetSurface.Card), (610, 20, 180, 140, WidgetSurface.Card),
            (300, 210, 160, 120, WidgetSurface.Card), (460, 210, 160, 120, WidgetSurface.Card), (460, 330, 220, 120, WidgetSurface.Card),
            (720, 210, 200, 120, WidgetSurface.Card), (720, 330, 200, 120, WidgetSurface.Transparent),
        };
        var cards = scene
            .Select(s => new GroupCandidate(GridGeometry.ToPixels(new FracRect(s.Item1 / 960, s.Item2 / 540, s.Item3 / 960, s.Item4 / 540), screen), s.Item5))
            .ToList();
        var tol = CardGroups.Tolerance(screen);

        var groups = CardGroups.Find(cards, tol);
        Assert.Equal([[0, 1], [2, 3], [6, 7, 8]], groups.Select(g => g.Members.ToList()).ToList());

        foreach (var (inset, convex, concave) in new[] { (4.0, 8.0, 4.0), (0, 8, 0), (6, 16, 6), (7, 15, 7) })
        {
            foreach (var g in groups)
            {
                Assert.NotNull(CardGroups.Outline([.. g.Members.Select(m => cards[m].Cell)], tol, inset, convex, concave));
            }
        }
    }
}
