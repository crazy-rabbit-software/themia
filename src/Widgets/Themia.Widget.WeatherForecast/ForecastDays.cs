using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>What the grid style draws at one size ([S-6171]).</summary>
/// <param name="Days">How many of the first days are shown.</param>
/// <param name="Columns">Cells across.</param>
/// <param name="Rows">Cells down.</param>
/// <param name="CellWidth">One cell's width.</param>
/// <param name="CellHeight">One cell's height.</param>
/// <param name="Icon">The sky's glyph: 32, or 48 from an expanded width where the cell holds it ([S-6713]); 0 only for a lone cell shorter than the floor (the smallest tile).</param>
public sealed record ForecastGridPlan(int Days, int Columns, int Rows, double CellWidth, double CellHeight, double Icon);

/// <summary>
/// [S-6171] [S-11146] The forecast's grid: the days as cells (the day, the sky's 32 glyph, the high
/// beside the low, centred), 12 apart as the family's cells ([S-6713]), in a fixed arrangement by the
/// day count (<see cref="Arrangement"/>); the days what the card's 64 cells across and cells down hold ([S-14410],
/// [S-14502], <see cref="DaysFor"/>), the cells sharing the content.
/// </summary>
public static class ForecastGrid
{
    /// <summary>Between two cells, both ways.</summary>
    public const double Gap = FamilySpace.S3;

    /// <summary>Between two parts of a cell.</summary>
    public const double PartGap = FamilySpace.S1;

    /// <summary>Between the high and the low.</summary>
    public const double FigureGap = FamilySpace.S2;

    /// <summary>The narrowest cell: "Today" and "16° 8°" in Body.</summary>
    public const double CellMinWidth = 56;

    /// <summary>The shortest cell: the day, the 32 glyph and the figures.</summary>
    public static double CellMinHeight => CellHeight(FamilyShape.IconLarge);

    /// <summary>A cell's parts with a glyph of <paramref name="icon"/>: the day, the glyph and the figures, 4 apart.</summary>
    public static double CellHeight(double icon) => (2 * FamilyType.Body.LineHeight) + icon + (2 * PartGap);

    /// <summary>[S-11146] A new card's cell width, and the widest a cell grows.</summary>
    public const double CellStart = 64;

    public const double CellMaxWidth = 80;

    /// <summary>[S-11146] The days' fixed arrangement: one row up to four days, two up to eight, three past.</summary>
    public static (int Columns, int Rows) Arrangement(int days)
    {
        days = Math.Clamp(days, 1, 14);
        var rows = days <= 4 ? 1 : days <= 8 ? 2 : 3;
        return ((days + rows - 1) / rows, rows);
    }

    /// <summary>
    /// [S-12403] How many cells each line holds, top to bottom: <paramref name="columns"/> a line, the
    /// last one the days left (it spreads them across the whole width). No days, no lines.
    /// </summary>
    public static IReadOnlyList<int> Lines(int days, int columns)
    {
        columns = Math.Max(1, columns);
        var lines = new List<int>();
        for (var left = Math.Max(0, days); left > 0; left -= columns)
        {
            lines.Add(Math.Min(columns, left));
        }

        return lines;
    }

    /// <summary>The length of <paramref name="count"/> cells of <paramref name="cell"/>, 12 apart.</summary>
    public static double Length(int count, double cell) => (count * cell) + ((count - 1) * Gap);

    /// <summary>[S-14410] The most cells across an arrangement has: five, fourteen days in three rows.</summary>
    public const int MostColumns = 5;

    /// <summary>[S-14410] The most rows an arrangement has.</summary>
    public const int MostRows = 3;

    /// <summary>[S-14410] The grid's width: the inset either side and <paramref name="columns"/> 64 cells, 12 apart.</summary>
    public static double Width(int columns) => (2 * FamilySpace.Inset) + Length(Math.Max(1, columns), CellStart);

    /// <summary>[S-14410] The grid's height: the content top and the inset under and <paramref name="rows"/> cells, 12 apart.</summary>
    public static double Height(int rows) => FamilySpace.ContentTop + FamilySpace.Inset + Length(Math.Max(1, rows), CellMinHeight);

    /// <summary>[S-14502] How many 64 cells, 12 apart, the box's content holds across: 1..<see cref="MostColumns"/>.</summary>
    public static int CellsAcross(WidgetArea area)
        => Math.Clamp((int)Math.Floor((WeatherForecastLayout.ContentWidth(area) + Gap + LayoutMath.Tolerance) / (CellStart + Gap)), 1, MostColumns);

    /// <summary>[S-14502] How many cells, 12 apart, the box's content holds down: 1..<see cref="MostRows"/>.</summary>
    public static int CellsDown(WidgetArea area)
        => Math.Clamp((int)Math.Floor((WeatherForecastLayout.ContentHeight(area) + Gap + LayoutMath.Tolerance) / (CellMinHeight + Gap)), 1, MostRows);

    /// <summary>
    /// [S-14410] The days a grid of <paramref name="columns"/> cells across and <paramref name="rows"/> down holds:
    /// the most days whose arrangement is that, else those of the nearest arrangement (the fewest cells off, the more
    /// days on a tie), so a resize lands on an arrangement and never leaves a whole row empty.
    /// </summary>
    public static int DaysFor(int columns, int rows)
        => Enumerable.Range(1, 14)
            .Select(d => (Days: d, Shape: Arrangement(d)))
            .OrderBy(a => Math.Abs(a.Shape.Columns - columns) + Math.Abs(a.Shape.Rows - rows))
            .ThenByDescending(a => a.Days)
            .First().Days;

    /// <summary>[S-6171] [S-11146] [S-14502] The grid for the box: the days its cells hold in their arrangement, the cells sharing the content, the 32 glyph.</summary>
    public static ForecastGridPlan Plan(WidgetArea area, WeatherForecastConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var days = WeatherForecastLayout.DaysIn(area, config);
        var (columns, rows) = Arrangement(days);
        var width = WeatherForecastLayout.ContentWidth(area);
        var height = WeatherForecastLayout.ContentHeight(area);
        return new ForecastGridPlan(days, columns, rows, Cell(width, columns), Cell(height, rows), FamilyShape.IconLarge);
    }

    private static double Cell(double length, int count) => Math.Max(0, (length - ((count - 1) * Gap)) / count);
}

/// <summary>[S-12813] The forecast's default: the days it shows, nothing read for any.</summary>
public static class ForecastDefaults
{
    /// <summary><paramref name="days"/> days from <paramref name="today"/>, no sky and no figures (each drawn "--").</summary>
    public static IReadOnlyList<ForecastDay> Unread(DateOnly today, int days)
        => [.. Enumerable.Range(0, Math.Max(0, days)).Select(i => new ForecastDay(today.AddDays(i), null, null, null, null))];
}
