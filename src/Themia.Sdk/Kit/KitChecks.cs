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
    /// <summary>The space between two columns of rows: none, each row carries its side inset (two hover fills meet, as the rich look's).</summary>
    public const double ColumnGap = 0;

    /// <summary>[S-786] A press only becomes a drag past this distance, so a click is never a tiny move.</summary>
    public const double DragThreshold = 5;

    /// <summary>
    /// [S-780] [S-2440] The check look's plan on the family's rows ([S-6711], [S-6712]): the list states
    /// its rows, so it shows the same at every size: its lines (the subtitle, a list's name, on the
    /// second where it has two), one column, the add row where it can add, the due in its short words,
    /// the done group under its heading. No tier decides it ([S-11580]).
    /// </summary>
    public static KitPlan Plan(WidgetArea area, KitSpec spec, KitChoices choices)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(choices);

        var on = choices.Shows & spec.Optional;
        bool Has(KitShow field) => (on & field) == field;

        var sized = choices.Rows;
        var rowLines = choices.OneLine ? 1 : KitSizing.ClampLines(sized.Lines);
        // [S-12807] The add row is the list's last. [S-14811] A check list's card is its rows and the add row's place
        // (KitSizing.Parts), each at its one size, the add row right under the rows; where the list cannot add (a read-only
        // list) the rows take the add row's room too.
        var reach = !area.IsMeasured ? ItemSize.Natural(rowLines, area.Grid()) with { Foot = choices.CanAdd ? ItemSize.Optimal(FamilyShape.Row1, area.Grid()) : 0 }
            : choices.CanAdd ? ItemSize.RowsAbove(Math.Max(0, area.Height), rowLines, area.Grid())
            : ItemSize.Rows(Math.Max(0, area.Height), rowLines, area.Grid());
        return new KitPlan(spec.Look, 1, Math.Max(0, area.Width), rowLines, true, rowLines >= 2 && Has(KitShow.Subtitle), false, Has(KitShow.When), HeadingHeight: reach.Pitch)
        {
            AddRow = choices.CanAdd,
            Headings = true,
        }.With(reach);
    }

    /// <summary>[S-782] A column's width when the rows stand in <paramref name="columns"/> columns.</summary>
    public static double ColumnWidth(double width, int columns)
        => columns <= 1 ? Math.Max(0, width) : Math.Max(0, (width - ((columns - 1) * ColumnGap)) / columns);

    /// <summary>[S-782] Rows per column when <paramref name="count"/> rows are laid out column by column.</summary>
    public static int LinesFor(int count, int columns)
        => count <= 0 ? 0 : (int)Math.Ceiling(count / (double)Math.Max(1, columns));

    /// <summary>
    /// [S-781] [S-14810] The viewport's height: the room, the last line in it cut by its end (owner, 2026-11-20: "they
    /// should just keep their size and the last item will be cutoff to some extent then"), unless the line the room ends in
    /// is a heading, which would announce rows that are not there ([S-639]): then the list ends over that heading.
    /// </summary>
    /// <param name="headings">Per line, whether it holds only a heading; null when there are none.</param>
    public static double ViewEnd(IReadOnlyList<double> lineBottoms, double room, IReadOnlyList<bool>? headings = null)
    {
        ArgumentNullException.ThrowIfNull(lineBottoms);
        var top = 0.0;
        var last = -1;
        var lastTop = 0.0;
        for (var i = 0; i < lineBottoms.Count && top < room - LayoutMath.Tolerance; i++)
        {
            last = i;
            lastTop = top;
            top = lineBottoms[i];
        }

        return last >= 0 && headings is not null && last < headings.Count && headings[last] ? lastTop : room;
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
