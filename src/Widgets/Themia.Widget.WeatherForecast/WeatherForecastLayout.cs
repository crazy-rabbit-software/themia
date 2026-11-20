using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>What the rows style draws. [S-725]</summary>
/// <param name="Days">How many days: as many rows as the card holds ([S-14502]).</param>
/// <param name="Lines">The lines each row shows (the config's, 1 or 2): its pitch follows, 28 or 40.</param>
/// <param name="Meter">The range bar's width between the day and its temperatures: a share of the row, so it grows with the card.</param>
/// <param name="Compact">No lead, the high alone: never, in the converted forecast (kept for the row builder).</param>
public sealed record ForecastRowsPlan(int Days, int Lines, double Meter, bool Compact);

/// <summary>What the columns style draws. [S-726]</summary>
/// <param name="Days">How many days: as many columns as the card holds ([S-14502]).</param>
/// <param name="ColumnWidth">Each column: the content's width shared equally.</param>
/// <param name="Icon">The sky's glyph, 32.</param>
/// <param name="Low">The low under the bar: always.</param>
/// <param name="Bar">The range bar standing up between the high and the low: always.</param>
/// <param name="Precipitation">The precipitation at the column's foot: the config's switch.</param>
public sealed record ForecastColumnsPlan(int Days, double ColumnWidth, double Icon, bool Low, bool Bar, bool Precipitation);

/// <summary>
/// [S-724]..[S-726] [S-11145] [S-11146] [S-14410] [S-14502] The forecast as an atom: its days (1..14, a new one 5) are
/// the card's, every one its box reaches at their one fixed size, the last cut ([S-14810], [S-14812]), never stored (no
/// menu line); every axis takes every grid cell ([S-14500]). Rows: a row a day (28 or 40, its optimal whole cells),
/// 220..420 wide. Columns: a 60 column a day (its 56 in whole cells) from the card's left edge, one height. Curve: a 60
/// column a day, the chart's band 72..144 tall. Grid:
/// the days in their arrangement (one row up to four days, two up to eight, three past), as many 64 cells across and
/// cells down as the box holds (<see cref="ForecastGrid.DaysFor"/>). Nothing else sheds or appears with the box: a bar
/// or a chart gets longer.
/// </summary>
public static class WeatherForecastLayout
{
    /// <summary>The range bar's share of a row's content, and its cap.</summary>
    public const double MeterShare = 0.32;

    /// <summary>The range bar's widest.</summary>
    public const double MeterMax = 140;

    /// <summary>The narrowest column that holds "Today" and "-18°" in Body.</summary>
    public const double ColumnMin = 56;

    /// <summary>A column's widest: past it the days stand apart.</summary>
    public const double ColumnMax = 72;

    /// <summary>A standing range bar's length.</summary>
    public const double ColumnBar = FamilyShape.IconLarge;

    /// <summary>Between two parts of a column.</summary>
    public const double ColumnGap = FamilySpace.S1;

    public const double RowsNarrowest = 220;

    public const double RowsWidest = 420;

    /// <summary>
    /// [S-725] [S-14502] The rows for the box: as many days as its height holds rows at their optimal from the card's top
    /// edge to its foot (<see cref="DaysIn"/>), the config's lines, the bar a share of the row.
    /// </summary>
    public static ForecastRowsPlan Rows(WidgetArea area, WeatherForecastConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var width = ContentWidth(area);
        return new ForecastRowsPlan(DaysIn(area, config), config.EffectiveLines, Math.Min(MeterMax, Math.Floor(width * MeterShare)), false);
    }

    /// <summary>
    /// [S-14502] The days a forecast shows in <paramref name="area"/> (its view's box): as many rows (the Rows style, from
    /// the card's top edge) or columns (Columns: <see cref="ColumnMin"/> each; Curve: <see cref="ForecastCurve.ColumnStart"/>;
    /// from the card's left edge) as it reaches at their one fixed size, the last cut by the card's foot or right edge
    /// ([S-14810]), the grid's arrangement its cells hold
    /// (<see cref="ForecastGrid.DaysFor"/>); 1..<see cref="WeatherForecastConfig.MostDays"/>. Before the box is measured, a
    /// new one's days.
    /// </summary>
    public static int DaysIn(WidgetArea area, WeatherForecastConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (!area.IsMeasured)
        {
            return WeatherForecastConfig.DefaultDays;
        }

        int Across(double column) => ItemSize.Count(area.Width, column, area.Grid(), 1, WeatherForecastConfig.MostDays);
        return config.Style switch
        {
            ForecastStyle.Columns => Across(ColumnMin),
            ForecastStyle.Curve => Across(ForecastCurve.ColumnStart),
            ForecastStyle.Grid => ForecastGrid.DaysFor(ForecastGrid.CellsAcross(area), ForecastGrid.CellsDown(area)),
            _ => ItemSize.Count(area.Height + FamilySpace.Inset, FamilyShape.RowHeight(config.EffectiveLines), area.Grid(), 1, WeatherForecastConfig.MostDays),
        };
    }

