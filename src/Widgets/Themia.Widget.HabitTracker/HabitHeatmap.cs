using Themia.Core.Widgets;
using Themia.Sdk.Kit;

namespace Themia.Core.Habits;

/// <summary>
/// [S-6409] [S-15770] One pass's decisions for the heatmap style.
/// </summary>
/// <param name="PerHabit">Each habit gets its own grid under its name; otherwise one grid for the whole list.</param>
/// <param name="Weeks">[S-15787] Columns of seven periods from the card's left edge: every one the width reaches, the last cut by its right edge.</param>
/// <param name="Box">[S-15770] A cell's box, its one fixed size (whole grid cells): the boxes touch, the mark stands in it.</param>
/// <param name="Name">[S-15770] A habit's name row over its own grid (whole grid cells); 0 for the list's one grid.</param>
public sealed record HabitHeatmapPlan(bool PerHabit, int Weeks, double Box, double Name = 0)
{
    /// <summary>[S-15770] The mark drawn in a box: the box less <see cref="HabitHeatmap.Gap"/>, half of it the padding on each side.</summary>
    public double Cell => Math.Max(1, Box - HabitHeatmap.Gap);

    /// <summary>[S-15770] The padding inside a box round its mark.</summary>
    public double Pad => (Box - Cell) / 2;

    /// <summary>The height of one grid: seven rows of boxes, touching.</summary>
    public double GridHeight => Weeks == 0 ? 0 : HabitHeatmap.Rows * Box;

    /// <summary>[S-15770] One habit's block (its name row over its grid), or the list's one grid.</summary>
    public double Block => Name + GridHeight;

    /// <summary>
    /// [S-15787] The weeks of the history: the columns the width holds whole, the newest (the current period's) the last
    /// of them; a column the right edge cuts after them is the next week's, its periods still to come.
    /// </summary>
    public int History { get; init; } = 1;
}

/// <summary>
/// [S-6409] [S-11532] [S-15770] The heatmap style's rules: GitHub's contribution calendar over a habit log. Columns
/// of seven periods, the newest column on the right; under a daily cadence the rows are the weekdays
/// from the language's first day, so a column is a calendar week and the days after today are left
/// blank. Pure, so every edge is pinned by a test.
/// <para>
/// What it draws is its config's: one grid for the list (each cell's mark filled from its foot by the share of the
/// habits done) or a grid per habit under its name. [S-15770] Every cell is an item of one fixed size (its natural, the
/// mark and the gap, in whole grid cells; docs/WIDGET-RULES.md WR-4, WR-6): the boxes touch (WR-2), the first row on the
/// card's top edge (WR-1), the newest column on its right edge and every week the width reaches, the oldest cut by its
/// left edge (WR-3, WR-4); the gap between two marks is the padding inside their boxes. A taller card draws nothing larger:
/// the room under the grid stays empty (WR-5), and a shorter one cuts the grid's last row by its foot.
/// </para>
/// </summary>
public static class HabitHeatmap
{
    /// <summary>Periods per column.</summary>
    public const int Rows = 7;

    /// <summary>[S-15770] The gap between two marks: the padding inside their boxes, half on each side.</summary>
    public const double Gap = 2;

    /// <summary>[S-11532] A mark of the list's one grid at its natural (its box the mark and the gap, in whole cells).</summary>
    public const double SharedCell = 12;

    /// <summary>[S-11532] A mark of a habit's own grid at its natural.</summary>
    public const double PerHabitCell = 8;

    /// <summary>[S-15770] A cell's box on <paramref name="grid"/>: its mark and the gap in whole grid cells, at every size of its card.</summary>
    public static double BoxOf(bool perHabit, ItemGrid grid) => ItemSize.Optimal((perHabit ? PerHabitCell : SharedCell) + Gap, grid);

    /// <summary>[S-15770] A habit's name row over its own grid on <paramref name="grid"/>: a Body line in whole cells.</summary>
    public static double NameOf(ItemGrid grid) => ItemSize.Optimal(FamilyType.Body.LineHeight, grid);

