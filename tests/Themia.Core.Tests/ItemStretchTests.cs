using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-12400] [S-12410] [S-12807] A widget's items fill its content (owner, 2026-11-19: "the items have no spacing
/// between them and they try to take up the whole space from top to bottom and left to right"): rows grow until they
/// touch and fill the room; texts keep their size.
/// </summary>
public class ItemStretchTests
{
    private static readonly ItemGrid Grid = ItemGrid.Default();

    [Theory]
    [InlineData(120, 4, 30, 4)]     // four rows of three cells: three cells each
    [InlineData(160, 4, 40, 4)]     // more room: four cells each, touching
    [InlineData(1000, 4, 250, 4)]   // no cap: the rows take it all
    [InlineData(112, 4, 30, 3)]     // [S-12830] 11 cells hold three rows of three, never four squeezed
    [InlineData(90, 4, 30, 3)]      // short of room: the rows the room holds
    public void S12830_rows_are_whole_cells_and_as_many_as_the_room_holds(double available, int count, double pitch, int shown)
    {
        var reach = ItemStretch.Reach(available, count, 28, Grid);

        Assert.Equal(pitch, reach.Pitch, 6);
        Assert.Equal(shown, reach.Shown);
        Assert.True(reach.Length(count) <= available + 1e-9);
    }

    [Fact]
    public void S12830_a_widget_ten_cells_tall_shows_three_rows_of_three_not_four_of_two_and_a_half()
    {
        // The owner's own case: "a widget that is 10 blocks hight should not show 4 items".
        var reach = ItemStretch.Rows(100 - FamilySpace.Inset, 4, 1, Grid);

        Assert.Equal(3, reach.Shown);
        Assert.Equal(30, reach.Pitch, 6);

        // The cell left over stands at the foot: the rows start on the card's top edge.
        Assert.Equal(90, reach.Length(4), 6);
    }

    [Theory]
    [InlineData(9.6)]
    [InlineData(10)]
    [InlineData(10.4)]
    public void S12830_rows_follow_the_displays_pitch(double cell)
    {
        var grid = new ItemGrid(cell);
        foreach (var card in new[] { 13, 20, 37, 60 })
        {
            var reach = ItemStretch.Rows((card * cell) - FamilySpace.Inset, 6, 1, grid);

            Assert.True(grid.IsWhole(reach.Pitch, 1e-6), $"{card} cells: {reach.Pitch} is not whole cells of {cell}");
            Assert.True(reach.Pitch >= FamilyShape.RowHeight(1));
            var left = (card * cell) - reach.Length(6);
            Assert.InRange(left, -1e-6, Math.Min(6, reach.Shown) * cell);
        }
    }

    [Fact]
    public void S12807_a_views_rows_run_from_the_cards_top_edge()
    {
        // [S-12815] A view under the card's top inset: the rows run from the card's top edge, whole cells, touching.
        Assert.Equal(ItemStretch.Reach(136, 4, 28, Grid), ItemStretch.Rows(124, 4, 1, Grid));
        Assert.Equal(new RowReach(40, 4), ItemStretch.Rows(172, 4, 1, Grid));

        // Under a tool line, a search box or column titles: from the view's top, what stands over them whole cells.
        Assert.Equal(new RowReach(30, 4), ItemStretch.Rows(172, 4, 1, Grid, reserved: 12, edge: false));
    }

    [Fact]
    public void S12807_rows_over_an_add_row_end_where_it_starts()
    {
        // Four rows, the add row of 28 (three cells) on the card's foot edge, the rows from its top edge.
        const double View = (4 * 40) + 30 - 12;
        Assert.Equal(new RowReach(40, 4), ItemStretch.RowsAbove(View, 4, 1, Grid));
    }

    [Fact]
    public void S12830_unlike_items_share_whole_cells_the_growing_ones_take_them()
    {
        // A row (28: three cells), a fixed readout of 36, a two-line row (40: four cells) in 140: 34 over them, three
        // whole cells, one each; the cell left over stands at the end.
        var slots = ItemStretch.Reach(140, [StretchItem.Row(1), StretchItem.Fixed(36), StretchItem.Row(2)], Grid);

        Assert.Equal(new ItemSlot(0, 40), slots[0]);
        Assert.Equal(new ItemSlot(40, 36), slots[1]);
        Assert.Equal(new ItemSlot(76, 50), slots[2]);
    }

