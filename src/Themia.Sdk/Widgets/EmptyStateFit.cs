namespace Themia.Core.Widgets;

/// <summary>
/// How an empty state — a glyph over a sentence — sheds to fit the box it was given.
/// <para>
/// [S-642] Every empty state used to be laid out at its natural height and centred, so on a tile
/// too short for it the sentence ran off the top and the bottom of the card at once: "Choose a
/// place in settings" became a cut-off glyph and "settin" sliced in half. The glyph is decoration
/// and the sentence is the message, so the glyph goes first; then the sentence is held to the
/// lines the box has and ends in an ellipsis rather than in a clipped line.
/// </para>
/// </summary>
public static class EmptyStateFit
{
    /// <summary>
    /// How many lines the sentence may take when it has the box to itself, or 0 for no limit (the
    /// value <c>TextBlock.MaxLines</c> uses for "unlimited"). Never less than one line: a box too
    /// short even for that still shows the start of the sentence rather than nothing.
    /// </summary>
    public static int LineBudget(double height, double lineHeight)
    {
        if (!double.IsFinite(height) || !double.IsFinite(lineHeight) || lineHeight <= 0)
        {
            return 0;
        }

        // Lines are rows like any other ([S-662]): one place counts them.
        return Math.Max(1, Disclosure.WholeRows(height, lineHeight));
    }

    /// <summary>
    /// Whether the glyph still fits above a sentence that has been measured at
    /// <paramref name="messageHeight"/>. An unbounded box always has room for it.
    /// </summary>
    /// <param name="height">The height the empty state may occupy.</param>
    /// <param name="iconBlock">The glyph's height including the gap under it.</param>
    /// <param name="messageHeight">The sentence's height, measured within <see cref="LineBudget"/>.</param>
    public static bool ShowsIcon(double height, double iconBlock, double messageHeight)
    {
        if (!double.IsFinite(height))
        {
            return true;
        }

        return iconBlock + messageHeight <= height + Tolerance;
    }

    /// <summary>Layout rounding: a box that is short by a fraction of a pixel still fits.</summary>
    private const double Tolerance = 0.5;
}
