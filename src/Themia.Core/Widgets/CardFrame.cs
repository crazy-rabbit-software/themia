using Themia.Core.Geometry;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-872] The window's hit region follows what a card paints, not the rect the widget owns. Pure,
/// so the shell's region code and its tests agree on every case.
/// </summary>
public static class CardFrame
{
    /// <summary>
    /// [S-872] The part of a card's painted rect that takes clicks, or null for none. A card that
    /// draws nothing ([S-873]) takes nothing: a window over a region the user sees the desktop
    /// through would leave the icons there dead.
    /// </summary>
    public static PixelRect? Hit(PixelRect painted, bool shown)
        => shown && painted.Width > 0 && painted.Height > 0 ? painted : null;

    /// <summary>
    /// [S-2245] The hit region's corners for a card: its round corners at its radius (in the region's
    /// pixels: the radius in DIPs times the scale, whole pixels), square where a joined neighbour
    /// squares it ([S-2241]), so no click falls through a notch at a square corner and none is taken
    /// outside a round one. A card that draws no surface (a bare widget) is square: its content may
    /// reach its corners, and nothing marks them.
    /// </summary>
    public static HitCorners Corners(CardOutline outline, bool drawsSurface, double radiusDip, double scale)
    {
        if (!drawsSurface || !double.IsFinite(radiusDip) || radiusDip <= 0 || !double.IsFinite(scale) || scale <= 0)
        {
            return HitCorners.Square;
        }

        var r = Math.Round(radiusDip * scale);
        return new HitCorners(
            outline.RoundTopLeft ? r : 0,
            outline.RoundTopRight ? r : 0,
            outline.RoundBottomRight ? r : 0,
            outline.RoundBottomLeft ? r : 0);
    }
}

/// <summary>[S-2245] The radii of a hit region's four corners, in the region's own pixels (0: square).</summary>
public readonly record struct HitCorners(double TopLeft, double TopRight, double BottomRight, double BottomLeft)
{
    /// <summary>A plain rectangle.</summary>
    public static HitCorners Square { get; } = new(0, 0, 0, 0);
}
