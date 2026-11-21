using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>What the grid style draws at one size ([S-6171]).</summary>
/// <param name="Days">How many of the first days are shown.</param>
/// <param name="Columns">Cells across: as many as the card's width holds whole.</param>
/// <param name="Rows">Lines of cells down: as many as the days fill, the last one the card's foot reaches cut by it.</param>
/// <param name="CellWidth">One cell's one width (<see cref="ForecastGrid.CellNaturalWidth"/> in whole grid cells).</param>
/// <param name="CellHeight">One cell's one height (<see cref="ForecastGrid.CellNaturalHeight"/> in whole grid cells).</param>
/// <param name="Icon">The sky's glyph: 32.</param>
public sealed record ForecastGridPlan(int Days, int Columns, int Rows, double CellWidth, double CellHeight, double Icon);

/// <summary>
/// [S-6171] [S-11146] [S-14815] The forecast's grid: the days as cells (the day, the sky's 32 glyph, the high beside the
/// low, centred), each of one fixed size (its natural in whole grid cells, [S-14810]), from the card's left and top
/// edges, touching (owner, 2026-11-21: "the weather forecast widget grid view items do not go from edge to edge, they
/// have space everywhere."): as many across as the card's width holds whole (a cut column would hide a day in the
/// middle of the week), lines down as the days fill, the last line the card's foot reaches cut by it ([S-14502]:
/// the days are every cell the card shows, never stored).
/// </summary>
public static class ForecastGrid
{
    /// <summary>Between two parts of a cell.</summary>
    public const double PartGap = FamilySpace.S1;

    /// <summary>Between the high and the low.</summary>
    public const double FigureGap = FamilySpace.S2;

    /// <summary>The narrowest cell content: "Today" and "16° 8°" in Body.</summary>
    public const double CellMinWidth = 56;

    /// <summary>A cell's content: the day, the 32 glyph and the figures, 4 apart.</summary>
    public static double CellContentHeight => (2 * FamilyType.Body.LineHeight) + FamilyShape.IconLarge + (2 * PartGap);

    /// <summary>[S-14815] A cell's natural width: its content and the inset either side, inside the cell (WR-3).</summary>
    public const double CellNaturalWidth = CellMinWidth + (2 * FamilySpace.Inset);

    /// <summary>[S-14815] A cell's natural height: its content and 8 over and under it, inside the cell.</summary>
    public static double CellNaturalHeight => CellContentHeight + (2 * FamilySpace.S2);

    /// <summary>[S-14410] The most cells across a card stands at: five.</summary>
    public const int MostColumns = 5;

    /// <summary>[S-14410] The most lines a card stands at: three.</summary>
    public const int MostRows = 3;

    /// <summary>[S-14815] A cell's one width on <paramref name="grid"/>.</summary>
    public static double CellWidth(ItemGrid grid) => ItemSize.Optimal(CellNaturalWidth, grid);

    /// <summary>[S-14815] A cell's one height on <paramref name="grid"/>.</summary>
    public static double CellHeight(ItemGrid grid) => ItemSize.Optimal(CellNaturalHeight, grid);

    /// <summary>[S-14410] The grid's width at <paramref name="columns"/> cells: the cells alone, touching.</summary>
    public static double Width(int columns) => Math.Max(1, columns) * CellWidth(ItemGrid.Default());

    /// <summary>[S-14410] The grid's height at <paramref name="rows"/> lines: the cells alone, from the card's top edge.</summary>
    public static double Height(int rows) => Math.Max(1, rows) * CellHeight(ItemGrid.Default());

    /// <summary>
    /// [S-12403] How many cells each line holds, top to bottom: <paramref name="columns"/> a line, the
    /// last one the days left (the room after them empty). No days, no lines.
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

    /// <summary>
    /// [S-14815] The cells in the box (the view's: its top the inset under the card's top edge): as many across as its width
    /// holds whole, every line its height reaches from the card's top edge, the last cut by the foot (<see cref="ItemSize.Cells"/>).
    /// </summary>
    public static CellReach Cells(WidgetArea area)
        => ItemSize.Cells(area.Width, area.Height + FamilySpace.Inset, CellNaturalWidth, CellNaturalHeight, area.Grid(), 1, WeatherForecastConfig.MostDays);

    /// <summary>[S-14502] [S-14815] The days <paramref name="columns"/> cells across and <paramref name="rows"/> lines hold: every cell, at most 14.</summary>
    public static int DaysFor(int columns, int rows)
        => Math.Clamp(Math.Max(1, columns) * Math.Max(1, rows), 1, WeatherForecastConfig.MostDays);

    /// <summary>[S-6171] [S-14502] [S-14815] The grid for the box: the days its cells hold, the cells their one size, the 32 glyph.</summary>
    public static ForecastGridPlan Plan(WidgetArea area, WeatherForecastConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var days = WeatherForecastLayout.DaysIn(area, config);
        var columns = area.IsMeasured ? Cells(area).Columns : 3;
        var grid = area.Grid();
        return new ForecastGridPlan(days, columns, Lines(days, columns).Count, CellWidth(grid), CellHeight(grid), FamilyShape.IconLarge);
    }
}

/// <summary>[S-12813] The forecast's default: the days it shows, nothing read for any.</summary>
public static class ForecastDefaults
{
    /// <summary><paramref name="days"/> days from <paramref name="today"/>, no sky and no figures (each drawn "--").</summary>
    public static IReadOnlyList<ForecastDay> Unread(DateOnly today, int days)
        => [.. Enumerable.Range(0, Math.Max(0, days)).Select(i => new ForecastDay(today.AddDays(i), null, null, null, null))];
}
