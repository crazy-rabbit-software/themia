using Themia.Core.Model;

namespace Themia.Core.Widgets;

/// <summary>What the rows style draws at one size. [S-725]</summary>
/// <param name="Days">How many of the first days are shown: as many as have <see cref="WeatherForecastLayout.RowMin"/>.</param>
/// <param name="RowHeight">Each row, <see cref="WeatherForecastLayout.RowMin"/>..<see cref="WeatherForecastLayout.RowMax"/>.</param>
/// <param name="Scale">The day's parts, grown with the row.</param>
public sealed record ForecastRowsPlan(
    int Days,
    double RowHeight,
    DayScale Scale,
    bool IconAndLow,
    bool Bar,
    bool Description,
    bool Precipitation,
    bool Chance,
    bool Wind,
    bool TwoLine);

/// <summary>What the columns style draws at one size. [S-726]</summary>
/// <param name="Days">How many of the first days are shown: as many as have <see cref="WeatherForecastLayout.ColumnMin"/>.</param>
/// <param name="ColumnWidth">Each column, <see cref="WeatherForecastLayout.ColumnMin"/>..<see cref="WeatherForecastLayout.ColumnMax"/>.</param>
/// <param name="Scale">The day's parts, grown with the column.</param>
public sealed record ForecastColumnsPlan(
    int Days,
    double ColumnWidth,
    DayScale Scale,
    bool Icon,
    bool Low,
    bool Bar,
    bool Precipitation,
    bool Description,
    bool Chance,
    bool Wind);

/// <summary>
/// [S-724]..[S-726] [S-6164] The forecast widget's styles and the ladder inside each, as pure
/// functions of the slot and the number of days. No style scrolls: a day is never thinner than its
/// floor; the first days that fit are shown instead ([S-6171]).
/// </summary>
public static class WeatherForecastLayout
{
    /// <summary>A row's floor: the base day-row parts with air above and below.</summary>
    public const double RowMin = 40;

    /// <summary>
    /// Past this a row stops growing (the list keeps to the top, the rest is empty): a row this tall
    /// already carries the largest parts.
    /// </summary>
    public const double RowMax = 80;

    /// <summary>The row height each <see cref="DayScale"/> level needs, by level.</summary>
    private static readonly double[] RowForLevel = [RowMin, 46, 58, 70];

    /// <summary>The list's own padding above and below the rows.</summary>
    public const double ListPadding = 14;

    /// <summary>A row's own padding either side, as a Windows list item's.</summary>
    public const double RowInset = 16;

    /// <summary>The narrowest a column can be and still hold "Today" and "-18°".</summary>
    public const double ColumnMin = 56;

    /// <summary>Past this a column stops growing and the days are centred: a week of 300px columns reads as islands.</summary>
    public const double ColumnMax = 160;

    /// <summary>The narrowest column that holds a one-word description.</summary>
    public const double ColumnDescriptionMin = 72;

    /// <summary>Between two parts of a column.</summary>
    public const double ColumnGap = 4;

    /// <summary>The columns' list padding on every side.</summary>
    public const double ColumnListPadding = 8;

    /// <summary>A column's own padding on every side.</summary>
    public const double ColumnPadding = 4;

    /// <summary>A vertical range bar's shortest useful length.</summary>
    public const double ColumnBarMin = 24;

    /// <summary>The shortest a horizontal range bar is worth drawing at.</summary>
    public const double RowBarMin = 64;

