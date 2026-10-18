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

    /// <summary>
    /// [S-7032] Rows of stacked lines: the title with its time beside it, then the subtitle with the
    /// first badge beside it, then the preview; the lead in front and the tone's bar in the row's
    /// inset. On a wide strip or a very wide tile the row goes onto one line (<see cref="Line"/>). Mail.
    /// </summary>
    TwoLine,

    /// <summary>
    /// [S-7032] One line per item: the title in a column of its own, the subtitle with the preview
    /// after a dash, the first badge, the time; the spec's inline actions swap in for the time on the
    /// hovered and the focused row ([S-7033]). The plan's look on a strip for <see cref="TwoLine"/>.
    /// </summary>
    Line,

    /// <summary>
    /// [S-7034] One line of the user's columns in the user's order (<see cref="KitColumn"/>), the
    /// least essential shed first, with the columns' titles over them where the height affords it. Mail.
    /// </summary>
    Table,

    /// <summary>
    /// [S-7036] A scheduled item: the stripe in its calendar's colour, the time column (start over
    /// end), the title over its detail; on a narrow column the time folds into the detail line, on a
    /// wide one where and who get columns of their own. Rows of one height, in pages with day
    /// groups ([S-7035]). The agenda.
    /// </summary>
    Schedule,
}

/// <summary>[S-7034] A table look's column: which field of the item it shows.</summary>
public enum KitColumn
{
    Title,
    Subtitle,
    Preview,
    When,

    /// <summary>The item's first badge.</summary>
    Badge,
}

/// <summary>[S-7034] One table column at the width it gets: a fixed width, or a share of what is left.</summary>
public readonly record struct KitColumnFit(KitColumn Column, double Width, double Star);

/// <summary>
/// [S-7033] A line row's boxes, leading to trailing: the title, the subtitle line, the badge (0 when
/// it is not shown) and the trailing slot that holds the time or, revealed, the inline actions.
/// </summary>
public readonly record struct KitLineBoxes(double Title, double Line, double Badge, double Slot);

/// <summary>
/// [S-7030] What the user chose for a kit list: the optional fields on, how many preview lines (the
/// stacked look's, 1 to 5), and a table's columns in their order.
/// </summary>
public sealed record KitChoices(KitShow Shows, int PreviewLines = 2, IReadOnlyList<KitColumn>? Columns = null);

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

    /// <summary>The first badge (an account's name).</summary>
    Badges = 16,

    All = Lead | Subtitle | Preview | When | Badges,
}

/// <summary>What happens to items that do not fit.</summary>
public enum KitOverflow
{
    /// <summary>A scroll away, under the shared fade ([S-1281]).</summary>
    Scroll,

    /// <summary>[S-7035] Pages of the rows that fit whole, turned by the widget's own controls and keys; never scrolled.</summary>
    Page,
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
    int MaxColumns = 1)
{
    /// <summary>[S-7034] A table column's title, in the current language.</summary>
    public Func<KitColumn, string>? ColumnHeading { get; init; }

    /// <summary>[S-7034] How a table's time column writes it.</summary>
    public KitTime ColumnTime { get; init; } = KitTime.Stamp;

    /// <summary>
    /// [S-7036] Every label a schedule's time column may show at its widest ("All day", "Now" in the
    /// strong step, a clock in the language's digits), measured in the current language for the
    /// column's width.
    /// </summary>
    public Func<IReadOnlyList<(string Text, bool Strong)>>? TimeLabels { get; init; }
}

