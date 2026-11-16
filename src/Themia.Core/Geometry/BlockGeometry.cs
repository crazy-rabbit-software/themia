namespace Themia.Core.Geometry;

/// <summary>
/// [S-1584]..[S-1586] Geometry for several widgets that move as one: a dragged selection, and a
/// placement payload of one or many widgets.
/// <para>
/// A block moves by a single translation. Its bounding box is moved and its top-left corner snapped
/// onto the grid ([S-06], always), or an edge of it flush on the allowed area's ([S-6609]); the move then
/// stops where the first widget would keep less than the visible minimum on the display's allowed
/// area ([S-6608]), and the same delta applies to every rect, so the widgets keep their offsets to
/// each other exactly. Settling each widget on its own would pull a carefully spaced row apart as
/// soon as one of them reached an edge.
/// </para>
/// <para>
/// A block is never resized. Only rects that cannot all stay in reach at once (laid out on a larger
/// allowed area) are then settled one by one, so none of them is ever left out of reach ([S-09]).
/// </para>
/// </summary>
public static class BlockGeometry
{
    /// <summary>The bounding box of some rects; a zero rect for none.</summary>
    public static FracRect Bounds(IReadOnlyList<FracRect> rects)
    {
        ArgumentNullException.ThrowIfNull(rects);

        var left = double.PositiveInfinity;
        var top = double.PositiveInfinity;
        var right = double.NegativeInfinity;
        var bottom = double.NegativeInfinity;

        foreach (var r in rects)
        {
            if (!r.IsFinite)
            {
                continue;
            }

            left = Math.Min(left, r.X);
            top = Math.Min(top, r.Y);
            right = Math.Max(right, r.Right);
            bottom = Math.Max(bottom, r.Bottom);
        }

        return double.IsFinite(left) ? FracRect.FromEdges(left, top, right, bottom) : default;
    }

    /// <summary>
    /// [S-1584] The translation a drag of <paramref name="dxFrac"/>, <paramref name="dyFrac"/> really
    /// applies to a block whose rects started at <paramref name="start"/>: the box's corner snapped to the
    /// grid as a block ([S-06]: always on), then every rect kept in reach of the allowed area ([S-6608]).
    /// <para>
    /// [S-11873] With the display's <paramref name="rasterScale"/> given, the snapped translation is then
    /// put on whole <see cref="GridGeometry.PixelStep"/>s: one offset in whole DIPs that is whole device
    /// pixels, so every card's cell (<see cref="GridGeometry.ToPixels"/>) moves by exactly the same pixels
    /// and the cards' spacing never changes while the block moves or where it lands (owner, 2026-11-15:
    /// "when moving a group around freely the spacing of the widgets inside the group changes slightly").
    /// On a display whose grid pitch is a whole number of those steps (10 DIPs at 100 % and 200 %) this
    /// changes nothing; elsewhere it moves the block less than one step off the line.
    /// </para>
    /// </summary>
    public static Delta Move(IReadOnlyList<FracRect> start, double dxFrac, double dyFrac, ScreenMetrics screen, double? rasterScale = null)
    {
        var delta = Settle(start, dxFrac, dyFrac, screen);
        return rasterScale is not { } scale ? delta : Whole(start, delta, screen, scale);
    }

    /// <summary>
    /// [S-11873] <paramref name="delta"/> as whole pixel steps: the nearest that keeps the block in reach as
    /// <see cref="Settle"/> left it; where no whole step does (a block already at its limit), as it was.
    /// </summary>
    private static Delta Whole(IReadOnlyList<FracRect> start, Delta delta, ScreenMetrics screen, double rasterScale)
    {
        if (screen.Width <= 0 || screen.Height <= 0)
        {
            return delta;
        }

        var step = GridGeometry.PixelStep(rasterScale);
        var (loX, hiX, loY, hiY) = Reach(start, screen);
        return new Delta(Axis(delta.Dx, screen.Width, loX, hiX), Axis(delta.Dy, screen.Height, loY, hiY));

        double Axis(double d, double length, double lo, double hi)
        {
            var dips = Math.Round(d * length / step, MidpointRounding.AwayFromZero) * step;
            foreach (var candidate in new[] { dips, dips - step, dips + step })
            {
                var frac = candidate / length;
                if (frac >= lo - 1e-12 && frac <= hi + 1e-12)
                {
                    return frac;
                }
            }

            return d;
        }
    }

    /// <summary>
    /// [S-1586] The translation that puts the centre of <paramref name="anchor"/> on a point, then
    /// settles the block as <see cref="Move"/> does. The anchor is the whole block's box for a paste
    /// (the block is centred on the pointer, [S-74]) and the grabbed widget for a drag handed to
    /// another display ([S-77]).
    /// </summary>
    public static Delta PlaceAt(IReadOnlyList<FracRect> rects, FracRect anchor, double xFrac, double yFrac, ScreenMetrics screen)
    {
        var dx = double.IsFinite(xFrac) ? xFrac - (anchor.X + (anchor.W / 2)) : 0;
        var dy = double.IsFinite(yFrac) ? yFrac - (anchor.Y + (anchor.H / 2)) : 0;
        return Settle(rects, dx, dy, screen);
    }

