namespace Themia.Core.Widgets;

/// <summary>
/// [S-1281] The bottom fade of a scrolling widget list: the rows nearest the bottom edge of the
/// viewport fade while there is more below, saying "this scrolls" without a scroll bar.
/// <para>
/// An opacity per row, not a gradient painted over the list: the card can be transparent, and a
/// gradient to "the card's colour" would lay a band over the wallpaper. The same depth and floor as
/// the Tasks list's fade ([S-781]), so every list on a desktop fades alike.
/// </para>
/// </summary>
public static class ListFade
{
    /// <summary>How far above the viewport's bottom edge the fade reaches, in DIPs.</summary>
    public const double Depth = 48;

    /// <summary>The faintest a faded row gets: still legible, clearly "more below".</summary>
    public const double Floor = 0.35;

    /// <summary>
    /// A row's opacity: 1 when nothing is below the viewport or the row's middle is at least
    /// <see cref="Depth"/> above its bottom; down to <see cref="Floor"/> at (or past) the edge.
    /// </summary>
    /// <param name="rowTop">The row's top in the list's own coordinates.</param>
    /// <param name="rowBottom">The row's bottom in the list's own coordinates.</param>
    /// <param name="viewBottom">The viewport's bottom in the same coordinates (scroll offset + viewport height).</param>
    /// <param name="moreBelow">Whether the list continues past the viewport.</param>
    public static double Opacity(double rowTop, double rowBottom, double viewBottom, bool moreBelow)
    {
        if (!moreBelow || !double.IsFinite(rowTop) || !double.IsFinite(rowBottom) || !double.IsFinite(viewBottom))
        {
            return 1;
        }

        var distance = viewBottom - ((rowTop + rowBottom) / 2);
        if (distance >= Depth)
        {
            return 1;
        }

        return Floor + ((1 - Floor) * Math.Max(0, distance) / Depth);
    }

    /// <summary>Whether a list of <paramref name="extent"/> continues past a viewport ending at <paramref name="viewBottom"/>.</summary>
    public static bool MoreBelow(double viewBottom, double extent) => viewBottom < extent - Disclosure.Tolerance;
}
