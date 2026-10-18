namespace Themia.Core.Geometry;

/// <summary>A box in pixels (or DIPs): the screen's, a card's, a face part's.</summary>
public readonly record struct PixelRect(double X, double Y, double Width, double Height)
{
    public static PixelRect FromEdges(double left, double top, double right, double bottom)
        => new(left, top, right - left, bottom - top);
}
