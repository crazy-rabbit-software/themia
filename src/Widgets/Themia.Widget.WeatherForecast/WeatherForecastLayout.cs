using Themia.Core.Model;
using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>What the rows style draws at one size. [S-725]</summary>
/// <param name="Days">How many of the first days are shown: as many whole rows as the content holds.</param>
/// <param name="Lines">The lines each row shows (1 or 2, by the tier, [S-6712]): its pitch follows, 28 or 40.</param>
/// <param name="Meter">The range bar's width between the day and its temperatures; 0 for none.</param>
/// <param name="Compact">A content too narrow for the glyph and both figures: no lead, the high alone.</param>
public sealed record ForecastRowsPlan(int Days, int Lines, double Meter, bool Compact);

/// <summary>What the columns style draws at one size. [S-726]</summary>
/// <param name="Days">How many of the first days are shown: as many as have <see cref="WeatherForecastLayout.ColumnMin"/>.</param>
/// <param name="ColumnWidth">Each column: the content's width shared equally.</param>
/// <param name="Icon">The sky's glyph (16, or 32 from a regular height); 0 for none.</param>
/// <param name="Low">The low under the high (or under the bar).</param>
/// <param name="Bar">The range bar standing up between the high and the low, taking the column's room left.</param>
/// <param name="Precipitation">The precipitation at the column's foot.</param>
public sealed record ForecastColumnsPlan(int Days, double ColumnWidth, double Icon, bool Low, bool Bar, bool Precipitation);

/// <summary>
/// [S-724]..[S-726] The forecast's rows and columns on the family's blocks ([S-6711], [S-6712]), as pure
/// functions of the view's area and the number of days. Nothing scrolls and nothing stretches: only
/// whole rows and columns are shown, the first days that fit, and room left stays empty ([S-666]).
/// </summary>
public static class WeatherForecastLayout
{
    /// <summary>The range bar in a row needs a content this wide.</summary>
    public const double MeterFrom = 240;

    /// <summary>The range bar's share of the content's width, and its cap.</summary>
    public const double MeterShare = 0.32;

    /// <summary>The range bar's widest.</summary>
    public const double MeterMax = 140;

    /// <summary>Under this content width a row has no lead and shows the high alone ("Today 16°" on the smallest tile).</summary>
    public const double RowFullFrom = 120;

    /// <summary>The narrowest column that holds "Today" and "-18°" in Body.</summary>
    public const double ColumnMin = 56;

    /// <summary>A standing range bar's shortest useful length.</summary>
    public const double ColumnBarMin = 24;

    /// <summary>Between two parts of a column.</summary>
    public const double ColumnGap = FamilySpace.S1;

    /// <summary>
    /// [S-725] The rows: one per day (the day's sky glyph, the day, a range bar, its low and high),
    /// two lines from a regular area both ways (the sky in words and the precipitation under the day),
    /// as many whole rows as the content holds, never more than the forecast has. Under
    /// <see cref="RowFullFrom"/> every row is the day and its high alone, so the day is not cut to a letter.
    /// </summary>
    public static ForecastRowsPlan Rows(WidgetArea area, int days)
    {
        var width = ContentWidth(area);
        var lines = RowRules.Lines(area, 2);
        var shown = Math.Clamp(RowRules.Fit(ContentHeight(area), lines), 0, Math.Max(0, days));
        var meter = width >= MeterFrom ? Math.Min(MeterMax, Math.Floor(width * MeterShare)) : 0;
        return new ForecastRowsPlan(shown, lines, meter, width < RowFullFrom);
    }

    /// <summary>
    /// [S-726] The columns: the days side by side, each the content's width shared equally and at least
    /// <see cref="ColumnMin"/>. Every column has its day and its high; then, as the height allows, the
    /// sky's glyph, the low, the range bar standing up between them and the precipitation.
    /// </summary>
    /// <param name="precipitation">Whether the precipitation line is on (it costs height).</param>
    public static ForecastColumnsPlan Columns(WidgetArea area, int days, bool precipitation = true)
    {
        var width = ContentWidth(area);
        var shown = Math.Clamp((int)Math.Floor((width + Disclosure.Tolerance) / ColumnMin), 1, Math.Max(1, days));
        var column = width / shown;
        var line = FamilyType.Body.LineHeight;
        var room = ContentHeight(area) - (2 * line) - ColumnGap;

        bool Take(double cost)
        {
            if (room + Disclosure.Tolerance < cost + ColumnGap)
            {
                return false;
            }

            room -= cost + ColumnGap;
            return true;
        }

        var size = area.HeightClass >= SizeClass.Regular ? FamilyShape.IconLarge : FamilyShape.Icon;
        var icon = Take(size) ? size : 0;
        var low = Take(line);
        var bar = Take(ColumnBarMin);
        var rain = precipitation && Take(FamilyType.Caption.LineHeight);
        return new ForecastColumnsPlan(shown, column, icon, low, bar, rain);
    }

    /// <summary>The content's width: the view less the inset either side.</summary>
    public static double ContentWidth(WidgetArea area) => Math.Max(0, area.Width - (2 * FamilySpace.Inset));

    /// <summary>The content's height: the view (its top is the content's) less the bottom inset.</summary>
    public static double ContentHeight(WidgetArea area) => Math.Max(0, area.Height - FamilySpace.Inset);
}
