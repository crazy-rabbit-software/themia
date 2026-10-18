using Themia.Core.Widgets;

namespace Themia.Sdk.Kit;

/// <summary>
/// [S-7037] The day an item of the check look is due, written by the source's rules: the short words
/// ("Overdue", "Today", "Thu"), the long ones ("Overdue · 2 Oct", "Thu 8 Oct"), and whether it needs
/// doing now (overdue or today), which is all a narrow tile says. A finished item that was due in the
/// past says its date, and nothing on the urgent rung.
/// </summary>
public sealed record KitDue(string Short, string Long, bool Urgent);

/// <summary>
/// [S-7037] [S-7038] The check look's rules ([S-780]..[S-788]): its ladder, the slots a drag moves
/// between, the whole lines a list shows, and the editor's Tab. Pure, so each tier and each edge is
/// pinned by a test rather than by looking at a render.
/// </summary>
public static class KitChecks
{
    /// <summary>Under this column width the time goes under the title: a column beside it would starve it.</summary>
    public const double BesideNeedsWidth = 240;

    /// <summary>Under this column width the emphasis mark ("!") is dropped; the weight and the tooltip still say it.</summary>
    public const double MarkNeedsWidth = 160;

    /// <summary>The space between two columns of rows, so two hover fills never read as one row.</summary>
    public const double ColumnGap = 8;

    /// <summary>The list's own padding under its last row.</summary>
    public const double ListBottomPadding = 8;

    /// <summary>Fluent's standard and compact list rows.</summary>
    public const double Row = 40;

    public const double CompactRow = 32;

    /// <summary>[S-786] A press only becomes a drag past this distance, so a click is never a tiny move.</summary>
    public const double DragThreshold = 5;

    /// <summary>
    /// [S-7037] The check look's ladder ([S-780]): rung 0 is one item (what is next); then the add
    /// footer ([S-3850]: from Regular height, always under an empty list); the list takes the rest, in
    /// two columns on a Huge-wide tile when one would not hold it. Width decides how much of the time
    /// is said and where; height decides the row density and the title's lines.
    /// </summary>
    public static KitPlan Plan(WidgetArea area, KitSpec spec, KitChoices choices)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(choices);

        var on = choices.Shows & spec.Optional;
        bool Has(KitShow field) => (on & field) == field;

        var compact = area.HeightClass < SizeClass.Expanded;
        var row = compact ? CompactRow : Row;
        var lines = Math.Clamp(choices.TitleLines, 1, 6);
        var titleLines = area.HeightClass == SizeClass.Compact ? 1 : compact ? Math.Min(2, lines) : lines;

        var room = new Disclosure(area);
        room.Reserve(row);
        var addRow = AddFooter.Show(room, area, choices.CanAdd, choices.Count == 0, row);

        var listHeight = area.Height - (addRow ? row : 0) - ListBottomPadding;
        var columns = Columns(area, listHeight, row, choices.Count);
        var columnWidth = ColumnWidth(area.Width, columns);

        // On the smallest tile a time line would cost the second item; the lead's tone still says it is late.
        var when = Has(KitShow.When) && !(area.WidthClass == SizeClass.Compact && area.HeightClass == SizeClass.Compact);
        var urgent = when && area.WidthClass == SizeClass.Compact;
        var wide = when && !urgent && area.AtLeast(SizeClass.Expanded, SizeClass.Expanded);
        var beside = columnWidth >= BesideNeedsWidth && (choices.WhenBeside || area.WidthClass >= SizeClass.Expanded);

        return new KitPlan(
            spec.Look,
            columns,
            columnWidth,
            true,
            Has(KitShow.Subtitle) && area.AtLeast(SizeClass.Expanded, SizeClass.Expanded),
            titleLines,
            0,
            when,
            Compact: compact,
            Narrow: area.WidthClass == SizeClass.Compact,
            RowHeight: row)
        {
            AddRow = addRow,
            Headings = area.HeightClass >= SizeClass.Expanded,
            Mark = columnWidth >= MarkNeedsWidth,
            UrgentOnly = urgent,
            LongWhen = wide,
            WhenBeside = beside,
        };
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
    /// [S-781] The viewport height that ends on a row boundary: the bottom of the last line that fits
    /// whole in <paramref name="room"/> — never a heading line, which would announce rows that are not
    /// there ([S-639]) — or the room itself when not even one does.
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
    /// <param name="accepts">Whether the dragged row may go to a slot (the same group, an item's row).</param>
    public static int Retarget(IReadOnlyList<KitSlot> slots, int current, double x, double y, Func<int, bool>? accepts = null)
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

    public static bool IsDrag(double dx, double dy) => (dx * dx) + (dy * dy) >= DragThreshold * DragThreshold;

    /// <summary>
    /// [S-788] Tab in a rename box: commit, and open the next item's box — none past the last row (no
    /// wrap). Returns the id to open next, or null to close.
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

/// <summary>A row's box in the list, for hit-testing a drag. Plain numbers so the rule is testable.</summary>
public readonly record struct KitSlot(double X, double Y, double Width, double Height)
{
    public bool Contains(double x, double y) => x >= X && x < X + Width && y >= Y && y < Y + Height;
}
