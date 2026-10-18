namespace Themia.Core.Widgets;

/// <summary>
/// [S-669] [S-6171] How large one forecast day's parts are drawn: the weekday, the sky's glyph, the
/// high and the low, and the range bar's thickness. One ladder for the rows, the columns and the
/// grid, so a day grows the same way in each: the room a day has goes to bigger figures and a bigger
/// glyph, never to more facts ([S-666]).
/// </summary>
/// <param name="Level">0 (the day-row parts at their base size) to <see cref="Largest"/>.</param>
/// <param name="Label">The weekday's step ("Today" is its strong form).</param>
/// <param name="High">The high's step, semibold.</param>
/// <param name="Low">The low's step, regular.</param>
/// <param name="Icon">The sky's glyph.</param>
/// <param name="Bar">The range bar's thickness.</param>
public sealed record DayScale(int Level, TypeStep Label, TypeStep High, TypeStep Low, double Icon, double Bar)
{
    /// <summary>The largest level.</summary>
    public const int Largest = 3;

    /// <summary>
    /// The level's parts. Level 0 is [S-669]'s day row (Body, Body Strong, 20px glyph); each level
    /// after it is a ramp step up. The bar is 6 at the base: at 4 it read as a scratch beside
    /// 14px figures.
    /// </summary>
    public static DayScale At(int level) => Math.Clamp(level, 0, Largest) switch
    {
        0 => new DayScale(0, FluentType.Body, FluentType.BodyStrong, FluentType.Body, PartMetrics.DayIcon, 6),
        1 => new DayScale(1, FluentType.Body, FluentType.Strong(FluentType.BodyLarge), FluentType.BodyLarge, 24, 6),
        2 => new DayScale(2, FluentType.BodyLarge, FluentType.Subtitle, FluentType.Subtitle with { SemiBold = false }, 32, 8),
        _ => new DayScale(3, FluentType.BodyLarge, FluentType.Title, FluentType.Title with { SemiBold = false }, 40, 8),
    };

    /// <summary>The weekday column: "Today", "Wed", "Do." at the label's step, never under [S-669]'s 44.</summary>
    public double LabelWidth => Math.Max(PartMetrics.DayLabelWidth, FluentType.EstimateWidth(Label, 5));

    /// <summary>One figure column ("-18°") at the high's step, never under [S-669]'s 32.</summary>
    public double FigureWidth => Math.Max(PartMetrics.FigureWidth, FluentType.EstimateWidth(High, 4));

    /// <summary>The tallest part of a one-line row: the glyph or the high's line.</summary>
    public double LineHeight => Math.Max(Icon, High.LineHeight);
}

/// <summary>What the grid style draws at one size ([S-6171]).</summary>
/// <param name="Days">How many of the first days are shown.</param>
/// <param name="Columns">Cells across.</param>
/// <param name="Rows">Cells down.</param>
/// <param name="CellWidth">One cell's width.</param>
/// <param name="CellHeight">One cell's height.</param>
/// <param name="Scale">The day's parts in a cell.</param>
/// <param name="Icon">The sky's glyph: shed only by a lone cell shorter than the floor (the smallest tile).</param>
public sealed record ForecastGridPlan(int Days, int Columns, int Rows, double CellWidth, double CellHeight, DayScale Scale, bool Icon);

/// <summary>
/// [S-6171] The forecast's grid: the days as cells (weekday, glyph, high and low), arranged in the
/// columns × rows that make the cells largest for the tile's shape: 7 days are 4 × 2 on a landscape
/// tile, 2 × 4 on a column and 7 × 1 on a strip. A cell is never smaller than
/// <see cref="CellMinWidth"/> × <see cref="CellMinHeight"/>: the grid shows fewer days instead.
/// </summary>
public static class ForecastGrid
{
    /// <summary>The grid's own inset on every side.</summary>
    public const double Padding = 8;

    /// <summary>Between two cells, both ways.</summary>
    public const double Gap = 4;

    /// <summary>Inside a cell, above and below its parts.</summary>
    public const double CellPadding = 6;

    /// <summary>Inside a cell, either side of its parts.</summary>
    public const double CellSidePadding = 4;

    /// <summary>Between two parts in a cell.</summary>
    public const double PartGap = 2;

    /// <summary>The narrowest cell: "Today" and "16° 8°" in the base parts.</summary>
    public static double CellMinWidth => CellWidthFor(DayScale.At(0));

    /// <summary>The shortest cell: the weekday, the glyph and the figures in the base parts.</summary>
    public static double CellMinHeight => CellHeightFor(DayScale.At(0));

