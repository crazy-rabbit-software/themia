using Themia.Sdk.Kit;
using M = Themia.Sdk.Kit.ShellMetrics.Bar;

namespace Themia.Core.Shell;

/// <summary>[S-2320] A part of the edit bar.</summary>
public enum BarPartKind
{
    /// <summary>A page's entry (<see cref="BarPart.Index"/> its place among the pages).</summary>
    Page,

    /// <summary>The hairline before the selection's room: drawn only while something is selected.</summary>
    Divider,

    /// <summary>The selection's entry.</summary>
    Selection,

    /// <summary>The errors' icon button: only while there are problems.</summary>
    Errors,

    Add,

    Done,
}

/// <summary>[S-2320] Where a part of the bar stands, in DIPs from the bar's left (its top is the bar's pad); <see cref="IconOnly"/> without its words.</summary>
public readonly record struct BarPart(BarPartKind Kind, int Index, double X, double Width, bool IconOnly)
{
    public double Right => X + Width;

    public double Middle => X + (Width / 2);
}

/// <summary>[S-2320] The bar laid out: its parts, its one width and the selection's room.</summary>
public sealed record EditBarPlan(IReadOnlyList<BarPart> Parts, double Width, double SelectionX, double SelectionRoom, bool Compact)
{
    /// <summary>The part of <paramref name="kind"/> (a page's by its index); null where it is not drawn.</summary>
    public BarPart? Of(BarPartKind kind, int index = 0)
    {
        foreach (var part in Parts)
        {
            if (part.Kind == kind && part.Index == index)
            {
                return part;
            }
        }

        return null;
    }
}

/// <summary>
/// [S-2320]..[S-2324] The edit bar's layout, Karpo's command bar (karpo-app, docs/widgets.md §575, §578,
/// §596, §608; <c>PanelLayout.Lay</c>): the pages' entries 4 apart; 8, the divider, 8; the selection's
/// room; a 16 gutter; the errors' room; 4; Add; 8; Done, 8 from the bar's end. One width whatever is
/// selected and whether there are errors: the divider's, the selection's and the errors' rooms are kept
/// empty. On a display too narrow for it the pages' entries show their icon alone first, then the
/// selection's room narrows. Pure: the bar measures its words and asks.
/// </summary>
public static class EditBarLayout
{
    /// <summary>An entry of an icon and <paramref name="words"/> DIPs of words: 12, the icon, 8, the words, 12.</summary>
    public static double EntryWidth(double words) => M.Lead + M.Icon + M.IconGap + Math.Ceiling(words) + M.Lead;

    /// <summary>[S-2324] An entry cut short keeps at least this: 12, the icon, 8, a name's least, 12.</summary>
    public static double CutLeast => EntryWidth(M.NameLeast);

    /// <summary>
    /// [S-2320] The bar: <paramref name="pages"/> the widths of the pages' words, <paramref name="selection"/>
    /// the selection entry's (null: nothing selected), whether there are <paramref name="errors"/>, Add's and
    /// Done's words, and the widest the bar may be (the work area less its margins; infinity for no limit).
    /// </summary>
    public static EditBarPlan Lay(IReadOnlyList<double> pages, double? selection, bool errors, double add, double done, double widest)
    {
        ArgumentNullException.ThrowIfNull(pages);

        var plan = Lay(pages, selection, errors, add, done, M.Selection, compact: false);
        if (plan.Width <= widest)
        {
            return plan;
        }

        // [S-2322] Too wide: the pages' entries their icon alone (the words in the tooltip), then the selection's room narrower.
        plan = Lay(pages, selection, errors, add, done, M.Selection, compact: true);
        var over = plan.Width - widest;
        return over > 0
            ? Lay(pages, selection, errors, add, done, Math.Max(M.IconOnly, M.Selection - over), compact: true)
            : plan;
    }

    /// <summary>
    /// [S-2324] The selection's entry in a room <paramref name="room"/> wide: its natural width, cut short
    /// where it does not fit but never shorter than <see cref="CutLeast"/>, else its icon alone.
    /// </summary>
    public static (double Width, bool IconOnly) Fit(double words, double room)
    {
        var natural = EntryWidth(words);
        return room >= Math.Min(natural, CutLeast)
            ? (Math.Floor(Math.Min(natural, room)), false)
            : (M.IconOnly, true);
    }

    private static EditBarPlan Lay(IReadOnlyList<double> pages, double? selection, bool errors, double add, double done, double room, bool compact)
    {
        var parts = new List<BarPart>();
        var x = M.Pad;
        for (var i = 0; i < pages.Count; i++)
        {
            var wide = compact ? M.IconOnly : EntryWidth(pages[i]);
            parts.Add(new BarPart(BarPartKind.Page, i, x, wide, compact));
            x += wide + M.EntryGap;
        }

        // [S-2323] The divider's room, 8 either side of its hairline, kept whether it shows or not.
        x += M.DividerRoom - (pages.Count > 0 ? M.EntryGap : 0);
        var divider = new BarPart(BarPartKind.Divider, 0, x, FamilyShape.Hairline, false);
        x += FamilyShape.Hairline + M.DividerRoom;

        var selectionX = x;
        var roomWidth = Math.Floor(room);
        if (selection is { } words)
        {
            parts.Add(divider);
            var (wide, icon) = Fit(words, roomWidth);
            parts.Add(new BarPart(BarPartKind.Selection, 0, x, wide, icon));
        }

        x = selectionX + roomWidth + M.Gutter;

        // The errors' room, kept whether there are any or not.
        if (errors)
        {
            parts.Add(new BarPart(BarPartKind.Errors, 0, x, M.IconOnly, true));
        }

        x += M.IconOnly + M.EntryGap;
        // Add and Done are Windows' standard and accent buttons: their rim round the entry's room.
        var addWidth = EntryWidth(add) + (2 * FamilyShape.Hairline);
        parts.Add(new BarPart(BarPartKind.Add, 0, x, addWidth, false));
        x += addWidth + M.Pad;
        var doneWidth = Math.Max(M.DoneLeast, EntryWidth(done) + (2 * FamilyShape.Hairline));
        parts.Add(new BarPart(BarPartKind.Done, 0, x, doneWidth, false));
        x += doneWidth + M.Pad;

        return new EditBarPlan(parts, x, selectionX, roomWidth, compact);
    }
}
