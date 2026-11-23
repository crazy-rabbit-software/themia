namespace Themia.Core.Widgets;

/// <summary>
/// [S-16745] What the gallery's editor does to a <see cref="CuratedLayout"/>, pure: each step a new layout (the old one kept
/// for undo). A slot out of range changes nothing.
/// </summary>
public static class CuratedEdits
{
    /// <summary>[S-17163] A column added at the right: <see cref="CuratedBento.NewColumn"/> blocks wide, one untitled section in it.</summary>
    public static CuratedLayout AddColumn(CuratedLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        return layout with { Columns = [.. layout.Columns, new CuratedColumn { Width = CuratedBento.NewColumn, Sections = [new CuratedSection()] }] };
    }

    /// <summary>A column removed, its widgets with it (they are then not placed); the last column stays.</summary>
    public static CuratedLayout RemoveColumn(CuratedLayout layout, int column)
    {
        ArgumentNullException.ThrowIfNull(layout);
        return layout.Columns.Count <= 1 || column < 0 || column >= layout.Columns.Count
            ? layout
            : layout with { Columns = [.. layout.Columns.Where((_, i) => i != column)] };
    }

    /// <summary>
    /// [S-17163] A column made exactly <paramref name="width"/> blocks wide (held to 1..<see cref="CuratedLayouts.MostCells"/>);
    /// the others keep theirs (the columns right of it move, none of them narrows).
    /// </summary>
    public static CuratedLayout Width(CuratedLayout layout, int column, int width)
        => Column(layout, column, c => c with { Width = Math.Clamp(width, 1, CuratedLayouts.MostCells) });

    /// <summary>A section added at a column's foot, titled <paramref name="title"/> (none: no heading).</summary>
    public static CuratedLayout AddSection(CuratedLayout layout, int column, string? title = null)
        => Column(layout, column, c => c with { Sections = [.. c.Sections, new CuratedSection { Title = Titled(title) }] });

    /// <summary>A section's title changed (empty: no heading).</summary>
    public static CuratedLayout Rename(CuratedLayout layout, CuratedSlot section, string? title)
        => Section(layout, section, s => s with { Title = Titled(title) });

    /// <summary>A section removed, its widgets with it.</summary>
    public static CuratedLayout RemoveSection(CuratedLayout layout, CuratedSlot section)
        => Column(layout, section.Column, c => section.Section < 0 || section.Section >= c.Sections.Count ? c : c with { Sections = [.. c.Sections.Where((_, i) => i != section.Section)] });

    /// <summary>A section moved <paramref name="by"/> places up (negative) or down its column, held to its ends.</summary>
    public static CuratedLayout MoveSection(CuratedLayout layout, CuratedSlot section, int by)
        => Column(layout, section.Column, c =>
        {
            if (section.Section < 0 || section.Section >= c.Sections.Count)
            {
                return c;
            }

            var list = c.Sections.ToList();
            var moved = list[section.Section];
            list.RemoveAt(section.Section);
            list.Insert(Math.Clamp(section.Section + by, 0, list.Count), moved);
            return c with { Sections = list };
        });

    /// <summary>A widget put into a section at <paramref name="index"/> (held to its ends).</summary>
    public static CuratedLayout AddWidget(CuratedLayout layout, CuratedSlot section, CuratedWidget widget, int index = int.MaxValue)
    {
        ArgumentNullException.ThrowIfNull(widget);
        return Section(layout, section, s =>
        {
            var list = s.Widgets.ToList();
            list.Insert(Math.Clamp(index, 0, list.Count), widget);
            return s with { Widgets = list };
        });
    }

    /// <summary>A widget taken out of its section.</summary>
    public static CuratedLayout RemoveWidget(CuratedLayout layout, CuratedSlot widget)
        => Section(layout, widget, s => widget.Widget < 0 || widget.Widget >= s.Widgets.Count ? s : s with { Widgets = [.. s.Widgets.Where((_, i) => i != widget.Widget)] });

    /// <summary>A widget's card made <paramref name="w"/> by <paramref name="h"/> cells (at least one each way).</summary>
    public static CuratedLayout ResizeWidget(CuratedLayout layout, CuratedSlot widget, int w, int h)
        => Section(layout, widget, s => widget.Widget < 0 || widget.Widget >= s.Widgets.Count
            ? s
            : s with { Widgets = [.. s.Widgets.Select((x, i) => i == widget.Widget ? x with { W = Math.Clamp(w, 1, CuratedLayouts.MostCells), H = Math.Clamp(h, 1, CuratedLayouts.MostCells) } : x)] });

    /// <summary>
    /// A widget moved to <paramref name="to"/>'s section at its place <paramref name="to"/>.Widget, counted before the move
    /// (in its own section, a place after it lands where it was pointed at).
    /// </summary>
    public static CuratedLayout MoveWidget(CuratedLayout layout, CuratedSlot from, CuratedSlot to)
    {
        ArgumentNullException.ThrowIfNull(layout);
        if (Widget(layout, from) is not { } widget || SectionOf(layout, to) is null)
        {
            return layout;
        }

        var index = to.Widget;
        if (from.Column == to.Column && from.Section == to.Section && index > from.Widget)
        {
            index--;
        }

        return AddWidget(RemoveWidget(layout, from), to with { Widget = -1 }, widget, index);
    }

    /// <summary>The widget at <paramref name="slot"/>, or null.</summary>
    public static CuratedWidget? Widget(CuratedLayout layout, CuratedSlot slot)
        => SectionOf(layout, slot) is { } s && slot.Widget >= 0 && slot.Widget < s.Widgets.Count ? s.Widgets[slot.Widget] : null;

    /// <summary>The section at <paramref name="slot"/>, or null.</summary>
    public static CuratedSection? SectionOf(CuratedLayout layout, CuratedSlot slot)
    {
        ArgumentNullException.ThrowIfNull(layout);
        return slot.Column >= 0 && slot.Column < layout.Columns.Count && slot.Section >= 0 && slot.Section < layout.Columns[slot.Column].Sections.Count
            ? layout.Columns[slot.Column].Sections[slot.Section]
            : null;
    }

    private static string? Titled(string? title) => string.IsNullOrWhiteSpace(title) ? null : title.Trim();

    private static CuratedLayout Column(CuratedLayout layout, int column, Func<CuratedColumn, CuratedColumn> change)
    {
        ArgumentNullException.ThrowIfNull(layout);
        return column < 0 || column >= layout.Columns.Count
            ? layout
            : layout with { Columns = [.. layout.Columns.Select((c, i) => i == column ? change(c) : c)] };
    }

    private static CuratedLayout Section(CuratedLayout layout, CuratedSlot slot, Func<CuratedSection, CuratedSection> change)
        => Column(layout, slot.Column, c => slot.Section < 0 || slot.Section >= c.Sections.Count
            ? c
            : c with { Sections = [.. c.Sections.Select((s, i) => i == slot.Section ? change(s) : s)] });
}
