using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-12400] [S-12410] [S-12807] [S-14500] A widget's items fill its content (owner, 2026-11-19: "the items have no spacing
/// between them and they try to take up the whole space from top to bottom and left to right"; 2026-11-20: "the items
/// should have an optimal size and then they may stretch a little to fill up the remaining space"): rows stand at their
/// optimal, as many as the room holds, and share it to its end; texts keep their size.
/// </summary>
public class ItemStretchTests
{
    private static readonly ItemGrid Grid = ItemGrid.Default();

    [Theory]
    [InlineData(120, 30, 4)]        // four rows of three cells: three cells each
    [InlineData(160, 32, 5)]        // five rows hold: each 32, touching
    [InlineData(1000, 1000.0 / 33, 33)] // no cap: as many as the room holds, sharing it
    [InlineData(112, 112.0 / 3, 3)] // 11 cells and a bit hold three rows, each a third, never four squeezed
    [InlineData(20, 30, 1)]         // short of room: one row at its optimal
    public void S14500_rows_are_as_many_as_the_room_holds_at_their_optimal_sharing_it(double available, double pitch, int shown)
    {
        var reach = ItemStretch.Reach(available, 28, Grid);

        Assert.Equal(pitch, reach.Pitch, 6);
        Assert.Equal(shown, reach.Shown);
    }

    [Theory]
    [InlineData(9.6)]
    [InlineData(10)]
    [InlineData(10.4)]
    public void S14500_rows_follow_the_displays_pitch(double cell)
    {
        var grid = new ItemGrid(cell);
        foreach (var card in new[] { 13, 20, 37, 60 })
        {
            var reach = ItemStretch.Rows((card * cell) - FamilySpace.Inset, 1, grid);

            Assert.True(reach.Pitch >= grid.Whole(FamilyShape.RowHeight(1)) - 1e-6);
            Assert.Equal(card * cell, reach.Room, 6);
        }
    }

    [Fact]
    public void S12807_a_views_rows_run_from_the_cards_top_edge()
    {
        // [S-12815] A view under the card's top inset: the rows run from the card's top edge, touching, to its foot.
        Assert.Equal(ItemStretch.Reach(136, 28, Grid), ItemStretch.Rows(124, 1, Grid));
        Assert.Equal(new RowReach(184.0 / 6, 6), ItemStretch.Rows(172, 1, Grid));

        // Under a tool line, a search box or column titles: from the view's top, what stands over them whole cells.
        Assert.Equal(new RowReach(152.0 / 5, 5), ItemStretch.Rows(172, 1, Grid, reserved: 12, edge: false));
    }

    [Fact]
    public void S12807_rows_over_an_add_row_end_where_it_starts()
    {
        // Four rows of 40 and the add row of 30 under them, the card's height: the rows from its top edge, the add row on its foot.
        const double View = (4 * 40) + 30 - 12;
        var reach = ItemStretch.RowsAbove(View, 2, Grid);
        Assert.Equal((40.0, 4, 30.0), (reach.Pitch, reach.Shown, reach.Foot));
    }

    [Fact]
    public void S14500_unlike_items_share_the_room_the_growing_ones_take_it()
    {
        // A row (28: three cells), a fixed readout of 36, a two-line row (40: four cells) in 140: the 34 over them shared by
        // the two rows, 17 each; nothing left at the end.
        var slots = ItemStretch.Reach(140, [StretchItem.Row(1), StretchItem.Fixed(36), StretchItem.Row(2)], Grid);

        Assert.Equal(new ItemSlot(0, 47), slots[0]);
        Assert.Equal(new ItemSlot(47, 36), slots[1]);
        Assert.Equal(new ItemSlot(83, 57), slots[2]);
        Assert.Equal(140, slots[^1].End, 6);
    }

    [Fact]
    public void S14500_like_rows_are_alike_and_end_on_the_rooms_end()
    {
        var slots = ItemStretch.Reach(137, [StretchItem.Row(1), StretchItem.Row(1), StretchItem.Row(1), StretchItem.Row(1)], Grid);

        Assert.All(slots, s => Assert.InRange(s.Length, 34, 35));
        Assert.Equal(0, slots[0].Start);
        Assert.Equal(137, slots[^1].End, 6);
        for (var i = 1; i < slots.Length; i++)
        {
            Assert.Equal(slots[i - 1].End, slots[i].Start, 6);
        }
    }

    [Fact]
    public void S14500_the_room_past_alike_rows_holds_empty_rows_at_the_foot()
    {
        var rows = new[] { StretchItem.Row(1), StretchItem.Row(1) };

        // Two rows of three cells in 130: two more stand empty under them; in 89 none.
        Assert.Equal(2, ItemStretch.SpareRows(130, rows, Grid));
        Assert.Equal(0, ItemStretch.SpareRows(89, rows, Grid));

        // Unlike growing items (a row and a two-line row) leave none: they stretch.
        Assert.Equal(0, ItemStretch.SpareRows(300, [StretchItem.Row(1), StretchItem.Row(2)], Grid));
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
        Assert.Equal(new RowReach(30, 1), ItemStretch.Reach(double.NaN, 28, Grid));
        Assert.Equal(new RowReach(30, 1), ItemStretch.Reach(-5, 28, Grid));
        var slots = ItemStretch.Reach(double.NaN, [new StretchItem(double.NaN, true), StretchItem.Fixed(-5)], new ItemGrid(double.NaN, 0), gap: double.NaN);
        Assert.Equal(10, slots[0].Length);
        Assert.Equal(0, slots[1].Length);
    }

    [Theory]
    [InlineData(600, 3, new double[] { 200, 200, 200 })]
    [InlineData(350, 3, new double[] { 117, 116, 117 })]       // a third each on whole pixels, the edges rounded
    [InlineData(355, 3, new double[] { 118, 119, 118 })]
    [InlineData(20, 3, new double[] { 7, 6, 7 })]
    public void S14500_columns_run_edge_to_edge_sharing_the_width(double width, int count, double[] expected)
    {
        var columns = ItemStretch.Columns(width, count, Grid);

        // WR-3: from the card's left edge to its right; alike to a pixel.
        Assert.Equal(width, columns.Sum(), 6);
        Assert.True(columns.Max() - columns.Min() <= 1 + 1e-6);
        Assert.Equal(expected, columns.Select(c => Math.Round(c, 6)), new ToleranceComparer());
    }

    private sealed class ToleranceComparer : IEqualityComparer<double>
    {
        public bool Equals(double x, double y) => Math.Abs(x - y) < 1e-6;

        public int GetHashCode(double obj) => 0;
    }

    [Fact]
    public void S12830_whole_cells_are_whole_cells()
    {
        Assert.Equal(3, Grid.Cells(28));
        Assert.Equal(3, Grid.Cells(30));
        Assert.Equal(1, Grid.Cells(0));
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
