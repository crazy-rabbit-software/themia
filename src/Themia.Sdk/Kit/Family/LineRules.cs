namespace Themia.Sdk.Kit;

/// <summary>[S-6710] What stands in a tool line's trailing slot (<c>ToolLine</c>, [S-12481]), from its start.</summary>
public enum LinePartKind
{
    /// <summary>The problem glyph (a 16 caution warning): state, never folded.</summary>
    Problem,

    /// <summary>The busy ring (16): state, never folded.</summary>
    Busy,

    /// <summary>An informing label ("by CPU", an age): the first to give way.</summary>
    Label,

    /// <summary>A <see cref="FamilyShape.LineButton"/>: the last one gives way first.</summary>
    Button,

    /// <summary>The count pill (Caption on the subtle fill, never the accent): the last to give way.</summary>
    Count,
}

/// <summary>[S-6710] One trailing part of a tool line and the width it takes.</summary>
public readonly record struct LinePart(LinePartKind Kind, double Width);

/// <summary>
/// [S-6710] [S-12481] A tool line (<c>ToolLine</c>): [label, Body, secondary, ellipsis] … [trailing], one line
/// of 20, the first line of a widget's own content where its kind has tools to offer there (the calendar's
/// month and its arrows, a folder's way back and up). The trailing slot holds, from its start, the problem
/// glyph, the busy ring, an informing label, at most two <see cref="FamilyShape.LineButton"/>s and the count pill.
/// <para>
/// On a narrow card the parts give way before the label is cut: the informing label first, then the buttons
/// (the last one first), then the count; the problem glyph and the ring are state and stay. Only when
/// nothing is left to give way does the label end in an ellipsis. Parts stand 4 apart, buttons side by
/// side; a last button's glyph lines up with the inset (the button reaches 4 past it).
/// </para>
/// </summary>
public static class LineRules
{
    /// <summary>At most this many buttons in a line.</summary>
    public const int MaxButtons = 2;

    /// <summary>The gap between two trailing parts that are not both buttons.</summary>
    public const double PartGap = FamilySpace.S1;

    /// <summary>
    /// [S-12834] A tool line's band under the card's top inset (owner, 2026-11-19, WR-1: "there should not be a space at
    /// the top"; WR-6: "items inside widgets should be designed in a way that they match the size of the grid on the
    /// desktop"): the line of 20 and the gap under it, grown to end on a line of <paramref name="grid"/> counted from the
    /// card's top edge, so the rows under it start right under it on a grid line and the line is whole cells with the
    /// card's inset over it. On the default grid of 10: 28 (12 + 28 = 40, four cells).
    /// </summary>
    public static double Band(Themia.Core.Widgets.ItemGrid grid)
        => grid.Whole(FamilySpace.Inset + FamilySpace.HeaderLine + FamilySpace.HeaderGap) - FamilySpace.Inset;

    /// <summary>
    /// [S-12834] A part <paramref name="natural"/> tall standing <paramref name="top"/> under the card's top edge (a widget's
    /// search field over its rows): grown to end on a line of <paramref name="grid"/>, so what stands under it starts right
    /// under it on a grid line (WR-1, WR-5).
    /// </summary>
    public static double Under(Themia.Core.Widgets.ItemGrid grid, double top, double natural)
        => grid.Whole(Math.Max(0, top) + Math.Max(0, natural)) - Math.Max(0, top);

    /// <summary>[S-12834] <see cref="Band"/> on the default grid: what a kind's sizing counts for its line before its card's grid is known.</summary>
    public static double NaturalBand => Band(Themia.Core.Widgets.ItemGrid.Default());

    /// <summary>
    /// [S-12834] A line standing on the card's top edge (a state's or a set-up's compact row, a list's first): one row of
    /// 28 grown to whole cells of <paramref name="grid"/> (30 on the default grid), the rows under it starting on a grid line.
    /// </summary>
    public static double EdgeRow(Themia.Core.Widgets.ItemGrid grid) => grid.Whole(FamilyShape.Row1);

    /// <summary>How far a last button reaches past the inset: its glyph sits on the inset.</summary>
    public const double ButtonOverhang = (FamilyShape.LineButton - FamilyShape.Icon) / 2;

