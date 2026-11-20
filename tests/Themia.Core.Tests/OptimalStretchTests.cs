using Themia.Core.Geometry;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-14500] Items have an optimal size and stretch a little; widgets step by one cell (owner, 2026-11-20: "if the widgets
/// only have steps in increments of four or six for example, you cannot properly align them. lets not limit the user in
/// the alignment. the items should have an optimal size and then they may stretch a little to fill up the remaining
/// space. the widgets should not have the size increments anymore but should be incrementable in steps of one"): a
/// card takes every whole grid cell, and its rows (laid by <see cref="ItemStretch.Rows"/>, the one rule the views use)
/// are as many as it holds at their optimal, sharing it evenly to its foot, nothing left over.
/// </summary>
public class OptimalStretchTests
{
    private static readonly ItemGrid Grid = ItemGrid.Default();

    /// <summary>
    /// Displays and content scales: the default pitch of 10 (1920 × 1080), 9.974 across and 10.047 down (1536 × 864: 1920 at
    /// 125 %), a content pitch of 8 (1920 × 1080 at a content scale of 1.25) and of 12.46 (1366 × 768 at 0.8).
    /// </summary>
    public static TheoryData<double, double, double> Displays => new()
    {
        { 1920, 1080, 1 },
        { 1536, 864, 1 },
        { 2560, 1440, 1 },
        { 1920, 1080, 1.25 },
        { 1366, 768, 0.8 },
    };

    [Fact]
    public void S14500_a_list_ten_cells_tall_shows_three_rows_of_ten_thirds_with_nothing_left_over()
    {
        // The owner's own case of 2026-11-19, now stretched: three rows of three cells (their optimal) in ten cells, each a
        // third of the card, never four squeezed in and never a cell at the foot.
        var reach = ItemStretch.Rows(100 - FamilySpace.Inset, 1, Grid);

        Assert.Equal(3, reach.Shown);
        Assert.Equal(100.0 / 3, reach.Pitch, 9);
        Assert.Equal(100, reach.Room, 9);
        Assert.Equal(100, reach.Start(3), 9);
        Assert.Equal(0, 100 - Enumerable.Range(0, 3).Sum(reach.Height), 9);
    }

    [Fact]
    public void S14500_a_list_eleven_cells_tall_shows_three_rows_of_eleven_thirds()
    {
        // A size whole-row steps forbade: eleven cells hold three rows of three, each 11/3 cells, nothing at the foot.
        var reach = ItemStretch.Rows(110 - FamilySpace.Inset, 1, Grid);

        Assert.Equal(3, reach.Shown);
        Assert.Equal(110.0 / 3, reach.Pitch, 9);
        Assert.Equal(110, Enumerable.Range(0, 3).Sum(reach.Height), 9);
    }

    [Fact]
    public void S14500_rows_stretch_less_than_one_more_row_and_never_under_their_optimal()
    {
        for (var cells = 3; cells <= 60; cells++)
        {
            var room = cells * Grid.Pitch;
            var reach = ItemStretch.Reach(room, FamilyShape.Row1, Grid);

            Assert.True(reach.Pitch >= 30 - 1e-9, $"{cells} cells: {reach.Pitch} is under its optimal");
            Assert.True((reach.Shown + 1) * 30 > room + 1e-9, $"{cells} cells hold one more row than {reach.Shown}");
            Assert.Equal(room, reach.Room, 9);
        }
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    [InlineData(1.5)]
    [InlineData(1.75)]
    public void S14500_a_rows_edges_are_whole_device_pixels_ending_on_the_rooms_end(double scale)
    {
        var grid = new ItemGrid(10, scale);
        foreach (var cells in new[] { 10, 11, 13, 17, 23, 31 })
        {
            // A card is whole device pixels: its room too.
            var room = Math.Round(cells * 10 * scale) / scale;
            var reach = ItemStretch.Reach(room, FamilyShape.Row1, grid);
            for (var i = 0; i <= reach.Shown; i++)
            {
                var px = reach.Start(i) * scale;
                Assert.Equal(Math.Round(px), px, 6);
            }

            Assert.Equal(room, reach.Start(reach.Shown), 6);
            var heights = Enumerable.Range(0, reach.Shown).Select(reach.Height).ToList();
            Assert.True((heights.Max() - heights.Min()) * scale <= 1 + 1e-6, $"{cells} cells at {scale}: rows differ by more than a pixel");

            // Scrolled by any number of rows, as many rows as are shown fill the room again.
            Assert.Equal(room, reach.Start(reach.Shown + 2) - reach.Start(2), 6);
        }
    }

    [Fact]
    public void S14500_rows_over_an_add_row_stretch_alike_and_fill_the_card()
    {
        // Tasks of one line at 11 cells (98 under the card's inset): two rows and the add row, each 11/3 cells.
        var one = ItemStretch.RowsAbove(110 - FamilySpace.Inset, 1, Grid);
        Assert.Equal(2, one.Shown);
        Assert.True(Math.Abs(one.Pitch - one.Foot) <= 1, $"rows {one.Pitch} and the add row {one.Foot} differ by more than a pixel");
        Assert.Equal(110, one.Room + one.Foot, 9);

        // Rows of two lines (40, four cells) over the one-line add row (three cells) at 16 cells: stretched by one share.
        var two = ItemStretch.RowsAbove(160 - FamilySpace.Inset, 2, Grid);
        Assert.Equal(3, two.Shown);
        Assert.Equal(160, two.Room + two.Foot, 9);
        Assert.Equal(40.0 / 30, two.Pitch / two.Foot, 2);
    }

    [Fact]
    public void S14500_columns_share_the_width_evenly_edge_to_edge()
    {
        var columns = ItemStretch.Columns(355, 7, Grid);

        Assert.Equal(355, columns.Sum(), 9);
        Assert.True(columns.Max() - columns.Min() <= 1 + 1e-9);
        Assert.All(columns, c => Assert.Equal(Math.Round(c), c, 9));
    }

    [Fact]
    public void S14500_a_count_that_follows_the_card_is_what_it_holds_at_the_optimal()
    {
        Assert.Equal(3, ItemStretch.Count(100, FamilyShape.Row1, Grid));
        Assert.Equal(3, ItemStretch.Count(119, FamilyShape.Row1, Grid));
        Assert.Equal(4, ItemStretch.Count(120, FamilyShape.Row1, Grid));
        Assert.Equal(1, ItemStretch.Count(10, FamilyShape.Row1, Grid));
        Assert.Equal(0, ItemStretch.Count(10, FamilyShape.Row1, Grid, least: 0));
        Assert.Equal(14, ItemStretch.Count(9999, FamilyShape.Row1, Grid, most: 14));
    }

    [Theory]
    [MemberData(nameof(Displays))]
    public void S14500_a_resize_lands_on_every_whole_cell_on_any_display(double w, double h, double scale)
    {
        var screen = new ScreenMetrics(w, h);
        var sizing = SizeStretch.Relax(KitSizing.Sizing(new KitRowsChoice(6, 1), addFooter: true));
        var limits = SizeFit.Limits(sizing, screen, scale)!;
        var (cols, rows) = GridGeometry.GridDims(screen);
        var (minW, minH) = ((int)Math.Round(limits.Width.Min * cols), (int)Math.Round(limits.Height.Min * rows));

        // Every whole cell from the least up is a size a resize lands on, on both axes: one cell a step.
        for (var k = 0; k < 30; k++)
        {
            Assert.Equal((minH + k) / (double)rows, limits.Height.Range((minH + k) / (double)rows), 9);
            Assert.Equal((minW + k) / (double)cols, limits.Width.Range((minW + k) / (double)cols), 9);
        }

        // The rows in the card at any of those heights fill it from its top edge to its foot, none left over.
        var grid = SizeFit.Grid(screen, scale);
        for (var k = 0; k < 30; k++)
        {
            var card = (minH + k) / (double)rows * h / scale;
            var reach = ItemStretch.RowsAbove(card - FamilySpace.Inset, 1, grid);
            Assert.Equal(card, reach.Room + reach.Foot, 6);
            // At least its optimal where the card holds it (every display at a content scale of one and more), its natural always.
            Assert.True(reach.Pitch >= (scale >= 1 ? grid.Whole(FamilyShape.Row1) : FamilyShape.Row1) - 0.5);
        }
    }

    [Fact]
    public void S14500_a_kit_lists_natural_is_its_rows_at_their_optimal_and_its_least_one_row()
    {
        // The forecast's five days and the tasks' six rows over the add row: whole rows at the natural, every cell between.
        var tasks = KitSizing.Sizing(new KitRowsChoice(6, 1), addFooter: true);
        Assert.Equal((60.0, 210.0), (tasks.Min.Height, tasks.Natural.Height));

        // A table's titles with the inset over them are one band of four cells; the rows under it.
        var table = KitSizing.Sizing(new KitRowsChoice(5, 1, ColumnTitles: true), addFooter: false);
        Assert.Equal(40 + 150, table.Natural.Height, 6);
        Assert.Equal(40 + 30, table.Min.Height, 6);
    }

    [Fact]
    public void S14500_a_sizing_made_twice_from_one_config_is_the_same_sizing()
    {
        // SizingNotes compares sizings: nothing refits for being asked twice.
        Assert.Equal(KitSizing.Sizing(new KitRowsChoice(6, 1), true), KitSizing.Sizing(new KitRowsChoice(6, 1), true));
        Assert.NotEqual(KitSizing.Sizing(new KitRowsChoice(6, 1), true), KitSizing.Sizing(new KitRowsChoice(6, 1), false));
        Assert.Empty(KitSizing.Sizing(new KitRowsChoice(6, 2), true).Problems());
    }

    [Fact]
    public void S14500_the_sizes_checked_are_cell_by_cell_round_the_natural()
    {
        var sizing = SizeStretch.Relax(WidgetSizing.Range(new(220, 60), new(300, 250), new(420, 600)));
        var sizes = SizeFit.CheckedCells(sizing);

        Assert.Equal(new CheckedSize(CheckedSize.Natural, 300, 250), sizes[0]);
        Assert.Contains(new CheckedSize("h+1", 300, 260), sizes);
        Assert.Contains(new CheckedSize("h-5", 300, 200), sizes);
        Assert.Contains(new CheckedSize("w+3", 330, 250), sizes);
        Assert.Contains(new CheckedSize("h11", 300, 110), sizes);
        Assert.Contains(new CheckedSize("h17", 300, 170), sizes);

        // Never under the least, never two of one size.
        var least = SizeFit.CheckedCells(SizeStretch.Relax(WidgetSizing.Range(new(220, 140), new(300, 150), new(420, 600))));
        Assert.All(least, s => Assert.True(s.Height >= 140));
        Assert.Equal(least.Count, least.Select(s => (s.Width, s.Height)).Distinct().Count());
    }
}
