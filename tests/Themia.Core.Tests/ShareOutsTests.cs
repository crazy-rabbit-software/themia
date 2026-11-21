using Themia.Core.Geometry;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-15026] [S-15027] A card shared out among several kinds, laid on whole grid cells, each part at least its minimum.</summary>
public sealed class ShareOutsTests
{
    private static readonly MergeShare[] LeadBesideStack = [new(0, 0, 1.0 / 3, 1), new(1.0 / 3, 0, 2.0 / 3, 0.5), new(1.0 / 3, 0.5, 2.0 / 3, 0.5)];

    private static readonly (int W, int H)[] Mins = [(4, 4), (20, 3), (13, 4)];

    private static readonly CellBox Screen = new(0, 0, 256, 144);

    private static IReadOnlyList<CellBox> Lay(CellBox card, params CellBox[] others)
        => ShareOuts.Lay(card, LeadBesideStack, Mins, box => !others.Any(o => o.Overlaps(box)), Screen)!;

    [Fact]
    public void S15027_whole_cells_are_shared_by_weight_a_length_under_its_minimum_held_and_what_rounding_left_given_where_most_was_lost()
    {
        Assert.Equal([10, 20, 30], ShareOuts.Split(60, [1, 2, 3], [0, 0, 0]));
        Assert.Equal([25, 14, 21], ShareOuts.Split(60, [1, 2, 3], [25, 0, 0]));
        Assert.Equal([11, 21], ShareOuts.Split(32, [1.0 / 3, 2.0 / 3], [4, 20]));
        Assert.Equal([6, 5], ShareOuts.Split(11, [0.5, 0.5], [3, 4]));
    }

    [Fact]
    public void S15027_a_card_that_holds_its_parts_lays_them_on_whole_cells_inside_it()
    {
        var card = new CellBox(187, 8, 30, 11);
        var boxes = Lay(card, new CellBox(187, 19, 30, 12));

        Assert.Equal([new CellBox(187, 8, 10, 11), new CellBox(197, 8, 20, 6), new CellBox(197, 14, 20, 5)], boxes);
    }

    [Fact]
    public void S15026_the_line_beside_the_lead_moves_before_the_card_is_stacked_or_grown()
        => Assert.Equal([new CellBox(60, 30, 5, 16), new CellBox(65, 30, 20, 8), new CellBox(65, 38, 20, 8)], Lay(new CellBox(60, 30, 25, 16)));

    [Fact]
    public void S15026_a_card_too_narrow_for_the_lead_beside_stacks_it_on_top()
    {
        var boxes = Lay(new CellBox(60, 30, 21, 16));

        Assert.All(boxes, b => Assert.Equal((60, 21), (b.X, b.W)));
        Assert.Equal([30, boxes[0].Bottom, boxes[1].Bottom], boxes.Select(b => b.Y));
        Assert.Equal(46, boxes[2].Bottom);
    }

    [Fact]
    public void S15027_a_card_too_small_grows_toward_free_cells_and_last_moves_to_the_nearest_free_ones()
    {
        var card = new CellBox(60, 30, 15, 6);
        var below = new CellBox(60, 36, 30, 12);
        var right = new CellBox(75, 30, 6, 6);
        var grown = Lay(card, below, right);
        Assert.All(grown, b => Assert.False(b.Overlaps(below) || b.Overlaps(right)));
        Assert.Equal(new CellBox(51, 29, 24, 7), Block(grown));

        var above = new CellBox(50, 18, 40, 12);
        var left = new CellBox(54, 30, 6, 6);
        var moved = Lay(card, new CellBox(50, 36, 40, 12), above, left, right);
        Assert.All(moved, b => Assert.False(b.Overlaps(above) || b.Overlaps(left) || b.Overlaps(right) || b.Overlaps(new CellBox(50, 36, 40, 12))));
    }

    [Fact]
    public void S15026_shares_that_are_not_a_lead_beside_a_stack_are_left_as_shared()
        => Assert.Null(ShareOuts.Lay(new CellBox(0, 0, 10, 10), [new(0, 0, 0.5, 0.5), new(0.5, 0, 0.5, 0.5)], [(1, 1), (1, 1)], _ => true, Screen));

    private static CellBox Block(IReadOnlyList<CellBox> boxes)
    {
        var (x, y) = (boxes.Min(b => b.X), boxes.Min(b => b.Y));
        return new CellBox(x, y, boxes.Max(b => b.Right) - x, boxes.Max(b => b.Bottom) - y);
    }
}
