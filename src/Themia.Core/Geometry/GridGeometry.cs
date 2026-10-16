namespace Themia.Core.Geometry;

/// <summary>
/// Pure layout maths: snapping, clamping, resize and placement. No UI types and no I/O, so every
/// rule that decides where a widget ends up is testable without a window.
/// </summary>
public static class GridGeometry
{
    /// <summary>[S-08] Minimum on-screen widget size, in device-independent pixels.</summary>
    public const double MinWidgetPx = 32;

    /// <summary>
    /// Target size of one snap cell, in device-independent pixels.
    /// <para>
    /// A fine ~10px snap, matching the original app. This only sets how many snap lines exist; the
    /// rendered cell is screen/cols, so the pitch follows the display.
    /// </para>
    /// </summary>
    public const double MinCellPx = 10;

    /// <summary>
    /// [S-06] Snap-grid granularity for a screen. The grid is an <b>aid</b>, never storage: it only
    /// rounds the values chosen during an interaction.
    /// </summary>
    public static (int Cols, int Rows) GridDims(ScreenMetrics screen)
    {
        if (!screen.IsUsable)
        {
            return (1, 1);
        }

        var cols = Math.Max(1, (int)Math.Round(screen.Width / MinCellPx));
        var rows = Math.Max(1, (int)Math.Round(screen.Height / MinCellPx));
        return (cols, rows);
    }

    /// <summary>[S-08] The pixel floor expressed as a fraction of the given screen.</summary>
    public static (double MinW, double MinH) MinFraction(ScreenMetrics screen)
    {
        if (!screen.IsUsable)
        {
            return (0.01, 0.01);
        }

        return (Math.Min(1, MinWidgetPx / screen.Width), Math.Min(1, MinWidgetPx / screen.Height));
    }

    /// <summary>
    /// [S-6608] How much of a widget always stays on the allowed area, in DIPs, on each axis: the
    /// [S-08] floor, so a floor-sized widget is never cut at all and a bigger one always leaves a
    /// whole minimum widget's worth to see, grab and drag back. A widget smaller than this (only
    /// possible on an allowed area narrower than the floor) keeps what the area can hold.
    /// </summary>
    public const double MinVisiblePx = MinWidgetPx;

    /// <summary>
    /// [S-6609] How close, in DIPs, a dragged edge must come to an allowed-area edge to land flush
    /// on it while the snap aid is on. Under one grid cell ([S-06b]), so a widget can still sit one
    /// cell in from the edge; enough to catch an edge the grid does not reach (a taskbar's top is
    /// rarely on a grid line), now that the edge no longer stops a drag by itself.
    /// </summary>
    public const double EdgeMagnetPx = 8;

    /// <summary>
    /// [S-09]/[S-6608] Keep a rect reachable: above the [S-08] size floor, never bigger than the
    /// screen's allowed area ([S-841]: the work area, so off the taskbar), and overlapping that area
    /// by at least <see cref="MinVisiblePx"/> on each axis. Past that it may hang over any edge; the
    /// surface clips it there. A rect pushed further slides back just far enough, keeping its size.
    /// Total: a non-finite or inverted input collapses to a valid rect instead of propagating NaN
    /// into storage.
    /// </summary>
    public static FracRect Clamp(FracRect r, ScreenMetrics screen)
    {
        var (minW, minH) = MinFraction(screen);
        var a = screen.Allowed;

        if (!r.IsFinite)
        {
            return new FracRect(a.X, a.Y, minW, minH);
        }

        var w = Math.Clamp(r.W, minW, Math.Max(minW, a.W));
        var h = Math.Clamp(r.H, minH, Math.Max(minH, a.H));
        var (keepX, keepY) = Keep(screen, w, h);

        var x = Between(r.X, a.X + keepX - w, a.Right - keepX);
        var y = Between(r.Y, a.Y + keepY - h, a.Bottom - keepY);

        return new FracRect(x, y, w, h);
    }

    /// <summary>
    /// [S-6608] The overlap, as fractions of the screen, a rect of this size must keep with the
    /// allowed area: <see cref="MinVisiblePx"/>, but never more than the rect or the area itself.
    /// </summary>
    public static (double X, double Y) Keep(ScreenMetrics screen, double w, double h)
    {
        var a = screen.Allowed;
        var (px, py) = screen.IsUsable ? (MinVisiblePx / screen.Width, MinVisiblePx / screen.Height) : (0.01, 0.01);
        return (Math.Max(0, Math.Min(px, Math.Min(w, a.W))), Math.Max(0, Math.Min(py, Math.Min(h, a.H))));
    }

