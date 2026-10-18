using Themia.Core.Widgets;

namespace Themia.Sdk.Kit;

/// <summary>The row looks a kit list draws.</summary>
public enum KitRowLook
{
    /// <summary>
    /// [S-7031] A row as tall as its title needs: the subtitle on a line of its own above the title
    /// (with the lead beside it), the title wrapping to the plan's lines, the preview under it, and
    /// the time last (with the lead at its start when the subtitle has no line). Rows are measured and
    /// shown whole ([S-639]). Feeds.
    /// </summary>
    Rich,
}

/// <summary>[S-7030] The optional fields of a kit row, which a spec offers and the user may turn off.</summary>
[Flags]
public enum KitShow
{
    None = 0,

    /// <summary>The lead (an icon, the site's icon).</summary>
    Lead = 1,

    /// <summary>The subtitle line.</summary>
    Subtitle = 2,

    /// <summary>The preview lines.</summary>
    Preview = 4,

    /// <summary>The time.</summary>
    When = 8,

    All = Lead | Subtitle | Preview | When,
}

/// <summary>What happens to items that do not fit.</summary>
public enum KitOverflow
{
    /// <summary>A scroll away, under the shared fade ([S-1281]).</summary>
    Scroll,
}

/// <summary>
/// [S-7030] A kit list widget's shape: which look its rows have, which optional fields exist, how
/// many columns it may grow to, how its time is written, and its sentences. The widget's own data,
/// so its breakpoints live in one place, <see cref="KitLadder.Plan"/>.
/// </summary>
/// <param name="Untitled">The title of an item that has none, in the current language.</param>
/// <param name="Empty">What the widget says when its sources answered with nothing ([S-7040]).</param>
public sealed record KitSpec(
    KitRowLook Look,
    KitShow Optional,
    Func<string> Untitled,
    Func<string> Empty,
    KitTime Time = KitTime.Age,
    KitOverflow Overflow = KitOverflow.Scroll,
    int MaxColumns = 1);

/// <summary>[S-7030] What one size of a kit list shows; chosen by <see cref="KitLadder.Plan"/>.</summary>
/// <param name="Columns">How many columns of rows, read left to right, then down.</param>
/// <param name="ColumnWidth">The width one column is laid out in (0 before the box is measured).</param>
/// <param name="Lead">The row carries its lead.</param>
/// <param name="Subtitle">The subtitle on its own line over the title (with the lead beside it).</param>
/// <param name="TitleLines">How many lines a title may wrap to.</param>
/// <param name="PreviewLines">Lines of preview; 0 for none.</param>
/// <param name="When">The time.</param>
public readonly record struct KitPlan(
    KitRowLook Look,
    int Columns,
    double ColumnWidth,
    bool Lead,
    bool Subtitle,
    int TitleLines,
    int PreviewLines,
    bool When)
{
    /// <summary>The line under the title and preview: the time, and the lead when the subtitle has no line.</summary>
    public bool MetaLine => When || (Lead && !Subtitle);
}

/// <summary>
/// [S-7031] The kit's disclosure ladder ([S-660]..[S-666]), the one place a kit list's breakpoints
/// live. For the rich look, at every size the newest title, whole (two lines, one when even that does
/// not fit); then its time, then its lead (from 180px of column width), then the subtitle line (from
/// 240px); from 400px wide two columns (three from 720px, while a column keeps 200px); a preview
/// where it costs no row, or where at least three rows still fit on a tile at least 280px high that
/// is not a tall column (more titles beat previews). Rows are never counted here: they are as tall as
/// their titles, so the list measures them and shows only those that fit whole ([S-639]); the
/// estimate below only chooses the rungs. Concise first ([S-666]): no rung adds a control or a pane.
/// </summary>
public static class KitLadder
{
    /// <summary>The space between columns: none, rows carry their own side padding so their fills meet.</summary>
    public const double Gutter = 0;

    /// <summary>A column narrower than this cannot hold a two-line title worth reading.</summary>
    public const double MinColumn = 200;

    /// <summary>From here a row has room for the subtitle beside its lead.</summary>
    public const double SubtitleNeedsWidth = 240;

    /// <summary>From here a row carries its lead.</summary>
    public const double LeadNeedsWidth = 180;

    /// <summary>A rich row's padding above and below its text.</summary>
    public const double RowPadding = 4;

    /// <summary>The space between the lines of a rich row.</summary>
    public const double LineGap = 2;

    /// <summary>Height lost to the list's own inset (top and bottom).</summary>
    public const double ListInset = 4;

    private static double Title => FluentType.Body.LineHeight;
    private static double Caption => FluentType.Caption.LineHeight;

    /// <param name="shows">The optional fields the user has on (the spec's <see cref="KitSpec.Optional"/> bounds them).</param>
    public static KitPlan Plan(WidgetArea area, KitSpec spec, KitShow shows)
    {
        ArgumentNullException.ThrowIfNull(spec);

        var on = shows & spec.Optional;
        bool Has(KitShow field) => (on & field) == field;

        if (!area.IsMeasured)
        {
            return new KitPlan(spec.Look, 1, 0, Has(KitShow.Lead), Has(KitShow.Subtitle), 2, Has(KitShow.Preview) ? 2 : 0, Has(KitShow.When));
        }

        var height = Math.Max(0, area.Height - ListInset);
        var columns = Math.Min(Math.Max(1, spec.MaxColumns), area.Width >= 720 ? 3 : area.Width >= 400 ? 2 : 1);

        while (columns > 1 && Column(area.Width, columns) < MinColumn)
        {
            columns--;
        }

        var width = Column(area.Width, columns);
        var plan = new KitPlan(
            spec.Look,
            columns,
            width,
            Has(KitShow.Lead) && width >= LeadNeedsWidth,
            Has(KitShow.Subtitle) && width >= SubtitleNeedsWidth,
            2,
            0,
            Has(KitShow.When));

        plan = Fit(plan, height);

        if (!Has(KitShow.Preview))
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
    /// What one row of <paramref name="plan"/> is expected to need with a title that wraps to all its
    /// lines. An upper bound: a one-line title makes a shorter row.
    /// </summary>
    public static double RowEstimate(KitPlan plan)
    {
        var height = (RowPadding * 2) + (plan.TitleLines * Title);

        if (plan.Subtitle)
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

    /// <summary>[S-639] How many lines of rows to keep from measured line heights: whole lines, at least one.</summary>
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
    /// Rung 0 must fit whole: shed the secondary lines (the subtitle, then the time and lead, then the
    /// title's second line) until one row does.
    /// </summary>
    private static KitPlan Fit(KitPlan plan, double height)
    {
        if (RowEstimate(plan) <= height + Disclosure.Tolerance)
        {
            return plan;
        }

        plan = plan with { Subtitle = false };
        if (RowEstimate(plan) <= height + Disclosure.Tolerance)
        {
            return plan;
        }

        plan = plan with { When = false, Lead = false };
        if (RowEstimate(plan) <= height + Disclosure.Tolerance)
        {
            return plan;
        }

        return plan with { TitleLines = 1 };
    }
}
