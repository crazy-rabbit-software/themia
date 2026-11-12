using Themia.Core.Model;
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
/// day count (<see cref="Arrangement"/>); a cell 64..80 wide and one height.
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

    /// <summary>The length of <paramref name="count"/> cells of <paramref name="cell"/>, 12 apart.</summary>
    public static double Length(int count, double cell) => (count * cell) + ((count - 1) * Gap);

    /// <summary>[S-6171] [S-11146] The grid for the box: the config's days in their arrangement, the cells sharing the content, the 32 glyph.</summary>
    public static ForecastGridPlan Plan(WidgetArea area, WeatherForecastConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var days = config.EffectiveDays;
        var (columns, rows) = Arrangement(days);
        var width = WeatherForecastLayout.ContentWidth(area);
        var height = WeatherForecastLayout.ContentHeight(area);
        return new ForecastGridPlan(days, columns, rows, Cell(width, columns), Cell(height, rows), FamilyShape.IconLarge);
    }

    private static double Cell(double length, int count) => Math.Max(0, (length - ((count - 1) * Gap)) / count);
}
