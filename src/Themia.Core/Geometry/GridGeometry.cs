namespace Themia.Core.Geometry;

/// <summary>
/// Pure layout maths: snapping, clamping, resize and placement. No UI types and no I/O, so every
/// rule that decides where a widget ends up is testable without a window.
/// </summary>
public static class GridGeometry
{
    /// <summary>[S-08] Minimum on-screen widget size, in device-independent pixels: one grid cell (<see cref="Themia.Sdk.WidgetSizing.Floor"/>).</summary>
    public const double MinWidgetPx = Themia.Sdk.WidgetSizing.Floor;

    /// <summary>
    /// Target size of one snap cell, in device-independent pixels.
    /// <para>
    /// A fine ~10px snap, matching the original app. This only sets how many snap lines exist; the
    /// rendered cell is screen/cols, so the pitch follows the display.
    /// </para>
    /// </summary>
    public const double MinCellPx = 10;

    /// <summary>
    /// [S-06] The widget grid's granularity for a screen. Every gesture lands on it (there is no way to
    /// switch it off, [S-06]); storage stays the fraction it lands on.
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

    /// <summary>
    /// [S-06b] [S-12830] The grid's drawn pitch on <paramref name="screen"/>, in DIPs: its width over its columns (about
    /// <see cref="MinCellPx"/>). What a widget's items are whole numbers of; the target for a screen that cannot be read.
    /// </summary>
    public static double CellPitch(ScreenMetrics screen)
        => screen.IsUsable ? screen.Width / GridDims(screen).Cols : MinCellPx;

    /// <summary>
    /// [S-06c] Every this many cells the overlay draws a heavier line, so the fine mesh can be
    /// counted at a glance. Purely visual: snapping stays on every line.
    /// </summary>
    public const int MajorEvery = 5;

    /// <summary>
    /// [S-06c] Whether grid line <paramref name="index"/> (counted from the monitor's top-left edge,
    /// the origin the snap rounds from) is a major line.
    /// </summary>
    public static bool IsMajorLine(int index) => index % MajorEvery == 0;

    /// <summary>
    /// [S-06c] Where to centre an overlay line of <paramref name="widthPx"/> physical pixels so it
    /// covers whole pixels: the grid position <paramref name="dip"/> rounded to a pixel edge (odd
    /// widths) or a pixel centre (even widths), back in DIPs. A non-positive scale counts as 1.
    /// </summary>
    public static double CrispLine(double dip, double scale, int widthPx)
    {
        var s = scale > 0 && double.IsFinite(scale) ? scale : 1;
        var half = (widthPx % 2) / 2.0;
        // [S-11873] Half up, as a card's edge is put on its pixel (DevicePixel), so a line and the edge on it share theirs.
        return (Math.Floor((dip * s) - half + 0.5) + half) / s;
    }

    /// <summary>
    /// [S-06c] The overlay's line widths in physical pixels: the minor line about one DIP (at least a
    /// pixel), the major line half as wide again, but always at least a pixel wider. Twice the width
    /// read as loud at 150 %.
    /// </summary>
    public static (int Minor, int Major) LineWidthsPx(double scale)
    {
        var minor = Math.Max(1, (int)Math.Round(scale > 0 && double.IsFinite(scale) ? scale : 1));
        return (minor, minor + Math.Max(1, minor / 2));
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
    /// [S-6608] How much of a widget always stays on the allowed area, in DIPs, on each axis: a grab's
    /// worth (32) to see, grab and drag back. A widget smaller than this (the [S-08] floor is one grid
    /// cell) is never cut at all; one on an allowed area narrower than this keeps what the area can hold.
    /// </summary>
    public const double MinVisiblePx = 32;

    /// <summary>
    /// [S-6609] How close, in DIPs, a dragged edge must come to an allowed-area edge to land flush
    /// on it. Under one grid cell ([S-06b]), so a widget can still sit one
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
        var (loX, hiX, loY, hiY) = Reach(screen, w, h);

        var x = Between(r.X, loX, hiX);
        var y = Between(r.Y, loY, hiY);

        return new FracRect(x, y, w, h);
    }

