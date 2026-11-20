using Themia.Core.Geometry;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-14100] A card holding items is exactly as tall as its whole items (owner, 2026-11-20: "now the weather forecast
/// widget always has one block space at the bottom. this should obviously not be"; "right now all of the widgets have a
/// space on the bottom, this means their size steps are not in sync with the item heights"): a sizing in whole cells,
/// part by part, laid on any display's grid, gives a card whose rows (laid by <see cref="ItemStretch.Rows"/>, the one rule
/// the views use) fill it to the foot with nothing left over, at every step.
/// </summary>
public class NoLeftoverTests
{
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

    /// <summary>The card's height in its content DIPs for an axis's size (fractions of the screen), at <paramref name="scale"/>.</summary>
    private static double Content(double frac, double screenPx, double scale) => frac * screenPx / scale;

    [Theory]
    [MemberData(nameof(Displays))]
    public void S14100_a_list_of_rows_is_exactly_its_whole_rows_at_every_count_on_any_display(double w, double h, double scale)
    {
        var screen = new ScreenMetrics(w, h);
        var grid = SizeFit.Grid(screen, scale);
        var (_, rows) = GridGeometry.GridDims(screen);
        for (var lines = 1; lines <= 2; lines++)
        {
            for (var n = 1; n <= 14; n++)
            {
                var sizing = WidgetSizing.Range(new(220, 1), new(300, 1), new(420, 1)).WithHeightSteps(SizeSteps.InCells(FamilyShape.RowHeight(lines), 1, 14, n));
                var limits = SizeFit.Limits(sizing, screen, scale)!;
                var card = limits.Height.Pinned!.Value;

                // Whole cells of the screen's grid, as every card is ([S-11807]).
                Assert.Equal(Math.Round(card * rows), card * rows, 6);

                // The view's rows from the card's top edge to its foot edge: n rows, nothing left over.
                var height = Content(card, h, scale);
                var reach = ItemStretch.Rows(height - FamilySpace.Inset, n, lines, grid);
                Assert.Equal(n, reach.Shown);
                Assert.Equal(grid.Whole(FamilyShape.RowHeight(lines)), reach.Pitch, 6);
                Assert.True(Math.Abs(height - reach.Length(n)) < grid.Pitch - 1e-6, $"{n} rows of {lines} lines leave {height - reach.Length(n):0.##} of a {height:0.##} card on {w}x{h} at {scale}");
            }
        }
    }

    [Fact]
    public void S14100_on_the_default_grid_a_row_step_is_three_cells_and_the_card_holds_no_cell_more()
    {
        // The forecast's five days: 164 DIPs before, painted 170, its rows 150 (two cells left at its foot).
        var five = SizeSteps.InCells(FamilyShape.Row1, 1, 14, 5);
        Assert.Equal(150, five.At(5), 6);
        Assert.Equal(30, five.Step, 6);
        Assert.Equal(0, five.Base, 6);

        // The tasks' six rows and the add row: 220 before (a cell left under the add row), now 210.
        var tasks = KitSizing.Steps(new KitRowsChoice(6, 1), addFooter: true);
        Assert.Equal(210, tasks.At(6), 6);

        // A table's titles with the inset over them are one band of four cells; the rows under it.
        var table = KitSizing.Steps(new KitRowsChoice(5, 1, ColumnTitles: true), addFooter: false);
        Assert.Equal(40 + 150, table.At(5), 6);
    }

    [Theory]
    [MemberData(nameof(Displays))]
    public void S14100_the_fixed_parts_and_the_add_row_are_each_their_own_whole_cells(double w, double h, double scale)
    {
        var screen = new ScreenMetrics(w, h);
        var grid = SizeFit.Grid(screen, scale);
        var steps = KitSizing.Steps(new KitRowsChoice(4, 1), addFooter: true, over: LineRules.BandOver);
        var limits = SizeFit.Limits(WidgetSizing.Range(new(220, 1), new(300, 1), new(420, 1)).WithHeightSteps(steps), screen, scale)!;
        var card = Content(limits.Height.Pinned!.Value, h, scale);

        // The view: the line's band (whole cells from the card's top edge), four rows, the add row: the card, short of
        // nothing but what the screen's rows and columns differ by (under a cell).
        var band = FamilySpace.Inset + LineRules.Band(grid);
        var rows = ItemStretch.Rows(card - band, 4, 1, grid, FamilyShape.Row1, edge: false);
        Assert.Equal(4, rows.Shown);
        Assert.Equal(grid.Whole(FamilyShape.Row1), rows.Pitch, 6);
        Assert.True(Math.Abs(card - (band + rows.Length(4) + grid.Whole(FamilyShape.Row1))) < grid.Pitch - 1e-6);
    }

    [Theory]
    [MemberData(nameof(Displays))]
    public void S14100_a_resize_lands_on_whole_rows_and_stores_the_rows_its_cells_hold(double w, double h, double scale)
    {
        var screen = new ScreenMetrics(w, h);
        var sizing = WidgetSizing.Range(new(220, 1), new(300, 1), new(420, 1)).WithHeightSteps(SizeSteps.InCells(FamilyShape.Row1, 1, 14));
        var limits = SizeFit.Limits(sizing, screen, scale)!;
        var (_, lines) = GridGeometry.GridDims(screen);
        var step = limits.Height.Step;
        var grid = SizeFit.Grid(screen, scale);
        Assert.Equal(grid.Cells(FamilyShape.Row1), step * lines, 6);

        // Anywhere between two steps, the resize lands on one of them: whole rows, never a cell more.
        for (var k = 0; k < 40; k++)
        {
            var asked = (k + 0.3) / lines;
            var landed = limits.Height.Range(asked);
            var rows = Math.Round((landed - limits.Height.StepBase) / step);
            Assert.Equal(limits.Height.StepBase + (rows * step), landed, 9);
        }
    }

    [Fact]
    public void S14100_items_that_grow_together_step_by_a_cell_each()
    {
        // A month's six weeks under the period's line and the weekday line: 4 + 2 cells over them, 3 a week at least.
        var weeks = SizeSteps.Growing(FamilyShape.Row1, 6, 40, LineRules.BandOver, FamilyType.Caption.LineHeight);
        Assert.Equal(60 + 180, weeks.At(0), 6);
        Assert.Equal(60, weeks.Step, 6);

        var grid = new ItemGrid(8);
        var (b, step) = weeks.Cells!.Count(grid);
        Assert.Equal(grid.Cells(LineRules.BandOver) + grid.Cells(FamilyType.Caption.LineHeight) + (6 * grid.Cells(FamilyShape.Row1)), b);
        Assert.Equal(6, step);
    }

    [Fact]
    public void S14100_a_sizing_made_twice_from_one_config_is_the_same_sizing()
    {
        // SizingNotes compares sizings: parts of equal lengths are one sizing, so nothing refits for being asked twice.
        Assert.Equal(KitSizing.Sizing(new KitRowsChoice(6, 1), true), KitSizing.Sizing(new KitRowsChoice(6, 1), true));
        Assert.NotEqual(KitSizing.Sizing(new KitRowsChoice(6, 1), true), KitSizing.Sizing(new KitRowsChoice(6, 1), false));
        Assert.Empty(KitSizing.Sizing(new KitRowsChoice(6, 2), true).Problems());
    }

    [Fact]
    public void S14100_the_steps_checked_are_the_first_heights_from_the_least()
    {
        var sizing = WidgetSizing.Range(new(220, 1), new(300, 1), new(420, 1)).WithHeightSteps(SizeSteps.InCells(FamilyShape.Row1, 2, 14, 5));
        var steps = SizeFit.CheckedSteps(sizing);
        Assert.Equal([60.0, 90, 120, 150], steps.Select(s => s.Height));
        Assert.All(steps, s => Assert.Equal(300, s.Width));

        // A free height: its natural on whole cells and the next cells.
        var free = SizeFit.CheckedSteps(WidgetSizing.Range(new(100, 52), new(100, 52), new(100, 200)));
        Assert.Equal([60.0, 70, 80, 90], free.Select(s => s.Height));
    }
}