    /// <summary>A cell's height for a scale: the Caption-or-larger label, the glyph and the figures line.</summary>
    public static double CellHeightFor(DayScale scale)
        => (2 * CellPadding) + LabelStep(scale).LineHeight + scale.Icon + scale.High.LineHeight + (2 * PartGap);

    /// <summary>A cell's width for a scale: the two figures side by side, or the label, whichever is wider.</summary>
    public static double CellWidthFor(DayScale scale)
        => (2 * CellSidePadding) + Math.Max(FluentType.EstimateWidth(LabelStep(scale), 5), (2 * FluentType.EstimateWidth(scale.High, 3)) + Gap);

    /// <summary>A cell's weekday: Caption at the base (the cell is narrow), the scale's label above it.</summary>
    public static TypeStep LabelStep(DayScale scale) => scale.Level == 0 ? FluentType.Caption : scale.Label;

    public static ForecastGridPlan Plan(WidgetArea slot, int days)
    {
        days = Math.Max(1, days);
        var width = Math.Max(0, slot.Width - (2 * Padding));
        var height = Math.Max(0, slot.Height - (2 * Padding));
        var minWidth = CellMinWidth;
        var minHeight = CellMinHeight;

        // The most days whose best arrangement still has cells at the floor; one day at the least.
        for (var shown = days; shown >= 1; shown--)
        {
            if (Arrange(shown, width, height, minWidth, minHeight) is { } best)
            {
                return Plan(shown, best.Columns, best.Rows, width, height);
            }
        }

        return Plan(1, 1, 1, width, height);
    }

    private static ForecastGridPlan Plan(int shown, int columns, int rows, double width, double height)
    {
        var cellWidth = Cell(width, columns);
        var cellHeight = Cell(height, rows);
        var level = 0;

        for (var k = DayScale.Largest; k > 0; k--)
        {
            var scale = DayScale.At(k);
            if (CellWidthFor(scale) <= cellWidth + Disclosure.Tolerance && CellHeightFor(scale) <= cellHeight + Disclosure.Tolerance)
            {
                level = k;
                break;
            }
        }

        return new ForecastGridPlan(shown, columns, rows, cellWidth, cellHeight, DayScale.At(level), cellHeight + Disclosure.Tolerance >= CellMinHeight);
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

/// <summary>
/// [S-6172] "Now" on the forecast: where the current time and temperature sit on the curve and on
/// today's range bar. The temperature is the current-conditions reading for the same place and unit
/// that is already held (no request of its own); without one, or one too old to be "now", there is
/// no temperature mark, only the time on the curve.
/// </summary>
public static class ForecastNow
{
    /// <summary>A reading older than this is no longer the temperature now.</summary>
    public static readonly TimeSpan MaxAge = TimeSpan.FromHours(2);

    /// <summary>The temperature to mark, or null: absent, not finite, from the future or too old.</summary>
    public static double? Temperature(double? temperature, DateTimeOffset at, DateTimeOffset now)
    {
        if (temperature is not { } t || !double.IsFinite(t))
        {
            return null;
        }

        var age = now - at;
        return age >= TimeSpan.FromMinutes(-5) && age <= MaxAge ? t : null;
    }

    /// <summary>How much of the day has gone, 0 at midnight to just under 1.</summary>
    public static double DayFraction(TimeOnly time) => time.ToTimeSpan().TotalHours / 24;

    /// <summary>
    /// Across the curve: today's column starts at <paramref name="todayIndex"/> × the column width
    /// and the day runs across it, so noon is its middle (where the day's high dot sits).
    /// </summary>
    public static double CurveX(int todayIndex, double dayFraction, double columnWidth)
        => (Math.Max(0, todayIndex) + Math.Clamp(dayFraction, 0, 1)) * columnWidth;

    /// <summary>Where today is among the days shown, or -1.</summary>
    public static int TodayIndex(IReadOnlyList<ForecastDay> days, DateOnly today, int shown)
    {
        ArgumentNullException.ThrowIfNull(days);
        for (var i = 0; i < Math.Min(shown, days.Count); i++)
        {
            if (days[i].Day == today)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Along a range bar scaled to the forecast's coldest and warmest: the same scale as
    /// <see cref="WeatherReport.RangeBar"/>, clamped to the bar's ends; one temperature all week is
    /// the middle.
    /// </summary>
    public static double OnBar(double temperature, double coldest, double warmest)
    {
        var span = warmest - coldest;
        if (!double.IsFinite(temperature) || !double.IsFinite(span) || span <= 0)
        {
            return 0.5;
        }

        return Math.Clamp((temperature - coldest) / span, 0, 1);
    }
}
