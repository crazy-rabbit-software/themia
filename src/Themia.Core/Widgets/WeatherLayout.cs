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

/// <summary>What one forecast row carries at a given width and height. [S-725]</summary>
public sealed record ForecastRowsPlan(
    double RowHeight,
    bool Stretch,
    bool Compact,
    bool IconAndLow,
    bool Bar,
    bool Description,
    bool Precipitation,
    bool Chance,
    bool Wind,
    bool TwoLine);

/// <summary>What one forecast column carries at a given height. [S-726]</summary>
public sealed record ForecastColumnsPlan(
    double ColumnWidth,
    bool Scroll,
    bool Icon,
    bool Low,
    bool Bar,
    bool Precipitation,
    bool Description,
    bool Chance,
    bool Wind);

/// <summary>
/// [S-724]..[S-726] [S-6164] The forecast widget's three styles and the ladder inside each, as pure
/// functions of the slot and the number of days.
/// </summary>
public static class WeatherForecastLayout
{
    /// <summary>A row's floor, normal and compact.</summary>
    public const double RowMin = 40;

    public const double RowMinCompact = 32;

    /// <summary>
    /// Past this a stretched row stops growing: the height of Windows' two-line list item. At 80 a
    /// row with one line on it was mostly gap, and the week read as seven islands.
    /// </summary>
    public const double RowMax = 56;

    /// <summary>A row at least this tall carries a second line (the sky, the precipitation).</summary>
    public const double TwoLineRow = 46;

    /// <summary>Under this list height the rows go compact.</summary>
    public const double CompactBelow = 200;

    /// <summary>The list's own padding above and below the rows.</summary>
    public const double ListPadding = 14;

    /// <summary>The narrowest a column can be and still hold "Today" and "-18°".</summary>
    public const double ColumnMin = 56;

    /// <summary>The narrowest column that holds a one-word description.</summary>
    public const double ColumnDescriptionMin = 72;

    /// <summary>Between two parts of a column.</summary>
    public const double ColumnGap = 4;

    /// <summary>
    /// The rows ladder: which columns a row carries and how tall rows are. The rows are the whole
    /// widget at every size: no Today panel beside or above them (it repeated the first row).
    /// </summary>
    public static ForecastRowsPlan Rows(WidgetArea slot, int days)
    {
        days = Math.Max(1, days);

        var listHeight = slot.Height - ListPadding;
        var width = slot.Width;
        var compact = listHeight < CompactBelow;
        var floor = compact ? RowMinCompact : RowMin;
        var stretch = days * floor <= listHeight + Disclosure.Tolerance;
        var rowHeight = stretch ? Math.Min(RowMax, listHeight / days) : floor;

        var iconAndLow = width >= 160;
        var bar = width >= 300;
        var description = width >= 380;
        var precipitation = width >= 420;
        var chance = width >= 500;

        // The wind is the first column a narrower list sheds after the chance, so the sky keeps its
        // words: at ~510px "Teilweise bewölkt" was trimmed to "Teilweise be…" with the wind in. [S-725]
        var wind = width >= 540;

        // A row tall enough for two lines puts the sky under the day and the precipitation under the
        // high, when the width has not already given them columns of their own.
        var twoLine = rowHeight >= TwoLineRow && !description;

        return new ForecastRowsPlan(rowHeight, stretch, compact, iconAndLow, bar, description, precipitation, chance, wind, twoLine);
    }

    /// <summary>The columns ladder: a week side by side, each column shedding from the bottom of its priority.</summary>
    public static ForecastColumnsPlan Columns(WidgetArea slot, int days)
    {
        days = Math.Max(1, days);
        var width = Math.Max(0, slot.Width - (2 * 8));
        var natural = width / days;
        var scroll = natural < ColumnMin;
        var columnWidth = scroll ? ColumnMin : natural;

        // Day and high are the column; the rest by priority: glyph, low, bar, precipitation, sky,
        // and on a tall column the chance and the wind. Inside the list padding (8 each side) and
        // the column's own (4 each side); the parts are 4 apart.
        var room = new Disclosure(columnWidth, slot.Height - (2 * 8) - (2 * 4), ColumnGap);
        room.Reserve(FluentType.Caption.LineHeight + ColumnGap + FluentType.BodyStrong.LineHeight);
        var icon = room.Take(20);
        var low = room.Take(FluentType.Caption.LineHeight);
        var bar = room.Take(24);
        var precipitation = room.Take(FluentType.Caption.LineHeight);
        var description = room.Take(FluentType.Caption.LineHeight, ColumnDescriptionMin);
        var chance = slot.HeightClass >= SizeClass.Expanded && room.Take(FluentType.Caption.LineHeight);
        var wind = chance && room.Take(FluentType.Caption.LineHeight, ColumnDescriptionMin);

        return new ForecastColumnsPlan(columnWidth, scroll, icon, low, bar, precipitation, description, chance, wind);
    }
}
