using Themia.Core.Geometry;
using Themia.Core.Widgets;

namespace Themia.Core.Desktop;

/// <summary>
/// [S-5544] The desktop's rubber band as <b>one rectangle in screen space</b> (physical pixels, the
/// space the displays and the icons' boxes of the one selection are in, [S-5540]): started on empty
/// desktop of any display, it follows the pointer onto the others, as Explorer's does.
/// <para>
/// Pure. The surface the press landed on holds the pointer and reports it in its own DIPs
/// (<see cref="ToScreen"/>); the band stops at the edge of the displays' work areas taken together
/// (<see cref="Of"/>); every display draws the band in its own DIPs and scale (<see cref="On"/>), cut
/// to its part by its window; and the icons it touches are found where they sit on any display
/// (<see cref="Hits"/>). How it combines with the selection stays <see cref="SelectionBand.Mode"/>.
/// </para>
/// </summary>
public static class DesktopBand
{
    /// <summary>
    /// A point in a surface's DIPs as a screen point. The surface covers its display from the display's
    /// corner ([S-840]) and physical pixels run on across displays, so a captured pointer beyond the
    /// surface's edge (negative, or past its size) lands on the neighbouring display, whatever that
    /// display's own scale is.
    /// </summary>
    /// <param name="display">The surface's display, in physical screen pixels.</param>
    /// <param name="scale">The surface's DIP-to-pixel factor.</param>
    public static (double X, double Y) ToScreen(DeviceRect display, double scale, double dipX, double dipY)
        => (display.X + (dipX * scale), display.Y + (dipY * scale));

    /// <summary>
    /// The point of <paramref name="areas"/> (the work areas, taken together) nearest to a screen point:
    /// the point itself inside one of them, else the nearest point on the nearest one's edge. Where the
    /// displays do not touch, a pointer in the gap holds the band at the display it is nearest to. No
    /// usable area leaves the point as it is.
    /// </summary>
    public static (double X, double Y) Into(IReadOnlyList<DeviceRect> areas, double x, double y)
    {
        ArgumentNullException.ThrowIfNull(areas);
        var best = (X: x, Y: y);
        var nearest = double.PositiveInfinity;

        foreach (var area in areas)
        {
            if (!(area.Width > 0 && area.Height > 0))
            {
                continue;
            }

            var inX = Math.Clamp(x, area.X, area.X + area.Width);
            var inY = Math.Clamp(y, area.Y, area.Y + area.Height);
            var distance = ((inX - x) * (inX - x)) + ((inY - y) * (inY - y));

            if (distance < nearest)
            {
                nearest = distance;
                best = (inX, inY);
            }
        }

        return best;
    }

    /// <summary>
    /// The band between where the press landed and where the pointer is, both screen points, each kept
    /// inside the work areas taken together: it grows across displays and stops at their outer edge and
    /// at what the taskbar or a dock reserved.
    /// </summary>
    public static BandRect Of(IReadOnlyList<DeviceRect> areas, (double X, double Y) start, (double X, double Y) now)
    {
        var from = Into(areas, start.X, start.Y);
        var to = Into(areas, now.X, now.Y);
        return new BandRect(from.X, from.Y, to.X, to.Y);
    }

    /// <summary>
    /// The band as one display draws it: the whole band in that display's DIPs, or null when it does
    /// not reach the display's work area. The display's window cuts it to its own part ([S-21]), so
    /// the band has no edge where it runs on onto the next display.
    /// </summary>
    /// <param name="band">The band, in screen pixels.</param>
    /// <param name="display">The display, in screen pixels.</param>
    /// <param name="workArea">Its work area, in screen pixels.</param>
    /// <param name="scale">Its surface's DIP-to-pixel factor.</param>
    public static BandRect? On(BandRect band, DeviceRect display, DeviceRect workArea, double scale)
    {
        if (!(scale > 0) || !Touches(band, workArea))
        {
            return null;
        }

        return new BandRect(
            (band.X1 - display.X) / scale,
            (band.Y1 - display.Y) / scale,
            (band.X2 - display.X) / scale,
            (band.Y2 - display.Y) / scale);
    }

    /// <summary>
    /// [S-6624] The keys of the icons the band touches at all, on any display, in the boxes' order.
    /// Both are in screen pixels, so a display's scale plays no part ([S-5563]).
    /// </summary>
    public static IReadOnlyList<string> Hits(BandRect band, IReadOnlyList<DesktopBox> boxes)
    {
        ArgumentNullException.ThrowIfNull(boxes);
        var hits = new List<string>();

        foreach (var box in boxes)
        {
            if (box.Width > 0 && box.Height > 0 && band.Hits(box.X, box.Y, box.Width, box.Height))
            {
                hits.Add(box.Key);
            }
        }

        return hits;
    }

    /// <summary>Whether the band and an area share a point, edges included: a band lying along a display's edge still shows its line there.</summary>
    private static bool Touches(BandRect band, DeviceRect area)
        => area.Width > 0 && area.Height > 0
            && band.Left <= area.X + area.Width && band.Left + band.Width >= area.X
            && band.Top <= area.Y + area.Height && band.Top + band.Height >= area.Y;
}