    /// <summary>A clamp that cannot throw: a range left empty by a rounding error yields its low end.</summary>
    internal static double Between(double value, double lo, double hi)
        => Math.Max(lo, Math.Min(value, Math.Max(lo, hi)));

    /// <summary>
    /// [S-6609] A moved edge under the snap aid: flush on an allowed-area edge it is within
    /// <see cref="EdgeMagnetPx"/> of (judged on the raw position, before the grid rounds it away),
    /// else on the nearest of <paramref name="lines"/> grid lines.
    /// </summary>
    internal static double SnapEdge(double raw, double areaStart, double areaEnd, int lines, double screenPx)
        => SnapStart(raw, 0, areaStart, areaEnd, lines, screenPx);

    /// <summary>
    /// [S-6609] A moved rect's start under the snap aid: its start edge flush on the area's start, or
    /// its end edge (start + <paramref name="size"/>) flush on the area's end, when either is within
    /// <see cref="EdgeMagnetPx"/>; else the start snaps to the grid.
    /// </summary>
    internal static double SnapStart(double rawStart, double size, double areaStart, double areaEnd, int lines, double screenPx)
    {
        var magnet = screenPx > 0 ? EdgeMagnetPx / screenPx : 0;
        if (Math.Abs(rawStart - areaStart) <= magnet)
        {
            return areaStart;
        }

        if (Math.Abs(rawStart + size - areaEnd) <= magnet)
        {
            return areaEnd - size;
        }

        return Math.Round(rawStart * lines) / lines;
    }

    /// <summary>[S-06] Round a rect onto the snap grid. Storage stays the raw fraction.</summary>
    public static FracRect Snap(FracRect r, ScreenMetrics screen)
    {
        if (!r.IsFinite)
        {
            return r;
        }

        var (cols, rows) = GridDims(screen);
        var (minW, minH) = MinFraction(screen);

        var w = Math.Max(minW, Math.Round(r.W * cols) / cols);
        var h = Math.Max(minH, Math.Round(r.H * rows) / rows);
        var x = Math.Round(r.X * cols) / cols;
        var y = Math.Round(r.Y * rows) / rows;

        return new FracRect(x, y, w, h);
    }

    /// <summary>
    /// Snap when the aid is enabled (Alt suppresses it mid-drag), then always keep the rect
    /// reachable ([S-09]/[S-6608]). The aid is the grid ([S-06]) with the allowed area's edges as a
    /// magnet ([S-6609]).
    /// <para>
    /// [S-31a] A resize passes its handle. Then only the edges that handle moves are snapped and
    /// limited; the anchored edges stay exactly where they were. Snapping the whole rect instead
    /// rounded the anchored edge too, and clamping the whole rect pushed the widget about instead
    /// of stopping the moved edge.
    /// </para>
    /// </summary>
    public static FracRect Settle(FracRect r, ScreenMetrics screen, bool snapEnabled, ResizeHandle? resizing = null)
    {
        if (!r.IsFinite)
        {
            return Clamp(r, screen);
        }

        var (cols, rows) = GridDims(screen);
        var a = screen.Allowed;

        if (resizing is not { } handle)
        {
            if (!snapEnabled)
            {
                return Clamp(r, screen);
            }

            var snapped = Snap(r, screen);
            return Clamp(snapped with
            {
                X = SnapStart(r.X, snapped.W, a.X, a.Right, cols, screen.Width),
                Y = SnapStart(r.Y, snapped.H, a.Y, a.Bottom, rows, screen.Height),
            }, screen);
        }

        var (minW, minH) = MinFraction(screen);
        var (keepX, keepY) = Keep(screen, double.PositiveInfinity, double.PositiveInfinity);

        double left = r.X, top = r.Y, right = r.Right, bottom = r.Bottom;

        // Each moved edge: snapped, then stopped before the widget outgrows the allowed area or
        // leaves less than the visible minimum on it, and the [S-08] floor last so it always wins.
        if (MovesLeft(handle))
        {
            left = snapEnabled ? SnapEdge(left, a.X, a.Right, cols, screen.Width) : left;
            left = Math.Min(Math.Max(left, right - a.W), a.Right - keepX);
            left = Math.Min(left, right - minW);
        }

        if (MovesRight(handle))
        {
            right = snapEnabled ? SnapEdge(right, a.X, a.Right, cols, screen.Width) : right;
            right = Math.Max(Math.Min(right, left + a.W), a.X + keepX);
            right = Math.Max(right, left + minW);
        }

        if (MovesTop(handle))
        {
            top = snapEnabled ? SnapEdge(top, a.Y, a.Bottom, rows, screen.Height) : top;
            top = Math.Min(Math.Max(top, bottom - a.H), a.Bottom - keepY);
            top = Math.Min(top, bottom - minH);
        }

        if (MovesBottom(handle))
        {
            bottom = snapEnabled ? SnapEdge(bottom, a.Y, a.Bottom, rows, screen.Height) : bottom;
            bottom = Math.Max(Math.Min(bottom, top + a.H), a.Y + keepY);
            bottom = Math.Max(bottom, top + minH);
        }

        // Whatever is still out of reach (an anchored edge the taskbar has since grown over far
        // enough to hide the rest) is the ordinary clamp's to fix.
        return Clamp(FracRect.FromEdges(left, top, right, bottom), screen);
    }

