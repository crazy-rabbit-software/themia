namespace Themia.Core.Widgets;

/// <summary>
/// [S-16745] [S-17221] What the gallery's editor does to a <see cref="CuratedLayout"/>, pure: each step a new layout (the old
/// one kept for undo). A slot out of range, or a move or resize onto another widget, changes nothing.
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

    /// <summary>
    /// [S-17222] A widget put into a section at the first free spot of it (<see cref="CuratedBento.FirstFree"/>: a cell from
    /// every other, joining none), its size as <paramref name="fit"/> makes it in the column, titles <paramref name="title"/> rows.
    /// </summary>
    public static CuratedLayout AddWidget(CuratedLayout layout, CuratedSlot section, CuratedWidget widget, CuratedFit fit, int title)
    {
        ArgumentNullException.ThrowIfNull(widget);
        ArgumentNullException.ThrowIfNull(fit);
        return Column(layout, section.Column, c => section.Section < 0 || section.Section >= c.Sections.Count ? c : c with
        {
            Sections = [.. c.Sections.Select((s, i) =>
            {
                if (i != section.Section)
                {
                    return s;
                }

                var cards = Cards(s.Widgets, c.Width, fit);
                var (w, h) = fit.Size(widget, c.Width);
                var spot = CuratedBento.FirstFree(cards, w, h, c.Width, layout.Titles ? title : 0);
                return s with { Widgets = [.. s.Widgets, widget with { X = spot.X, Y = spot.Y }] };
            })],
        });
    }

    /// <summary>A widget taken out of its section.</summary>
    public static CuratedLayout RemoveWidget(CuratedLayout layout, CuratedSlot widget)
        => Section(layout, widget, s => widget.Widget < 0 || widget.Widget >= s.Widgets.Count ? s : s with { Widgets = [.. s.Widgets.Where((_, i) => i != widget.Widget)] });

    /// <summary>
    /// [S-16745] [S-17222] A widget's card made <paramref name="w"/> by <paramref name="h"/> cells (at least one each way,
    /// no further right than its column), where it stands; unchanged where it would then clash with another widget of its
    /// section (<see cref="CuratedBento.Clashes"/>, sizes as <paramref name="fit"/> makes them, titles <paramref name="title"/> rows).
    /// </summary>
    public static CuratedLayout ResizeWidget(CuratedLayout layout, CuratedSlot widget, int w, int h, CuratedFit fit, int title)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(fit);
        if (widget.Column < 0 || widget.Column >= layout.Columns.Count)
        {
            return layout;
        }

        var width = layout.Columns[widget.Column].Width;
        return Placed(layout, widget, x => x with { W = Math.Clamp(w, 1, Math.Clamp(width - (x.X ?? 0), 1, CuratedLayouts.MostCells)), H = Math.Clamp(h, 1, CuratedLayouts.MostCells) }, widget, fit, title);
    }

    /// <summary>
    /// [S-17221] [S-17222] A widget moved as on the desktop, inside its column alone (owner, 2026-11-24: "in the gallery edit
    /// mode i should be able to rearrange the widgets like i am on the desktop, just in the column that it is placed though"):
    /// its card to (<paramref name="x"/>, <paramref name="y"/>) of <paramref name="to"/>'s section (its own or another of the
    /// same column), held inside the column (its x moved left as far as it takes, never above the section's room); refused,
    /// the layout unchanged, where it would land on another widget (<see cref="CuratedBento.Clashes"/>), in another column or
    /// in no section.
    /// </summary>
    public static CuratedLayout MoveWidget(CuratedLayout layout, CuratedSlot from, CuratedSlot to, int x, int y, CuratedFit fit, int title)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(fit);
        if (Widget(layout, from) is not { } widget || SectionOf(layout, to with { Widget = -1 }) is null || to.Column != from.Column)
        {
            return layout;
        }

        var width = layout.Columns[from.Column].Width;
        var (w, _) = fit.Size(widget, width);
        var moved = widget with { X = Math.Clamp(x, 0, Math.Max(0, width - Math.Clamp(w, 1, width))), Y = Math.Max(0, y) };
        if (to.Section == from.Section)
        {
            return Placed(layout, from, _ => moved, from, fit, title);
        }

        var landed = AddTo(RemoveWidget(layout, from), to.Section, from.Column, moved);
        return Clear(landed, new CuratedSlot(from.Column, to.Section, SectionOf(landed, to with { Widget = -1 })!.Widgets.Count - 1), fit, title) ? landed : layout;

        static CuratedLayout AddTo(CuratedLayout layout, int section, int column, CuratedWidget widget)
            => Section(layout, new CuratedSlot(column, section), s => s with { Widgets = [.. s.Widgets, widget] });
    }

    /// <summary>[S-17222] <paramref name="slot"/>'s widget changed by <paramref name="change"/>, kept only where its section is then clear.</summary>
    private static CuratedLayout Placed(CuratedLayout layout, CuratedSlot slot, Func<CuratedWidget, CuratedWidget> change, CuratedSlot check, CuratedFit fit, int title)
    {
        if (Widget(layout, slot) is null)
        {
            return layout;
        }

        var after = Section(layout, slot, s => s with { Widgets = [.. s.Widgets.Select((x, i) => i == slot.Widget ? change(x) : x)] });
        return Clear(after, check, fit, title) ? after : layout;
    }

    /// <summary>[S-17222] Whether the section of <paramref name="slot"/> stands clear: no card on another, no title over one.</summary>
    private static bool Clear(CuratedLayout layout, CuratedSlot slot, CuratedFit fit, int title)
    {
        var column = layout.Columns[slot.Column];
        return !CuratedBento.Clashes(Cards(column.Sections[slot.Section].Widgets, column.Width, fit), layout.Titles ? title : 0);
    }

    /// <summary>A section's cards from its room, each at its place held inside its column, as the gallery lays them.</summary>
    private static List<CellBox> Cards(IReadOnlyList<CuratedWidget> widgets, int width, CuratedFit fit)
        => [.. CuratedBento.Places(widgets, width, 0, fit, out _, repairClashes: false)];

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
