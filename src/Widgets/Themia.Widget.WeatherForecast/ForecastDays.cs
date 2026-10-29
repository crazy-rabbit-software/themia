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
/// [S-6171] The forecast's grid: the days as cells (the day, the sky's glyph, the high beside the
/// low, centred), 12 apart as the family's cells ([S-6713]), arranged in the columns × rows that make
/// the cells largest for the tile's shape: 7 days are 4 × 2 on a landscape tile, 2 × 4 on a column and
/// 7 × 1 on a strip. A cell is never smaller than <see cref="CellMinWidth"/> × <see cref="CellMinHeight"/>:
/// the grid shows fewer days instead.
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

    public static ForecastGridPlan Plan(WidgetArea area, int days)
    {
        days = Math.Max(1, days);
        var width = WeatherForecastLayout.ContentWidth(area);
        var height = WeatherForecastLayout.ContentHeight(area);

        // The most days whose best arrangement still has cells at the floor; one day at the least.
        for (var shown = days; shown >= 1; shown--)
        {
            if (Arrange(shown, width, height, CellMinWidth, CellMinHeight) is { } best)
            {
                var cellHeight = Cell(height, best.Rows);
                var large = RowRules.CellIcon(area);
                var icon = CellHeight(large) <= cellHeight + Disclosure.Tolerance ? large : FamilyShape.IconLarge;
                return new ForecastGridPlan(shown, best.Columns, best.Rows, Cell(width, best.Columns), cellHeight, icon);
            }
        }

        return new ForecastGridPlan(1, 1, 1, width, height, height + Disclosure.Tolerance >= CellMinHeight ? FamilyShape.IconLarge : 0);
    }

    /// <summary>
    /// The arrangement of <paramref name="shown"/> cells whose cells are largest against the floor's
    /// shape (the smaller of the two ratios), or null when none reaches the floor. Ties go to fewer
    /// empty cells, then to fewer rows.
    /// </summary>
    public static (int Columns, int Rows)? Arrange(int shown, double width, double height, double minWidth, double minHeight)
    {
        (int Columns, int Rows)? best = null;
        var bestScore = 0.0;
        var bestEmpty = int.MaxValue;

        for (var columns = 1; columns <= shown; columns++)
        {
            var rows = (shown + columns - 1) / columns;

            // A shape that leaves a whole row (or column) empty is a smaller grid drawn bigger.
            if ((rows - 1) * columns >= shown || (columns - 1) * rows >= shown)
            {
                continue;
            }

            var cellWidth = Cell(width, columns);
            var cellHeight = Cell(height, rows);
            if (cellWidth + Disclosure.Tolerance < minWidth || cellHeight + Disclosure.Tolerance < minHeight)
            {
                continue;
            }

            var score = Math.Min(cellWidth / minWidth, cellHeight / minHeight);
            var empty = (columns * rows) - shown;

            if (best is null || score > bestScore + 1e-9 || (Math.Abs(score - bestScore) <= 1e-9 && empty < bestEmpty))
            {
                best = (columns, rows);
                bestScore = score;
                bestEmpty = empty;
            }
        }

        return best;
    }

    private static double Cell(double length, int count) => Math.Max(0, (length - ((count - 1) * Gap)) / count);
}
