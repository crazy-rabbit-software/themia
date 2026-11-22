namespace Themia.Sdk.Kit;

/// <summary>
/// [S-13950] What an inline box (the one <c>InlineBox</c>) is, in the box of the words it stands in for: as tall as their
/// lines, no padding and no frame round its text (so the text stands where theirs stood), one line never wrapping
/// taller. The platform's text box would add a 32 minimum, padding of 10 and 5, a frame of 1 and a clear button, and
/// wrap long words onto more lines: each of those, in an add row of 28, grew the row and shortened the list over it
/// (owner, 2026-11-19: "layout shifts for these kind of inputs should never happen").
/// </summary>
/// <param name="Height">The box's height: the words' lines exactly.</param>
/// <param name="TextInset">Between the box's edge and its text: none, so the text edge is the words' edge.</param>
/// <param name="Wraps">Whether its text wraps: only a box of more than one line does, inside its lines.</param>
public readonly record struct InlineFit(double Height, double TextInset, bool Wraps)
{
    /// <summary>How far a box may stand from where the measure expects it: a device pixel's rounding at 150 %.</summary>
    public const double Tolerance = 0.75;

    /// <summary>
    /// [S-15793] Where the platform's text box stands its baseline under its top, a fraction of its size: measured on the
    /// renders (a Body box of 14 inked "Northwind" 1.2 DIP over the same words on their line of 20, whose baseline stands at
    /// <see cref="LineInk.FaceBaseline"/> of it; with this the box's words ink where the words do at 150 %, every pixel row).
    /// </summary>
    public const double BoxBaseline = 1.09;

    /// <summary>
    /// [S-15793] How far the box's text moves down to stand on its words' baseline (WR-12: "An inline input stands in exactly
    /// the box of what it replaces"): their line's baseline less the box's own, for words of <paramref name="fontSize"/> on a
    /// line <paramref name="lineHeight"/> tall; never up.
    /// </summary>
    public static double BaselineShift(double lineHeight, double fontSize)
    {
        var shift = (LineInk.FaceBaseline * lineHeight) - (BoxBaseline * fontSize);
        return double.IsFinite(shift) ? Math.Max(0, shift) : 0;
    }

    /// <summary>The box for words whose line is <paramref name="lineHeight"/>, taking <paramref name="lines"/> of them (at least one).</summary>
    public static InlineFit Of(double lineHeight, int lines = 1)
    {
        var taken = Math.Max(1, lines);
        return new(Math.Max(0, lineHeight) * taken, 0, taken > 1);
    }

    /// <summary>
    /// [S-13950] The inline check's judgement of a widget drawn at rest (<paramref name="rest"/>) and with its box open
    /// (<paramref name="open"/>), each part by its key: the card keeps its size; the words the box stands in for (the part
    /// drawn at rest and not open that the box covers most) are the box's box (their left edge, top and height; the box
    /// may take their whole room to the right); every other part is drawn where it was and as large, none gone. A box
    /// that replaces no words (a search box that is always there, taking the keys) only has to move nothing. Each
    /// finding is one line; none is the rule kept.
    /// <para>
    /// A search that <paramref name="filters"/> what a list shows ([S-13950]: its words typed, or its line over the list's
    /// first line) changes what the list's rows say, never where they stand: the parts in a list (<see cref="InlinePart.Listed"/>)
    /// are judged by their rows alone, each row drawn open standing on a row drawn at rest; everything round the list is
    /// held to the rule above.
    /// </para>
    /// </summary>
    public static IReadOnlyList<string> Judge(
        (double Width, double Height) restCard,
        (double Width, double Height) openCard,
        InlinePart? box,
        IReadOnlyList<InlinePart> rest,
        IReadOnlyList<InlinePart> open,
        bool filters = false)
    {
        ArgumentNullException.ThrowIfNull(rest);
        ArgumentNullException.ThrowIfNull(open);

        var findings = new List<string>();
        if (!Near(restCard.Width, openCard.Width) || !Near(restCard.Height, openCard.Height))
        {
            findings.Add($"the card went from {restCard.Width:0.#}x{restCard.Height:0.#} to {openCard.Width:0.#}x{openCard.Height:0.#}");
        }

        if (filters)
        {
            var slots = rest.Where(p => p.Listed && p.Key.StartsWith(RowKey, StringComparison.Ordinal)).ToList();
            foreach (var row in open.Where(p => p.Listed && p.Key.StartsWith(RowKey, StringComparison.Ordinal)))
            {
                if (!slots.Any(slot => Near(slot.X, row.X) && Near(slot.Y, row.Y) && Near(slot.Width, row.Width) && Near(slot.Height, row.Height)))
                {
                    findings.Add($"a row {row.Box} stands on no row's place");
                }
            }

            rest = [.. rest.Where(Held)];
            open = [.. open.Where(Held)];
        }

        var drawn = open.GroupBy(p => p.Key, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var gone = rest.Where(p => !drawn.ContainsKey(p.Key)).ToList();
        InlinePart? words = box is { } over && gone.Where(p => p.Overlap(over) > 0).ToList() is { Count: > 0 } covered ? covered.MaxBy(p => p.Overlap(over)) : null;

        if (box is { } stands && words is { } replaced
            && (!Near(stands.X, replaced.X) || !Near(stands.Y, replaced.Y) || !Near(stands.Height, replaced.Height) || stands.Width < replaced.Width - Tolerance))
        {
            findings.Add($"the box {stands.Box} is not the box of \"{replaced.Key}\" {replaced.Box}");
        }

        foreach (var part in rest)
        {
            if (words is { } w && part == w)
            {
                continue;
            }

            if (!drawn.TryGetValue(part.Key, out var now))
            {
                findings.Add($"\"{part.Key}\" {part.Box} is gone");
            }
            else if (!Near(now.X, part.X) || !Near(now.Y, part.Y) || !Near(now.Width, part.Width) || !Near(now.Height, part.Height))
            {
                findings.Add($"\"{part.Key}\" moved from {part.Box} to {now.Box}");
            }
        }

        return findings;
    }

    /// <summary>The key a row of a list is drawn under (numbered where rows repeat).</summary>
    public const string RowKey = "row";

    /// <summary>The key of a list's viewport.</summary>
    public const string ScrollKey = "scroll";

    /// <summary>Whether a filtering search holds a part to its place: everything round the list but the list's own viewport.</summary>
    private static bool Held(InlinePart part) => !part.Listed && !part.Key.StartsWith(ScrollKey, StringComparison.Ordinal);

    private static bool Near(double a, double b) => Math.Abs(a - b) <= Tolerance;
}

/// <summary>
/// [S-13950] One drawn part as the inline check sees it: what it is (its kind and words, numbered when they repeat), its box
/// in the card, and whether it is one of a list's (inside the list's viewport).
/// </summary>
public readonly record struct InlinePart(string Key, double X, double Y, double Width, double Height, bool Listed = false)
{
    /// <summary>The box, as a finding prints it.</summary>
    public string Box => $"({X:0.#},{Y:0.#} {Width:0.#}x{Height:0.#})";

    /// <summary>The area this part and <paramref name="other"/> share.</summary>
    public double Overlap(InlinePart other)
        => Math.Max(0, Math.Min(X + Width, other.X + other.Width) - Math.Max(X, other.X))
            * Math.Max(0, Math.Min(Y + Height, other.Y + other.Height) - Math.Max(Y, other.Y));
}
