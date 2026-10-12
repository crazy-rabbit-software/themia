namespace Themia.Core.Widgets;

/// <summary>Where the air widget puts its readings ([S-5107]).</summary>
public enum AirFacts
{
    /// <summary>The index and its band only.</summary>
    None = 0,

    /// <summary>One stat line per reading under the index (a tall or narrow tile).</summary>
    Lines = 1,

    /// <summary>Stat cells in two or three columns under the index.</summary>
    Cells = 2,

    /// <summary>Stat lines in a column beside the index (a strip).</summary>
    Side = 3,
}

/// <summary>[S-5107] The air widget's ladder for one size.</summary>
/// <param name="HeroRoom">The height the index and its band may grow into.</param>
/// <param name="Count">How many readings are shown, in priority order.</param>
/// <param name="Columns">Cells or side columns.</param>
/// <param name="SideWidth">The side column's width, on a strip.</param>
public sealed record AirPlan(double HeroRoom, AirFacts Facts, int Count, int Columns, double SideWidth)
{
    /// <summary>[S-5109] The band strip under the readings, once every reading is shown.</summary>
    public bool Strip { get; init; }
}

/// <summary>[S-5126] The rain widget's ladder for one size.</summary>
/// <param name="HeadlineRoom">The height the headline may grow into.</param>
/// <param name="Caption">The intensity / chance line under the headline.</param>
/// <param name="ChartHeight">The bar timeline's height, bars and their time labels; 0 for none.</param>
public sealed record RainPlan(double HeadlineRoom, bool Caption, double ChartHeight);

/// <summary>[S-5146] The exchange-rate widget's ladder for one size.</summary>
/// <param name="Hero">The first currency as the big readout.</param>
/// <param name="HeroRoom">The hero's height (value line and its caption).</param>
/// <param name="Rows">How many currencies are listed as rows (after the hero's, when there is one).</param>
/// <param name="RowHeight">The rows' height: the standard row, or the compact one on a short tile.</param>
/// <param name="RowSparkWidth">The width of the line in each row; 0 for none.</param>
/// <param name="ChartHeight">The hero's 90-day chart under everything else; 0 for none.</param>
public sealed record RatesPlan(bool Hero, double HeroRoom, int Rows, double RowHeight, double RowSparkWidth, double ChartHeight);

/// <summary>
/// [S-5107] [S-5126] [S-5146] The size ladders of the three online widgets (S-660..S-665): classify on
/// the area, spend the deflated box's height through one <see cref="Disclosure"/> in priority order,
/// costs in line heights, spare height to the hero.
/// </summary>
public static class OnlineLayouts
{
    public const double Inset = 16;

    public const double VerticalInset = 12;

    public const double Gap = 8;

    /// <summary>Fluent's list row heights (<c>Fluent.RowHeight</c> / <c>RowHeightCompact</c> in the app).</summary>
    public const double RowHeight = 40;

    public const double RowHeightCompact = 32;

    /// <summary>Below this height the rate list uses compact rows (<c>Fluent.CompactBelow</c>).</summary>
    public const double CompactBelow = RowHeight * 5;

    /// <summary>A stat cell's narrowest column.</summary>
    public const double CellMinWidth = 120;

    /// <summary>[S-5109] The band strip: a marker, the bar and a Caption line of breakpoints.</summary>
    public const double StripHeight = 36;

    /// <summary>[S-5126] The bars need this much to read as a timeline; the time labels come on top.</summary>
    public const double ChartMin = 32;

    /// <summary>[S-5146] The hero chart needs this much to show a trend rather than a squiggle.</summary>
    public const double HeroChartMin = 56;