    /// <summary>
    /// The rows ladder: how many days, how tall, which columns a row carries and how large its parts
    /// are. The rows are the whole widget at every size: no Today panel beside or above them.
    /// </summary>
    /// <param name="precipitation">Whether the precipitation column is on (it costs width).</param>
    public static ForecastRowsPlan Rows(WidgetArea slot, int days, bool precipitation = true)
    {
        days = Math.Max(1, days);

        var listHeight = Math.Max(0, slot.Height - ListPadding);
        var width = slot.Width;
        var shown = Math.Clamp((int)Math.Floor((listHeight + Disclosure.Tolerance) / RowMin), 1, days);
        var rowHeight = Math.Clamp(listHeight / shown, RowMin, RowMax);

        var iconAndLow = width >= 160;
        var bar = width >= 300;
        var description = width >= 380;
        var rain = precipitation && width >= 420;
        var chance = width >= 500;

        // The wind is the first column a narrower list sheds after the chance, so the sky keeps its
        // words: at ~510px "Teilweise bewölkt" was trimmed to "Teilweise be…" with the wind in. [S-725]
        var wind = width >= 540;

        // The parts grow with the row while the width has room for them beside the other columns:
        // what the facts take, a bar at its shortest, the row's own inset.
        var others = (description ? 100 : 0) + (rain ? 52 : 0) + (chance ? 36 : 0) + (wind ? 56 : 0) + (bar ? RowBarMin : 0);
        var level = 0;

        for (var k = DayScale.Largest; k > 0 && iconAndLow; k--)
        {
            var scale = DayScale.At(k);
            var core = (2 * RowInset) + scale.LabelWidth + scale.Icon + 16 + (2 * (scale.FigureWidth + 8));
            if (rowHeight + Disclosure.Tolerance >= RowForLevel[k] && core + others <= width + Disclosure.Tolerance)
            {
                level = k;
                break;
            }
        }

        var parts = DayScale.At(level);

        // A row tall enough for two lines puts the sky under the day and the precipitation under the
        // high, when the width has not already given them columns of their own.
        var twoLine = !description && rowHeight + Disclosure.Tolerance >= parts.High.LineHeight + FluentType.Caption.LineHeight + 8;

        return new ForecastRowsPlan(shown, rowHeight, parts, iconAndLow, bar, description, rain, chance, wind, twoLine);
    }

    /// <summary>The columns ladder: the days side by side, each column shedding from the bottom of its priority.</summary>
    /// <param name="precipitation">Whether the precipitation line is on (it costs height).</param>
    public static ForecastColumnsPlan Columns(WidgetArea slot, int days, bool precipitation = true)
    {
        days = Math.Max(1, days);
        var width = Math.Max(0, slot.Width - (2 * ColumnListPadding));
        var shown = Math.Clamp((int)Math.Floor((width + Disclosure.Tolerance) / ColumnMin), 1, days);
        var columnWidth = Math.Min(ColumnMax, width / shown);
        var inner = columnWidth - (2 * ColumnPadding);
        var height = slot.Height - (2 * ColumnListPadding) - (2 * ColumnPadding);

        // The largest parts whose whole core fits: day, glyph, high, a bar, low.
        for (var k = DayScale.Largest; k > 0; k--)
        {
            var scale = DayScale.At(k);
            var room = new Disclosure(inner, height, ColumnGap);
            var across = Math.Max(scale.Icon, Math.Max(FluentType.EstimateWidth(scale.Label, 5), scale.FigureWidth));
            var fits = inner + Disclosure.Tolerance >= across
                && room.Reserve(scale.Label.LineHeight) && room.Reserve(scale.Icon) && room.Reserve(scale.High.LineHeight)
                && room.Reserve(ColumnBarMin) && room.Reserve(scale.Low.LineHeight);

            if (fits)
            {
                return Extras(room, scale, shown, columnWidth, slot, precipitation);
            }
        }

        // The base parts: day and high always, then by priority glyph, low, bar.
        var parts = DayScale.At(0);
        var rest = new Disclosure(inner, height, ColumnGap);
        rest.Reserve(parts.Label.LineHeight + ColumnGap + parts.High.LineHeight);
        var icon = rest.Take(parts.Icon);
        var low = rest.Take(parts.Low.LineHeight);
        var bar = rest.Take(ColumnBarMin);

        return icon && low && bar
            ? Extras(rest, parts, shown, columnWidth, slot, precipitation)
            : new ForecastColumnsPlan(shown, columnWidth, parts, icon, low, bar, false, false, false, false);
    }

    /// <summary>After a whole core: the precipitation, the sky, and on a tall column the chance and the wind.</summary>
    private static ForecastColumnsPlan Extras(Disclosure room, DayScale scale, int shown, double columnWidth, WidgetArea slot, bool precipitation)
    {
        var caption = FluentType.Caption.LineHeight;
        var rain = precipitation && room.Take(caption);
        var description = room.Take(caption, ColumnDescriptionMin);
        var chance = slot.HeightClass >= SizeClass.Expanded && room.Take(caption);
        var wind = chance && room.Take(caption, ColumnDescriptionMin);
        return new ForecastColumnsPlan(shown, columnWidth, scale, true, true, true, rain, description, chance, wind);
    }
}
