using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>What the rows style draws. [S-725]</summary>
/// <param name="Days">How many days: the config's.</param>
/// <param name="Lines">The lines each row shows (the config's, 1 or 2): its pitch follows, 28 or 40.</param>
/// <param name="Meter">The range bar's width between the day and its temperatures: a share of the row, so it grows with the card.</param>
/// <param name="Compact">No lead, the high alone: never, in the converted forecast (kept for the row builder).</param>
public sealed record ForecastRowsPlan(int Days, int Lines, double Meter, bool Compact);

/// <summary>What the columns style draws. [S-726]</summary>
/// <param name="Days">How many days: the config's.</param>
/// <param name="ColumnWidth">Each column: the content's width shared equally.</param>
/// <param name="Icon">The sky's glyph, 32.</param>
/// <param name="Low">The low under the bar: always.</param>
/// <param name="Bar">The range bar standing up between the high and the low: always.</param>
/// <param name="Precipitation">The precipitation at the column's foot: the config's switch.</param>
public sealed record ForecastColumnsPlan(int Days, double ColumnWidth, double Icon, bool Low, bool Bar, bool Precipitation);

/// <summary>
/// [S-724]..[S-726] [S-11145] [S-11146] [S-14410] The forecast as an atom: its <c>Days</c> (1..14, a new one 5)
/// decide what it shows in every style, and they are the card's: each style steps an axis by one day and a resize
/// stores the days its box holds (<see cref="SizeSets"/>; no menu line). Rows: a row a day, the height stepped by
/// the days, 220..420 wide. Columns: a 56 column a day, the width stepped by the days, one height. Curve: a 56
/// column a day, the width stepped by the days, the chart's band 72..144 tall. Grid: the days in their arrangement
/// (one row up to four days, two up to eight, three past), a 64 cell a step across and a row a step down
/// (<see cref="ForecastGrid.DaysFor"/>). Nothing else sheds or appears with the box: a bar or a chart gets longer.
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

    /// <summary>[S-725] The rows for the box's width: the config's days and lines, the bar a share of the row.</summary>
    public static ForecastRowsPlan Rows(WidgetArea area, WeatherForecastConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var width = ContentWidth(area);
        return new ForecastRowsPlan(config.EffectiveDays, config.EffectiveLines, Math.Min(MeterMax, Math.Floor(width * MeterShare)), false);
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

    /// <summary>[S-726] The columns for the box's width: the config's days sharing it, every part drawn.</summary>
    public static ForecastColumnsPlan Columns(WidgetArea area, WeatherForecastConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return new ForecastColumnsPlan(config.EffectiveDays, ContentWidth(area) / config.EffectiveDays, FamilyShape.IconLarge, true, true, config.ShowPrecipitation);
    }

    /// <summary>[S-726] A column's parts top to bottom: the day, the 32 glyph, the high, the bar, the low, the rain if on, 4 apart.</summary>
    public static double ColumnHeight(bool precipitation)
    {
        var line = FamilyType.Body.LineHeight;
        var parts = line + FamilyShape.IconLarge + line + ColumnBar + line;
        return parts + (4 * ColumnGap) + (precipitation ? ColumnGap + FamilyType.Caption.LineHeight : 0);
    }

    /// <summary>[S-11146] Each style's sizing from the config.</summary>
    public static WidgetSizing Sizing(WeatherForecastConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var days = config.EffectiveDays;
        var inset = 2 * FamilySpace.Inset;
        var chrome = FamilySpace.ContentTop + FamilySpace.Inset;
        switch (config.Style)
        {
            case ForecastStyle.Columns:
            {
                var height = chrome + ColumnHeight(config.ShowPrecipitation);
                return WidgetSizing.Range(new(inset + ColumnMin, height), new(inset + (days * ColumnMin), height), new(inset + (14 * ColumnMin), height))
                    .WithWidthSteps(new SizeSteps(inset, ColumnMin, 1, 14, days));
            }

            case ForecastStyle.Curve:
            {
                var parts = ForecastCurve.Parts(config.ShowPrecipitation);
                return WidgetSizing.Range(
                    new(1, chrome + parts + ForecastCurve.ChartMin),
                    new(1, chrome + parts + ForecastCurve.ChartStart),
                    new(1, chrome + parts + ForecastCurve.ChartMax))
                    .WithWidthSteps(new SizeSteps(inset, ForecastCurve.ColumnStart, 1, 14, days));
            }

            case ForecastStyle.Grid:
            {
                var (columns, rows) = ForecastGrid.Arrangement(days);
                return WidgetSizing.Range(new(1, 1), new(1, 1), new(1, 1))
                    .WithWidthSteps(ForecastGrid.ColumnSteps(columns))
                    .WithHeightSteps(ForecastGrid.RowSteps(rows));
            }

            default:
                // [S-14100] A row a day, whole grid cells, from the card's top edge to its foot edge: the card is exactly
                // its rows (owner, 2026-11-20: "now the weather forecast widget always has one block space at the bottom").
                return WidgetSizing.Range(new(RowsNarrowest, 1), new(300, 1), new(RowsWidest, 1))
                    .WithHeightSteps(SizeSteps.InCells(FamilyShape.RowHeight(config.EffectiveLines), 1, 14, days));
        }
    }

    /// <summary>
    /// [S-2442] [S-11146] [S-14410] The config a forecast of <paramref name="size"/> means: in Rows the days its height
    /// holds, in Columns and Curve the days its width holds, in Grid the days whose arrangement its cells across and
    /// rows down hold (<see cref="ForecastGrid.DaysFor"/>).
    /// </summary>
    public static WidgetConfig SizeSets(WidgetConfig config, SizeDip size)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (config is not WeatherForecastConfig forecast)
        {
            return config;
        }

        var sizing = Sizing(forecast);
        int? days = forecast.Style == ForecastStyle.Grid
            ? ForecastGrid.DaysFor(sizing.WidthSteps!.Nearest(size.Width), sizing.HeightSteps!.Nearest(size.Height))
            : sizing switch
            {
                { HeightSteps: { FollowsConfig: true } rows } => rows.Nearest(size.Height),
                { WidthSteps: { FollowsConfig: true } columns } => columns.Nearest(size.Width),
                _ => null,
            };
        if (days is not { } wanted || wanted == forecast.EffectiveDays)
        {
            return config;
        }

        var copy = (WeatherForecastConfig)forecast.Clone();
        copy.Days = wanted;
        return copy;
    }

    /// <summary>The content's width: the view less the inset either side.</summary>
    public static double ContentWidth(WidgetArea area) => Math.Max(0, area.Width - (2 * FamilySpace.Inset));

    /// <summary>The content's height: the view (its top is the content's) less the bottom inset.</summary>
    public static double ContentHeight(WidgetArea area) => Math.Max(0, area.Height - FamilySpace.Inset);
}