    /// <summary>
    /// [S-12403] The range bar's length in a row: what the row leaves once its lead, its widest day name
    /// (with two lines, the widest second line too) and its widest value are in, 12 either side, so the
    /// bars fill the rows to the values and every day's bar starts and ends in the same place; never
    /// shorter than the plan's share (<paramref name="floor"/>), where a long second line takes the room.
    /// </summary>
    /// <param name="inner">The row's width inside its side insets (<see cref="ContentWidth"/>).</param>
    /// <param name="lead">The lead and its gap (0 without a lead).</param>
    public static double MeterWidth(double inner, double lead, double widestName, double widestValue, double floor)
    {
        var left = inner - lead - Math.Ceiling(widestName) - Math.Ceiling(widestValue) - (2 * FamilySpace.ColumnGap);
        return Math.Max(Math.Max(0, floor), Math.Floor(left));
    }

    /// <summary>[S-726] [S-14502] [S-14810] The columns for the box's width: every day it reaches at a column's one width (its 56 in whole cells), the last cut by the card's right edge, every part drawn.</summary>
    public static ForecastColumnsPlan Columns(WidgetArea area, WeatherForecastConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var days = DaysIn(area, config);
        return new ForecastColumnsPlan(days, ItemSize.Optimal(ColumnMin, area.Grid()), FamilyShape.IconLarge, true, true, config.ShowPrecipitation);
    }

    /// <summary>[S-726] A column's parts top to bottom: the day, the 32 glyph, the high, the bar, the low, the rain if on, 4 apart.</summary>
    public static double ColumnHeight(bool precipitation)
    {
        var line = FamilyType.Body.LineHeight;
        var parts = line + FamilyShape.IconLarge + line + ColumnBar + line;
        return parts + (4 * ColumnGap) + (precipitation ? ColumnGap + FamilyType.Caption.LineHeight : 0);
    }

    /// <summary>
    /// [S-11146] [S-14500] [S-14810] Each style's sizing from the config: a new one <see cref="WeatherForecastConfig.DefaultDays"/>
    /// days, one day at least, every cell between (the days keep their size; the last one the card reaches is cut).
    /// </summary>
    public static WidgetSizing Sizing(WeatherForecastConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        const int days = WeatherForecastConfig.DefaultDays, most = WeatherForecastConfig.MostDays;
        var grid = ItemGrid.Default();
        var chrome = FamilySpace.ContentTop + FamilySpace.Inset;
        switch (config.Style)
        {
            case ForecastStyle.Columns:
            {
                var height = chrome + ColumnHeight(config.ShowPrecipitation);
                var column = ItemSize.Optimal(ColumnMin, grid);
                return WidgetSizing.Range(new(column, height), new(days * column, height), new(most * column, height));
            }

            case ForecastStyle.Curve:
            {
                var parts = ForecastCurve.Parts(config.ShowPrecipitation);
                double Across(int n) => n * ItemSize.Optimal(ForecastCurve.ColumnStart, grid);
                return WidgetSizing.Range(
                    new(Across(1), chrome + parts + ForecastCurve.ChartMin),
                    new(Across(days), chrome + parts + ForecastCurve.ChartStart),
                    new(Across(most), chrome + parts + ForecastCurve.ChartMax));
            }

            case ForecastStyle.Grid:
            {
                var (columns, rows) = ForecastGrid.Arrangement(days);
                return WidgetSizing.Range(
                    new(ForecastGrid.Width(1), ForecastGrid.Height(1)),
                    new(ForecastGrid.Width(columns), ForecastGrid.Height(rows)),
                    new(ForecastGrid.Width(ForecastGrid.MostColumns), ForecastGrid.Height(ForecastGrid.MostRows)));
            }

            default:
                // [S-14810] A row a day at its one fixed size, from the card's top edge (owner, 2026-11-20: "the widget items
                // should actually have a fixed size").
                var row = ItemSize.Optimal(FamilyShape.RowHeight(config.EffectiveLines), grid);
                return WidgetSizing.Range(new(RowsNarrowest, row), new(300, days * row), new(RowsWidest, most * row));
        }
    }

    /// <summary>The content's width: the view less the inset either side.</summary>
    public static double ContentWidth(WidgetArea area) => Math.Max(0, area.Width - (2 * FamilySpace.Inset));

    /// <summary>The content's height: the view (its top is the content's) less the bottom inset.</summary>
    public static double ContentHeight(WidgetArea area) => Math.Max(0, area.Height - FamilySpace.Inset);
}