    /// <summary>
    /// [S-5107] The index and its band are reserved; the readings take what is left, one stat line
    /// each on a tall or narrow tile, cells in columns where two fit across, a column beside the
    /// index on a strip. Whatever the readings leave grows the index.
    /// </summary>
    public static AirPlan Air(WidgetArea area, double heroFloor, int facts)
    {
        if (!area.IsMeasured)
        {
            return new AirPlan(0, AirFacts.None, 0, 0, 0);
        }

        var box = area.Deflate(Inset, VerticalInset);
        facts = Math.Max(0, facts);

        if (facts > 0 && area.Shape == AreaShape.Wide && box.Width >= 360)
        {
            var columns = box.Width >= 640 ? 2 : 1;
            var sideWidth = Math.Min(box.Width * (columns == 2 ? 0.6 : 0.5), columns * 220.0);
            var rows = Disclosure.WholeRows(box.Height, PartMetrics.StatLine);
            var count = Math.Min(facts, rows * columns);
            return count > 0
                ? new AirPlan(box.Height, AirFacts.Side, count, columns, sideWidth)
                : new AirPlan(box.Height, AirFacts.None, 0, 0, 0);
        }

        var room = new Disclosure(box.Width, box.Height, Gap);
        room.Reserve(heroFloor);

        var across = (int)Math.Floor((box.Width + Gap) / (CellMinWidth + Gap));
        var cellColumns = Math.Clamp(across, 1, 3);

        if (facts > 0 && area.Shape != AreaShape.Tall && cellColumns >= 2)
        {
            var wanted = (facts + cellColumns - 1) / cellColumns;
            var rows = room.TakeRows(PartMetrics.StatCell, wanted, Gap);
            var count = Math.Min(facts, rows * cellColumns);
            var strip = count == facts && box.Width >= 240 && room.Take(StripHeight);
            return new AirPlan(heroFloor + room.Remaining, count > 0 ? AirFacts.Cells : AirFacts.None, count, count > 0 ? cellColumns : 0, 0) { Strip = strip };
        }

        var lines = facts > 0 ? room.TakeRows(PartMetrics.StatLine, facts) : 0;
        var stripped = lines == facts && box.Width >= 160 && room.Take(StripHeight);
        return new AirPlan(heroFloor + room.Remaining, lines > 0 ? AirFacts.Lines : AirFacts.None, lines, lines > 0 ? 1 : 0, 0) { Strip = stripped };
    }

    /// <summary>
    /// [S-5126] The headline is reserved; then the caption line; then the bar timeline (bars and a
    /// Caption line of times) where at least <see cref="ChartMin"/> of bars fit, and the timeline
    /// takes the rest. The caller sizes the headline by its width before asking (its floor), so a
    /// large tile has a large sentence over a tall timeline.
    /// </summary>
    public static RainPlan Rain(WidgetArea area, double headlineFloor, bool hasCaption)
    {
        if (!area.IsMeasured)
        {
            return new RainPlan(0, false, 0);
        }

        var box = area.Deflate(Inset, VerticalInset);
        var room = new Disclosure(box.Width, box.Height, Gap);
        room.Reserve(headlineFloor);

        var caption = hasCaption && room.Take(FluentType.Caption.LineHeight);
        var labels = FluentType.Caption.LineHeight;
        var chart = 0.0;

        if (box.Width >= 120 && room.Fits(ChartMin + labels))
        {
            var spare = room.Remaining - (caption || headlineFloor > 0 ? Gap : 0);
            chart = spare;
            room.Take(chart);
        }

        return new RainPlan(headlineFloor + room.Remaining, caption, chart);
    }

    /// <summary>
    /// [S-5146] A tile under two compact rows tall (or under 160 wide) is the first currency as a
    /// hero. From the regular height up the first currency is the hero and the rest are rows under
    /// it, and a 90-day chart of the first takes what is left; below that, every currency is a row.
    /// Rows carry a small line of their own from 300 wide, where it does not crowd the figures.
    /// </summary>
    public static RatesPlan Rates(WidgetArea area, int pairs, bool chart, double heroFloor)
    {
        if (!area.IsMeasured || pairs <= 0)
        {
            return new RatesPlan(false, 0, 0, RowHeightCompact, 0, 0);
        }

        var box = area.Deflate(0, VerticalInset);
        var width = box.Width - (2 * Inset);
        var compact = area.Height < CompactBelow;
        var rowHeight = compact ? RowHeightCompact : RowHeight;
        var spark = chart && width >= 300 ? Math.Clamp(width * 0.28, 48, 160) : 0;

        if (width < 160 || box.Height < 2 * RowHeightCompact)
        {
            return new RatesPlan(true, box.Height, 0, rowHeight, 0, 0);
        }

        if (area.HeightClass >= SizeClass.Regular)
        {
            var room = new Disclosure(box.Width, box.Height, Gap);
            room.Reserve(heroFloor);
            var rows = pairs > 1 ? room.TakeRows(rowHeight, pairs - 1) : 0;
            var heroChart = chart && room.Fits(HeroChartMin) ? room.Remaining - (rows > 0 || heroFloor > 0 ? Gap : 0) : 0;
            if (heroChart > 0)
            {
                room.Take(heroChart);
            }

            return new RatesPlan(true, heroFloor + room.Remaining, rows, rowHeight, heroChart > 0 ? 0 : spark, heroChart);
        }

        return new RatesPlan(false, 0, Math.Min(pairs, Disclosure.WholeRows(box.Height, rowHeight)), rowHeight, spark, 0);
    }
}
