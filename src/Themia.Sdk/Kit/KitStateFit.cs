namespace Themia.Sdk.Kit;

/// <summary>
/// [S-15784] Whether a kit list's state can say why over its face: the state's line stands on the card's top edge (the
/// inset over the list's slot) as the list's first row, the face's empty rows under it. A slot whose room under the line
/// holds nothing of a row cannot show both, and keeps the face (WR-10: "it never swaps its face for a status line alone").
/// </summary>
public static class KitStateFit
{
    /// <summary>
    /// Whether a slot <paramref name="slot"/> tall, with the card's <paramref name="inset"/> over it, holds the state's line
    /// (<paramref name="line"/> tall) and none of the face under it.
    /// </summary>
    public static bool Squeezed(double slot, double line, double inset)
        => double.IsFinite(slot) && double.IsFinite(line) && slot + Math.Max(0, inset) - line <= Themia.Core.Widgets.LayoutMath.Tolerance;
}