    [Fact]
    public void S12830_like_rows_are_alike_and_the_left_over_cells_stand_at_the_foot()
    {
        var slots = ItemStretch.Reach(137, [StretchItem.Row(1), StretchItem.Row(1), StretchItem.Row(1), StretchItem.Row(1)], Grid);

        Assert.All(slots, s => Assert.Equal(30, s.Length, 6));
        Assert.Equal(0, slots[0].Start);
        Assert.Equal(120, slots[^1].End, 6);
    }

    [Fact]
    public void S12807_with_nothing_to_grow_the_items_stand_together_centred()
    {
        var slots = ItemStretch.Reach(100, [StretchItem.Fixed(36), StretchItem.Fixed(16)], Grid, gap: 8);

        Assert.Equal(new ItemSlot(20, 36), slots[0]);
        Assert.Equal(new ItemSlot(64, 16), slots[1]);
    }

    [Fact]
    public void S12807_items_never_overlap_nor_shrink()
    {
        foreach (var room in new[] { 0.0, 37, 60, 113, 200, 999 })
        {
            var items = new[] { StretchItem.Row(1), StretchItem.Row(3), StretchItem.Fixed(24), StretchItem.Row(2) };
            var slots = ItemStretch.Reach(room, items, Grid, gap: 4);
            for (var i = 0; i < items.Length; i++)
            {
                Assert.True(slots[i].Length >= items[i].Natural - 1e-9);
            }

            for (var i = 1; i < slots.Length; i++)
            {
                Assert.True(slots[i].Start >= slots[i - 1].End + 4 - 1e-9, $"room {room}: item {i} overlaps");
            }
        }
    }

    [Fact]
    public void S12807_nothing_to_lay_and_bad_input_are_harmless()
    {
        Assert.Empty(ItemStretch.Reach(100, [], Grid));
        Assert.Equal(new RowReach(30, 3), ItemStretch.Reach(double.NaN, 3, 28, Grid));
        Assert.Equal(new RowReach(30, 0), ItemStretch.Reach(100, 0, 28, Grid));
        var slots = ItemStretch.Reach(double.NaN, [new StretchItem(double.NaN, true), StretchItem.Fixed(-5)], new ItemGrid(double.NaN, 0), gap: double.NaN);
        Assert.Equal(10, slots[0].Length);
        Assert.Equal(0, slots[1].Length);
    }

    [Fact]
    public void S12830_whole_cells_are_whole_cells()
    {
        Assert.Equal(3, Grid.Cells(28));
        Assert.Equal(3, Grid.Cells(30));
        Assert.Equal(1, Grid.Cells(0));
        Assert.Equal(11, Grid.Within(112));
        Assert.True(Grid.IsWhole(30));
        Assert.False(Grid.IsWhole(25));
        Assert.Equal(10, new ItemGrid(-1).Pitch);
    }

    [Fact]
    public void S12807_a_line_inks_from_its_capitals_to_its_baseline()
    {
        // The family's face on a line of the family's: a Body line (14/20) inks its capitals 6.2 under its top and
        // stands its baseline 4 over its foot; a 28 readout (28/36) 9.2 and 7.2. What a one-readout block and a clock
        // fill their box with ([S-12808]).
        Assert.Equal(6.2, LineInk.Of(FamilyType.Body).Over, 6);
        Assert.Equal(4, LineInk.Of(FamilyType.Body).Under, 6);
        Assert.Equal(9.2, LineInk.Of(FamilyType.Big28).Over, 6);
        Assert.Equal(7.2, LineInk.Of(FamilyType.Big28).Under, 6);
        Assert.Equal(new LineInk(2, 2), LineInk.Row(1, FamilyShape.Icon));
        Assert.Equal(LineInk.None, LineInk.Of(new TypeStep(double.NaN, 20, false)));
    }
}
