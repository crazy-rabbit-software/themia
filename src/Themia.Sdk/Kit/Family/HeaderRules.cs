namespace Themia.Sdk.Kit;

/// <summary>[S-6710] What stands in a Header's trailing slot, from its start.</summary>
public enum HeaderPartKind
{
    /// <summary>The problem glyph (a 16 caution warning): state, never folded.</summary>
    Problem,

    /// <summary>The busy ring (16): state, never folded.</summary>
    Busy,

    /// <summary>An informing label ("by CPU", an age): the first to give way.</summary>
    Label,

    /// <summary>A <see cref="FamilyShape.HeaderButton"/>: the last one gives way first.</summary>
    Button,

    /// <summary>The count pill (Caption on the subtle fill, never the accent): the last to give way.</summary>
    Count,
}

/// <summary>[S-6710] One trailing part and the width it takes.</summary>
public readonly record struct HeaderPart(HeaderPartKind Kind, double Width);

/// <summary>
/// [S-6710] The Header block: [glyph 16] 8 [name, BodyStrong, secondary, ellipsis] … [trailing], one
/// line of 20 at the inset. The trailing slot holds, from its start, the problem glyph, the busy
/// ring, an informing label, at most two <see cref="FamilyShape.HeaderButton"/>s and the count pill.
/// <para>
/// On a narrow card the parts give way before the name is cut: the label first, then the buttons
/// (the last one first), then the count; the problem glyph and the ring are state and stay. Only
/// when nothing is left to give way does the name end in an ellipsis. Parts stand 4 apart, buttons
/// side by side; a last button's glyph lines up with the inset (the button reaches 4 past it).
/// </para>
/// </summary>
public static class HeaderRules
{
    /// <summary>At most this many buttons in a header.</summary>
    public const int MaxButtons = 2;

    /// <summary>The gap between two trailing parts that are not both buttons.</summary>
    public const double PartGap = FamilySpace.S1;

    /// <summary>How far a last button reaches past the inset: its glyph sits on the inset.</summary>
    public const double ButtonOverhang = (FamilyShape.HeaderButton - FamilyShape.Icon) / 2;

    /// <summary>The glyph and its gap before the name.</summary>
    public const double Leading = FamilyShape.Icon + FamilySpace.GlyphGap;

    /// <summary>
    /// Which <paramref name="parts"/> show in a header <paramref name="width"/> wide (the inset already
    /// taken off) whose name needs <paramref name="nameWidth"/>: one flag per part, in order. An
    /// unmeasured header shows every part. More than <see cref="MaxButtons"/> buttons throw: a header
    /// that wants a third has a menu's line instead.
    /// </summary>
    public static bool[] Shown(double width, double nameWidth, IReadOnlyList<HeaderPart> parts)
    {
        ArgumentNullException.ThrowIfNull(parts);

        if (parts.Count(p => p.Kind == HeaderPartKind.Button) > MaxButtons)
        {
            throw new ArgumentException($"a header has at most {MaxButtons} buttons", nameof(parts));
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
    /// [S-6710] <see cref="Shown(double, double, IReadOnlyList{HeaderPart})"/> over the parts that have
    /// something to say now (<paramref name="wanted"/>): a part that is not wanted is never shown and
    /// takes no room and no gap, so it never folds a wanted one away.
    /// </summary>
    public static bool[] Shown(double width, double nameWidth, IReadOnlyList<HeaderPart> parts, IReadOnlyList<bool> wanted)
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

    /// <summary>The width the header needs with the parts <paramref name="shown"/>: leading, name, the parts and their gaps.</summary>
    public static double Needs(double nameWidth, IReadOnlyList<HeaderPart> parts, IReadOnlyList<bool> shown)
    {
        ArgumentNullException.ThrowIfNull(parts);
        ArgumentNullException.ThrowIfNull(shown);

        var total = Leading + Math.Max(0, nameWidth);
        HeaderPart? previous = null;
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
        return previous is { Kind: HeaderPartKind.Button } ? total - ButtonOverhang : total;
    }

    /// <summary>The gap between two trailing parts: none between two buttons, <see cref="PartGap"/> otherwise.</summary>
    public static double Gap(HeaderPartKind before, HeaderPartKind after)
        => before == HeaderPartKind.Button && after == HeaderPartKind.Button ? 0 : PartGap;

    /// <summary>
    /// [S-2260] What a header's informing label says about where or which the widget is about (the
    /// place of a sky widget, the adapter of a GPU widget, the host of a ping): the text trimmed, or null
    /// when there is none, and then the label is not wanted and takes no room. The name says what the
    /// widget is; this label never replaces it.
    /// </summary>
    public static string? About(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    /// <summary>
    /// [S-2260] Where a part of <paramref name="kind"/> stands among <paramref name="kinds"/> (the parts
    /// already there, in order): after every part of its own kind and of the kinds before it, so a part
    /// added later (a label set once the place is known) still stands in the header's order.
    /// </summary>
    public static int InsertAt(IReadOnlyList<HeaderPartKind> kinds, HeaderPartKind kind)
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
    private static IEnumerable<int> FoldOrder(IReadOnlyList<HeaderPart> parts)
    {
        for (var i = 0; i < parts.Count; i++)
        {
            if (parts[i].Kind == HeaderPartKind.Label)
            {
                yield return i;
            }
        }

        for (var i = parts.Count - 1; i >= 0; i--)
        {
            if (parts[i].Kind == HeaderPartKind.Button)
            {
                yield return i;
            }
        }

        for (var i = 0; i < parts.Count; i++)
        {
            if (parts[i].Kind == HeaderPartKind.Count)
            {
                yield return i;
            }
        }
    }

    /// <summary>
    /// [S-7064] Which of a kit header's parts have something to say: the problem glyph while there is a
    /// problem line, the ring while a fetch is out, the count pill for a positive count, the widget's own
    /// buttons (at most <see cref="MaxButtons"/>). With none of them the extras take no room at all.
    /// Where the wanted ones show is the fold's (<see cref="Shown"/>).
    /// </summary>
    public static HeaderWants Wants(int problems, bool busy, long? count, int buttons = 0)
        => new(problems > 0, busy, count is > 0, Math.Clamp(buttons, 0, MaxButtons));
}

/// <summary>[S-7064] The parts a kit header wants: what <see cref="HeaderRules.Wants"/> decided.</summary>
public readonly record struct HeaderWants(bool Problem, bool Busy, bool Count, int Buttons = 0)
{
    /// <summary>Whether the extras show at all.</summary>
    public bool Any => Problem || Busy || Count || Buttons > 0;
}
