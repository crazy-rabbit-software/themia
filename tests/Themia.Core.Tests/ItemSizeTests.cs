using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-14810] [S-12807] Items have one fixed size (owner, 2026-11-20: "the widget items should actually have a fixed size.
/// then when you change the height of a widget the items should not grow or shrink to fit all the way from the top to the
/// bottom, they should just keep their size and the last item will be cutoff to some extent then."): rows stand at their
/// optimal from the room's start, touching, whatever the room; the last one the room reaches is cut by its end; texts keep
/// their size.
/// </summary>
public class ItemSizeTests
{
    private static readonly ItemGrid Grid = ItemGrid.Default();

    [Theory]
    [InlineData(120, 4)]  // four rows of three cells, whole
    [InlineData(160, 6)]  // five whole and the sixth cut by the foot
    [InlineData(1000, 34)] // no cap: every row the room reaches
    [InlineData(112, 4)]  // 11 cells and a bit: three whole, the fourth cut
    [InlineData(20, 1)]   // short of room: one row at its optimal, cut
    public void S14810_rows_keep_their_optimal_and_the_room_shows_every_one_it_reaches(double available, int shown)
    {
        var reach = ItemSize.Reach(available, 28, Grid);

        Assert.Equal(30, reach.Pitch, 6);
        Assert.Equal(shown, reach.Shown);
        Assert.Equal(available, reach.Room, 6);
    }

    [Theory]
    [InlineData(9.6)]
    [InlineData(10)]
    [InlineData(10.4)]
    public void S14810_rows_are_whole_cells_of_the_displays_pitch_at_every_card(double cell)
    {
        var grid = new ItemGrid(cell);
        foreach (var card in new[] { 13, 20, 37, 60 })
        {
            var reach = ItemSize.Rows((card * cell) - FamilySpace.Inset, 1, grid);

            Assert.Equal(grid.Whole(FamilyShape.RowHeight(1)), reach.Pitch, 6);
            Assert.Equal(card * cell, reach.Room, 6);
        }
    }

    [Fact]
    public void S12807_a_views_rows_run_from_the_cards_top_edge()
    {
        // [S-12815] A view under the card's top inset: the rows run from the card's top edge, touching, at their pitch.
        Assert.Equal(ItemSize.Reach(136, 28, Grid), ItemSize.Rows(124, 1, Grid));
        Assert.Equal((30.0, 7, 184.0), (ItemSize.Rows(172, 1, Grid).Pitch, ItemSize.Rows(172, 1, Grid).Shown, ItemSize.Rows(172, 1, Grid).Room));

        // Under a tool line, a search box or column titles: from the view's top, what stands over them whole cells.
        var under = ItemSize.Rows(172, 1, Grid, reserved: 12, edge: false);
        Assert.Equal((30.0, 6, 152.0), (under.Pitch, under.Shown, under.Room));
    }

    [Fact]
    public void S14811_rows_over_an_add_row_keep_their_size_and_the_add_row_its_own()
    {
        // Four rows of 40 and the add row of 30 under them, the card's height: every one at its optimal.
        const double View = (4 * 40) + 30 - 12;
        var reach = ItemSize.RowsAbove(View, 2, Grid);
        Assert.Equal((40.0, 4, 30.0, 160.0), (reach.Pitch, reach.Shown, reach.Foot, reach.Room));

        // Eleven cells of one-line rows: two whole, the third cut by the add row, which keeps its three cells.
        var eleven = ItemSize.RowsAbove(110 - FamilySpace.Inset, 1, Grid);
        Assert.Equal((30.0, 3, 30.0, 80.0), (eleven.Pitch, eleven.Shown, eleven.Foot, eleven.Room));
    }

    [Fact]
    public void S14810_rows_and_parts_down_a_stack_keep_their_lengths_from_its_start()
    {
        // A row (28: three cells), a fixed readout of 36, a two-line row (40: four cells) in 140: 30, 36, 40 from the top,
        // the 34 under them empty; nothing grows.
        var slots = ItemSize.Lay(140, [StackItem.Row(1), StackItem.Fixed(36), StackItem.Row(2)], Grid);

        Assert.Equal(new ItemSlot(0, 30), slots[0]);
        Assert.Equal(new ItemSlot(30, 36), slots[1]);
        Assert.Equal(new ItemSlot(66, 40), slots[2]);
    }

    [Fact]
    public void S14810_like_rows_touch_at_their_optimal_and_the_last_runs_past_the_room()
    {
        // 137 holds four rows of 30 and part of a fifth: the fifth stands at its size, cut by the room's end.
        var slots = ItemSize.Lay(137, [.. Enumerable.Repeat(StackItem.Row(1), 5)], Grid);

        Assert.All(slots, s => Assert.Equal(30, s.Length, 6));
        Assert.Equal(0, slots[0].Start);
        for (var i = 1; i < slots.Length; i++)
        {
            Assert.Equal(slots[i - 1].End, slots[i].Start, 6);
        }

        Assert.Equal(150, slots[^1].End, 6);
    }

    [Fact]
    public void S14810_fewer_rows_than_room_leave_the_room_under_the_last_empty()
    {
        var slots = ItemSize.Lay(300, [StackItem.Row(1), StackItem.Row(1)], Grid);

        Assert.Equal(new ItemSlot(0, 30), slots[0]);
        Assert.Equal(new ItemSlot(30, 30), slots[1]);
    }

    [Fact]
    public void S12807_with_no_row_the_parts_stand_together_centred()
    {
        var slots = ItemSize.Lay(100, [StackItem.Fixed(36), StackItem.Fixed(16)], Grid, gap: 8);

        Assert.Equal(new ItemSlot(20, 36), slots[0]);
        Assert.Equal(new ItemSlot(64, 16), slots[1]);
    }

    [Fact]
    public void S14810_items_never_overlap_grow_nor_shrink()
    {
        foreach (var room in new[] { 0.0, 37, 60, 113, 200, 999 })
        {
            var items = new[] { StackItem.Row(1), StackItem.Row(3), StackItem.Fixed(24), StackItem.Row(2) };
            var slots = ItemSize.Lay(room, items, Grid, gap: 4);
            for (var i = 0; i < items.Length; i++)
            {
                var own = items[i].IsRow ? Grid.Whole(items[i].Natural) : items[i].Natural;
                Assert.Equal(own, slots[i].Length, 6);
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
        Assert.Empty(ItemSize.Lay(100, [], Grid));
        Assert.Equal((30.0, 1), (ItemSize.Reach(double.NaN, 28, Grid).Pitch, ItemSize.Reach(double.NaN, 28, Grid).Shown));
        Assert.Equal((30.0, 1), (ItemSize.Reach(-5, 28, Grid).Pitch, ItemSize.Reach(-5, 28, Grid).Shown));
        var slots = ItemSize.Lay(double.NaN, [new StackItem(double.NaN, true), StackItem.Fixed(-5)], new ItemGrid(double.NaN, 0), gap: double.NaN);
        Assert.Equal(10, slots[0].Length);
        Assert.Equal(0, slots[1].Length);
    }

    [Theory]
    [InlineData(600, 3, new double[] { 200, 200, 200 })]
    [InlineData(350, 3, new double[] { 117, 116, 117 })]       // a third each on whole pixels, the edges rounded
    [InlineData(355, 3, new double[] { 118, 119, 118 })]
    [InlineData(20, 3, new double[] { 7, 6, 7 })]
    public void S14812_the_months_columns_are_the_cards_grid_edge_to_edge(double width, int count, double[] expected)
    {
        // The month is a grid of the card, not a list (WR-9: "the calendar cells and numbers fill the card"): its seven
        // days share the width, from the card's left edge to its right, alike to a pixel.
        var columns = ItemSize.Columns(width, count, Grid);

        Assert.Equal(width, columns.Sum(), 6);
        Assert.True(columns.Max() - columns.Min() <= 1 + 1e-6);
        Assert.Equal(expected, columns.Select(c => Math.Round(c, 6)), new ToleranceComparer());
    }

    private sealed class ToleranceComparer : IEqualityComparer<double>
    {
        public bool Equals(double x, double y) => Math.Abs(x - y) < 1e-6;

        public int GetHashCode(double obj) => 0;
    }

    /// <summary>
    /// [S-14816] Owner, 2026-11-21: "the weather forecast widget grid view items do not go from edge to edge, they have space
    /// everywhere." A grid's cells keep one size from the box's edges, touching (WR-4: "the last item will be cutoff to some
    /// extent then"): across, every column its width reaches, the last cut by its right edge; down, every line its height
    /// reaches, the last cut by its foot.
    /// </summary>
    [Theory]
    [InlineData(240, 200, 3, 2)]
    [InlineData(259, 200, 4, 2)]
    [InlineData(239, 200, 3, 2)]
    [InlineData(240, 201, 3, 3)]
    [InlineData(10, 10, 1, 1)]
    public void S14816_a_grid_of_cells_takes_every_column_and_line_it_reaches_the_last_cut(double width, double height, int columns, int lines)
    {
        var cells = ItemSize.Cells(width, height, 76, 96, Grid);

        Assert.Equal((columns, lines), (cells.Columns, cells.Lines));
        Assert.Equal(80, cells.Across.Pitch);
        Assert.Equal(100, cells.Down.Pitch);
        Assert.Equal(160, cells.Across.Start(2));
        Assert.Equal(100, cells.Down.Start(1));
    }

    /// <summary>
    /// [S-16438] A list wrapped into lines (WR-4: only the last item the card reaches is cut; owner, 2026-11-23: "Fixed
    /// size, gap at right"): its cells keep their one size at every card width; across, the whole columns the width holds,
    /// never one cut by the right edge with later items under it, the room past them less than a cell; down every line the
    /// height reaches, the last cut by the foot.
    /// </summary>
    [Theory]
    [InlineData(240, 200, 3, 2)]
    [InlineData(259, 200, 3, 2)]
    [InlineData(320, 200, 4, 2)]
    [InlineData(240, 201, 3, 3)]
    [InlineData(10, 10, 1, 1)]
    public void S16438_a_wrapped_list_takes_whole_columns_and_every_line_the_last_cut(double width, double height, int columns, int lines)
    {
        var cells = ItemSize.Wrapped(width, height, 76, 96, Grid);

        Assert.Equal((columns, lines), (cells.Columns, cells.Lines));
        Assert.Equal((80.0, 100.0), (cells.Across.Pitch, cells.Down.Pitch));
        Assert.InRange(Math.Max(width, 80) - (cells.Columns * cells.Across.Pitch), -LayoutMath.Tolerance, 80);
        Assert.Equal(2, ItemSize.Wrapped(90, 100, 76, 96, Grid, least: 2).Columns);
        Assert.Equal((4, 80.0), (ItemSize.Wrapped(900, 100, 76, 96, Grid, most: 4).Columns, ItemSize.Wrapped(900, 100, 76, 96, Grid, most: 4).Across.Pitch));
        Assert.Equal(1, ItemSize.Wrapped(double.NaN, 100, 76, 96, Grid).Columns);
    }

    [Fact]
    public void S14816_a_grid_of_cells_keeps_its_least_and_most_columns()
    {
        Assert.Equal(2, ItemSize.Cells(90, 100, 76, 96, Grid, least: 2).Columns);
        Assert.Equal(4, ItemSize.Cells(900, 100, 76, 96, Grid, most: 4).Columns);
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