    /// <summary>
    /// [S-841] The work area as a fraction of its monitor: the region new geometry may use. Both
    /// rects are in the same (physical) pixel space. A work area that does not overlap its monitor,
    /// or a degenerate monitor, yields the whole screen: refusing every placement because Windows
    /// reported something odd would be worse than allowing the taskbar band.
    /// </summary>
    public static FracRect AllowedArea(PixelRect monitor, PixelRect workArea)
    {
        if (!(monitor.Width > 0 && monitor.Height > 0) || Intersect(monitor, workArea) is not { } inside)
        {
            return FracRect.Full;
        }

        return NormaliseAllowed(new FracRect(
            (inside.X - monitor.X) / monitor.Width,
            (inside.Y - monitor.Y) / monitor.Height,
            inside.Width / monitor.Width,
            inside.Height / monitor.Height)) ?? FracRect.Full;
    }

    /// <summary>
    /// An allowed area clipped to the screen, or null for "the whole screen" when it is unusable.
    /// </summary>
    public static FracRect? NormaliseAllowed(FracRect r)
    {
        if (!r.IsFinite)
        {
            return null;
        }

        var left = Math.Clamp(r.X, 0, 1);
        var top = Math.Clamp(r.Y, 0, 1);
        var right = Math.Clamp(r.Right, 0, 1);
        var bottom = Math.Clamp(r.Bottom, 0, 1);

        return right > left && bottom > top ? FracRect.FromEdges(left, top, right, bottom) : null;
    }

    /// <summary>[S-841] The allowed area in the screen's own DIPs, for chrome that must stay off the taskbar.</summary>
    public static PixelRect AllowedPixels(ScreenMetrics screen)
    {
        var a = screen.Allowed;
        return new PixelRect(a.X * screen.Width, a.Y * screen.Height, a.W * screen.Width, a.H * screen.Height);
    }

    /// <summary>The overlap of two rects, or null when they do not overlap.</summary>
    public static PixelRect? Intersect(PixelRect a, PixelRect b)
    {
        var left = Math.Max(a.X, b.X);
        var top = Math.Max(a.Y, b.Y);
        var right = Math.Min(a.X + a.Width, b.X + b.Width);
        var bottom = Math.Min(a.Y + a.Height, b.Y + b.Height);

        return right > left && bottom > top ? new PixelRect(left, top, right - left, bottom - top) : null;
    }

    /// <summary>Move a rect by a fractional delta. Size is untouched.</summary>
    public static FracRect Move(FracRect start, double dxFrac, double dyFrac)
        => start with { X = start.X + dxFrac, Y = start.Y + dyFrac };

    /// <summary>
    /// [S-31] Resize by dragging one handle. Every handle anchors the edges it does not move, so a
    /// side handle never shifts the opposite edge or the other axis. Edges that would cross stop at
    /// the [S-08] floor instead of inverting the rect.
    /// </summary>
    public static FracRect Resize(
        FracRect start,
        ResizeHandle handle,
        double dxFrac,
        double dyFrac,
        ScreenMetrics screen)
    {
        var (minW, minH) = MinFraction(screen);

        double left = start.X, top = start.Y, right = start.Right, bottom = start.Bottom;

        if (MovesLeft(handle))
        {
            left = Math.Min(left + dxFrac, right - minW);
        }

        if (MovesRight(handle))
        {
            right = Math.Max(right + dxFrac, left + minW);
        }

        if (MovesTop(handle))
        {
            top = Math.Min(top + dyFrac, bottom - minH);
        }

        if (MovesBottom(handle))
        {
            bottom = Math.Max(bottom + dyFrac, top + minH);
        }

        return FracRect.FromEdges(left, top, right, bottom);
    }

