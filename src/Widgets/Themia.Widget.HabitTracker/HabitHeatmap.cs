using Themia.Core.Widgets;
using Themia.Sdk.Kit;

namespace Themia.Core.Habits;

/// <summary>
/// [S-6409] One pass's decisions for the heatmap style.
/// </summary>
/// <param name="PerHabit">Each habit gets its own grid under its name; otherwise one grid for the whole list.</param>
/// <param name="Weeks">Columns of seven periods, the newest on the right.</param>
/// <param name="Cell">A cell's side in DIPs.</param>
/// <param name="Gap">[S-15676] The room between two cells: the grid's own, grown with its cells.</param>
public sealed record HabitHeatmapPlan(bool PerHabit, int Weeks, double Cell, double Gap = HabitHeatmap.Gap)
{
    /// <summary>A cell and the gap after it.</summary>
    public double Pitch => Cell + Gap;

    /// <summary>The height of one grid: seven rows.</summary>
    public double GridHeight => Weeks == 0 ? 0 : (HabitHeatmap.Rows * Pitch) - Gap;
}

/// <summary>
/// [S-6409] [S-11532] The heatmap style's rules: GitHub's contribution calendar over a habit log. Columns
/// of seven periods, the newest column on the right; under a daily cadence the rows are the weekdays
/// from the language's first day, so a column is a calendar week and the days after today are left
/// blank. Pure, so every edge is pinned by a test.
/// <para>
/// What it draws is its config's: one grid for the list (cells of <see cref="SharedCell"/>, each filled
/// from its foot by the share of the habits done) or a grid per habit under its name (cells of
/// <see cref="PerHabitCell"/>). A wider card is a longer calendar: more weeks; a taller one draws one grid for the list
/// larger ([S-15676]).
/// </para>
/// </summary>
public static class HabitHeatmap
{
    /// <summary>Periods per column.</summary>
    public const int Rows = 7;

    /// <summary>Space between two cells.</summary>
    public const double Gap = 2;

    /// <summary>[S-11532] A cell of the list's one grid.</summary>
    public const double SharedCell = 12;

    /// <summary>[S-11532] A cell of a habit's own grid.</summary>
    public const double PerHabitCell = 8;

    /// <summary>Space between one habit's grid and the next habit's name.</summary>
    public const double BlockGap = 12;

    /// <summary>Space between a habit's name and its grid.</summary>
    public const double NameGap = 4;

    /// <summary>[S-11532] A grid's height: seven rows of <paramref name="cell"/>, <see cref="Gap"/> apart.</summary>
    public static double GridOf(double cell) => (Rows * (cell + Gap)) - Gap;

    /// <summary>
    /// [S-11532] [S-15676] The plan for a box <paramref name="width"/> wide and <paramref name="height"/> tall (the card less
    /// its inset): a grid per habit or one for the list, as the config says. One grid for the list is a calendar grid of the
    /// card, as the month is (docs/WIDGET-RULES.md WR-9: "The month is a grid of the card, not a list"; WR-1, WR-3, WR-4): its
    /// cells and their gaps grow as one until its seven rows fill the height (never under their own 12 and 2), and every week
    /// the width reaches at that size is drawn, the oldest cut by the card's left edge. A grid per habit keeps its 8 cells
    /// (its habits are a list of blocks, WR-6), every week the width reaches.
    /// </summary>
    public static HabitHeatmapPlan Plan(double width, double height, bool perHabit)
    {
        var scale = perHabit ? 1 : Scale(height);
        var cell = (perHabit ? PerHabitCell : SharedCell) * scale;
        var gap = Gap * scale;
        return new HabitHeatmapPlan(perHabit, WeeksFor(Math.Max(0, width), cell, gap), cell, gap);
    }

    /// <summary>[S-15676] How much larger one grid for the list is drawn in a room <paramref name="height"/> tall: its rows fill it, never under 1.</summary>
    public static double Scale(double height)
    {
        var scale = height / GridOf(SharedCell);
        return double.IsFinite(scale) ? Math.Max(1, scale) : 1;
    }

    /// <summary>How many columns of <paramref name="cell"/> the width reaches (the last one in part), at least one.</summary>
    private static int WeeksFor(double width, double cell, double gap)
        => Math.Max(1, (int)Math.Ceiling((width + gap - LayoutMath.Tolerance) / (cell + gap)));

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
