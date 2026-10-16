using Themia.Core.Model;

namespace Themia.Core.Widgets;

/// <summary>
/// Which edge of the card the header strip stands on, in the card's own flow: <c>Start</c> is the
/// left edge left to right and the right edge right to left.
/// </summary>
public enum HeaderEdge
{
    None = 0,
    Top = 1,
    Bottom = 2,
    Start = 3,
    End = 4,
}

/// <summary>How the header is turned to stand along a side edge ([S-405]).</summary>
public enum HeaderTurn
{
    None = 0,

    /// <summary>A quarter turn counter-clockwise: the title reads bottom to top (the left edge).</summary>
    CounterClockwise = 1,

    /// <summary>A quarter turn clockwise: the title reads top to bottom (the right edge).</summary>
    Clockwise = 2,
}

/// <summary>A cell of the card's 3 × 3 content grid (rows and columns: auto, star, auto).</summary>
public readonly record struct GridCell(int Row, int Column, int RowSpan, int ColumnSpan);

/// <summary>Which of the four corners are the card's own, in <c>CornerRadius</c> order and the card's flow.</summary>
public readonly record struct CardCorners(bool TopStart, bool TopEnd, bool BottomEnd, bool BottomStart);

/// <summary>Which of the four sides lie on the card's edge, in <c>Thickness</c> order and the card's flow.</summary>
public readonly record struct CardSides(bool Start, bool Top, bool End, bool Bottom);

/// <summary>The render transform that stands a header laid out <c>length × thickness</c> along a side edge.</summary>
public readonly record struct HeaderRotation(double Degrees, double X, double Y);

/// <summary>
/// Where the card's header strip stands and what it leaves the content ([S-401], [S-404]..[S-408]).
/// <para>
/// The card's content is a 3 × 3 grid: auto, star, auto both ways. The view always sits in the
/// middle cell; the strip takes a whole outer row (top, bottom) or a whole outer column (left,
/// right), so the content area is the card less the strip on that one edge and the size ladder
/// classifies on what is left. Switching the placement moves cells; nothing is rebuilt.
/// </para>
/// </summary>
public static class HeaderStrip
{
    /// <summary>The view's cell: the middle one, whatever the placement.</summary>
    public static readonly GridCell ViewCell = new(1, 1, 1, 1);

    /// <summary>[S-404] Whether the header stands along a side edge, turned.</summary>
    public static bool IsSide(HeaderPlacement placement)
        => placement is HeaderPlacement.Left or HeaderPlacement.Right;

    /// <summary>
    /// [S-408] A side strip carries the icon and the title only. The trailing part (a badge, a
    /// caption, a picker, Notes' formatting bar) is sized by its view for a header as wide as the
    /// content, so along a strip it would be measured against the card's height and its controls
    /// would read sideways; it stays on the view and comes back with a top or bottom header.
    /// </summary>
    public static bool ShowsTrailing(HeaderPlacement placement) => !IsSide(placement);

    /// <summary>
    /// [S-406] Left and Right are physical edges in every language: the edge in the card's flow,
    /// which mirrors right to left, is the other one there.
    /// </summary>
    public static HeaderEdge Edge(HeaderPlacement placement, bool rightToLeft) => placement switch
    {
        HeaderPlacement.Top => HeaderEdge.Top,
        HeaderPlacement.Bottom => HeaderEdge.Bottom,
        HeaderPlacement.Left => rightToLeft ? HeaderEdge.End : HeaderEdge.Start,
        HeaderPlacement.Right => rightToLeft ? HeaderEdge.Start : HeaderEdge.End,
        _ => HeaderEdge.None,
    };

    /// <summary>
    /// [S-405] The left edge turns the header a quarter counter-clockwise (read bottom to top, as a
    /// spine label or a vertical tab on the left reads), the right edge a quarter clockwise (read
    /// top to bottom). Either way the tops of the letters face out of the card. Physical, like the edge.
    /// </summary>
    public static HeaderTurn Turn(HeaderPlacement placement) => placement switch
    {
        HeaderPlacement.Left => HeaderTurn.CounterClockwise,
        HeaderPlacement.Right => HeaderTurn.Clockwise,
        _ => HeaderTurn.None,
    };

    /// <summary>
    /// [S-405] The transform (rotate about the origin, then translate) that stands a header laid out
    /// <paramref name="length"/> wide and <paramref name="thickness"/> high in a strip
    /// <paramref name="thickness"/> wide and <paramref name="length"/> high. Counter-clockwise the
    /// header's start lands at the bottom, clockwise at the top; its top edge faces out either way.
    /// </summary>
    public static HeaderRotation Rotation(HeaderTurn turn, double length, double thickness) => turn switch
    {
        HeaderTurn.CounterClockwise => new HeaderRotation(-90, 0, length),
        HeaderTurn.Clockwise => new HeaderRotation(90, thickness, 0),
        _ => new HeaderRotation(0, 0, 0),
    };

