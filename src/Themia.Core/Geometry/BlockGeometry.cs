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

    /// <summary>
    /// [S-6608] How far the block may move each way before one of its rects keeps less than the visible minimum;
    /// [S-855] or, against a taskbar's edge, before one of them reaches under it.
    /// </summary>
    private static (double LoX, double HiX, double LoY, double HiY) Reach(IReadOnlyList<FracRect> rects, ScreenMetrics screen)
    {
        double loX = double.NegativeInfinity, hiX = double.PositiveInfinity;
        double loY = double.NegativeInfinity, hiY = double.PositiveInfinity;
        foreach (var r in rects)
        {
            if (!r.IsFinite)
            {
                continue;
            }

            var (startLoX, startHiX, startLoY, startHiY) = GridGeometry.Reach(screen, r.W, r.H);
            loX = Math.Max(loX, startLoX - r.X);
            hiX = Math.Min(hiX, startHiX - r.X);
            loY = Math.Max(loY, startLoY - r.Y);
            hiY = Math.Min(hiY, startHiY - r.Y);
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
    /// <para>
    /// The grid is always on ([S-06]), so a drag's <paramref name="wanted"/> is already whole cells and a key's one
    /// cell: the block slides cell by cell. With the display's <paramref name="rasterScale"/> given (the drag's
    /// whole pixel steps, [S-11873]), an overlap thinner than one such step is no overlap: the steps may leave a
    /// block that far off the grid line its neighbour stands on, and it must still land flush beside it.
    /// </para>
    /// </summary>
    public static Delta Apart(IReadOnlyList<FracRect> start, Delta wanted, Delta last, IReadOnlyList<FracRect> others, ScreenMetrics screen, double? rasterScale = null)
    {
        ArgumentNullException.ThrowIfNull(start);
        ArgumentNullException.ThrowIfNull(others);

        var slack = Slack(screen, rasterScale);
        var alongX = wanted with { Dy = last.Dy };
        var alongY = wanted with { Dx = last.Dx };

        // [S-11890] Only the cards near where any of the three tries lands can hold it: the rest are never looked at.
        others = Near(start, [wanted, alongX, alongY], others, screen, slack);
        if (others.Count == 0 || Clear(start, wanted, others, screen, slack))
        {
            return wanted;
        }

        if (Clear(start, alongX, others, screen, slack))
        {
            return alongX;
        }

        return Clear(start, alongY, others, screen, slack) ? alongY : last;
    }

    /// <summary>
    /// [S-11890] The rects of <paramref name="others"/> that share area with the box every rect of
    /// <paramref name="start"/> lands in under any of <paramref name="deltas"/>: the only ones that can hold the
    /// block (<see cref="Clear"/>). Exact, not a guess: a rect that shares more than <paramref name="slack"/> with a
    /// landed rect shares at least as much with the box round all of them.
    /// </summary>
    internal static IReadOnlyList<FracRect> Near(IReadOnlyList<FracRect> start, ReadOnlySpan<Delta> deltas, IReadOnlyList<FracRect> others, ScreenMetrics screen, (double X, double Y) slack)
    {
        var landed = new List<FracRect>(start.Count * deltas.Length);
        foreach (var delta in deltas)
        {
            foreach (var from in start)
            {
                if (from.IsFinite)
                {
                    landed.Add(Apply(from, delta, screen));
                }
            }
        }

        if (landed.Count == 0)
        {
            return others;
        }

        var box = Bounds(landed);
        var near = new List<FracRect>();
        foreach (var other in others)
        {
            if (other.IsFinite && Over(box, other, slack))
            {
                near.Add(other);
            }
        }

        return near;
    }

    /// <summary>[S-11959] How thin an overlap may be and still count as touching, on each axis, in fractions.</summary>
    internal static (double X, double Y) Slack(ScreenMetrics screen, double? rasterScale)
    {
        const double Rounding = 1e-6;
        if (rasterScale is not { } scale || screen.Width <= 0 || screen.Height <= 0)
        {
            return (Rounding, Rounding);
        }

        // Thinner than one whole pixel step, so just under it.
        var step = GridGeometry.PixelStep(scale) - 1e-3;
        return (Math.Max(Rounding, step / screen.Width), Math.Max(Rounding, step / screen.Height));
    }

    /// <summary>[S-11959] No moving rect lands over a staying one it did not already lie over at the start.</summary>
    internal static bool Clear(IReadOnlyList<FracRect> start, Delta delta, IReadOnlyList<FracRect> others, ScreenMetrics screen, (double X, double Y) slack)
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
                if (other.IsFinite && Over(landed, other, slack) && !Over(from, other, slack))
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
    internal static bool Over(FracRect a, FracRect b) => Over(a, b, (1e-6, 1e-6));

    private static bool Over(FracRect a, FracRect b, (double X, double Y) slack)
        => Math.Min(a.Right, b.Right) - Math.Max(a.X, b.X) > slack.X
            && Math.Min(a.Bottom, b.Bottom) - Math.Max(a.Y, b.Y) > slack.Y;

    /// <summary>A translation, in fractions of the screen.</summary>
    public readonly record struct Delta(double Dx, double Dy);
}
