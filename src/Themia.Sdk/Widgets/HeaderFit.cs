namespace Themia.Core.Widgets;

/// <summary>
/// How much of a card header an informational trailing label — "Just now", "by CPU" — may take.
/// <para>
/// [S-655] The title's column is the star and the label's is sized to the label, so on a narrow
/// tile the label won and the title was squeezed to its first letter: "Z..Just now", "M.Just now".
/// The title says which widget this is; the label is a footnote to it. So the title keeps a
/// minimum, the label has what is left, and a label left too little to say anything is not drawn.
/// </para>
/// </summary>
public static class HeaderFit
{
    /// <summary>The least the title keeps: about six characters of Body Strong, enough to name it.</summary>
    public const double MinimumTitle = 64;

    /// <summary>Less than this and a label is an ellipsis with nothing before it.</summary>
    public const double MinimumLabel = 40;

    /// <summary>
    /// The widest the trailing label may be in a header <paramref name="width"/> wide whose icon
    /// and gap take <paramref name="leading"/>: 0 when it should not be drawn at all, and
    /// unbounded for a header that has not been measured.
    /// </summary>
    public static double TrailingLabelWidth(double width, double leading)
    {
        if (!double.IsFinite(width) || width <= 0)
        {
            return double.PositiveInfinity;
        }

        var room = width - leading - MinimumTitle;
        return room >= MinimumLabel ? room : 0;
    }
}
