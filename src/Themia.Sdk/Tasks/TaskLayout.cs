using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.Core.Tasks;

/// <summary>[S-780] How much of a due date a row shows.</summary>
public enum DueDetail
{
    /// <summary>"Show due date" is off.</summary>
    None,

    /// <summary>Only the dates that need doing something about now: overdue and today.</summary>
    Urgent,

    /// <summary>The short label: Overdue / Today / Tomorrow / Thu / 14 Oct.</summary>
    Short,

    /// <summary>The label with its date: "Overdue · 2 Oct", "Thu 8 Oct".</summary>
    Long,
}

/// <summary>What the list is asked to hold, independent of the tile's size.</summary>
/// <param name="Count">Rows to draw.</param>
/// <param name="Writable">Whether a task can be added to this list (the add row has a purpose).</param>
/// <param name="IsToday">The Today view, whose rows come from several lists.</param>
public readonly record struct TaskLayoutInput(int Count, bool Writable, bool IsToday, bool ShowDueDate, TasksStyle Mode, int TitleLines);

/// <summary>
/// [S-780] One layout pass's decisions for the Tasks widget: everything that depends on the size of
/// the tile, decided together so the rows and the add row never disagree.
/// <see cref="NarrowInset"/>: on a Compact-wide tile the rows' side inset halves, so a title has
/// more than a few characters.
/// </summary>
public sealed record TaskLayoutPlan(
    bool CompactRows,
    double RowHeight,
    int TitleLines,
    DueDetail Due,
    bool DueBeside,
    bool PriorityGlyph,
    bool ListNames,
    bool AddRow,
    int Columns,
    bool CompletedHeading,
    bool NarrowInset);

/// <summary>A row's box in the list, for hit-testing a drag. Plain numbers so the rule is testable.</summary>
public readonly record struct SlotBox(double X, double Y, double Width, double Height)
{
    public bool Contains(double x, double y) => x >= X && x < X + Width && y >= Y && y < Y + Height;
}

/// <summary>
/// [S-780]..[S-788] The Tasks widget's size ladder, scroll fade, drag targeting and editor Tab (its keys are the kit's, [S-608]). Pure, so
/// each tier and each edge is pinned by a test rather than by looking at a render.
/// </summary>
public static class TaskLayout
{
    /// <summary>Under this column width the due date goes under the title: a column beside it would starve it.</summary>
    public const double NarrowBelow = 240;

    /// <summary>Under this column width the "!" importance glyph is dropped; the weight and the tooltip still say it.</summary>
    public const double PriorityNeedsWidth = 160;

    /// <summary>The space between two columns of rows, so two hover fills never read as one row.</summary>
    public const double ColumnGap = 8;

    /// <summary>The list's own padding under its last row.</summary>
    public const double ListBottomPadding = 8;

    /// <summary>How far up from the viewport's bottom edge the scroll fade reaches.</summary>
    public const double FadeDepth = 48;

    /// <summary>The faintest a row gets under the fade: still legible, plainly "more below".</summary>
    public const double FadeFloor = 0.35;

    /// <summary>
    /// [S-780] The ladder. Rung 0 is one task (what is next); then the add footer
    /// ([S-3850]: from Regular height, always under an empty list); the list takes the rest (no summary strip: a larger tile shows more tasks, not counts of them), in two
    /// columns on a Huge-wide tile when one column would not hold it. Width decides how much of a
    /// due date is said and where; height decides row density and title lines.
    /// </summary>
    /// <param name="rowHeight">The standard row height.</param>
    /// <param name="compactRowHeight">The compact row height.</param>
    public static TaskLayoutPlan Plan(WidgetArea area, TaskLayoutInput input, double rowHeight, double compactRowHeight)
    {
        var compact = area.HeightClass < SizeClass.Expanded;
        var row = compact ? compactRowHeight : rowHeight;
        var lines = TaskRules.ClampTitleLines(input.TitleLines);
        var titleLines = area.HeightClass == SizeClass.Compact ? 1 : compact ? Math.Min(2, lines) : lines;

        var room = new Disclosure(area);
        room.Reserve(row);
        var addRow = AddFooter.Show(room, area, input.Writable, input.Count == 0, row);

        var listHeight = area.Height - (addRow ? row : 0) - ListBottomPadding;
        var columns = Columns(area, listHeight, row, input.Count);
        var columnWidth = ColumnWidth(area.Width, columns);

        // On the smallest tile a due line would cost the second task; the marker's colour still
        // says overdue there.
        var due = !input.ShowDueDate || (area.WidthClass == SizeClass.Compact && area.HeightClass == SizeClass.Compact) ? DueDetail.None
            : area.WidthClass == SizeClass.Compact ? DueDetail.Urgent
            : area.AtLeast(SizeClass.Expanded, SizeClass.Expanded) ? DueDetail.Long
            : DueDetail.Short;

        var beside = columnWidth >= NarrowBelow && (input.Mode == TasksStyle.Compact || area.WidthClass >= SizeClass.Expanded);

        return new TaskLayoutPlan(
            CompactRows: compact,
            RowHeight: row,
            TitleLines: titleLines,
            Due: due,
            DueBeside: beside,
            PriorityGlyph: columnWidth >= PriorityNeedsWidth,
            ListNames: input.IsToday && area.AtLeast(SizeClass.Expanded, SizeClass.Expanded),
            AddRow: addRow,
            Columns: columns,
            CompletedHeading: area.HeightClass >= SizeClass.Expanded,
            NarrowInset: area.WidthClass == SizeClass.Compact);
    }

