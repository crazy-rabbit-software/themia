using System.Runtime.InteropServices;
using Themia.Core.Diagnostics;

namespace Themia.App.Interop;

/// <summary>
/// Turns a plain WinUI window into a desktop widget surface: no taskbar entry, never steals focus,
/// parked in the requested z-band, and — critically — click-through everywhere except the widgets
/// themselves.
/// </summary>
internal static class DesktopLayer
{
    private const string Scope = "desktop-layer";

    /// <summary>
    /// Apply the window styles a widget surface needs.
    /// <para>
    /// <c>WS_EX_NOACTIVATE</c> is what makes this feel like part of the desktop rather than an app:
    /// clicking a widget neither focuses it nor raises it over whatever the user is working in.
    /// <c>WS_EX_TOOLWINDOW</c> keeps it out of the taskbar and Alt-Tab. In edit mode the surface has
    /// to accept keyboard input, so NOACTIVATE is lifted for the duration.
    /// </para>
    /// <para>
    /// Styles only. Where the surface sits in the z-order — including the brief lift while Windows is
    /// showing the desktop — belongs to <see cref="DesktopZOrder"/>, which is the one place that
    /// decides it. Touching <c>WS_EX_TOPMOST</c> here as well would mean two things fighting over one
    /// bit, and the loser is whichever ran first.
    /// </para>
    /// </summary>
    public static void Apply(nint hwnd, bool interactive)
    {
        if (hwnd == 0 || !Win32.IsWindow(hwnd))
        {
            return;
        }

        var ex = (uint)Win32.GetWindowLongPtr(hwnd, Win32.GwlExStyle);

        ex |= Win32.WsExToolWindow;
        ex &= ~Win32.WsExAppWindow;

        if (interactive)
        {
            ex &= ~Win32.WsExNoActivate;
        }
        else
        {
            ex |= Win32.WsExNoActivate;
        }

        if (Win32.SetWindowLongPtr(hwnd, Win32.GwlExStyle, (nint)ex) == 0 && Marshal.GetLastWin32Error() != 0)
        {
            Log.Warn(Scope, $"SetWindowLongPtr(GWL_EXSTYLE) failed (win32 {Marshal.GetLastWin32Error()})");
        }

        // Changing the activation style is exactly the kind of thing that reshuffles a window, so
        // let the controller put it back where it belongs.
        DesktopZOrder.Reconcile();
    }

