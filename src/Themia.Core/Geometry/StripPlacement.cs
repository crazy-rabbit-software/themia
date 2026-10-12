namespace Themia.Core.Geometry;

/// <summary>[S-3708] Which side of the selection the properties strip stands on.</summary>
public enum StripSide
{
    /// <summary>Above the selection's top edge: the first choice.</summary>
    Above,

    /// <summary>Below its bottom edge: no room above.</summary>
    Below,

    /// <summary>Neither side has room (the selection fills the work area's height): over its edge, inside the work area.</summary>
    Over,
}

/// <summary>[S-3708] Where the strip goes: its top-left corner in the surface's DIPs, and the side it took.</summary>
public readonly record struct StripSpot(double X, double Y, StripSide Side);

/// <summary>
/// [S-3708]/[S-3709] Where the properties strip of the selected widgets stands: against the
/// selection's bounding box, above it, or below it where there is no room above, never over the
/// widgets' content while either side has room, always inside the display's work area, and never on
/// the edit bar or the view bar. Pure: the surface measures the strip and asks.
/// </summary>
public static class StripPlacement
{
    /// <summary>[S-3708] The gap between the strip and the selection's painted edge, as a flyout's from its anchor.</summary>
    public const double Gap = 8;

    /// <summary>
    /// [S-3709] The box the strip is anchored to: the union of the selected widgets' painted rects
    /// on this display. Null for none.
    /// </summary>
    public static PixelRect? Bounds(IEnumerable<PixelRect> rects)
    {
        ArgumentNullException.ThrowIfNull(rects);

        double left = double.PositiveInfinity, top = double.PositiveInfinity;
        double right = double.NegativeInfinity, bottom = double.NegativeInfinity;
        foreach (var r in rects)
        {
            if (!(r.Width > 0) || !(r.Height > 0))
            {
                continue;
            }

            left = Math.Min(left, r.X);
            top = Math.Min(top, r.Y);
            right = Math.Max(right, r.X + r.Width);
            bottom = Math.Max(bottom, r.Y + r.Height);
        }

        return double.IsFinite(left) ? PixelRect.FromEdges(left, top, right, bottom) : null;
    }

    /// <summary>
    /// [S-3708] Place a strip of <paramref name="width"/> × <paramref name="height"/> against
    /// <paramref name="target"/> inside <paramref name="area"/>. Centred on the target and slid along
    /// to stay inside the area (its start edge wins when it is wider than the area). Above the
    /// target, <see cref="Gap"/> off its top edge, when that is inside the area and clear of every
    /// rect in <paramref name="avoid"/>; else below, the same gap off its bottom edge, on the same
    /// terms; else over the target's edge on the side with more room (ties go below, away from the
    /// edit bar at the top), pressed into the area.
    /// </summary>
    public static StripSpot Place(PixelRect target, double width, double height, PixelRect area, IReadOnlyList<PixelRect> avoid)
    {
        ArgumentNullException.ThrowIfNull(avoid);

        width = Math.Max(0, width);
        height = Math.Max(0, height);

        var areaRight = area.X + area.Width;
        var areaBottom = area.Y + area.Height;

        var x = target.X + ((target.Width - width) / 2);
        x = Math.Max(area.X, Math.Min(x, areaRight - width));

        var above = target.Y - Gap - height;
        if (above >= area.Y && above + height <= areaBottom && Clear(new PixelRect(x, above, width, height), avoid))
        {
            return new StripSpot(x, above, StripSide.Above);
        }

        var below = target.Y + target.Height + Gap;
        if (below >= area.Y && below + height <= areaBottom && Clear(new PixelRect(x, below, width, height), avoid))
        {
            return new StripSpot(x, below, StripSide.Below);
        }

        var roomAbove = target.Y - area.Y;
        var roomBelow = areaBottom - (target.Y + target.Height);
        var y = roomAbove > roomBelow ? Math.Max(area.Y, above) : Math.Min(areaBottom - height, below);
        y = Math.Max(area.Y, Math.Min(y, areaBottom - height));
        return new StripSpot(x, y, StripSide.Over);
    }

    private static bool Clear(PixelRect strip, IReadOnlyList<PixelRect> avoid)
    {
        foreach (var other in avoid)
        {
            if (Overlaps(strip, other))
            {
                return false;
            }
        }

        return true;
    }

    private static bool Overlaps(PixelRect a, PixelRect b)
        => a.Width > 0 && a.Height > 0 && b.Width > 0 && b.Height > 0
            && a.X < b.X + b.Width && b.X < a.X + a.Width
            && a.Y < b.Y + b.Height && b.Y < a.Y + a.Height;
}
