namespace Themia.Core.Settings;

/// <summary>A rectangle in <b>physical</b> screen pixels, edges inclusive on the left/top and exclusive on the right/bottom.</summary>
public readonly record struct PixelBox(int Left, int Top, int Width, int Height)
{
    public int Right => Left + Width;

    public int Bottom => Top + Height;

    public bool IsEmpty => Width <= 0 || Height <= 0;

    public bool Contains(int x, int y) => x >= Left && x < Right && y >= Top && y < Bottom;

    public static PixelBox FromEdges(int left, int top, int right, int bottom)
        => new(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));

    /// <summary>The overlap, or an empty rectangle when there is none.</summary>
    public PixelBox Intersect(PixelBox other)
        => FromEdges(Math.Max(Left, other.Left), Math.Max(Top, other.Top), Math.Min(Right, other.Right), Math.Min(Bottom, other.Bottom));

    /// <summary>The smallest rectangle holding both. An empty side is ignored.</summary>
    public PixelBox Union(PixelBox other)
    {
        if (IsEmpty)
        {
            return other;
        }

        if (other.IsEmpty)
        {
            return this;
        }

        return FromEdges(Math.Min(Left, other.Left), Math.Min(Top, other.Top), Math.Max(Right, other.Right), Math.Max(Bottom, other.Bottom));
    }
}

/// <summary>One display as the eyedropper sees it: its bounds in physical pixels and its effective DPI.</summary>
public readonly record struct ScreenMonitor(PixelBox Bounds, uint Dpi);

/// <summary>
/// The loupe's sizes in DIPs, scaled by the DPI of the display under the pointer. The shell passes
/// its design tokens in, so the overlay is sized from the same ramp as everything else.
/// </summary>
/// <param name="Cells">Pixels shown per side. Odd, so one cell is the pixel under the pointer.</param>
/// <param name="CellDips">How large one magnified pixel is drawn.</param>
/// <param name="OffsetDips">The gap between the pointer and the loupe, so the loupe never hides what is picked.</param>
/// <param name="GapDips">The gap between the loupe and the hex label under it.</param>
/// <param name="LabelHeightDips">The hex label's height.</param>
public readonly record struct LoupeMetrics(int Cells, double CellDips, double OffsetDips, double GapDips, double LabelHeightDips);

/// <summary>Where the loupe and its label go, in physical screen pixels.</summary>
/// <param name="Loupe">The magnified square.</param>
/// <param name="Centre">The cell holding the picked pixel, inside <paramref name="Loupe"/>.</param>
/// <param name="Label">The hex label under (or over) the loupe.</param>
/// <param name="CellPx">One magnified pixel, in physical pixels.</param>
public readonly record struct LoupeLayout(PixelBox Loupe, PixelBox Centre, PixelBox Label, int CellPx)
{
    /// <summary>Everything the loupe paints: what must be repainted when it moves.</summary>
    public PixelBox Bounds => Loupe.Union(Label);
}

/// <summary>Which part of the capture the loupe magnifies, and where it lands.</summary>
/// <param name="Source">In capture-bitmap pixels (0,0 is the capture's top left).</param>
/// <param name="Destination">In physical screen pixels: the part of the loupe those pixels fill.</param>
public readonly record struct LoupeSource(PixelBox Source, PixelBox Destination);

/// <summary>
/// [S-1556]..[S-1559] The pure half of the eyedropper: which display the pointer is on, where the
/// loupe goes on it, which captured pixels it magnifies, and what colour a captured pixel is.
/// <para>
/// Everything is in physical pixels, because the process is per-monitor DPI aware (v2): the cursor,
/// the capture and the overlay all share one pixel space across every display, and only the loupe's
/// own size depends on the DPI of the display it is drawn on.
/// </para>
/// </summary>
public static class ScreenPick
{
    /// <summary>The DPI Windows calls 100 %.</summary>
    public const double BaseDpi = 96;

    /// <summary>The whole desktop the capture covers: every display's bounds together.</summary>
    public static PixelBox VirtualScreen(IReadOnlyList<ScreenMonitor> monitors)
    {
        ArgumentNullException.ThrowIfNull(monitors);

        var all = default(PixelBox);
        foreach (var monitor in monitors)
        {
            all = all.Union(monitor.Bounds);
        }

        return all;
    }

    /// <summary>
    /// The display a point is on, or the nearest one when it falls in a gap between displays of
    /// different sizes (the virtual screen is their bounding box, so such gaps exist). Null with no displays.
    /// </summary>
    public static ScreenMonitor? MonitorAt(IReadOnlyList<ScreenMonitor> monitors, int x, int y)
    {
        ArgumentNullException.ThrowIfNull(monitors);

        ScreenMonitor? nearest = null;
        var best = long.MaxValue;
        foreach (var monitor in monitors)
        {
            if (monitor.Bounds.IsEmpty)
            {
                continue;
            }

            if (monitor.Bounds.Contains(x, y))
            {
                return monitor;
            }

            long dx = Math.Max(0, Math.Max(monitor.Bounds.Left - x, x - (monitor.Bounds.Right - 1)));
            long dy = Math.Max(0, Math.Max(monitor.Bounds.Top - y, y - (monitor.Bounds.Bottom - 1)));
            var distance = (dx * dx) + (dy * dy);
            if (distance < best)
            {
                best = distance;
                nearest = monitor;
            }
        }

        return nearest;
    }

