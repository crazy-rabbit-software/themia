using Themia.Core.Geometry;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-14810] [S-14500] Items have one fixed size; widgets step by one cell (owner, 2026-11-20: "the widget items should
/// actually have a fixed size. then when you change the height of a widget the items should not grow or shrink to fit all
/// the way from the top to the bottom, they should just keep their size and the last item will be cutoff to some extent
/// then."; "the widgets should not have the size increments anymore but should be incrementable in steps of one"): a card
/// takes every whole grid cell, and its rows (laid by <see cref="ItemSize.Rows"/>, the one rule the views use) stand at
/// their optimal from its top edge, the last one its foot reaches cut by it.
/// </summary>
public class FixedItemTests
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
    public void S14810_a_list_ten_cells_tall_shows_three_rows_of_three_cells_and_a_third_of_a_fourth()
    {
        // The owner's case: the rows keep their three cells; the fourth row stands at its size, its last cell in the card.
        var reach = ItemSize.Rows(100 - FamilySpace.Inset, 1, Grid);

        Assert.Equal(4, reach.Shown);
        Assert.Equal(30, reach.Pitch, 9);
        Assert.Equal(100, reach.Room, 9);
        Assert.All(Enumerable.Range(0, 4), i => Assert.Equal(30, reach.Height(i), 9));
        Assert.Equal(90, reach.Start(3), 9);
    }

    [Fact]
    public void S14810_one_cell_more_or_less_never_changes_a_rows_size()
    {
        for (var cells = 3; cells <= 60; cells++)
        {
            var room = cells * Grid.Pitch;
            var reach = ItemSize.Reach(room, FamilyShape.Row1, Grid);

            Assert.Equal(30, reach.Pitch, 9);
            Assert.Equal((int)Math.Ceiling(cells / 3.0), reach.Shown);
            Assert.Equal(room, reach.Room, 9);

            // The rows the room reaches end at or past its end, less than one row past it.
            Assert.InRange(reach.Start(reach.Shown) - room, 0, 30 - 1e-9);
        }
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    [InlineData(1.5)]
    [InlineData(1.75)]
    public void S14810_a_rows_edges_are_whole_device_pixels_and_alike_to_a_pixel(double scale)
    {
        var grid = new ItemGrid(10, scale);
        foreach (var cells in new[] { 10, 11, 13, 17, 23, 31 })
        {
            var room = Math.Round(cells * 10 * scale) / scale;
            var reach = ItemSize.Reach(room, FamilyShape.Row1, grid);
            for (var i = 0; i <= reach.Shown; i++)
            {
                var px = reach.Start(i) * scale;
                Assert.Equal(Math.Round(px), px, 6);
            }

            var heights = Enumerable.Range(0, reach.Shown).Select(reach.Height).ToList();
            Assert.All(heights, h => Assert.True(Math.Abs(h - 30) * scale <= 1 + 1e-6, $"{cells} cells at {scale}: a row of {h}"));
        }
    }

    [Fact]
    public void S14811_rows_over_an_add_row_keep_their_size_the_add_row_its_own()
    {
        // Tasks of one line at 11 cells (98 under the card's inset): two rows whole, the third cut by the add row, 30 each.
        var one = ItemSize.RowsAbove(110 - FamilySpace.Inset, 1, Grid);
        Assert.Equal((30.0, 3, 30.0, 80.0), (one.Pitch, one.Shown, one.Foot, one.Room));

        // Rows of two lines (40, four cells) over the one-line add row (three cells) at 16 cells: 40 and 30, never stretched.
        var two = ItemSize.RowsAbove(160 - FamilySpace.Inset, 2, Grid);
        Assert.Equal((40.0, 4, 30.0, 130.0), (two.Pitch, two.Shown, two.Foot, two.Room));
    }

    [Fact]
    public void S14810_a_lists_viewport_is_its_rows_up_to_the_room()
    {
        var reach = ItemSize.Rows(110 - FamilySpace.Inset, 1, Grid);

        // Two items: their two rows, the room under them empty; ten: the whole room, the fourth row cut by its foot.
        Assert.Equal(60, reach.Span(2), 9);
        Assert.Equal(110, reach.Span(10), 9);
        Assert.Equal(0, reach.Span(0), 9);
    }

    [Fact]
    public void S14810_a_count_that_follows_the_card_is_every_item_the_card_reaches()
    {
        Assert.Equal(4, ItemSize.Count(100, FamilyShape.Row1, Grid));
        Assert.Equal(4, ItemSize.Count(119, FamilyShape.Row1, Grid));
        Assert.Equal(4, ItemSize.Count(120, FamilyShape.Row1, Grid));
        Assert.Equal(5, ItemSize.Count(121, FamilyShape.Row1, Grid));
        Assert.Equal(1, ItemSize.Count(10, FamilyShape.Row1, Grid));
        Assert.Equal(0, ItemSize.Count(0, FamilyShape.Row1, Grid, least: 0));
        Assert.Equal(14, ItemSize.Count(9999, FamilyShape.Row1, Grid, most: 14));
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

        // The rows in the card at any of those heights keep their one size: the card's height never reaches them.
        var grid = SizeFit.Grid(screen, scale);
        var row = ItemSize.Optimal(FamilyShape.Row1, grid);
        for (var k = 0; k < 30; k++)
        {
            var card = (minH + k) / (double)rows * h / scale;
            var reach = ItemSize.RowsAbove(card - FamilySpace.Inset, 1, grid);
            Assert.Equal(row, reach.Pitch, 9);
            Assert.Equal(row, reach.Foot, 9);
            Assert.Equal(card, reach.Room + reach.Foot, 6);
        }
    }

    [Fact]
    public void S14500_a_kit_lists_natural_is_its_rows_at_their_optimal_and_its_least_one_row()
    {
        // The forecast's five days and the tasks' six rows over the add row: whole rows at the natural, every cell between.
        var tasks = KitSizing.Sizing(new KitRowsChoice(6, 1), addFooter: true);
        Assert.Equal((60.0, 210.0), (tasks.Min.Height, tasks.Natural.Height));

        // A table's titles with the inset over them are one band of three cells, a one-line row's, on the card's top edge;
        // the rows under it.
        var table = KitSizing.Sizing(new KitRowsChoice(5, 1, ColumnTitles: true), addFooter: false);
        Assert.Equal(30 + 150, table.Natural.Height, 6);
        Assert.Equal(30 + 30, table.Min.Height, 6);
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
