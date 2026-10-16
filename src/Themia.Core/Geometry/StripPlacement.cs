namespace Themia.Core.Geometry;

/// <summary>[S-3708] Which side of the selection the properties panel stands on, on the screen.</summary>
public enum StripSide
{
    /// <summary>Beside the selection's right edge: the first choice left to right.</summary>
    Right,

    /// <summary>Beside its left edge: the first choice right to left, the second otherwise.</summary>
    Left,

    /// <summary>Under its bottom edge: neither side has room.</summary>
    Below,

    /// <summary>Over its top edge: no side and no room below.</summary>
    Above,

    /// <summary>Nowhere has room (the selection fills the work area): over the selection's end corner, inside the work area.</summary>
    Over,
}

/// <summary>[S-3708] Where the panel goes: its top-left corner in the surface's DIPs, and the side it took.</summary>
public readonly record struct StripSpot(double X, double Y, StripSide Side);

/// <summary>
/// [S-3708]/[S-3709] Where the properties panel of the selected widgets stands: beside the
/// selection's bounding box — at its end side (right, or left right to left), else its start side —
/// and only above or below it when neither side has room; never over the widgets while anywhere
/// has room, always inside the display's work area, and never on the edit bar or the view bar.
/// Pure: the surface measures the panel and asks.
/// </summary>
public static class StripPlacement
{
    /// <summary>[S-3708] The gap between the panel and the selection's painted edge, as a flyout's from its anchor.</summary>
    public const double Gap = 8;

    /// <summary>
    /// [S-3709] The box the panel is anchored to: the union of the selected widgets' painted rects
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
    /// [S-3708] Place a panel of <paramref name="width"/> × <paramref name="height"/> against
    /// <paramref name="target"/> inside <paramref name="area"/>, clear of every rect in
    /// <paramref name="avoid"/>. In order:
    /// <list type="number">
    /// <item>Beside the end side (<see cref="StripSide.Right"/>, or <see cref="StripSide.Left"/>
    /// when <paramref name="rightToLeft"/>), <see cref="Gap"/> off the edge, [S-3714] centred on the
    /// target's height and slid down or up to stay inside the area; where that meets a rect to avoid,
    /// just under or over that rect instead.</item>
    /// <item>Beside the start side, on the same terms.</item>
    /// <item>Below the target, the gap off its bottom edge, its start edge level with the target's
    /// and slid along to stay inside the area; then above it.</item>
    /// <item>Over the target's top end corner, pressed into the area: the one case it covers the
    /// widgets, when they leave no room anywhere.</item>
    /// </list>
    /// </summary>
    public static StripSpot Place(PixelRect target, double width, double height, PixelRect area, IReadOnlyList<PixelRect> avoid, bool rightToLeft = false)
    {
        ArgumentNullException.ThrowIfNull(avoid);

        width = Math.Max(0, width);
        height = Math.Max(0, height);

        // [S-6610] A selection hanging over the area's edge ([S-6608]) is anchored by the part of it
        // that shows: beside an edge that is off the screen the panel could never stand.
        target = GridGeometry.Intersect(target, area) ?? target;

        var areaRight = area.X + area.Width;
        var areaBottom = area.Y + area.Height;
        var targetRight = target.X + target.Width;
        var targetBottom = target.Y + target.Height;

        // [S-3714] Beside the selection, its middle level with the selection's middle, as a flyout
        // opened to the side of its anchor stands.
        var centredY = target.Y + ((target.Height - height) / 2);
        var rightX = targetRight + Gap;
        var leftX = target.X - Gap - width;
        StripSide[] sides = rightToLeft ? [StripSide.Left, StripSide.Right] : [StripSide.Right, StripSide.Left];

        foreach (var side in sides)
        {
            var x = side == StripSide.Right ? rightX : leftX;
            if (x < area.X || x + width > areaRight)
            {
                continue;
            }

            if (Beside(x, centredY, width, height, area, avoid) is { } y)
            {
                return new StripSpot(x, y, side);
            }
        }

        // Under or over: the start edge level with the target's, slid along inside the area.
        var alongX = rightToLeft ? targetRight - width : target.X;
        alongX = Math.Max(area.X, Math.Min(alongX, areaRight - width));

        var below = targetBottom + Gap;
        if (below >= area.Y && below + height <= areaBottom && Clear(new PixelRect(alongX, below, width, height), avoid))
        {
            return new StripSpot(alongX, below, StripSide.Below);
        }

        var above = target.Y - Gap - height;
        if (above >= area.Y && above + height <= areaBottom && Clear(new PixelRect(alongX, above, width, height), avoid))
        {
            return new StripSpot(alongX, above, StripSide.Above);
        }

        // Nowhere: inside the target's top end corner, pressed into the area.
        var overX = rightToLeft ? target.X + Gap : targetRight - Gap - width;
        overX = Math.Max(area.X, Math.Min(overX, areaRight - width));
        var overY = Math.Max(area.Y, Math.Min(target.Y + Gap, areaBottom - height));
        return new StripSpot(overX, overY, StripSide.Over);
    }

    /// <summary>
    /// The top of a panel standing in the column at <paramref name="x"/>: level with
    /// <paramref name="preferredY"/>, pressed into the area; where that meets a rect to avoid, just
    /// under it, else just over it. Null when the column has no clear place.
    /// </summary>
    private static double? Beside(double x, double preferredY, double width, double height, PixelRect area, IReadOnlyList<PixelRect> avoid)
    {
        var areaBottom = area.Y + area.Height;
        if (height > area.Height)
        {
            return null;
        }

        double Pressed(double y) => Math.Max(area.Y, Math.Min(y, areaBottom - height));

        var first = Pressed(preferredY);
        var others = new List<double>();
        foreach (var other in avoid)
        {
            others.Add(Pressed(other.Y + other.Height + Gap));
            others.Add(Pressed(other.Y - Gap - height));
        }

        // The nearest clear place to where it would rather stand.
        others.Sort((a, b) => Math.Abs(a - first).CompareTo(Math.Abs(b - first)));
        others.Insert(0, first);

        foreach (var y in others)
        {
            if (Clear(new PixelRect(x, y, width, height), avoid))
            {
                return y;
            }
        }

        return null;
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
