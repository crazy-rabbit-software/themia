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
/// <param name="ChartHeight">The next-24-hours chart's height, 0 when it is not shown.</param>
/// <param name="HeroRoom">The height the reading and its condition may grow into.</param>
/// <param name="SideWidth">The width of the Side column, 0 in every other mode.</param>
public sealed record WeatherNowPlan(
    WeatherDetailMode Detail,
    int FactCount,
    int FactColumns,
    bool HighLow,
    double ChartHeight,
    double HeroRoom,
    double SideWidth);

/// <summary>
/// [S-723] The current-conditions widget's disclosure ladder, as a pure function of its slot.
/// <list type="number">
/// <item>Always: the temperature (and the sky, beside or under it).</item>
/// <item>A strip puts the readings in a column <i>beside</i> the reading, where they cost no height.</item>
/// <item>A tall column stacks them as rows under it.</item>
/// <item>Anything else: a row of cells once the slot is Regular high, else one "Feels" token.</item>
/// <item>Today's high and low.</item>
/// <item>A second row of cells, once the slot is Huge both ways.</item>
/// <item>The next 24 hours as a chart, once the slot is Expanded high; it takes what the reading leaves.</item>
/// </list>
/// No next-days strip at any size: the days are the forecast widget's job.
/// </summary>
public static class WeatherNowLayout
{
    /// <summary>The view's own inset either side; the facts and the reading are measured inside it.</summary>
    public const double Inset = 16;

    /// <summary>The inset above and below.</summary>
    public const double VerticalInset = 8;

    /// <summary>Between two parts of the ladder.</summary>
    public const double Gap = 8;

    /// <summary>A cell: a caption label over a strong body value.</summary>
    public const double CellHeight = 36;

    /// <summary>The narrowest a cell still reads ("Humidity" / "63%").</summary>
    public const double CellMinWidth = 88;

    /// <summary>Space between two cells.</summary>
    public const double CellGap = 8;

    /// <summary>A label/value line in the Side column.</summary>
    public const double SideLine = 20;

    /// <summary>Between two Side lines.</summary>
    public const double SideGap = 4;

    /// <summary>A Stack row, edge to edge.</summary>
    public const double StackRow = 28;

    /// <summary>The most Stack rows: past this the reading is worth more than an eighth fact.</summary>
    public const int StackMax = 8;

    /// <summary>The smallest chart that still reads as a curve, axis captions included.</summary>
    public const double ChartMin = 72;

    /// <summary>
    /// The reading's ceiling once a chart is below it: Display plus its condition and today's line.
    /// Past this, height is worth more as chart than as a bigger number.
    /// </summary>
    public const double HeroCeiling = 92 + 20 + 16;