    /// <summary>One rect of the block, moved by the block's delta and kept in reach ([S-09]).</summary>
    public static FracRect Apply(FracRect rect, Delta delta, ScreenMetrics screen)
        => GridGeometry.Clamp(rect with { X = rect.X + delta.Dx, Y = rect.Y + delta.Dy }, screen);

    private static Delta Settle(IReadOnlyList<FracRect> rects, double dx, double dy, ScreenMetrics screen)
    {
        var box = Bounds(rects);
        if (!box.IsFinite || !double.IsFinite(dx) || !double.IsFinite(dy))
        {
            return default;
        }

        var x = box.X + dx;
        var y = box.Y + dy;
        var a = screen.Allowed;

        // [S-6609] The box's own edges meet the allowed area's flush; otherwise its corner snaps.
        var (cols, rows) = GridGeometry.GridDims(screen);
        x = GridGeometry.SnapStart(x, box.W, a.X, a.Right, cols, screen.Width);
        y = GridGeometry.SnapStart(y, box.H, a.Y, a.Bottom, rows, screen.Height);

        // [S-6608] The block may hang over any edge until one of its widgets would keep less than
        // the visible minimum on the allowed area: that widget stops the whole block, so none of
        // them is ever pushed out of reach and the offsets never change.
        var (loX, hiX, loY, hiY) = Reach(rects, screen);

        // Rects that cannot all stay in reach at once (laid out on a larger area) settle on the
        // nearest the block allows; Apply then keeps each one in reach on its own.
        return new Delta(GridGeometry.Between(x - box.X, loX, hiX), GridGeometry.Between(y - box.Y, loY, hiY));
    }

    /// <summary>[S-6608] How far the block may move each way before one of its rects keeps less than the visible minimum.</summary>
    private static (double LoX, double HiX, double LoY, double HiY) Reach(IReadOnlyList<FracRect> rects, ScreenMetrics screen)
    {
        var a = screen.Allowed;
        double loX = double.NegativeInfinity, hiX = double.PositiveInfinity;
        double loY = double.NegativeInfinity, hiY = double.PositiveInfinity;
        foreach (var r in rects)
        {
            if (!r.IsFinite)
            {
                continue;
            }

            var (keepX, keepY) = GridGeometry.Keep(screen, r.W, r.H);
            loX = Math.Max(loX, a.X + keepX - r.Right);
            hiX = Math.Min(hiX, a.Right - keepX - r.X);
            loY = Math.Max(loY, a.Y + keepY - r.Bottom);
            hiY = Math.Min(hiY, a.Bottom - keepY - r.Y);
        }

        return (loX, hiX, loY, hiY);
    }

    /// <summary>
    /// [S-11959] With widgets kept apart (<c>Design.WidgetCollisions</c>): the translation a moving block really takes when
    /// it may not land over a widget that stays. <paramref name="wanted"/> is the drag's or the key's (already
    /// <see cref="Move"/>d); <paramref name="last"/> where the block stands now. The block goes all the way where it
    /// lands clear, else slides along whichever axis is clear, else stays. Only the moving widgets are held: one
    /// already lying over another may stay there, and may leave it, but never comes over one it was not over at the
    /// start of the move (<paramref name="start"/>); the widgets that stay are never touched.
    /// </summary>
    public static Delta Apart(IReadOnlyList<FracRect> start, Delta wanted, Delta last, IReadOnlyList<FracRect> others, ScreenMetrics screen)
    {
        ArgumentNullException.ThrowIfNull(start);
        ArgumentNullException.ThrowIfNull(others);

        if (others.Count == 0 || Clear(start, wanted, others, screen))
        {
            return wanted;
        }

        var alongX = wanted with { Dy = last.Dy };
        if (Clear(start, alongX, others, screen))
        {
            return alongX;
        }

        var alongY = wanted with { Dx = last.Dx };
        return Clear(start, alongY, others, screen) ? alongY : last;
    }

    /// <summary>[S-11959] No moving rect lands over a staying one it did not already lie over at the start.</summary>
    private static bool Clear(IReadOnlyList<FracRect> start, Delta delta, IReadOnlyList<FracRect> others, ScreenMetrics screen)
    {
        foreach (var from in start)
        {
            if (!from.IsFinite)
            {
                continue;
            }

            var landed = Apply(from, delta, screen);
            foreach (var other in others)
            {
                if (other.IsFinite && Over(landed, other) && !Over(from, other))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// [S-11959] Two rects share area: touching edges do not, nor a sliver under a pixel's thousandth
    /// (the fractions' rounding).
    /// </summary>
    internal static bool Over(FracRect a, FracRect b)
    {
        const double Slack = 1e-6;
        return Math.Min(a.Right, b.Right) - Math.Max(a.X, b.X) > Slack
            && Math.Min(a.Bottom, b.Bottom) - Math.Max(a.Y, b.Y) > Slack;
    }

    /// <summary>A translation, in fractions of the screen.</summary>
    public readonly record struct Delta(double Dx, double Dy);
}
