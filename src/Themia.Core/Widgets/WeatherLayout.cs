using Themia.Core.Model;

namespace Themia.Core.Widgets;

/// <summary>How the current-conditions widget shows its secondary readings. [S-723]</summary>
public enum WeatherDetailMode
{
    /// <summary>The temperature and the sky only.</summary>
    None,

    /// <summary>One token under the reading, "Feels 22°C": the small tile's one extra.</summary>
    Feels,

    /// <summary>A flat row (or two) of icon + label + value cells under the reading.</summary>
    Cells,

    /// <summary>A strip: the readings as label/value lines in a column beside the reading.</summary>
    Side,

    /// <summary>A tall column: the readings as edge-to-edge label/value rows under the reading.</summary>
    Stack,
}

/// <summary>What the current-conditions widget draws at one size, worked out by <see cref="WeatherNowLayout"/>.</summary>
/// <param name="Detail">How the facts are shown.</param>
/// <param name="FactCount">How many facts (in <see cref="WeatherFacts"/> order) are shown.</param>
/// <param name="FactColumns">Cells per row in <see cref="WeatherDetailMode.Cells"/>, columns of lines in Side.</param>
/// <param name="HighLow">Today's high and low under the condition.</param>
/// <param name="HeroRoom">The height the reading and its condition may grow into.</param>
/// <param name="SideWidth">The width of the Side column, 0 in every other mode.</param>
public sealed record WeatherNowPlan(
    WeatherDetailMode Detail,
    int FactCount,
    int FactColumns,
    bool HighLow,
    double HeroRoom,
    double SideWidth);

/// <summary>
/// [S-723] The current-conditions widget's disclosure ladder, as a pure function of its slot.
/// <list type="number">
/// <item>Always: the temperature (and the sky, beside or under it).</item>
/// <item>A strip, or a tile Expanded wide (not tall), puts the readings in a column <i>beside</i> the reading, where they cost no height.</item>
/// <item>A tall column stacks them as rows under it.</item>
/// <item>Anything else: a row of cells once the slot is Regular high, else one "Feels" token.</item>
/// <item>Today's high and low.</item>
/// <item>A second row of cells, once the slot is Huge both ways.</item>
/// </list>
/// No hourly chart and no next-days strip at any size: the hours and the days are the forecast
/// widget's job. What the rungs leave goes to the reading.
/// </summary>
public static class WeatherNowLayout
{
    /// <summary>The view's own inset either side; the facts and the reading are measured inside it.</summary>
    public const double Inset = 16;

    /// <summary>
    /// The inset above and below. 12, not 8: the bottom row of readings used to run to within a few
    /// pixels of the card's edge, closer than anything else on the desktop sits to it.
    /// </summary>
    public const double VerticalInset = 12;

    /// <summary>Between two parts of the ladder.</summary>
    public const double Gap = 8;

    /// <summary>A cell: a caption label over a strong body value ([S-668]).</summary>
    public static double CellHeight => PartMetrics.StatCell;

    /// <summary>The narrowest a cell still reads ("Humidity" / "63%").</summary>
    public const double CellMinWidth = 88;

    /// <summary>Space between two cells.</summary>
    public const double CellGap = 8;

    /// <summary>A label/value line, beside the reading or under it: one stat line for both ([S-668]).</summary>
    public static double StatLine => PartMetrics.StatLine;

    /// <summary>The most Stack rows: past this the reading is worth more than an eighth fact.</summary>
    public const int StackMax = 8;

    /// <param name="slot">The view's slot (the card less its header).</param>
    /// <param name="heroFloor">The reading at its smallest step, with the condition under it if it must be.</param>
    /// <param name="facts">How many facts the reading has (<see cref="WeatherFacts.For"/>).</param>
    /// <param name="details">The user's "Show detail" switch: off is the reading alone.</param>
    /// <param name="hasToday">Whether today's high/low are known.</param>
    public static WeatherNowPlan Plan(
        WidgetArea slot,
        double heroFloor,
        int facts,
        bool details,
        bool hasToday)
    {
        var box = slot.Deflate(Inset, VerticalInset);
        var room = new Disclosure(box, Gap);
        room.Reserve(heroFloor);

        if (!details || !slot.IsMeasured)
        {
            return new WeatherNowPlan(WeatherDetailMode.None, 0, 0, false, heroFloor + room.Remaining, 0);
        }

        var mode = WeatherDetailMode.None;
        var count = 0;
        var columns = 0;
        var sideWidth = 0.0;
        facts = Math.Max(0, facts);

        var strip = slot.Shape == AreaShape.Wide && slot.WidthClass >= SizeClass.Regular;
        var broad = slot.WidthClass >= SizeClass.Expanded && slot.Shape != AreaShape.Tall && slot.HeightClass >= SizeClass.Regular;

        if ((strip || broad) && facts > 0)
        {
            // A strip, or a tile wide enough for two columns: the readings beside the reading, as
            // many lines as the height holds, in one column while they fit and two only when not.
            // Under the reading they left a dead band between the two at L and XL.
            sideWidth = Math.Clamp(box.Width * 0.4, 150, 320);
            var lines = Disclosure.WholeRows(box.Height, StatLine);
            columns = lines < facts && sideWidth >= (2 * 140) + Gap ? 2 : 1;
            count = Math.Min(facts, lines * columns);

            if (count > 0)
            {
                mode = WeatherDetailMode.Side;
            }
            else
            {
                sideWidth = 0;
                columns = 0;
            }
        }

        var highLow = false;

        if (mode == WeatherDetailMode.None && slot.Shape == AreaShape.Tall && slot.HeightClass >= SizeClass.Expanded && facts > 0)
        {
            // A column has the height and not the width: today's line first, then the rows.
            highLow = hasToday && room.Take(FluentType.Caption.LineHeight);
            count = room.TakeRows(StatLine, Math.Min(facts, StackMax));

            if (count > 0)
            {
                mode = WeatherDetailMode.Stack;
                columns = 1;
            }
        }
        else if (mode == WeatherDetailMode.None && facts > 0)
        {
            var fitColumns = (int)Math.Floor((box.Width + CellGap) / (CellMinWidth + CellGap));
            var cellsFit = fitColumns >= 3;

            if (slot.HeightClass >= SizeClass.Regular && cellsFit && room.Take(CellHeight))
            {
                mode = WeatherDetailMode.Cells;
                columns = Math.Min(Math.Min(fitColumns, 5), facts);
                count = columns;
            }
            else if (room.Take(FluentType.Caption.LineHeight, FluentType.EstimateWidth(FluentType.Caption, 10)))
            {
                mode = WeatherDetailMode.Feels;
                count = 1;
            }
        }

        if (mode is WeatherDetailMode.Side or WeatherDetailMode.Cells or WeatherDetailMode.None)
        {
            highLow = hasToday && (slot.Least >= SizeClass.Regular || mode == WeatherDetailMode.Side)
                && room.Take(FluentType.Caption.LineHeight);
        }

        // A second row of cells, on a tile large both ways, for the readings past the first five.
        if (mode == WeatherDetailMode.Cells && slot.Least >= SizeClass.Huge && facts > count && room.Take(CellHeight))
        {
            count = Math.Min(facts, columns * 2);
        }

        // What the rungs left goes to the reading.
        return new WeatherNowPlan(mode, count, columns, highLow, heroFloor + room.Remaining, sideWidth);
    }
}

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
