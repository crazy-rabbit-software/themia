using Themia.Core.Widgets;

namespace Themia.Core.Habits;

/// <summary>
/// [S-6409] One pass's decisions for the heatmap style.
/// </summary>
/// <param name="PerHabit">Each habit gets its own grid under its name; otherwise one grid for the whole list.</param>
/// <param name="Weeks">Columns of seven periods, the newest on the right.</param>
/// <param name="Cell">A cell's side in DIPs; cells are <see cref="HabitHeatmap.Gap"/> apart.</param>
public sealed record HabitHeatmapPlan(bool PerHabit, int Weeks, double Cell)
{
    /// <summary>A cell and the gap after it.</summary>
    public double Pitch => Cell + HabitHeatmap.Gap;

    /// <summary>The height of one grid: seven rows.</summary>
    public double GridHeight => Weeks == 0 ? 0 : (HabitHeatmap.Rows * Pitch) - HabitHeatmap.Gap;
}

/// <summary>
/// [S-6409] The heatmap style's rules: GitHub's contribution calendar over a habit log. Columns of
/// seven periods, the newest column on the right; under a daily cadence the rows are the weekdays
/// from the language's first day, so a column is a calendar week and the days after today are
/// left blank. Pure, so every tier and every edge is pinned by a test.
/// <para>
/// Decided by size: when every habit's name and its own seven-row grid (at least
/// <see cref="MinWeeks"/> weeks of <see cref="MinCell"/> cells) fit the tile, each habit gets one;
/// otherwise the list shares one grid whose cells are shaded by how many of its habits were done.
/// A larger tile spends its room on bigger cells (up to <see cref="MaxCell"/>) and then on more
/// weeks (up to <see cref="MaxWeeks"/>), nothing else ([S-666]).
/// </para>
/// </summary>
public static class HabitHeatmap
{
    /// <summary>Periods per column.</summary>
    public const int Rows = 7;

    /// <summary>Space between two cells.</summary>
    public const double Gap = 2;

    /// <summary>The smallest cell that still reads as a square.</summary>
    public const double MinCell = 6;

    /// <summary>Past this a cell is a tile, not a day: more weeks are worth more than bigger squares.</summary>
    public const double MaxCell = 20;

    /// <summary>A month: fewer columns would not be a calendar.</summary>
    public const int MinWeeks = 4;

    /// <summary>A year of weeks, as GitHub draws it.</summary>
    public const int MaxWeeks = 53;

    /// <summary>The shades above "none".</summary>
    public const int Levels = 4;

    /// <summary>Space between one habit's grid and the next habit's name.</summary>
    public const double BlockGap = 12;

    /// <summary>Space between a habit's name and its grid.</summary>
    public const double NameGap = 4;

    /// <summary>
    /// [S-6409] The plan for <paramref name="habits"/> habits in <paramref name="box"/> (the tile less
    /// its padding), each name <paramref name="nameLine"/> high when it has its own grid.
    /// </summary>
    public static HabitHeatmapPlan Plan(WidgetArea box, int habits, double nameLine)
    {
        if (habits <= 0 || !box.IsMeasured)
        {
            return new HabitHeatmapPlan(false, 0, 0);
        }

        // The widest a cell may be and still leave a month of columns.
        var byWidth = ((box.Width + Gap) / MinWeeks) - Gap;

        var block = ((box.Height - ((habits - 1) * BlockGap)) / habits) - nameLine - NameGap;
        var perHabit = Math.Floor(Math.Min(Math.Min(((block + Gap) / Rows) - Gap, byWidth), MaxCell));
        if (perHabit >= MinCell)
        {
            return new HabitHeatmapPlan(true, WeeksFor(box.Width, perHabit), perHabit);
        }

        var shared = Math.Floor(Math.Clamp(Math.Min(((box.Height + Gap) / Rows) - Gap, byWidth), MinCell, MaxCell));
        return new HabitHeatmapPlan(false, WeeksFor(box.Width, shared), shared);
    }

    /// <summary>How many whole columns of <paramref name="cell"/> fit across <paramref name="width"/>, within the bounds.</summary>
    private static int WeeksFor(double width, double cell)
        => Math.Clamp((int)Math.Floor((width + Gap) / (cell + Gap)), 1, MaxWeeks);

    /// <summary>
    /// [S-6409] The periods of a grid <paramref name="weeks"/> columns wide, column by column (index
    /// <c>column * 7 + row</c>), the current period in the last column. Null is a cell with no
    /// period: under a daily cadence, the days of this week still to come. Walks the same step as
    /// the streak and the dots ([S-1423]), so the three always agree about which period is which.
    /// </summary>
    public static IReadOnlyList<HabitBucket?> Cells(HabitBucket current, HabitCadence cadence, TimeZoneInfo zone, int weeks, DayOfWeek firstDay)
    {
        ArgumentNullException.ThrowIfNull(zone);

        var columns = Math.Clamp(weeks, 0, MaxWeeks);
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

    /// <summary>[S-6409] How dark a cell is: 0 for none done, then 1..<see cref="Levels"/> by the share done, rounded up.</summary>
    public static int Level(int done, int count)
    {
        if (count <= 0 || done <= 0)
        {
            return 0;
        }

        return Math.Clamp((int)Math.Ceiling(done * (double)Levels / count), 1, Levels);
    }
}
