namespace Themia.Core.Widgets;

/// <summary>How a feed widget arranges its items.</summary>
public enum FeedArrangement
{
    /// <summary>One column of rows.</summary>
    List = 0,

    /// <summary>Two or three columns of rows, read left to right then down.</summary>
    Columns,
}

/// <summary>What one layout of a feed widget shows.</summary>
/// <param name="Arrangement">List or columns.</param>
/// <param name="Columns">How many columns of rows.</param>
/// <param name="ListWidth">The width one column of rows is laid out in.</param>
/// <param name="Favicon">Each row carries its site's icon.</param>
/// <param name="Host">The site's name beside the icon, on its own line over the headline.</param>
/// <param name="HeadlineLines">How many lines a headline may wrap to.</param>
/// <param name="PreviewLines">Lines of summary under the headline; 0 for none.</param>
/// <param name="Date">The item's age.</param>
public readonly record struct FeedPlan(
    FeedArrangement Arrangement,
    int Columns,
    double ListWidth,
    bool Favicon,
    bool Host,
    int HeadlineLines,
    int PreviewLines,
    bool Date)
{
    /// <summary>The icon sits at the start of the date line when the host has no line of its own.</summary>
    public bool MetaLine => Date || (Favicon && !Host);
}

/// <summary>
/// [S-1305] The feed widget's disclosure ladder. Pure, so every tier is pinned by a test.
/// <list type="number">
/// <item>Rung 0, at every size: the newest headline, whole (two lines, one when even that does not fit).</item>
/// <item>Its age, then its site's icon (from 180px wide), then the site's name on its own line once a
/// row is 240px wide (the original hid the name at ≤160px and kept the icon).</item>
/// <item>From 400px wide, two columns of rows (three from 720px) instead of one column that a
/// single item fills.</item>
/// <item>A summary where it costs no row, or where at least three rows still fit on a tall-enough
/// tile — more headlines beat previews; never on a narrow tall column, which is for headlines.</item>
/// </list>
/// The feed is its headlines at every size: no reading pane beside the list (user decision).
/// Row counts are never decided here: rows are as tall as their headlines, so the view measures
/// them and shows only those that fit whole ([S-639]); the estimate below only chooses the rungs.
/// </summary>
public static class FeedLayout
{
    /// <summary>
    /// The space between columns. None: rows carry their own side
    /// padding, so their hover fills meet edge to edge and the text still has a 32px gutter.
    /// </summary>
    public const double Gutter = 0;

    /// <summary>A column narrower than this cannot hold a two-line headline worth reading.</summary>
    public const double MinColumn = 200;

    /// <summary>From here a row has room for the site's name beside its icon.</summary>
    public const double HostNeedsWidth = 240;

    /// <summary>From here a row carries its site's icon.</summary>
    public const double FaviconNeedsWidth = 180;

    /// <summary>A row's padding above and below its text.</summary>
    public const double RowPadding = 4;

    /// <summary>The space between the lines of a row.</summary>
    public const double LineGap = 2;

    /// <summary>Height lost to the list's own inset (top and bottom).</summary>
    public const double ListInset = 4;

    private static double Headline => FluentType.Body.LineHeight;
    private static double Caption => FluentType.Caption.LineHeight;

    public static FeedPlan Plan(WidgetArea area, bool showSource, bool showPreview, bool showDate)
    {
        if (!area.IsMeasured)
        {
            return new FeedPlan(FeedArrangement.List, 1, 0, showSource, showSource, 2, showPreview ? 2 : 0, showDate);
        }

        var height = Math.Max(0, area.Height - ListInset);

        var columns = area.Width >= 720 ? 3 : area.Width >= 400 ? 2 : 1;

        while (columns > 1 && Column(area.Width, columns) < MinColumn)
        {
            columns--;
        }

        var width = Column(area.Width, columns);
        var plan = new FeedPlan(
            columns > 1 ? FeedArrangement.Columns : FeedArrangement.List,
            columns,
            width,
            showSource && width >= FaviconNeedsWidth,
            showSource && width >= HostNeedsWidth,
            2,
            0,
            showDate);

        plan = Fit(plan, height);

        if (!showPreview)
        {
            return plan;
        }

        var without = Disclosure.WholeRows(height, RowEstimate(plan));

        foreach (var lines in new[] { 2, 1 })
        {
            var with = plan with { PreviewLines = lines };
            var rows = Disclosure.WholeRows(height, RowEstimate(with));

            var free = rows >= 1 && rows == without;
            var roomy = area.Shape != AreaShape.Tall && area.HeightClass >= SizeClass.Expanded && rows >= 3;

            if (free || roomy)
            {
                return with;
            }
        }

        return plan;
    }

    /// <summary>
    /// What one row of <paramref name="plan"/> is expected to need with a headline that wraps to all
    /// its lines. An upper bound: a one-line headline makes a shorter row.
    /// </summary>
    public static double RowEstimate(FeedPlan plan)
    {
        var height = (RowPadding * 2) + (plan.HeadlineLines * Headline);

        if (plan.Host)
        {
            height += Caption + LineGap;
        }

        if (plan.PreviewLines > 0)
        {
            height += LineGap + (plan.PreviewLines * Caption);
        }

        if (plan.MetaLine)
        {
            height += LineGap + Caption;
        }

        return height;
    }

    /// <summary>How many rows of one column to keep from measured row heights: whole rows, at least one.</summary>
    public static int WholeRows(IReadOnlyList<double> rowHeights, double available)
    {
        ArgumentNullException.ThrowIfNull(rowHeights);

        var used = 0.0;
        var count = 0;

        foreach (var row in rowHeights)
        {
            if (count > 0 && used + row > available + Disclosure.Tolerance)
            {
                break;
            }

            used += row;
            count++;
        }

        return count;
    }

    private static double Column(double width, int columns)
        => columns <= 1 ? width : (width - ((columns - 1) * Gutter)) / columns;

    /// <summary>
    /// Rung 0 must fit whole: shed the secondary lines (site name, then age and icon, then the
    /// headline's second line) until one row does.
    /// </summary>
    private static FeedPlan Fit(FeedPlan plan, double height)
    {
        if (RowEstimate(plan) <= height + Disclosure.Tolerance)
        {
            return plan;
        }

        plan = plan with { Host = false };
        if (RowEstimate(plan) <= height + Disclosure.Tolerance)
        {
            return plan;
        }

        plan = plan with { Date = false, Favicon = false };
        if (RowEstimate(plan) <= height + Disclosure.Tolerance)
        {
            return plan;
        }

        return plan with { HeadlineLines = 1 };
    }
}