    /// <summary>DIPs to physical pixels at a DPI, never below one pixel. A DPI of 0 (unknown) reads as 100 %.</summary>
    public static int ToPixels(double dips, uint dpi)
        => Math.Max(1, (int)Math.Round(dips * (dpi == 0 ? BaseDpi : dpi) / BaseDpi, MidpointRounding.AwayFromZero));

    /// <summary>
    /// [S-1557] The loupe for a pointer at (<paramref name="x"/>, <paramref name="y"/>) on <paramref name="monitor"/>:
    /// below and right of the pointer, flipped to the other side where that would leave the display,
    /// and held inside it. Sized for that display's DPI, so it looks the same size on every screen.
    /// </summary>
    public static LoupeLayout Layout(int x, int y, ScreenMonitor monitor, LoupeMetrics metrics)
    {
        if (metrics.Cells < 1 || metrics.Cells % 2 == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(metrics), metrics.Cells, "the loupe needs an odd number of cells");
        }

        var cell = ToPixels(metrics.CellDips, monitor.Dpi);
        var side = cell * metrics.Cells;
        var offset = ToPixels(metrics.OffsetDips, monitor.Dpi);
        var gap = ToPixels(metrics.GapDips, monitor.Dpi);
        var labelHeight = ToPixels(metrics.LabelHeightDips, monitor.Dpi);
        var height = side + gap + labelHeight;

        var bounds = monitor.Bounds;
        var left = x + offset;
        if (left + side > bounds.Right)
        {
            left = x - offset - side;
        }

        var top = y + offset;
        if (top + height > bounds.Bottom)
        {
            top = y - offset - height;
        }

        // A display smaller than the loupe (or a pointer in a gap) still gets it at its top left.
        left = Math.Max(bounds.Left, Math.Min(left, bounds.Right - side));
        top = Math.Max(bounds.Top, Math.Min(top, bounds.Bottom - height));

        var loupe = new PixelBox(left, top, side, side);
        var half = metrics.Cells / 2;
        var centre = new PixelBox(left + (half * cell), top + (half * cell), cell, cell);
        var label = new PixelBox(left, top + side + gap, side, labelHeight);
        return new LoupeLayout(loupe, centre, label, cell);
    }

    /// <summary>
    /// [S-1557] The captured pixels around the pointer the loupe shows, clipped to the capture so
    /// the edge of the desktop shows as the loupe's own background rather than as garbage. Null when
    /// none of them were captured.
    /// </summary>
    public static LoupeSource? Source(PixelBox capture, int x, int y, LoupeLayout layout, int cells)
    {
        if (cells < 1 || layout.CellPx < 1)
        {
            return null;
        }

        var half = cells / 2;
        var wanted = new PixelBox(x - half, y - half, cells, cells);
        var shown = wanted.Intersect(capture);
        if (shown.IsEmpty)
        {
            return null;
        }

        var source = new PixelBox(shown.Left - capture.Left, shown.Top - capture.Top, shown.Width, shown.Height);
        var destination = new PixelBox(
            layout.Loupe.Left + ((shown.Left - wanted.Left) * layout.CellPx),
            layout.Loupe.Top + ((shown.Top - wanted.Top) * layout.CellPx),
            shown.Width * layout.CellPx,
            shown.Height * layout.CellPx);
        return new LoupeSource(source, destination);
    }

    /// <summary>
    /// [S-1556] The colour of the captured pixel under a screen point, as <c>#RRGGBB</c>, or null
    /// when the point is outside the capture or the buffer is too short for it.
    /// <paramref name="bgra"/> is a top-down 32-bit DIB: blue, green, red, then an unused byte.
    /// </summary>
    public static string? HexAt(ReadOnlySpan<byte> bgra, PixelBox capture, int x, int y)
    {
        if (!capture.Contains(x, y))
        {
            return null;
        }

        var offset = ((long)(y - capture.Top) * capture.Width * 4) + ((long)(x - capture.Left) * 4);
        if (offset < 0 || offset + 3 > bgra.Length)
        {
            return null;
        }

        var i = (int)offset;
        return Widgets.HexColor.From(bgra[i + 2], bgra[i + 1], bgra[i]);
    }

    /// <summary>
    /// [S-1557] Whether text and outlines drawn on a colour should be dark. The same luma rule the
    /// colour swatches' check uses.
    /// </summary>
    public static bool WantsDarkInk(byte r, byte g, byte b) => (0.299 * r) + (0.587 * g) + (0.114 * b) > 150;
}