/// <summary>[S-7030] What one size of a kit list shows; chosen by <see cref="KitLadder.Plan"/>.</summary>
/// <param name="Columns">How many columns of rows, read left to right, then down.</param>
/// <param name="ColumnWidth">The width one column is laid out in (0 before the box is measured).</param>
/// <param name="Lead">The row carries its lead.</param>
/// <param name="Subtitle">The subtitle on its own line over the title (with the lead beside it).</param>
/// <param name="TitleLines">How many lines a title may wrap to.</param>
/// <param name="PreviewLines">Lines of preview; 0 for none.</param>
/// <param name="When">The time.</param>
/// <param name="Badges">[S-7032] The first badge.</param>
/// <param name="Compact">[S-7032] The compact row density of a short tile.</param>
/// <param name="ColumnHeader">[S-7034] The table's column titles over its rows.</param>
/// <param name="InlineActions">[S-7033] The spec's inline actions on the hovered and the focused row.</param>
/// <param name="Table">[S-7034] The table's columns at their widths, in the user's order; null for any other look.</param>
/// <param name="Narrow">[S-7036] A narrow column: the time folds into the detail line.</param>
/// <param name="Details">[S-7036] Where and who in columns of their own.</param>
/// <param name="RowHeight">[S-7035] A paged look's one row height (one title line).</param>
/// <param name="HeadingHeight">[S-7035] A group heading's height.</param>
public readonly record struct KitPlan(
    KitRowLook Look,
    int Columns,
    double ColumnWidth,
    bool Lead,
    bool Subtitle,
    int TitleLines,
    int PreviewLines,
    bool When,
    bool Badges = false,
    bool Compact = false,
    bool ColumnHeader = false,
    bool InlineActions = false,
    IReadOnlyList<KitColumnFit>? Table = null,
    bool Narrow = false,
    bool Details = false,
    double RowHeight = 0,
    double HeadingHeight = 0)
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
    public static KitPlan Plan(WidgetArea area, KitSpec spec, KitShow shows) => Plan(area, spec, new KitChoices(shows));

    /// <param name="choices">What the user chose: the optional fields (bounded by the spec's <see cref="KitSpec.Optional"/>), preview lines, a table's columns.</param>
    public static KitPlan Plan(WidgetArea area, KitSpec spec, KitChoices choices)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(choices);

        if (spec.Look == KitRowLook.Schedule)
        {
            return Scheduled(area, spec);
        }

        if (spec.Look != KitRowLook.Rich)
        {
            return Lined(area, spec, choices);
        }

        var shows = choices.Shows;

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

    // ---- the lined looks ([S-7032]..[S-7034]) ---------------------------------------------------

    /// <summary>A lined row's side inset and focus ring, each side: the room its text never has (Fluent's Space4 and the ring).</summary>
    public const double LinedInset = 16 + 1.5;

    /// <summary>[S-7032] A lined row's lead glyph.</summary>
    public const double LeadSize = 14;

    /// <summary>[S-7032] The lead in front of a lined row's text and the gap after it (Fluent's Space2).</summary>
    public const double LeadLane = LeadSize + 8;

    /// <summary>Fluent's compact list row.</summary>
    public const double CompactRow = 32;

    /// <summary>Under this height a tile's lined rows go compact (Fluent's five standard rows).</summary>
    public const double CompactBelow = 40 * 5;

    /// <summary>[S-7032] From this width the lead costs the title nothing it needs.</summary>
    public const double LeadFromWidth = 300;

    /// <summary>[S-7032] Below this width the badge has no room beside the subtitle.</summary>
    public const double BadgeNeedsWidth = 260;

    /// <summary>[S-7032] Below this width a time beside the title leaves it a handful of letters.</summary>
    public const double WhenNeedsWidth = 110;

    /// <summary>[S-7032] Below this height one more line per item costs a whole item.</summary>
    public const double PreviewNeedsHeight = 120;

    /// <summary>[S-7032] Below this width there is no preview on a stacked row.</summary>
    public const double PreviewNeedsWidth = 180;

    /// <summary>[S-7032] From this width a stacked row is one line whatever the height.</summary>
    public const double LineFromWidth = 640;

    /// <summary>[S-7032] A strip at least this wide and shorter than <see cref="LineStripBelowHeight"/> goes one line per item.</summary>
    public const double LineStripWidth = 420;

    /// <summary>[S-7032] Under this height a wide tile is a strip: stacked rows would show two items.</summary>
    public const double LineStripBelowHeight = 220;

    /// <summary>[S-7032] On one line the preview needs this much width beside the subtitle.</summary>
    public const double LinePreviewNeedsWidth = 440;

    /// <summary>[S-7032] On one line the badge column needs this much width.</summary>
    public const double LineBadgeNeedsWidth = 560;

    /// <summary>[S-7033] The narrowest a line row's time is drawn, so "now" and "12:45" line up.</summary>
    public const double LineTimeWidth = 36;

    /// <summary>[S-7034] Below this height the table's column titles cost a row.</summary>
    public const double ColumnHeaderNeedsHeight = 140;

    /// <summary>[S-7034] From this width the table adds the preview column when the subtitle would hog the line.</summary>
    public const double RoomyPreviewFromWidth = 700;

    /// <summary>[S-7034] The share of the line a lone subtitle column may take before the preview joins it.</summary>
    public const double RoomySubtitleShare = 0.4;

    /// <summary>[S-7034] The gap between table columns (Fluent's Space2).</summary>
    public const double ColumnGap = 8;

    /// <summary>
    /// [S-7032]..[S-7034] The lined looks' ladder (Mail's, [S-556], [S-1280]..[S-1287]): a row never
    /// clips or overlaps; the badge goes first, then the preview, then extra preview lines; a stacked
    /// row goes onto one line on a wide strip or a very wide tile, where the inline actions show on a
    /// Huge-wide one; a table sheds whole columns, the least essential first, the first column the
    /// user chose always staying, and shows its column titles where they cost no row.
    /// </summary>
    private static KitPlan Lined(WidgetArea area, KitSpec spec, KitChoices choices)
    {
        var on = choices.Shows & spec.Optional;
        bool Has(KitShow field) => (on & field) == field;

        var width = area.Width;
        var height = area.Height;
        var compact = height > 0 && height < CompactBelow;
        var lead = Has(KitShow.Lead) && width >= LeadFromWidth;

        if (spec.Look == KitRowLook.Table)
        {
            var columns = TableColumns(choices.Columns is { Count: > 0 } chosen ? chosen : [KitColumn.Title], InnerWidth(width, lead), ColumnGap);
            var room = new Disclosure(area);
            room.Reserve(CompactRow);
            var header = height >= ColumnHeaderNeedsHeight && room.Take(FluentType.Caption.LineHeight + 4);
            return new KitPlan(KitRowLook.Table, 1, width, lead, false, 1, 0, false, Compact: compact, ColumnHeader: header, Table: columns);
        }

        if (spec.Look == KitRowLook.Line || width >= LineFromWidth || (width >= LineStripWidth && height < LineStripBelowHeight))
        {
            return new KitPlan(
                KitRowLook.Line,
                1,
                width,
                lead,
                Has(KitShow.Subtitle),
                1,
                Has(KitShow.Preview) && width >= LinePreviewNeedsWidth ? 1 : 0,
                Has(KitShow.When),
                Has(KitShow.Badges) && width >= LineBadgeNeedsWidth,
                compact,
                InlineActions: area.WidthClass >= SizeClass.Huge);
        }

        var preview = Has(KitShow.Preview) && height >= PreviewNeedsHeight && width >= PreviewNeedsWidth;
        var lines = preview ? (height < 2 * PreviewNeedsHeight ? 1 : Math.Clamp(choices.PreviewLines, 1, 5)) : 0;

        return new KitPlan(
            KitRowLook.TwoLine,
            1,
            width,
            lead,
            Has(KitShow.Subtitle),
            1,
            lines,
            Has(KitShow.When) && width >= WhenNeedsWidth,
            Has(KitShow.Badges) && width >= BadgeNeedsWidth,
            compact);
    }

    /// <summary>A lined row's width inside its inset and ring and past its lead: what its text has to share.</summary>
    public static double InnerWidth(double width, bool lead)
        => Math.Max(0, width - (2 * LinedInset) - (lead ? LeadLane : 0));

    /// <summary>[S-7033] How wide a row of <paramref name="count"/> inline-action buttons is.</summary>
    public static double InlineActionsWidth(int count, double button, double gap)
        => count <= 0 ? 0 : (count * button) + ((count - 1) * gap);

    /// <summary>
    /// [S-7033] A line row's boxes at <paramref name="inner"/> (its inner width). The trailing slot is
    /// as wide as the wider of the time and the inline actions whether or not they show, so revealing
    /// them under the pointer or the keyboard swaps the slot's content and moves nothing else: the
    /// title, the subtitle line and the badge keep their boxes, the subtitle truncates in one place.
    /// </summary>
    /// <param name="badge">The badge column's width; 0 when it is not shown.</param>
    /// <param name="actions">The inline actions' width (<see cref="InlineActionsWidth"/>); 0 when the row has none.</param>
    public static KitLineBoxes LineBoxes(double inner, double gap, double badge, double actions)
    {
        var title = Math.Clamp(inner * 0.24, 96, 200);
        var slot = Math.Max(LineTimeWidth, actions);
        var gaps = (badge > 0 ? 3 : 2) * gap;
        var line = Math.Max(0, inner - title - badge - slot - gaps);
        return new KitLineBoxes(title, line, badge, slot);
    }

    /// <summary>[S-7034] The narrowest each column can be and still say anything.</summary>
    public static double MinWidth(KitColumn column) => column switch
    {
        KitColumn.When => 56,
        KitColumn.Badge => 84,
        KitColumn.Title => 80,
        _ => 96,
    };

    /// <summary>The order columns are given up in when the tile is too narrow for all of them.</summary>
    private static int Expendability(KitColumn column) => column switch
    {
        KitColumn.Preview => 0,
        KitColumn.Badge => 1,
        KitColumn.When => 2,
        KitColumn.Subtitle => 3,
        _ => 4,
    };

    /// <summary>
    /// [S-7034] A table's columns at <paramref name="width"/> (the row's inner width): the title, the
    /// time and the badge have fixed widths, the subtitle and the preview share what is left. The most
    /// essential columns are kept first as long as their minimums fit, in the user's order, so a narrow
    /// tile keeps the title and the time rather than the title alone; the most essential always stays.
    /// A wide row with the subtitle as its only flexible column gives the rest of the line to the
    /// preview, after the subtitle.
    /// </summary>
    public static IReadOnlyList<KitColumnFit> TableColumns(IReadOnlyList<KitColumn> chosen, double width, double gap)
    {
        ArgumentNullException.ThrowIfNull(chosen);

        var distinct = chosen.Distinct().ToList();
        var keep = new HashSet<KitColumn>();
        var used = 0.0;

        foreach (var column in distinct.OrderByDescending(Expendability))
        {
            var cost = MinWidth(column) + (keep.Count > 0 ? gap : 0);
            if (keep.Count == 0 || used + cost <= width)
            {
                keep.Add(column);
                used += cost;
            }
        }

        var kept = distinct.Where(keep.Contains).ToList();

        if (width >= RoomyPreviewFromWidth && !kept.Contains(KitColumn.Preview) && kept.Contains(KitColumn.Subtitle))
        {
            var fixedWidth = kept.Where(c => c != KitColumn.Subtitle).Sum(c => c == KitColumn.Title ? Math.Clamp(width * 0.28, MinWidth(c), 200) : MinWidth(c));
            var share = (width - fixedWidth - ((kept.Count - 1) * gap)) / width;

            if (share > RoomySubtitleShare && share * width >= MinWidth(KitColumn.Subtitle) + MinWidth(KitColumn.Preview) + gap)
            {
                kept.Insert(kept.IndexOf(KitColumn.Subtitle) + 1, KitColumn.Preview);
            }
        }

        var hasStar = kept.Any(c => c is KitColumn.Subtitle or KitColumn.Preview);

        return
        [
            .. kept.Select(c => c switch
            {
                KitColumn.Subtitle or KitColumn.Preview => new KitColumnFit(c, 0, 1),

                // With no flexible column the first one takes the slack, so the row still fills.
                _ when !hasStar && c == kept[0] => new KitColumnFit(c, 0, 1),
                KitColumn.Title => new KitColumnFit(c, Math.Clamp(width * 0.28, MinWidth(c), 200), 0),
                _ => new KitColumnFit(c, MinWidth(c), 0),
            }),
        ];
    }

    // ---- the schedule look ([S-7035], [S-7036]) -------------------------------------------------

    /// <summary>[S-1267] From this column width a schedule row puts where and who in columns of their own.</summary>
    public const double DetailsFrom = 480;

    /// <summary>[S-1267] From this width a schedule flows its days into two columns.</summary>
    public const double TwoScheduleColumnsFrom = 720;

    /// <summary>[S-574] Below this column width the time column folds into the line under the title.</summary>
    public const double ScheduleNarrowBelow = 260;

    /// <summary>[S-571] The time column at its narrowest: a clock and the English "All day".</summary>
    public const double TimeColumnMin = 52;

    /// <summary>The space between a schedule's two columns (Fluent's Space2).</summary>
    public const double ScheduleGutter = 8;

    /// <summary>The list's padding under its last row (Fluent's ListPadding).</summary>
    public const double ListFoot = 8;

    /// <summary>A schedule row's padding above and below, and a heading's (Fluent's Space1 and Space2).</summary>
    private const double PadSmall = 4;

    private const double PadLarge = 8;

    /// <summary>
    /// [S-7036] The schedule's plan: two columns from 720px, a narrow column under 260px (the time in
    /// the detail line), where and who in columns from 480px of column width on a tile that is not
    /// compact; rows of one height (two lines where a second is drawn: always but on a compact wide
    /// tile), headings of one height.
    /// </summary>
    private static KitPlan Scheduled(WidgetArea area, KitSpec spec)
    {
        var width = area.Width;
        var height = area.Height;
        var columns = width >= TwoScheduleColumnsFrom ? 2 : 1;
        var column = columns == 1 ? width : (width - ScheduleGutter) / 2;
        var compact = height > 0 && height < CompactBelow;
        var narrow = column > 0 && column < ScheduleNarrowBelow;
        var details = column >= DetailsFrom && !compact;
        var twoLines = narrow || !compact;
        var row = twoLines
            ? FluentType.BodyStrong.LineHeight + FluentType.Caption.LineHeight + (2 * PadSmall)
            : Math.Max(CompactRow, FluentType.BodyStrong.LineHeight + (2 * PadSmall));

        return new KitPlan(
            spec.Look,
            columns,
            column,
            false,
            twoLines,
            1,
            0,
            true,
            Compact: compact,
            Narrow: narrow,
            Details: details,
            RowHeight: row,
            HeadingHeight: FluentType.BodyStrong.LineHeight + PadLarge + PadSmall);
    }

    /// <summary>
    /// [S-571] The time column for a widest label of <paramref name="widestLabel"/> DIPs: wide enough
    /// for it on the 4px grid, so a long word for "all day" is not cut, but never more than 35% of the
    /// row, where the title keeps the rest and an even longer label trims.
    /// </summary>
    public static double TimeColumn(double widestLabel, double columnWidth)
        => Math.Clamp(Math.Ceiling(widestLabel / 4) * 4, TimeColumnMin, Math.Max(TimeColumnMin, Math.Floor(columnWidth * 0.35)));
}
