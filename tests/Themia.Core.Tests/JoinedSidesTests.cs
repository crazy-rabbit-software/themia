using Themia.Core.Geometry;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-5700] [S-12480]..[S-12484] A card has no header: its view's top is the card's inset, none where a group
/// mate joins it; on a joined top or foot the content runs to the edge so neighbouring members' items meet
/// across the seam; the left and the right keep the inset.
/// </summary>
public sealed class JoinedSidesTests
{
    private static CardOutline[] Paint(PixelRect[] cells, string?[] groups)
        => CardJoins.Paint(cells, [.. cells.Select(_ => (int?)0)], groups, 1);

    [Fact]
    public void S5700_a_free_card_starts_its_view_at_the_inset_and_keeps_the_foot()
    {
        var free = JoinedSides.Free;
        Assert.Equal(FamilySpace.Inset, free.TopInset(ownsTopEdge: false));
        Assert.Equal(FamilySpace.Inset, free.Foot);
        Assert.Equal(BlockEdge.Centre, free.Block);
        Assert.False(free.Any);
    }

    [Fact]
    public void S5700_a_view_that_owns_its_top_edge_gets_its_whole_box()
        => Assert.Equal(0, JoinedSides.Free.TopInset(ownsTopEdge: true));

    [Fact]
    public void S12480_the_content_starts_at_the_inset_with_no_header_room()
        => Assert.Equal(FamilySpace.Inset, FamilySpace.ContentTop);

    [Fact]
    public void S12482_a_joined_top_has_no_inset_and_a_joined_foot_none_either()
    {
        var both = new JoinedSides(Top: true, Bottom: true);
        Assert.Equal(0, both.TopInset(ownsTopEdge: false));
        Assert.Equal(0, both.Foot);
        Assert.Equal(FamilySpace.Inset, new JoinedSides(Top: true, Bottom: false).Foot);
        Assert.Equal(FamilySpace.Inset, new JoinedSides(Top: false, Bottom: true).TopInset(ownsTopEdge: false));
    }

    [Theory]
    [InlineData(false, false, BlockEdge.Centre, 10)]
    [InlineData(true, false, BlockEdge.Top, 0)]
    [InlineData(false, true, BlockEdge.Bottom, 21)]
    [InlineData(true, true, BlockEdge.Centre, 10)]
    public void S12483_a_fixed_block_stands_against_the_one_joined_side(bool top, bool bottom, BlockEdge block, double above)
    {
        var sides = new JoinedSides(top, bottom);
        Assert.Equal(block, sides.Block);
        Assert.Equal(above, sides.Above(21));
        Assert.Equal(0, sides.Above(-5));
        Assert.Equal(0, sides.Above(double.NaN));
    }

    [Fact]
    public void S12482_members_stacked_in_a_group_are_joined_at_the_seam_only()
    {
        // A header over a reading over a list, one group: the header's foot, the reading's top and foot, the list's top.
        var outlines = Paint(
            [new PixelRect(0, 0, 300, 40), new PixelRect(0, 40, 300, 80), new PixelRect(0, 120, 300, 140)],
            ["g", "g", "g"]);
        Assert.Equal(new JoinedSides(false, true), outlines[0].Joined);
        Assert.Equal(new JoinedSides(true, true), outlines[1].Joined);
        Assert.Equal(new JoinedSides(true, false), outlines[2].Joined);
    }

    [Fact]
    public void S12482_a_mate_under_part_of_the_foot_joins_it_and_cards_of_no_group_never_do()
    {
        // A wide header over two members side by side: each member's top is joined, and so is the header's foot.
        var outlines = Paint(
            [new PixelRect(0, 0, 600, 40), new PixelRect(0, 40, 300, 200), new PixelRect(300, 40, 300, 200)],
            ["g", "g", "g"]);
        Assert.True(outlines[0].Joined.Bottom);
        Assert.True(outlines[1].Joined.Top);
        Assert.True(outlines[2].Joined.Top);

        // [S-12484] Side by side the members are not joined on the top or the foot: the left and the right keep the inset.
        Assert.False(outlines[1].Joined.Bottom);

        // [S-2240] Cards of no group that touch stay separate cards: free.
        var loose = Paint([new PixelRect(0, 0, 300, 40), new PixelRect(0, 40, 300, 80)], [null, null]);
        Assert.All(loose, o => Assert.Equal(JoinedSides.Free, o.Joined));
    }

    [Theory]
    [InlineData(false, false, BlockEdge.Top)]
    [InlineData(true, false, BlockEdge.Top)]
    [InlineData(false, true, BlockEdge.Bottom)]
    [InlineData(true, true, BlockEdge.Top)]
    public void S12800_rows_from_the_top_stand_at_the_foot_only_where_only_the_foot_is_joined(bool top, bool bottom, BlockEdge rows)
        => Assert.Equal(rows, new JoinedSides(top, bottom).Rows);
}
