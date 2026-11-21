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
    /// [S-11892] The translation is the snap's, whole cells, at any scale: a block on the grid's lines lands on them, so
    /// two blocks moved apart and laid side by side meet on one line and paint one body. Each line is drawn on its
    /// whole device pixel (<see cref="GridGeometry.PaintedCell"/>), so cards on one line share its pixel wherever the block
    /// goes ([S-11873]). Until 2026-11-20 the snapped translation was then put on whole device pixels, which on a
    /// screen whose pitch is no whole number of them (the window a pixel short at 100 %, 125 %, 150 % at 2560) left
    /// the block up to half a pixel step off the line: two blocks moved on their own met a pixel or more apart.
    /// </para>
    /// </summary>
    public static Delta Move(IReadOnlyList<FracRect> start, double dxFrac, double dyFrac, ScreenMetrics screen)
        => Settle(start, dxFrac, dyFrac, screen, default);

    /// <summary>
    /// [S-1586] The translation that puts the centre of <paramref name="anchor"/> on a point, then
    /// settles the block as <see cref="Move"/> does. The anchor is the whole block's box for a paste
    /// (the block is centred on the pointer, [S-74]) and the grabbed widget for a drag handed to
    /// another display ([S-77]). [S-855] A placement lands wholly clear of every taskbar of the view's displays
    /// (<see cref="GridGeometry.Land"/>): what a gesture may take over a taskbar's edge, a placement never puts there.
    /// </summary>
    public static Delta PlaceAt(IReadOnlyList<FracRect> rects, FracRect anchor, double xFrac, double yFrac, ScreenMetrics screen)
    {
        var dx = double.IsFinite(xFrac) ? xFrac - (anchor.X + (anchor.W / 2)) : 0;
        var dy = double.IsFinite(yFrac) ? yFrac - (anchor.Y + (anchor.H / 2)) : 0;
        return Settle(rects, dx, dy, screen, GridGeometry.Strips(screen.Allowed));
    }

    /// <summary>One rect of the block, moved by the block's delta and kept in reach ([S-09]).</summary>
    public static FracRect Apply(FracRect rect, Delta delta, ScreenMetrics screen)
        => GridGeometry.Clamp(rect with { X = rect.X + delta.Dx, Y = rect.Y + delta.Dy }, screen);

    /// <summary>[S-11] [S-855] One rect of a placed block, moved by its delta (<see cref="PlaceAt"/>) and landed clear of the taskbars.</summary>
    public static FracRect Land(FracRect rect, Delta delta, ScreenMetrics screen)
        => GridGeometry.Land(rect with { X = rect.X + delta.Dx, Y = rect.Y + delta.Dy }, screen);

    private static Delta Settle(IReadOnlyList<FracRect> rects, double dx, double dy, ScreenMetrics screen, EdgeHolds held)
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

        // [S-6608] The block may hang over any edge, a taskbar's too, until one of its widgets would keep less than
        // the visible minimum on the allowed area: that widget stops the whole block, so none of
        // them is ever pushed out of reach and the offsets never change. [S-855] A placement's held sides stop it whole.
        var (loX, hiX, loY, hiY) = Reach(rects, screen, held);

        // Rects that cannot all stay in reach at once (laid out on a larger area) settle on the
        // nearest the block allows; Apply then keeps each one in reach on its own.
        return new Delta(GridGeometry.Between(x - box.X, loX, hiX), GridGeometry.Between(y - box.Y, loY, hiY));
    }

    /// <summary>
    /// [S-6608] How far the block may move each way before one of its rects keeps less than the visible minimum;
    /// [S-855] or, against a side in <paramref name="held"/> (a placement's taskbar edges), before one of them reaches under it.
    /// </summary>
    private static (double LoX, double HiX, double LoY, double HiY) Reach(IReadOnlyList<FracRect> rects, ScreenMetrics screen, EdgeHolds held)
    {
        double loX = double.NegativeInfinity, hiX = double.PositiveInfinity;
        double loY = double.NegativeInfinity, hiY = double.PositiveInfinity;
        foreach (var r in rects)
        {
            if (!r.IsFinite)
            {
                continue;
            }

            var (startLoX, startHiX, startLoY, startHiY) = GridGeometry.Reach(screen, r.W, r.H, held);
            loX = Math.Max(loX, startLoX - r.X);
            hiX = Math.Min(hiX, startHiX - r.X);
            loY = Math.Max(loY, startLoY - r.Y);
            hiY = Math.Min(hiY, startHiY - r.Y);
        }

        return (loX, hiX, loY, hiY);
    }

    /// <summary>
    /// Two rects share area: touching edges do not, nor a sliver under a pixel's thousandth (the fractions' rounding).
    /// A work-area settle checks it; [S-2463]'s tests pin a block's resize with it.
    /// </summary>
    internal static bool Over(FracRect a, FracRect b)
        => Math.Min(a.Right, b.Right) - Math.Max(a.X, b.X) > Rounding
            && Math.Min(a.Bottom, b.Bottom) - Math.Max(a.Y, b.Y) > Rounding;

    /// <summary>How thin an overlap may be and still count as touching, in fractions: the fractions' rounding.</summary>
    private const double Rounding = 1e-6;

    /// <summary>A translation, in fractions of the screen.</summary>
    public readonly record struct Delta(double Dx, double Dy);
}
