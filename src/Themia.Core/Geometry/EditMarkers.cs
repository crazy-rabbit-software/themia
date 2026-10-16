namespace Themia.Core.Geometry;

/// <summary>
/// [S-31b] One edit-mode marker: the handle it resizes, the box that takes the press (transparent,
/// so the grip is larger than the ink), and the bars it is drawn from. Every box is in DIPs relative
/// to the card's cell, and the bars are relative to the hit box.
/// </summary>
public sealed record EditMarker(ResizeHandle Handle, PixelRect Hit, IReadOnlyList<PixelRect> Bars);

/// <summary>
/// [S-31b] Where edit mode draws a widget's eight markers instead of an outline: a square-cornered
/// L-bracket at each corner and a short bar at the centre of each edge, flush inside the cell
/// bounds. The corners are never rounded, whatever the card's radius. Every edge lands on a device
/// pixel at the given rasterization scale, so the bars are crisp at any DPI.
/// </summary>
public static class EditMarkers
{
    /// <summary>The bar thickness, DIPs.</summary>
    public const double Thickness = 3;

    /// <summary>How far a corner bracket's arms run along each edge, DIPs.</summary>
    public const double Arm = 14;

    /// <summary>The length of an edge-centre bar, DIPs.</summary>
    public const double EdgeLength = 16;

    /// <summary>The least clear space kept between a corner bracket and an edge bar on a small widget, DIPs.</summary>
    public const double MinGap = 4;

    /// <summary>The grip each handle has always had: a square this size centred on its corner or edge midpoint, DIPs.</summary>
    public const double GripSize = 10;

    /// <summary>The contrast stroke drawn round every bar so it reads on any wallpaper, DIPs.</summary>
    public const double Halo = 1;

    /// <summary>The eight markers for a cell of <paramref name="width"/> by <paramref name="height"/> DIPs.</summary>
    public static IReadOnlyList<EditMarker> For(double width, double height, double scale)
    {
        var s = double.IsFinite(scale) && scale > 0 ? scale : 1;
        var w = Snap(Math.Max(0, double.IsFinite(width) ? width : 0), s);
        var h = Snap(Math.Max(0, double.IsFinite(height) ? height : 0), s);
        var t = Device(Thickness, s);

        var (armX, edgeX) = Fit(w, t, s);
        var (armY, edgeY) = Fit(h, t, s);

        var midX = Snap((w - edgeX) / 2, s);
        var midY = Snap((h - edgeY) / 2, s);
        var grip = GripSize / 2;

        return
        [
            Corner(ResizeHandle.TopLeft, 0, 0, left: true, top: true),
            Edge(ResizeHandle.Top, PixelRect.FromEdges(midX, 0, midX + edgeX, t), w / 2, 0),
            Corner(ResizeHandle.TopRight, w, 0, left: false, top: true),
            Edge(ResizeHandle.Left, PixelRect.FromEdges(0, midY, t, midY + edgeY), 0, h / 2),
            Edge(ResizeHandle.Right, PixelRect.FromEdges(w - t, midY, w, midY + edgeY), w, h / 2),
            Corner(ResizeHandle.BottomLeft, 0, h, left: true, top: false),
            Edge(ResizeHandle.Bottom, PixelRect.FromEdges(midX, h - t, midX + edgeX, h), w / 2, h),
            Corner(ResizeHandle.BottomRight, w, h, left: false, top: false),
        ];

        EditMarker Corner(ResizeHandle handle, double x, double y, bool left, bool top)
        {
            var x0 = left ? x : x - armX;
            var y0 = top ? y : y - armY;
            var across = PixelRect.FromEdges(x0, top ? y : y - t, x0 + armX, top ? y + t : y);
            var down = PixelRect.FromEdges(left ? x : x - t, y0, left ? x + t : x, y0 + armY);
            var ink = Union(across, down);
            return Make(handle, Union(ink, PixelRect.FromEdges(x - grip, y - grip, x + grip, y + grip)), across, down);
        }

        EditMarker Edge(ResizeHandle handle, PixelRect bar, double cx, double cy)
        {
            var old = PixelRect.FromEdges(cx - grip, cy - grip, cx + grip, cy + grip);
            return Make(handle, Union(bar, old), bar);
        }

        EditMarker Make(ResizeHandle handle, PixelRect hit, params PixelRect[] bars)
        {
            var box = PixelRect.FromEdges(Snap(hit.X, s), Snap(hit.Y, s), Snap(hit.X + hit.Width, s), Snap(hit.Y + hit.Height, s));
            return new EditMarker(handle, box, [.. bars.Select(b => new PixelRect(b.X - box.X, b.Y - box.Y, b.Width, b.Height))]);
        }
    }

    /// <summary>A bar grown by the contrast stroke on every side, still on device pixels.</summary>
    public static PixelRect Haloed(PixelRect bar, double scale)
    {
        var s = double.IsFinite(scale) && scale > 0 ? scale : 1;
        var halo = Device(Halo, s);
        return new PixelRect(bar.X - halo, bar.Y - halo, bar.Width + (2 * halo), bar.Height + (2 * halo));
    }

    /// <summary>
    /// The arm and the edge bar for one side of <paramref name="length"/> DIPs. A side too short for
    /// both brackets, the bar and the gaps between them shrinks the arms and the bar alike, never
    /// below the thickness, so a small widget still shows eight separate marks.
    /// </summary>
    private static (double Arm, double Edge) Fit(double length, double thickness, double scale)
    {
        var full = (2 * Arm) + EdgeLength;
        var room = length - (2 * MinGap);
        if (room >= full)
        {
            return (Device(Arm, scale), Device(EdgeLength, scale));
        }

        var ratio = Math.Max(0, room) / full;
        return (Math.Max(thickness, Floor(Arm * ratio, scale)), Math.Max(thickness, Floor(EdgeLength * ratio, scale)));
    }

    private static PixelRect Union(PixelRect a, PixelRect b)
        => PixelRect.FromEdges(
            Math.Min(a.X, b.X),
            Math.Min(a.Y, b.Y),
            Math.Max(a.X + a.Width, b.X + b.Width),
            Math.Max(a.Y + a.Height, b.Y + b.Height));

    /// <summary>The nearest device-pixel boundary.</summary>
    private static double Snap(double dips, double scale) => Math.Round(dips * scale) / scale;

    /// <summary>A device-pixel boundary at or below, so a shrunk length never grows back into the gap.</summary>
    private static double Floor(double dips, double scale) => Math.Floor(dips * scale) / scale;

    /// <summary>A length of at least one device pixel, in whole device pixels.</summary>
    private static double Device(double dips, double scale) => Math.Max(1, Math.Round(dips * scale)) / scale;
}