    /// <summary>
    /// [S-6608] [S-855] Where a rect of this size may start on each axis and still be in reach: on a side that holds
    /// it (<see cref="Holds"/>: a taskbar's) wholly inside the allowed area; on any other side (the monitor's own edge)
    /// over the edge by all but the visible minimum (<see cref="Keep"/>).
    /// </summary>
    public static (double LoX, double HiX, double LoY, double HiY) Reach(ScreenMetrics screen, double w, double h)
    {
        var a = screen.Allowed;
        var (keepX, keepY) = Keep(screen, w, h);
        var held = Holds(screen);
        return (
            held.Left ? a.X : a.X + keepX - w,
            held.Right ? a.Right - w : a.Right - keepX,
            held.Top ? a.Y : a.Y + keepY - h,
            held.Bottom ? a.Bottom - h : a.Bottom - keepY);
    }

    /// <summary>
    /// [S-6608] The overlap, as fractions of the screen, a rect of this size must keep with the
    /// allowed area: <see cref="MinVisiblePx"/>, but never more than the rect or the area itself. Widgets may
    /// always go over the monitor's own edge ([S-2330]: no setting keeps them whole on it, a taskbar's edge holds them,
    /// [S-855]); this is what stops one being lost off it.
    /// </summary>
    public static (double X, double Y) Keep(ScreenMetrics screen, double w, double h)
    {
        var a = screen.Allowed;
        var (px, py) = screen.IsUsable ? (MinVisiblePx / screen.Width, MinVisiblePx / screen.Height) : (0.01, 0.01);
        return (Math.Max(0, Math.Min(px, Math.Min(w, a.W))), Math.Max(0, Math.Min(py, Math.Min(h, a.H))));
    }

    /// <summary>
    /// [S-855] How far in from the monitor's edge (a fraction) an allowed area's edge must be to be a strip's: an edge
    /// nearer is the monitor's own (rounding of a work area that reserves nothing there).
    /// </summary>
    private const double StripEdge = 1e-7;

    /// <summary>
    /// [S-855] The sides of the allowed area a rect stays wholly inside. A side where the allowed area ends inside
    /// the monitor ends at a strip another program reserved (the taskbar, a dock registered as an appbar), on this
    /// display or on another that shows the same view: what lies past it is hidden there, so nothing hangs over it
    /// (owner, 2026-11-20: "hidden widgets on both screens"). A side that is the monitor's own edge always lets a rect
    /// hang over it ([S-6608]; owner, 2026-11-20: "they should always be able to go over the screen", [S-2330]). An
    /// auto-hidden taskbar reserves nothing, so its edge is the monitor's.
    /// </summary>
    public static EdgeHolds Holds(ScreenMetrics screen) => Strips(screen.Allowed);

    /// <summary>[S-855] The sides of <paramref name="allowed"/> that end at a reserved strip (inside the monitor).</summary>
    public static EdgeHolds Strips(FracRect allowed)
        => new(allowed.X > StripEdge, allowed.Y > StripEdge, allowed.Right < 1 - StripEdge, allowed.Bottom < 1 - StripEdge);

    /// <summary>
    /// [S-855] The allowed area of a view shown on several displays: the part of the screen every one of their
    /// allowed areas holds. Stored fractions resolve on each display against its own whole monitor ([S-840]), so a
    /// rect inside this part is inside each display's work area, whichever edge each one's taskbar is on. None
    /// given: the whole screen; areas that share nothing (no real displays do): the first, never an empty area.
    /// </summary>
    public static FracRect SharedAllowed(IEnumerable<FracRect> areas)
    {
        ArgumentNullException.ThrowIfNull(areas);
        FracRect? first = null;
        double left = 0, top = 0, right = 1, bottom = 1;
        foreach (var area in areas)
        {
            if (NormaliseAllowed(area) is not { } a)
            {
                continue;
            }

            first ??= a;
            left = Math.Max(left, a.X);
            top = Math.Max(top, a.Y);
            right = Math.Min(right, a.Right);
            bottom = Math.Min(bottom, a.Bottom);
        }

        if (first is not { } any)
        {
            return FracRect.Full;
        }

        return right > left && bottom > top ? FracRect.FromEdges(left, top, right, bottom) : any;
    }

    /// <summary>A clamp that cannot throw: a range left empty by a rounding error yields its low end.</summary>
    internal static double Between(double value, double lo, double hi)
        => Math.Max(lo, Math.Min(value, Math.Max(lo, hi)));

    /// <summary>
    /// [S-6609] A moved edge on the grid: flush on an allowed-area edge it is within
    /// <see cref="EdgeMagnetPx"/> of (judged on the raw position, before the grid rounds it away),
    /// else on the nearest of <paramref name="lines"/> grid lines.
    /// </summary>
    internal static double SnapEdge(double raw, double areaStart, double areaEnd, int lines, double screenPx)
        => SnapStart(raw, 0, areaStart, areaEnd, lines, screenPx);