    /// <summary>
    /// [S-782] Two columns only on a Huge-wide tile, and only when one column would not hold every
    /// row: a short list in two columns is a list with a hole under it.
    /// </summary>
    public static int Columns(WidgetArea area, double listHeight, double rowHeight, int count)
        => area.WidthClass == SizeClass.Huge && count > Disclosure.WholeRows(listHeight, rowHeight) ? 2 : 1;

    public static double ColumnWidth(double width, int columns)
        => columns <= 1 ? Math.Max(0, width) : Math.Max(0, (width - ((columns - 1) * ColumnGap)) / columns);

    /// <summary>[S-782] Rows per column when <paramref name="count"/> rows are laid out column by column.</summary>
    public static int LinesFor(int count, int columns)
        => count <= 0 ? 0 : (int)Math.Ceiling(count / (double)Math.Max(1, columns));

    /// <summary>
    /// [S-781] How opaque a line of rows is under the bottom fade. Only while there is more below;
    /// the fade deepens toward the viewport's bottom edge, measured at the line's middle.
    /// </summary>
    public static double FadeOpacity(double lineTop, double lineBottom, double viewBottom, bool moreBelow)
    {
        if (!moreBelow)
        {
            return 1;
        }

        var distance = viewBottom - ((lineTop + lineBottom) / 2);
        if (distance >= FadeDepth)
        {
            return 1;
        }

        return FadeFloor + ((1 - FadeFloor) * Math.Max(0, distance) / FadeDepth);
    }

    /// <summary>
    /// [S-781] The viewport height that ends on a row boundary: the bottom of the last line that fits
    /// whole in <paramref name="room"/> — never a section heading, which would announce rows that
    /// are not there ([S-639]) — or the room itself when not even one does.
    /// </summary>
    /// <param name="headings">Per line, whether it holds only a heading; null when there are none.</param>
    public static double WholeLineCap(IReadOnlyList<double> lineBottoms, double room, IReadOnlyList<bool>? headings = null)
    {
        ArgumentNullException.ThrowIfNull(lineBottoms);
        var cap = 0.0;

        for (var i = 0; i < lineBottoms.Count; i++)
        {
            var bottom = lineBottoms[i];
            if (bottom > room + Disclosure.Tolerance)
            {
                break;
            }

            if (headings is null || i >= headings.Count || !headings[i])
            {
                cap = bottom;
            }
        }

        return cap > 0 ? cap : room;
    }

    /// <summary>
    /// [S-786] Where a dragged row goes: into another slot once the pointer is over the middle half of
    /// it. The middle-half rule is the hysteresis — after the swap the pointer sits in the dragged
    /// row's own new slot, so rows of different heights never trade places back and forth.
    /// </summary>
    /// <param name="slots">Every slot as last laid out, in list order.</param>
    /// <param name="current">The slot the dragged row is in now.</param>
    /// <param name="accepts">Whether the dragged row may go to a slot (same section, a task row).</param>
    public static int Retarget(IReadOnlyList<SlotBox> slots, int current, double x, double y, Func<int, bool>? accepts = null)
    {
        ArgumentNullException.ThrowIfNull(slots);

        for (var i = 0; i < slots.Count; i++)
        {
            if (i == current || (accepts is not null && !accepts(i)))
            {
                continue;
            }

            var slot = slots[i];
            var quarter = slot.Height / 4;

            if (x >= slot.X && x < slot.X + slot.Width && y >= slot.Y + quarter && y <= slot.Y + slot.Height - quarter)
            {
                return i;
            }
        }

        return current;
    }

    /// <summary>[S-786] A press only becomes a drag past this distance, so a click is never a tiny move.</summary>
    public const double DragThreshold = 5;

    public static bool IsDrag(double dx, double dy) => (dx * dx) + (dy * dy) >= DragThreshold * DragThreshold;

    /// <summary>
    /// [S-788] Tab in a rename box: commit, and open the next task's box — none past the last row
    /// (no wrap). Returns the id to open next, or null to close.
    /// </summary>
    public static string? NextEditor(IReadOnlyList<string> ids, string editing, bool backwards)
    {
        ArgumentNullException.ThrowIfNull(ids);

        if (backwards)
        {
            return null;
        }

        for (var i = 0; i < ids.Count - 1; i++)
        {
            if (ids[i] == editing)
            {
                return ids[i + 1];
            }
        }

        return null;
    }
}