    public static bool MovesLeft(ResizeHandle h)
        => h is ResizeHandle.TopLeft or ResizeHandle.Left or ResizeHandle.BottomLeft;

    public static bool MovesRight(ResizeHandle h)
        => h is ResizeHandle.TopRight or ResizeHandle.Right or ResizeHandle.BottomRight;

    public static bool MovesTop(ResizeHandle h)
        => h is ResizeHandle.TopLeft or ResizeHandle.Top or ResizeHandle.TopRight;

    public static bool MovesBottom(ResizeHandle h)
        => h is ResizeHandle.BottomLeft or ResizeHandle.Bottom or ResizeHandle.BottomRight;

    /// <summary>
    /// [S-11] Placement: centre a default-sized widget on the drop point, then settle it as a drag
    /// does: near an edge it may hang over it, but always keeps the visible minimum on the allowed
    /// area ([S-6608]).
    /// </summary>
    public static FracRect PlaceAt(
        double xFrac,
        double yFrac,
        double defaultWidthPx,
        double defaultHeightPx,
        ScreenMetrics screen,
        bool snapEnabled)
    {
        var size = FracRect.FromPixels(0, 0, defaultWidthPx, defaultHeightPx, screen);
        var raw = new FracRect(xFrac - (size.W / 2), yFrac - (size.H / 2), size.W, size.H);
        return Settle(raw, screen, snapEnabled);
    }

    /// <summary>
    /// [S-04] Resolve a stored fraction to its <b>cell</b> in pixels. The widget gap is deliberately
    /// not applied here, and neither is the allowed area: [S-840] a fraction resolves against the
    /// whole monitor, so a taskbar change never moves or rescales a widget already placed.
    /// <para>
    /// The gap is padding <i>inside</i> the cell, never part of the geometry. Folding it in here
    /// made the stored rect mean something different at every gap setting: the widget's size and
    /// position changed when the gap slider moved, the edit outline traced the shrunken box instead
    /// of the cell, and a placement ghost showed a rect the widget would not actually occupy.
    /// Keeping the cell gap-free means changing the gap changes only how much of the cell is
    /// painted.
    /// </para>
    /// <para>
    /// Edges are rounded to a shared whole-pixel boundary, so adjacent cells meet exactly.
    /// </para>
    /// </summary>
    public static PixelRect ToPixels(FracRect r, ScreenMetrics screen)
    {
        var left = Math.Round(r.X * screen.Width);
        var top = Math.Round(r.Y * screen.Height);
        var right = Math.Round(r.Right * screen.Width);
        var bottom = Math.Round(r.Bottom * screen.Height);

        return new PixelRect(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top));
    }

    /// <summary>
    /// The painted surface inside a cell: inset on every side by half the gap, so two neighbouring
    /// widgets show a full gap between them while each keeps its own cell.
    /// <para>
    /// Never shrinks below a pixel, so an absurd gap on a small widget leaves something to click
    /// rather than nothing.
    /// </para>
    /// </summary>
    public static PixelRect Inset(PixelRect cell, double gapPx)
    {
        if (!double.IsFinite(gapPx) || gapPx <= 0)
        {
            return cell;
        }

        var half = gapPx / 2;
        var width = cell.Width - gapPx;
        var height = cell.Height - gapPx;

        // A gap wider than the cell would invert it; pin to the centre instead.
        if (width < 1)
        {
            half = Math.Max(0, (cell.Width - 1) / 2);
            width = 1;
        }

        var halfY = gapPx / 2;
        if (height < 1)
        {
            halfY = Math.Max(0, (cell.Height - 1) / 2);
            height = 1;
        }

        return new PixelRect(cell.X + half, cell.Y + halfY, width, height);
    }
}

public readonly record struct PixelRect(double X, double Y, double Width, double Height)
{
    public static PixelRect FromEdges(double left, double top, double right, double bottom)
        => new(left, top, right - left, bottom - top);
}