    /// <summary>[S-15770] One grid's whole height on <paramref name="grid"/> (a habit's own with its name row).</summary>
    public static double BlockOf(bool perHabit, ItemGrid grid) => (Rows * BoxOf(perHabit, grid)) + (perHabit ? NameOf(grid) : 0);

    /// <summary>
    /// [S-11532] [S-15770] [S-15787] The plan for a card <paramref name="width"/> wide on <paramref name="grid"/>: a grid per
    /// habit or one for the list, as the config says; its boxes at their one size whatever the card's height, from the
    /// card's left edge, every week the width reaches (at least one): the history's whole weeks, the current period's the
    /// last of them (<see cref="HabitHeatmapPlan.History"/>), and the next week's cut by the right edge (WR-4: "for items
    /// side by side, its right edge").
    /// </summary>
    public static HabitHeatmapPlan Plan(double width, bool perHabit, ItemGrid grid)
    {
        var box = BoxOf(perHabit, grid);
        var room = double.IsFinite(width) ? Math.Max(0, width) : 0;
        var weeks = ItemSize.Count(room, box, grid, least: 1);
        var whole = Math.Clamp((int)Math.Floor((room + LayoutMath.Tolerance) / box), 1, weeks);
        return new HabitHeatmapPlan(perHabit, weeks, box, perHabit ? NameOf(grid) : 0) { History = whole };
    }

    /// <summary>
    /// [S-15770] How many habits' grids a room <paramref name="height"/> tall shows, at most <paramref name="count"/>: every
    /// block it reaches, touching from its top, the last cut by its foot (WR-4), at least one.
    /// </summary>
    public static int Blocks(double height, int count, HabitHeatmapPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var reaches = double.IsFinite(height) && height > LayoutMath.Tolerance && plan.Block > 0
            ? (int)Math.Ceiling((height - LayoutMath.Tolerance) / plan.Block)
            : 1;
        return Math.Clamp(reaches, 1, Math.Max(1, count));
    }

    /// <summary>
    /// [S-6409] The periods of a grid <paramref name="weeks"/> columns wide, column by column (index
    /// <c>column * 7 + row</c>), the current period in the last column. Null is a cell with no
    /// period: under a daily cadence, the days of this week still to come. Walks the same step as
    /// the streak and the dots ([S-1423]), so the three always agree about which period is which.
    /// </summary>
    public static IReadOnlyList<HabitBucket?> Cells(HabitBucket current, HabitCadence cadence, TimeZoneInfo zone, int weeks, DayOfWeek firstDay)
    {
        ArgumentNullException.ThrowIfNull(zone);

        var columns = Math.Max(0, weeks);
        var cells = new HabitBucket?[columns * Rows];

        if (columns == 0)
        {
            return cells;
        }

        // A daily grid's rows are weekdays; every other cadence fills its last column to the end.
        var row = Rows - 1;
        if (cadence.Period == HabitPeriod.Daily)
        {
            var day = TimeZoneInfo.ConvertTime(current.Start, zone).DayOfWeek;
            row = ((int)day - (int)firstDay + Rows) % Rows;
        }

        var cursor = current;
        for (var i = ((columns - 1) * Rows) + row; i >= 0; i--)
        {
            cells[i] = cursor;
            cursor = HabitPeriods.Previous(cursor, cadence, zone);
        }

        return cells;
    }

    /// <summary>
    /// [S-6409] How full a shared cell is: the share of the list's habits done in its period, 0 to 1.
    /// The cell is a column of the Chart block, its track filled from the foot in the accent to that
    /// share ([S-6717]), never an opacity per share; the tooltip says how many.
    /// </summary>
    public static double Fill(int done, int count)
        => count <= 0 || done <= 0 ? 0 : Math.Clamp(done / (double)count, 0, 1);

    /// <summary>
    /// [S-6409] A cell's corner: the family's radius, round on a cell too small for it (half its side).
    /// </summary>
    public static double Radius(double cell) => Math.Min(FamilyShape.Radius, cell / 2);
}