    /// <param name="slot">The view's slot (the card less its header).</param>
    /// <param name="heroFloor">The reading at its smallest step, with the condition under it if it must be.</param>
    /// <param name="facts">How many facts the reading has (<see cref="WeatherFacts.For"/>).</param>
    /// <param name="details">The user's "Show detail" switch: off is the reading alone.</param>
    /// <param name="hasToday">Whether today's high/low are known.</param>
    /// <param name="hours">How many hourly points there are for the chart.</param>
    public static WeatherNowPlan Plan(
        WidgetArea slot,
        double heroFloor,
        int facts,
        bool details,
        bool hasToday,
        int hours)
    {
        var box = slot.Deflate(Inset, VerticalInset);
        var room = new Disclosure(box, Gap);
        room.Reserve(heroFloor);

        if (!details || !slot.IsMeasured)
        {
            return new WeatherNowPlan(WeatherDetailMode.None, 0, 0, false, 0, heroFloor + room.Remaining, 0);
        }

        var mode = WeatherDetailMode.None;
        var count = 0;
        var columns = 0;
        var sideWidth = 0.0;
        facts = Math.Max(0, facts);

        if (slot.Shape == AreaShape.Wide && slot.WidthClass >= SizeClass.Regular && facts > 0)
        {
            // A strip: a column beside the reading, as many lines as its height holds.
            sideWidth = Math.Clamp(box.Width * 0.4, 150, 320);
            columns = sideWidth >= (2 * 140) + Gap ? 2 : 1;
            var lines = Disclosure.WholeRows(box.Height, SideLine, SideGap);
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
            count = room.TakeRows(StackRow, Math.Min(facts, StackMax));

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

        var chart = mode != WeatherDetailMode.Stack && mode != WeatherDetailMode.Side
            && hours >= 2
            && slot.HeightClass >= SizeClass.Expanded
            && slot.WidthClass >= SizeClass.Regular
            && room.Take(ChartMin);

        // What the rungs left goes to the reading, up to its ceiling when a chart is below to
        // take the rest; with no chart the reading has it all.
        var heroRoom = heroFloor + room.Remaining;
        var chartHeight = 0.0;

        if (chart)
        {
            heroRoom = Math.Min(heroRoom, Math.Max(heroFloor, HeroCeiling));
            chartHeight = ChartMin + Math.Max(0, heroFloor + room.Remaining - heroRoom);
        }

        return new WeatherNowPlan(mode, count, columns, highLow, chartHeight, heroRoom, sideWidth);
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

/// <summary>What one forecast card carries in the grid. [S-726]</summary>
public sealed record ForecastGridPlan(
    int Columns,
    int Rows,
    double CardHeight,
    bool Scroll,
    bool Bar,
    bool Description,
    bool Precipitation,
    bool Extras,
    bool Sun);

/// <summary>
/// [S-724]..[S-726] The forecast widget's three layouts and the ladder inside each, as pure
/// functions of the slot and the number of days.
/// </summary>
public static class WeatherForecastLayout
{
    /// <summary>A row's floor, normal and compact.</summary>
    public const double RowMin = 40;

    public const double RowMinCompact = 32;

    /// <summary>Past this a stretched row stops growing: an 80px row with one line on it is a gap.</summary>
    public const double RowMax = 80;

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

    /// <summary>A grid card's floor.</summary>
    public const double CardMin = 64;

    /// <summary>The narrowest a grid card can be.</summary>
    public const double CardMinWidth = 140;

    /// <summary>
    /// The layout to draw: the user's choice, or for <see cref="ForecastLayout.Auto"/> columns on
    /// a strip (which has the width for a week side by side and the height for three rows) and rows
    /// everywhere else.
    /// </summary>
    public static ForecastLayout Resolve(ForecastLayout configured, WidgetArea slot)
    {
        if (configured is ForecastLayout.Rows or ForecastLayout.Columns or ForecastLayout.Grid)
        {
            return configured;
        }

        return slot.Shape == AreaShape.Wide && slot.WidthClass >= SizeClass.Regular
            ? ForecastLayout.Columns
            : ForecastLayout.Rows;
    }

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

    /// <summary>The grid ladder: cards across, and what each card has the height for.</summary>
    public static ForecastGridPlan Grid(WidgetArea slot, int days)
    {
        days = Math.Max(1, days);
        var width = Math.Max(0, slot.Width - (2 * 8));
        var columns = width <= 220 ? 1 : Math.Clamp((int)Math.Floor(width / (CardMinWidth + 8)), 2, 4);
        columns = Math.Min(columns, days);
        var rows = (days + columns - 1) / columns;

        var height = Math.Max(0, slot.Height - (2 * 8));
        var natural = (height - ((rows - 1) * 8)) / rows;
        var scroll = natural < CardMin;
        var cardHeight = scroll ? CardMin : natural;

        // The card: day + glyph, high/low; then the bar, the sky, the precipitation.
        var room = new Disclosure((width - ((columns - 1) * 8)) / columns, cardHeight - 16, 4);
        room.Reserve(20 + FluentType.Body.LineHeight);
        var bar = room.Take(4);
        var description = room.Take(FluentType.Caption.LineHeight);
        var precipitation = room.Take(FluentType.Caption.LineHeight);
        var extras = room.Take(FluentType.Caption.LineHeight);
        var sun = room.Take(FluentType.Caption.LineHeight);

        return new ForecastGridPlan(columns, rows, cardHeight, scroll, bar, description, precipitation, extras, sun);
    }
}