    /// <summary>
    /// [S-6609] A moved rect's start on the grid: its start edge flush on the area's start, or
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

    /// <summary>[S-06] Round a rect onto the grid.</summary>
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
    /// [S-06] Every gesture's rect onto the grid, then kept reachable ([S-09]/[S-6608]): the grid with the
    /// allowed area's edges as a magnet ([S-6609]). There is no free path: the grid is always on.
    /// <para>
    /// [S-31a] A resize passes its handle. Then only the edges that handle moves are snapped and
    /// limited; the anchored edges stay exactly where they were. Snapping the whole rect instead
    /// rounded the anchored edge too, and clamping the whole rect pushed the widget about instead
    /// of stopping the moved edge.
    /// </para>
    /// <para>
    /// [S-2445] A resize passes the widget's sizing's <paramref name="limits"/> too: the moved edge
    /// also stops at the sizing's Min and Max, and on a stepped axis lands on the nearest step (the
    /// steps, not the grid, are its snap); a fixed axis does not move.
    /// </para>
    /// </summary>
    public static FracRect Settle(FracRect r, ScreenMetrics screen, ResizeHandle? resizing = null, SizeLimits? limits = null)
    {
        if (!r.IsFinite)
        {
            return Clamp(r, screen);
        }

        var (cols, rows) = GridDims(screen);
        var a = screen.Allowed;

        if (resizing is not { } handle)
        {
            var snapped = Snap(r, screen);
            return Clamp(snapped with
            {
                X = SnapStart(r.X, snapped.W, a.X, a.Right, cols, screen.Width),
                Y = SnapStart(r.Y, snapped.H, a.Y, a.Bottom, rows, screen.Height),
            }, screen);
        }

        var (minW, minH) = MinFraction(screen);
        var (keepX, keepY) = Keep(screen, double.PositiveInfinity, double.PositiveInfinity);

        // [S-855] A moved edge stops on a taskbar's edge; past the monitor's own it may go ([S-2330]).
        var held = Holds(screen);

        double left = r.X, top = r.Y, right = r.Right, bottom = r.Bottom;

        // Each moved edge: snapped, then stopped before the widget outgrows the allowed area or
        // leaves less than the visible minimum on it, and the [S-08] floor last so it always wins.
        var (snapX, snapY) = (limits is not { Width.Stepped: true }, limits is not { Height.Stepped: true });

        if (MovesLeft(handle))
        {
            left = snapX ? SnapEdge(left, a.X, a.Right, cols, screen.Width) : left;
            left = limits is null ? left : right - limits.Width.Range(right - left);
            left = Math.Min(Math.Max(left, right - a.W), a.Right - keepX);
            left = held.Left ? Math.Max(left, a.X) : left;
            left = Math.Min(left, right - minW);
        }

        if (MovesRight(handle))
        {
            right = snapX ? SnapEdge(right, a.X, a.Right, cols, screen.Width) : right;
            right = limits is null ? right : left + limits.Width.Range(right - left);
            right = Math.Max(Math.Min(right, left + a.W), a.X + keepX);
            right = held.Right ? Math.Min(right, a.Right) : right;
            right = Math.Max(right, left + minW);
        }

        if (MovesTop(handle))
        {
            top = snapY ? SnapEdge(top, a.Y, a.Bottom, rows, screen.Height) : top;
            top = limits is null ? top : bottom - limits.Height.Range(bottom - top);
            top = Math.Min(Math.Max(top, bottom - a.H), a.Bottom - keepY);
            top = held.Top ? Math.Max(top, a.Y) : top;
            top = Math.Min(top, bottom - minH);
        }

        if (MovesBottom(handle))
        {
            bottom = snapY ? SnapEdge(bottom, a.Y, a.Bottom, rows, screen.Height) : bottom;
            bottom = limits is null ? bottom : top + limits.Height.Range(bottom - top);
            bottom = Math.Max(Math.Min(bottom, top + a.H), a.Y + keepY);
            bottom = held.Bottom ? Math.Min(bottom, a.Bottom) : bottom;
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
    /// [S-21] [S-851] The one rectangle a surface's window is cut to: its display's work area, relative
    /// to the window's own corner and never past the window. Both rects are physical screen pixels.
    /// What another program reserved at a screen edge (the taskbar, a dock registered as an appbar) is
    /// outside the work area, so the surface neither paints nor takes input there, whatever its
    /// z-order. A work area that does not overlap the window (Windows mid-change) gives the whole
    /// window, as <see cref="AllowedArea"/> gives the whole screen: the next display pass corrects it.
    /// </summary>
    /// <summary>
    /// [S-852] The window of a display's surface: the display itself, **one pixel short at the foot**, so
    /// it never covers the whole display. A window that does and comes to the front reads as a
    /// full-screen app: Windows' taskbar steps behind it and a dock hides for it (seen 2026-11-14: Karpo
    /// hid its bar whenever a click made the surface the front window; its rule, like the shell's, is
    /// "every edge of the display is inside the window"). The stored fractions still resolve against the
    /// display; the last pixel row is the taskbar's or the display's edge. A display no taller than a
    /// pixel is returned as it is.
    /// </summary>
    public static PixelRect SurfaceWindow(PixelRect display)
        => display.Height > 1 ? display with { Height = display.Height - 1 } : display;

    public static PixelRect SurfaceRegion(PixelRect window, PixelRect workArea)
    {
        var inside = window.Width > 0 && window.Height > 0 ? Intersect(window, workArea) : null;
        return inside is { } r
            ? new PixelRect(r.X - window.X, r.Y - window.Y, r.Width, r.Height)
            : new PixelRect(0, 0, Math.Max(0, window.Width), Math.Max(0, window.Height));
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
        ScreenMetrics screen)
    {
        var size = FracRect.FromPixels(0, 0, defaultWidthPx, defaultHeightPx, screen);
        var raw = new FracRect(xFrac - (size.W / 2), yFrac - (size.H / 2), size.W, size.H);
        return Settle(raw, screen);
    }

    /// <summary>
    /// [S-11873] The whole DIP a cell's edge is put on: half up (never to even), so moving every edge of a
    /// block by the same whole DIPs moves every rounded edge by the same whole DIPs.
    /// </summary>
    public static double WholeDip(double dip) => Math.Floor(dip + 0.5);

    /// <summary>
    /// [S-11873] The device pixel a whole DIP <paramref name="wholeDip"/> lands on at <paramref name="scale"/>,
    /// half up, in DIPs. Rounding to even put 4.5 px on 4 and 7.5 px on 8, so two cards moved by the same
    /// 3 px came out 2 and 4 px further on: a block moved freely changed its spacing.
    /// </summary>
    public static double DevicePixel(double wholeDip, double scale) => Math.Floor((wholeDip * scale) + 0.5) / scale;

    /// <summary>
    /// [S-11873] The fewest whole DIPs that are whole device pixels at <paramref name="scale"/>: 1 at 100 % and
    /// 200 %, 2 at 150 %, 4 at 125 % and 175 %. A block moved by a whole number of these keeps every card's
    /// pixels where they were relative to the others; a scale no small count makes whole takes 1.
    /// </summary>
    public static int PixelStep(double scale)
    {
        var s = double.IsFinite(scale) && scale > 0 ? scale : 1;
        for (var n = 1; n <= 8; n++)
        {
            var px = n * s;
            if (Math.Abs(px - Math.Round(px)) < 1e-6)
            {
                return n;
            }
        }

        return 1;
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
    /// <para>
    /// [S-11872] With the display's <paramref name="rasterScale"/> each edge is then put on a whole
    /// device pixel. A card edge between two device pixels (an odd DIP at 150 %, most at 125 %) was
    /// drawn half by each of two neighbours: their translucent fills, half covering one row each, let
    /// the wallpaper through there, a light line across joined cards even with no border drawn; a layout
    /// that rounds offset and size apart could open or overlap a pixel. Every edge is rounded from the
    /// same whole DIP, so a side two cards share is the same device pixel in both.
    /// </para>
    /// </summary>
    public static PixelRect ToPixels(FracRect r, ScreenMetrics screen, double rasterScale = 1)
    {
        var scale = double.IsFinite(rasterScale) && rasterScale > 0 ? rasterScale : 1;
        double Edge(double dip) => DevicePixel(WholeDip(dip), scale);

        var left = Edge(r.X * screen.Width);
        var top = Edge(r.Y * screen.Height);
        var right = Edge(r.Right * screen.Width);
        var bottom = Edge(r.Bottom * screen.Height);

        return new PixelRect(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top));
    }
}
