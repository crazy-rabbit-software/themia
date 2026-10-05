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
}