    /// <summary>
    /// Restrict the window to the union of the given rects, so clicks anywhere else land on the
    /// desktop underneath instead of on an invisible full-screen pane.
    /// <para>
    /// Rects are in <b>physical</b> pixels relative to the window's top-left — the <i>window</i>, not
    /// the client area. Those differ by the frame, so a caller working in client space has to add the
    /// client origin first (see <c>DesktopWindow.ClientToWindowOffset</c>) or the whole mask sits
    /// shifted from the widgets it is supposed to trace. Passing an empty list hides the surface
    /// entirely (an empty view really should not swallow the desktop).
    /// </para>
    /// <para>
    /// [S-2245] A piece may round its corners (a card's round ones, square where a joined neighbour
    /// squares them), and the union is cut to <paramref name="clip"/> last, so a card the clip cuts
    /// keeps the corners of its whole shape.
    /// </para>
    /// </summary>
    public static void SetHitRegion(nint hwnd, IReadOnlyList<RegionPiece> pieces, Win32.Rect clip)
    {
        if (hwnd == 0 || !Win32.IsWindow(hwnd))
        {
            return;
        }

        // Built as a union, then handed to the window. SetWindowRgn takes ownership on success, so
        // the handle is only ours to free while we still hold it, and on every failure path.
        var combined = Win32.CreateRectRgn(0, 0, 0, 0);
        if (combined == 0)
        {
            Log.Error(Scope, "CreateRectRgn failed; leaving the previous hit region in place");
            return;
        }

        try
        {
            foreach (var p in pieces)
            {
                if (p.Rect.Width <= 0 || p.Rect.Height <= 0)
                {
                    continue;
                }

                var piece = Shape(p);
                if (piece == 0)
                {
                    Log.Warn(Scope, "building a widget's region failed; it will not be clickable");
                    continue;
                }

                try
                {
                    Win32.CombineRgn(combined, combined, piece, Win32.RgnOr);
                }
                finally
                {
                    Win32.DeleteObject(piece);
                }
            }

            Combine(combined, Win32.CreateRectRgn(clip.Left, clip.Top, clip.Right, clip.Bottom), Win32.RgnAnd);

            if (Win32.SetWindowRgn(hwnd, combined, redraw: true) == 0)
            {
                Log.Warn(Scope, $"SetWindowRgn failed (win32 {Marshal.GetLastWin32Error()})");
                Win32.DeleteObject(combined);
            }

            // Success: the window owns `combined` now. Deleting it here would free a live region.
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "building the hit region threw", ex);
            Win32.DeleteObject(combined);
        }
    }

    /// <summary>
    /// [S-2245] One piece's region: its rect, each round corner's square taken out and the quarter of
    /// a circle of that radius put back. Zero when GDI failed (logged by the caller).
    /// </summary>
    private static nint Shape(RegionPiece p)
    {
        var r = p.Rect;
        var region = Win32.CreateRectRgn(r.Left, r.Top, r.Right, r.Bottom);
        if (region == 0)
        {
            return 0;
        }

        // A radius larger than half the side would turn the circle outside the rect.
        var most = Math.Min(r.Width, r.Height) / 2;
        int Clamp(double radius) => (int)Math.Clamp(Math.Round(radius), 0, most);
        var corners = new (int Radius, int X, int Y)[]
        {
            (Clamp(p.Corners.TopLeft), r.Left, r.Top),
            (Clamp(p.Corners.TopRight), r.Right, r.Top),
            (Clamp(p.Corners.BottomRight), r.Right, r.Bottom),
            (Clamp(p.Corners.BottomLeft), r.Left, r.Bottom),
        };

        // Every square out first, then every circle in: a circle never reaches outside its own corner's round shape.
        foreach (var (radius, x, y) in corners)
        {
            if (radius > 0)
            {
                var left = x == r.Left ? x : x - radius;
                var top = y == r.Top ? y : y - radius;
                Combine(region, Win32.CreateRectRgn(left, top, left + radius, top + radius), Win32.RgnDiff);
            }
        }

        foreach (var (radius, x, y) in corners)
        {
            if (radius > 0)
            {
                var left = x == r.Left ? x : x - (2 * radius);
                var top = y == r.Top ? y : y - (2 * radius);
                Combine(region, Win32.CreateEllipticRgn(left, top, left + (2 * radius), top + (2 * radius)), Win32.RgnOr);
            }
        }

        return region;
    }

    /// <summary>Combine <paramref name="other"/> into <paramref name="into"/> and free it; a piece GDI could not make is logged and left out.</summary>
    private static void Combine(nint into, nint other, int mode)
    {
        if (other == 0)
        {
            Log.Warn(Scope, "a region piece could not be created; the hit region may be off by its corner");
            return;
        }

        try
        {
            Win32.CombineRgn(into, into, other, mode);
        }
        finally
        {
            Win32.DeleteObject(other);
        }
    }

    /// <summary>
    /// Strip the window frame at the Win32 level.
    /// <para>
    /// <c>OverlappedPresenter.SetBorderAndTitleBar(false, false)</c> does <b>not</b> clear
    /// <c>WS_CAPTION</c> here: the surface kept a caption and a 3px border the whole time, which the
    /// click-through region happened to clip away. The moment edit mode dropped the region the title
    /// bar became visible, and the border meant client coordinates were offset from window
    /// coordinates, so the mask never quite lined up with the widgets either.
    /// </para>
    /// <para>
    /// Re-applied whenever the styles are touched, because toggling activation makes the presenter
    /// reassert its own frame.
    /// </para>
    /// </summary>
    public static void MakeChromeless(nint hwnd)
    {
        if (hwnd == 0 || !Win32.IsWindow(hwnd))
        {
            return;
        }

        var style = (uint)Win32.GetWindowLongPtr(hwnd, Win32.GwlStyle);
        var wanted = (style & ~(Win32.WsCaption | Win32.WsThickFrame | Win32.WsSysMenu | Win32.WsMinimizeBox | Win32.WsMaximizeBox))
            | Win32.WsPopup
            | Win32.WsClipChildren;

        if (style == wanted)
        {
            return;
        }

        Win32.SetWindowLongPtr(hwnd, Win32.GwlStyle, (nint)wanted);

        RemoveDwmBorder(hwnd);

        // SWP_FRAMECHANGED is required here, and only here: the frame genuinely changed, so the
        // non-client area has to be recomputed for the new style to take effect.
        Win32.SetWindowPos(
            hwnd,
            0,
            0, 0, 0, 0,
            Win32.SwpNoMove | Win32.SwpNoSize | Win32.SwpNoZOrder | Win32.SwpNoActivate | Win32.SwpFrameChanged);
    }

    /// <summary>
    /// Tell DWM not to draw its border.
    /// <para>
    /// Windows 11 outlines every top-level window with a 1px border, drawn by the compositor rather
    /// than by the window, so clearing <c>WS_BORDER</c> does not remove it. On a transparent
    /// full-screen surface that border traces a rectangle around the entire display.
    /// </para>
    /// </summary>
    public static void RemoveDwmBorder(nint hwnd)
    {
        if (hwnd == 0 || !Win32.IsWindow(hwnd))
        {
            return;
        }

        var none = Win32.DwmwaColorNone;
        var hr = Win32.DwmSetWindowAttribute(hwnd, Win32.DwmwaBorderColor, in none, sizeof(uint));

        if (hr < 0)
        {
            // Older Windows 10 builds do not know this attribute. Not fatal: those builds also do
            // not draw the border this removes.
            Log.Debug(Scope, $"DwmSetWindowAttribute(BORDER_COLOR) returned 0x{hr:X8}");
        }
    }
}

/// <summary>[S-2245] A piece of the hit region in physical window pixels, with its corners' radii (0: square).</summary>
internal readonly record struct RegionPiece(Win32.Rect Rect, Themia.Core.Widgets.HitCorners Corners)
{
    public static RegionPiece Plain(Win32.Rect rect) => new(rect, Themia.Core.Widgets.HitCorners.Square);
}