    /// <summary>[S-402] Below this card height a top or bottom header sheds: the content is worth more than its label.</summary>
    public const double FlatShedsBelow = 96;

    /// <summary>
    /// [S-409] Below this card width a side header sheds. A side strip eats width, about 40 DIPs
    /// (a header's height), so a card keeps one only while the content beside it is still at least
    /// as wide as the narrowest card every widget is laid out for, the XS tier's 120 DIPs
    /// (<see cref="WidgetTiers"/>): 120 + 40. Narrower, text wraps per letter and buttons clip.
    /// </summary>
    public const double SideShedsBelow = 160;

    /// <summary>
    /// [S-402] [S-404] The header sheds on a card too small across the strip to carry it: too short
    /// for a top or bottom strip, too narrow for a side one ([S-409]), since that is the axis the
    /// strip eats. A size of zero is a card not measured yet, not a small one.
    /// </summary>
    public static bool Sheds(HeaderPlacement placement, double width, double height)
    {
        var side = IsSide(placement);
        var across = side ? width : height;
        return across > 0 && across < (side ? SideShedsBelow : FlatShedsBelow);
    }

    /// <summary>The strip's cell: the whole outer row or column on its edge.</summary>
    public static GridCell StripCell(HeaderEdge edge) => edge switch
    {
        HeaderEdge.Bottom => new GridCell(2, 0, 1, 3),
        HeaderEdge.Start => new GridCell(0, 0, 3, 1),
        HeaderEdge.End => new GridCell(0, 2, 3, 1),
        _ => new GridCell(0, 0, 1, 3),
    };

    /// <summary>[S-1761] The body fill's cells: all the strip leaves, the whole grid without one.</summary>
    public static GridCell BodyCell(HeaderEdge edge) => edge switch
    {
        HeaderEdge.Top => new GridCell(1, 0, 2, 3),
        HeaderEdge.Bottom => new GridCell(0, 0, 2, 3),
        HeaderEdge.Start => new GridCell(0, 1, 3, 2),
        HeaderEdge.End => new GridCell(0, 0, 3, 2),
        _ => new GridCell(0, 0, 3, 3),
    };

    /// <summary>[S-407] The strip's rounded corners: the card's two on its edge.</summary>
    public static CardCorners StripCorners(HeaderEdge edge) => edge switch
    {
        HeaderEdge.Top => new CardCorners(true, true, false, false),
        HeaderEdge.Bottom => new CardCorners(false, false, true, true),
        HeaderEdge.Start => new CardCorners(true, false, false, true),
        HeaderEdge.End => new CardCorners(false, true, true, false),
        _ => new CardCorners(false, false, false, false),
    };

    /// <summary>[S-407] The body's rounded corners: the card's that the strip does not have.</summary>
    public static CardCorners BodyCorners(HeaderEdge edge)
    {
        if (edge == HeaderEdge.None)
        {
            return new CardCorners(true, true, true, true);
        }

        var strip = StripCorners(edge);
        return new CardCorners(!strip.TopStart, !strip.TopEnd, !strip.BottomEnd, !strip.BottomStart);
    }

    /// <summary>[S-836] The strip's sides on the card's edge (a solid stroke insets the tint there): all but its inner one.</summary>
    public static CardSides StripSides(HeaderEdge edge) => edge switch
    {
        HeaderEdge.Top => new CardSides(true, true, true, false),
        HeaderEdge.Bottom => new CardSides(true, false, true, true),
        HeaderEdge.Start => new CardSides(true, true, false, true),
        HeaderEdge.End => new CardSides(false, true, true, true),
        _ => new CardSides(false, false, false, false),
    };

    /// <summary>[S-836] The body's sides on the card's edge: all but the strip's.</summary>
    public static CardSides BodySides(HeaderEdge edge) => new(
        edge != HeaderEdge.Start,
        edge != HeaderEdge.Top,
        edge != HeaderEdge.End,
        edge != HeaderEdge.Bottom);

    /// <summary>
    /// [S-1761] The side the body's base reaches one DIP under the strip on, so the two fills
    /// overlap rather than meet at an edge that falls between pixels: the strip's own edge.
    /// </summary>
    public static CardSides Seam(HeaderEdge edge) => new(
        edge == HeaderEdge.Start,
        edge == HeaderEdge.Top,
        edge == HeaderEdge.End,
        edge == HeaderEdge.Bottom);
}