    /// <summary>
    /// Which <paramref name="parts"/> show in a line <paramref name="width"/> wide (the inset already
    /// taken off) whose name needs <paramref name="nameWidth"/>: one flag per part, in order. An
    /// unmeasured line shows every part. More than <see cref="MaxButtons"/> buttons throw: a line
    /// that wants a third has a menu's line instead.
    /// </summary>
    public static bool[] Shown(double width, double nameWidth, IReadOnlyList<LinePart> parts)
    {
        ArgumentNullException.ThrowIfNull(parts);

        if (parts.Count(p => p.Kind == LinePartKind.Button) > MaxButtons)
        {
            throw new ArgumentException($"a line has at most {MaxButtons} buttons", nameof(parts));
        }

        var shown = Enumerable.Repeat(true, parts.Count).ToArray();
        if (!double.IsFinite(width) || width <= 0)
        {
            return shown;
        }

        foreach (var index in FoldOrder(parts))
        {
            if (Needs(nameWidth, parts, shown) <= width)
            {
                break;
            }

            shown[index] = false;
        }

        return shown;
    }

    /// <summary>
    /// [S-6710] <see cref="Shown(double, double, IReadOnlyList{LinePart})"/> over the parts that have
    /// something to say now (<paramref name="wanted"/>): a part that is not wanted is never shown and
    /// takes no room and no gap, so it never folds a wanted one away.
    /// </summary>
    public static bool[] Shown(double width, double nameWidth, IReadOnlyList<LinePart> parts, IReadOnlyList<bool> wanted)
    {
        ArgumentNullException.ThrowIfNull(parts);
        ArgumentNullException.ThrowIfNull(wanted);

        var indices = Enumerable.Range(0, parts.Count).Where(i => i < wanted.Count && wanted[i]).ToList();
        var folded = Shown(width, nameWidth, [.. indices.Select(i => parts[i])]);
        var shown = new bool[parts.Count];
        for (var k = 0; k < indices.Count; k++)
        {
            shown[indices[k]] = folded[k];
        }

        return shown;
    }

    /// <summary>The width the line needs with the parts <paramref name="shown"/>: its label, the parts and their gaps.</summary>
    public static double Needs(double nameWidth, IReadOnlyList<LinePart> parts, IReadOnlyList<bool> shown)
    {
        ArgumentNullException.ThrowIfNull(parts);
        ArgumentNullException.ThrowIfNull(shown);

        var total = Math.Max(0, nameWidth);
        LinePart? previous = null;
        for (var i = 0; i < parts.Count; i++)
        {
            if (!shown[i])
            {
                continue;
            }

            total += (previous is null ? FamilySpace.S2 : Gap(previous.Value.Kind, parts[i].Kind)) + parts[i].Width;
            previous = parts[i];
        }

        // A last button's glyph sits on the inset: the button's own padding runs past it.
        return previous is { Kind: LinePartKind.Button } ? total - ButtonOverhang : total;
    }

    /// <summary>The gap between two trailing parts: none between two buttons, <see cref="PartGap"/> otherwise.</summary>
    public static double Gap(LinePartKind before, LinePartKind after)
        => before == LinePartKind.Button && after == LinePartKind.Button ? 0 : PartGap;

    /// <summary>
    /// [S-2260] What a line's informing label says about where or which the widget is about (the
    /// place of a sky widget, the adapter of a GPU widget, the host of a ping): the text trimmed, or null
    /// when there is none, and then the label is not wanted and takes no room. The name says what the
    /// widget is; this label never replaces it.
    /// </summary>
    public static string? About(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    /// <summary>
    /// [S-2260] Where a part of <paramref name="kind"/> stands among <paramref name="kinds"/> (the parts
    /// already there, in order): after every part of its own kind and of the kinds before it, so a part
    /// added later (a label set once the place is known) still stands in the line's order.
    /// </summary>
    public static int InsertAt(IReadOnlyList<LinePartKind> kinds, LinePartKind kind)
    {
        ArgumentNullException.ThrowIfNull(kinds);

        for (var i = 0; i < kinds.Count; i++)
        {
            if (kinds[i] > kind)
            {
                return i;
            }
        }

        return kinds.Count;
    }

    /// <summary>The order parts give way in: labels, then buttons from the last, then the count.</summary>
    private static IEnumerable<int> FoldOrder(IReadOnlyList<LinePart> parts)
    {
        for (var i = 0; i < parts.Count; i++)
        {
            if (parts[i].Kind == LinePartKind.Label)
            {
                yield return i;
            }
        }

        for (var i = parts.Count - 1; i >= 0; i--)
        {
            if (parts[i].Kind == LinePartKind.Button)
            {
                yield return i;
            }
        }

        for (var i = 0; i < parts.Count; i++)
        {
            if (parts[i].Kind == LinePartKind.Count)
            {
                yield return i;
            }
        }
    }
}
