using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Themia.Core.Diagnostics;
using Themia.Core.Settings;
using Themia.Core.Widgets;

namespace Themia.App.Interop;

/// <summary>
/// [S-1556]..[S-1559] The eyedropper: pick the colour of any pixel on any display.
/// <para>
/// The whole desktop is captured once into a 32-bit DIB, then a topmost popup covering every
/// display shows that frozen picture with a loupe beside the pointer: the pixels around it
/// magnified, the one under it framed, and its hex under the loupe. A click picks; Escape, a right
/// click, another window taking the focus, or a display change cancels. A frozen picture rather than
/// a live read because the overlay would otherwise sample itself, and it is what the user saw the
/// moment they asked.
/// </para>
/// <para>
/// One pixel space: the process is per-monitor DPI aware (v2), so the cursor, the capture and the
/// overlay's client area are all physical pixels across every display, whatever each one's scale.
/// Only the loupe's own size follows the DPI of the display under the pointer. The pure rules are
/// <see cref="ScreenPick"/>.
/// </para>
/// <para>
/// GDI, not XAML: the overlay lives for seconds and paints a bitmap and a few rectangles, so a XAML
/// window and its render thread would cost far more than the job. Every DC, bitmap, brush and font
/// is a <see cref="SafeHandle"/>, released on every path.
/// </para>
/// </summary>
internal static unsafe partial class ScreenColorPicker
{
    private const string Scope = "eyedropper";
    private const string ClassName = "ThemiaScreenColorPicker";

    private const uint WsExTopmost = 0x00000008;
    private const uint WsExToolWindow = 0x00000080;
    private const uint WsPopup = 0x80000000;
    private const int SwShow = 5;

    private const uint WmActivate = 0x0006;
    private const uint WmPaint = 0x000F;
    private const uint WmEraseBkgnd = 0x0014;
    private const uint WmCancelMode = 0x001F;
    private const uint WmDisplayChange = 0x007E;
    private const uint WmNcDestroy = 0x0082;
    private const uint WmKeyDown = 0x0100;
    private const uint WmMouseMove = 0x0200;
    private const uint WmLButtonDown = 0x0201;
    private const uint WmLButtonUp = 0x0202;
    private const uint WmRButtonDown = 0x0204;
    private const uint WmRButtonUp = 0x0205;
    private const uint WmCaptureChanged = 0x0215;
    private const uint WmDpiChanged = 0x02E0;

    private const int VkReturn = 0x0D;
    private const int VkShift = 0x10;
    private const int VkEscape = 0x1B;
    private const int VkSpace = 0x20;
    private const int VkLeft = 0x25;
    private const int VkUp = 0x26;
    private const int VkRight = 0x27;
    private const int VkDown = 0x28;

    private const uint SrcCopy = 0x00CC0020;
    private const uint CaptureBlt = 0x40000000;
    private const int ColorOnColor = 3;
    private const int Transparent = 1;
    private const uint DtCentreSingleLine = 0x0001 | 0x0004 | 0x0020;
    private const int IdcCross = 32515;
    private const uint MonitorDefaultToNearest = 2;
    private const int MdtEffectiveDpi = 0;

    /// <summary>The one pick in progress; a second request while it runs is refused.</summary>
    private static Session? _active;
    private static ushort _atom;

    /// <summary>Whether a pick is on screen now.</summary>
    public static bool IsActive => _active is not null;

    /// <summary>
    /// Capture the desktop and let the user pick a pixel. Completes on the calling (UI) thread's
    /// message loop, never inside the overlay's window procedure.
    /// </summary>
    /// <param name="metrics">The loupe's sizes in DIPs, from the shell's design tokens.</param>
    /// <param name="labelFontDips">The hex label's text size in DIPs.</param>
    public static Task<ScreenPickResult> PickAsync(LoupeMetrics metrics, double labelFontDips)
    {
        if (_active is not null)
        {
            Log.Warn(Scope, "a pick is already on screen; ignoring the second request");
            return Task.FromResult(new ScreenPickResult(ScreenPickOutcome.Cancelled));
        }

        Session? session = null;
        try
        {
            session = new Session(metrics, labelFontDips);
            if (!session.Start())
            {
                session.Dispose();
                return Task.FromResult(new ScreenPickResult(ScreenPickOutcome.Failed));
            }

            return session.Completion;
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "the eyedropper could not start", ex);
            session?.Dispose();
            return Task.FromResult(new ScreenPickResult(ScreenPickOutcome.Failed));
        }
    }

    [UnmanagedCallersOnly]
    private static nint StaticWndProc(nint hwnd, uint msg, nint wParam, nint lParam)
    {
        // Called by Windows: a managed exception must not unwind out of here. A pick that throws is
        // ended as a failure so the overlay never stays over the desktop.
        try
        {
            var session = _active;
            if (session is not null && session.Hwnd == hwnd)
            {
                if (session.Handle(msg, wParam, lParam, out var result))
                {
                    return result;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(Scope, $"the eyedropper window threw handling message 0x{msg:X}", ex);
            _active?.Fail();
        }

        return Win32.DefWindowProc(hwnd, msg, wParam, lParam);
    }

    private static bool EnsureClass(nint instance)
    {
        if (_atom != 0)
        {
            return true;
        }

        fixed (char* className = ClassName)
        {
            var wc = new Win32.WndClassEx
            {
                Size = (uint)sizeof(Win32.WndClassEx),
                WndProc = (nint)(delegate* unmanaged<nint, uint, nint, nint, nint>)&StaticWndProc,
                Instance = instance,
                Cursor = LoadCursorW(0, IdcCross),
                ClassName = (nint)className,
            };

            _atom = Win32.RegisterClassEx(ref wc);
        }

        if (_atom == 0)
        {
            Log.Error(Scope, $"RegisterClassEx failed (win32 {Marshal.GetLastWin32Error()})");
            return false;
        }

        return true;
    }

    /// <summary>One pick: the capture, the overlay window, and where the pointer is.</summary>
    private sealed class Session : IDisposable
    {
        private readonly LoupeMetrics _metrics;
        private readonly double _labelFontDips;
        private readonly TaskCompletionSource<ScreenPickResult> _done = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly Dictionary<uint, GdiObject> _fonts = [];

        private ScreenMonitor[] _monitors = [];
        private PixelBox _capture;
        private DcHandle? _captureDc;
        private GdiObject? _captureBitmap;
        private nint _captureOld;
        private byte* _bits;
        private int _bitsLength;

        private GdiObject? _black;
        private GdiObject? _white;

        private nint _previousForeground;
        private int _x;
        private int _y;
        private LoupeLayout? _shown;
        private bool _wasActive;
        private bool _leftDown;
        private bool _rightDown;
        private bool _finished;
        private bool _disposed;
        private ScreenPickResult _result = new(ScreenPickOutcome.Cancelled);

        public Session(LoupeMetrics metrics, double labelFontDips)
        {
            _metrics = metrics;
            _labelFontDips = labelFontDips;
        }

        public nint Hwnd { get; private set; }

        public Task<ScreenPickResult> Completion => _done.Task;

        /// <summary>Capture, then show the overlay. False (logged, nothing left behind for the caller to see) on failure.</summary>
        public bool Start()
        {
            _monitors = [.. Monitors.All().Select(ToScreenMonitor)];
            _capture = ScreenPick.VirtualScreen(_monitors);
            if (_capture.IsEmpty)
            {
                Log.Error(Scope, "no displays to pick from");
                return false;
            }

            if (!Capture())
            {
                return false;
            }

            _black = GdiObject.Own(CreateSolidBrush(Rgb(0, 0, 0)), "black brush");
            _white = GdiObject.Own(CreateSolidBrush(Rgb(255, 255, 255)), "white brush");
            if (_black is null || _white is null)
            {
                return false;
            }

            var instance = Win32.GetModuleHandle(null);
            if (!EnsureClass(instance))
            {
                return false;
            }

            _previousForeground = GetForegroundWindow();

            Hwnd = Win32.CreateWindowEx(
                WsExTopmost | WsExToolWindow,
                ClassName,
                null,
                WsPopup,
                _capture.Left,
                _capture.Top,
                _capture.Width,
                _capture.Height,
                0,
                0,
                instance,
                0);

            if (Hwnd == 0)
            {
                Log.Error(Scope, $"CreateWindowEx failed (win32 {Marshal.GetLastWin32Error()})");
                return false;
            }

            _active = this;

            if (Win32.GetCursorPos(out var cursor))
            {
                Move(cursor.X, cursor.Y);
            }
            else
            {
                Move(_capture.Left + (_capture.Width / 2), _capture.Top + (_capture.Height / 2));
            }

            _ = Win32.ShowWindow(Hwnd, SwShow);

            // Our own window, shown from a click in our own settings window: Windows lets the
            // foreground pass. Without it the keys (Escape, arrows) go nowhere; the mouse still works.
            if (!Win32.SetForegroundWindow(Hwnd))
            {
                Log.Warn(Scope, "the eyedropper could not take the keyboard; Escape will not cancel, a right click still does");
            }

            Log.Info(Scope, $"picking over {_monitors.Length} display(s), {_capture.Width}x{_capture.Height} px");
            return true;
        }

        private static ScreenMonitor ToScreenMonitor(MonitorTarget target)
        {
            var b = target.Bounds;
            var centre = new Win32.Point { X = b.Left + (b.Width / 2), Y = b.Top + (b.Height / 2) };
            var handle = Win32.MonitorFromPoint(centre, MonitorDefaultToNearest);
            uint dpi = 0;
            if (handle == 0 || Win32.GetDpiForMonitor(handle, MdtEffectiveDpi, out dpi, out _) != 0)
            {
                // 0 reads as 100 %: the loupe is only smaller on that display, the pick is unaffected.
                Log.Warn(Scope, $"no DPI for {target.GdiName}; drawing the loupe there at 100 %");
                dpi = 0;
            }

            return new ScreenMonitor(new PixelBox(b.Left, b.Top, b.Width, b.Height), dpi);
        }

        /// <summary>Copy every display into one top-down 32-bit DIB.</summary>
        private bool Capture()
        {
            var length = (long)_capture.Width * _capture.Height * 4;
            if (length > int.MaxValue)
            {
                Log.Error(Scope, $"the desktop ({_capture.Width}x{_capture.Height}) is too large to capture");
                return false;
            }

            using var screen = ScreenDc.Get();
            if (screen is null)
            {
                return false;
            }

            _captureDc = DcHandle.Own(CreateCompatibleDC(screen.DangerousGetHandle()), "capture DC");
            if (_captureDc is null)
            {
                return false;
            }

            var info = new BitmapInfo
            {
                Header = new BitmapInfoHeader
                {
                    Size = (uint)sizeof(BitmapInfoHeader),
                    Width = _capture.Width,
                    Height = -_capture.Height, // negative: top-down, row 0 is the top of the desktop
                    Planes = 1,
                    BitCount = 32,
                },
            };

            void* bits;
            _captureBitmap = GdiObject.Own(CreateDIBSection(screen.DangerousGetHandle(), &info, 0, &bits, 0, 0), "capture bitmap");
            if (_captureBitmap is null || bits is null)
            {
                return false;
            }

            _bits = (byte*)bits;
            _bitsLength = (int)length;
            _captureOld = SelectObject(_captureDc.DangerousGetHandle(), _captureBitmap.DangerousGetHandle());

            // CAPTUREBLT takes layered windows too (the widgets themselves, tooltips), which is what is on screen.
            if (!BitBlt(_captureDc.DangerousGetHandle(), 0, 0, _capture.Width, _capture.Height, screen.DangerousGetHandle(), _capture.Left, _capture.Top, SrcCopy | CaptureBlt))
            {
                Log.Error(Scope, $"BitBlt from the screen failed (win32 {Marshal.GetLastWin32Error()})");
                return false;
            }

            // The DIB's memory is read directly below; GDI may still be writing it.
            _ = GdiFlush();
            return true;
        }

        private string? HexAt(int x, int y)
            => ScreenPick.HexAt(new ReadOnlySpan<byte>(_bits, _bitsLength), _capture, x, y);

        public bool Handle(uint msg, nint wParam, nint lParam, out nint result)
        {
            result = 0;
            switch (msg)
            {
                case WmPaint:
                    Paint();
                    return true;

                case WmEraseBkgnd:
                    // Everything is painted in WM_PAINT; erasing first would flash.
                    result = 1;
                    return true;

                case WmMouseMove:
                    Move(_capture.Left + LowSigned(lParam), _capture.Top + HighSigned(lParam));
                    return true;

                case WmLButtonDown:
                    _leftDown = true;
                    _ = SetCapture(Hwnd);
                    Move(_capture.Left + LowSigned(lParam), _capture.Top + HighSigned(lParam));
                    return true;

                case WmLButtonUp:
                    // On the release, not the press: the overlay must still be here to take the
                    // button-up, or it lands on whatever is under it and clicks that.
                    if (_leftDown)
                    {
                        Pick(_capture.Left + LowSigned(lParam), _capture.Top + HighSigned(lParam));
                    }

                    return true;

                case WmRButtonDown:
                    _rightDown = true;
                    _ = SetCapture(Hwnd);
                    return true;

                case WmRButtonUp:
                    if (_rightDown)
                    {
                        Finish(new ScreenPickResult(ScreenPickOutcome.Cancelled));
                    }

                    return true;

                case WmCaptureChanged:
                    if (lParam != Hwnd)
                    {
                        _leftDown = _rightDown = false;
                    }

                    return true;

                case WmKeyDown:
                    Key((int)wParam);
                    return true;

                case WmActivate:
                    if ((wParam & 0xFFFF) != 0)
                    {
                        _wasActive = true;
                    }
                    else if (_wasActive)
                    {
                        // Alt+Tab, Win key, a notification taking focus: the user went elsewhere.
                        Finish(new ScreenPickResult(ScreenPickOutcome.Cancelled));
                    }

                    return false;

                case WmCancelMode:
                case WmDisplayChange:
                    // The capture no longer matches the screens, or the system wants modes ended.
                    Finish(new ScreenPickResult(ScreenPickOutcome.Cancelled));
                    return false;

                case WmDpiChanged:
                    // Windows suggests a resize for the new DPI; the overlay is placed in physical
                    // pixels and must keep covering exactly the capture.
                    return true;

                case WmNcDestroy:
                    // Destroyed by us (Finish) or by the system (shutdown): either way the pick is over.
                    Hwnd = 0;
                    Complete();
                    return false;

                default:
                    return false;
            }
        }

        private void Key(int vk)
        {
            switch (vk)
            {
                case VkEscape:
                    Finish(new ScreenPickResult(ScreenPickOutcome.Cancelled));
                    return;

                case VkReturn:
                case VkSpace:
                    Pick(_x, _y);
                    return;

                case VkLeft:
                case VkRight:
                case VkUp:
                case VkDown:
                    // [S-1558] Arrows move the pointer a pixel (Shift: ten) for an exact pick.
                    var step = (GetKeyState(VkShift) & 0x8000) != 0 ? 10 : 1;
                    var x = _x + (vk == VkLeft ? -step : vk == VkRight ? step : 0);
                    var y = _y + (vk == VkUp ? -step : vk == VkDown ? step : 0);
                    if (SetCursorPos(x, y) && Win32.GetCursorPos(out var moved))
                    {
                        // Windows clamps the pointer to the displays; follow where it really is.
                        Move(moved.X, moved.Y);
                    }

                    return;

                default:
                    return;
            }
        }

        private void Pick(int x, int y)
        {
            var hex = HexAt(x, y);
            if (hex is null)
            {
                // A point in a gap between displays: nothing was there to pick.
                Log.Warn(Scope, $"no captured pixel at {x},{y}; the pick is cancelled");
                Finish(new ScreenPickResult(ScreenPickOutcome.Cancelled));
                return;
            }

            Finish(new ScreenPickResult(ScreenPickOutcome.Picked, hex));
        }

        public void Fail() => Finish(new ScreenPickResult(ScreenPickOutcome.Failed));

        private void Finish(ScreenPickResult result)
        {
            if (_finished)
            {
                return;
            }

            _finished = true;
            _result = result;

            if (_leftDown || _rightDown)
            {
                _ = ReleaseCapture();
            }

            if (Hwnd != 0 && !Win32.DestroyWindow(Hwnd))
            {
                Log.Warn(Scope, $"DestroyWindow failed (win32 {Marshal.GetLastWin32Error()})");
            }

            // WM_NCDESTROY normally completes the pick; this covers a window that was never made.
            Complete();
        }

        /// <summary>Release everything and hand the result back. Runs once.</summary>
        private void Complete()
        {
            if (_disposed)
            {
                return;
            }

            _finished = true;
            Dispose();

            if (_previousForeground != 0 && Win32.IsWindow(_previousForeground))
            {
                // ignore: best effort; failing leaves the settings window behind, nothing else.
                _ = Win32.SetForegroundWindow(_previousForeground);
            }

            _done.TrySetResult(_result);
        }

        private void Move(int x, int y)
        {
            var monitor = ScreenPick.MonitorAt(_monitors, x, y);
            if (monitor is null)
            {
                return;
            }

            var layout = ScreenPick.Layout(x, y, monitor.Value, _metrics);
            if (x == _x && y == _y && _shown == layout)
            {
                return;
            }

            if (_shown is { } old)
            {
                Invalidate(old.Bounds);
            }

            _x = x;
            _y = y;
            _shown = layout;
            Invalidate(layout.Bounds);
        }

        private void Invalidate(PixelBox screen)
        {
            if (Hwnd == 0)
            {
                return;
            }

            var client = new Win32.Rect
            {
                Left = screen.Left - _capture.Left,
                Top = screen.Top - _capture.Top,
                Right = screen.Right - _capture.Left,
                Bottom = screen.Bottom - _capture.Top,
            };

            _ = InvalidateRect(Hwnd, &client, false);
        }

        /// <summary>The frozen desktop under the dirty area, then the loupe, through one back buffer so nothing flickers.</summary>
        private void Paint()
        {
            PaintStruct ps;
            var hdc = BeginPaint(Hwnd, &ps);
            try
            {
                if (hdc == 0 || _captureDc is null)
                {
                    return;
                }

                var area = ps.Paint;
                var width = area.Right - area.Left;
                var height = area.Bottom - area.Top;
                if (width <= 0 || height <= 0)
                {
                    return;
                }

                using var back = DcHandle.Own(CreateCompatibleDC(hdc), "back buffer DC");
                using var backBitmap = GdiObject.Own(CreateCompatibleBitmap(hdc, width, height), "back buffer");
                if (back is null || backBitmap is null)
                {
                    // Draw straight to the window rather than leave it blank.
                    _ = BitBlt(hdc, area.Left, area.Top, width, height, _captureDc.DangerousGetHandle(), area.Left, area.Top, SrcCopy);
                    return;
                }

                var b = back.DangerousGetHandle();
                using (new Selection(b, backBitmap.DangerousGetHandle()))
                {
                    _ = BitBlt(b, 0, 0, width, height, _captureDc.DangerousGetHandle(), area.Left, area.Top, SrcCopy);

                    // From here the back buffer is addressed in screen pixels, as the layout is.
                    _ = SetViewportOrgEx(b, -(area.Left + _capture.Left), -(area.Top + _capture.Top), null);
                    if (_shown is { } layout)
                    {
                        DrawLoupe(b, layout);
                    }

                    _ = SetViewportOrgEx(b, 0, 0, null);
                    _ = BitBlt(hdc, area.Left, area.Top, width, height, b, 0, 0, SrcCopy);
                }
            }
            finally
            {
                _ = EndPaint(Hwnd, &ps);
            }
        }

        private void DrawLoupe(nint dc, LoupeLayout layout)
        {
            var black = _black!.DangerousGetHandle();
            var white = _white!.DangerousGetHandle();
            var dpi = ScreenPick.MonitorAt(_monitors, _x, _y)?.Dpi ?? 0;
            var rule = ScreenPick.ToPixels(1, dpi);

            // Off-desktop cells stay black rather than show whatever the buffer held.
            Fill(dc, layout.Loupe, black);
            if (ScreenPick.Source(_capture, _x, _y, layout, _metrics.Cells) is { } source)
            {
                _ = SetStretchBltMode(dc, ColorOnColor);
                _ = StretchBlt(
                    dc,
                    source.Destination.Left,
                    source.Destination.Top,
                    source.Destination.Width,
                    source.Destination.Height,
                    _captureDc!.DangerousGetHandle(),
                    source.Source.Left,
                    source.Source.Top,
                    source.Source.Width,
                    source.Source.Height,
                    SrcCopy);
            }

            // Two rules, dark outside light, so the frame reads on any colour.
            Frame(dc, layout.Loupe, rule, black);
            Frame(dc, Deflate(layout.Loupe, rule), rule, white);
            Frame(dc, layout.Centre, rule, black);
            Frame(dc, Deflate(layout.Centre, rule), rule, white);

            var hex = HexAt(_x, _y);
            if (hex is null || !HexColor.TryParse(hex, out var r, out var g, out var bl))
            {
                Fill(dc, layout.Label, black);
                Frame(dc, layout.Label, rule, white);
                return;
            }

            using (var swatch = GdiObject.Own(CreateSolidBrush(Rgb(r, g, bl)), "label brush"))
            {
                Fill(dc, layout.Label, swatch?.DangerousGetHandle() ?? black);
            }

            Frame(dc, layout.Label, rule, black);
            Frame(dc, Deflate(layout.Label, rule), rule, white);

            var font = FontFor(dpi);
            if (font == 0)
            {
                return;
            }

            var dark = ScreenPick.WantsDarkInk(r, g, bl);
            using (new Selection(dc, font))
            {
                _ = SetBkMode(dc, Transparent);
                _ = SetTextColor(dc, dark ? Rgb(0, 0, 0) : Rgb(255, 255, 255));
                var box = new Win32.Rect { Left = layout.Label.Left, Top = layout.Label.Top, Right = layout.Label.Right, Bottom = layout.Label.Bottom };
                fixed (char* text = hex)
                {
                    _ = DrawTextW(dc, text, hex.Length, &box, DtCentreSingleLine);
                }
            }
        }

        /// <summary>The label font for a DPI, made once per DPI while the pick lasts.</summary>
        private nint FontFor(uint dpi)
        {
            if (_fonts.TryGetValue(dpi, out var cached))
            {
                return cached.DangerousGetHandle();
            }

            // Negative height: the character height, which is what a DIP font size means.
            var font = GdiObject.Own(CreateFontW(-ScreenPick.ToPixels(_labelFontDips, dpi), 0, 0, 0, 600, 0, 0, 0, 1, 0, 0, 5, 0, "Segoe UI"), "label font");
            if (font is null)
            {
                return 0;
            }

            _fonts[dpi] = font;
            return font.DangerousGetHandle();
        }

        private static PixelBox Deflate(PixelBox r, int by)
            => PixelBox.FromEdges(r.Left + by, r.Top + by, r.Right - by, r.Bottom - by);

        private static void Fill(nint dc, PixelBox r, nint brush)
        {
            if (r.IsEmpty)
            {
                return;
            }

            var rect = new Win32.Rect { Left = r.Left, Top = r.Top, Right = r.Right, Bottom = r.Bottom };
            _ = FillRect(dc, &rect, brush);
        }

        private static void Frame(nint dc, PixelBox r, int thickness, nint brush)
        {
            Fill(dc, new PixelBox(r.Left, r.Top, r.Width, thickness), brush);
            Fill(dc, new PixelBox(r.Left, r.Bottom - thickness, r.Width, thickness), brush);
            Fill(dc, new PixelBox(r.Left, r.Top, thickness, r.Height), brush);
            Fill(dc, new PixelBox(r.Right - thickness, r.Top, thickness, r.Height), brush);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            if (ReferenceEquals(_active, this))
            {
                _active = null;
            }

            if (Hwnd != 0)
            {
                var hwnd = Hwnd;
                Hwnd = 0;
                _ = Win32.DestroyWindow(hwnd);
            }

            foreach (var font in _fonts.Values)
            {
                font.Dispose();
            }

            _fonts.Clear();
            _black?.Dispose();
            _white?.Dispose();

            if (_captureDc is not null && _captureOld != 0)
            {
                // The bitmap must leave the DC before either is deleted.
                _ = SelectObject(_captureDc.DangerousGetHandle(), _captureOld);
            }

            _captureBitmap?.Dispose();
            _captureDc?.Dispose();
            _bits = null;
            _bitsLength = 0;
        }
    }

    private static uint Rgb(byte r, byte g, byte b) => r | ((uint)g << 8) | ((uint)b << 16);

    private static int LowSigned(nint value) => (short)(value & 0xFFFF);

    private static int HighSigned(nint value) => (short)((value >> 16) & 0xFFFF);

    /// <summary>Selects a GDI object into a DC for a scope and puts the previous one back.</summary>
    private readonly ref struct Selection
    {
        private readonly nint _dc;
        private readonly nint _old;

        public Selection(nint dc, nint selected)
        {
            _dc = dc;
            _old = SelectObject(dc, selected);
        }

        public void Dispose()
        {
            if (_old != 0)
            {
                _ = SelectObject(_dc, _old);
            }
        }
    }

    /// <summary>A memory DC, deleted with <c>DeleteDC</c>.</summary>
    private sealed class DcHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        private DcHandle(nint handle)
            : base(ownsHandle: true) => SetHandle(handle);

        public static DcHandle? Own(nint handle, string what)
        {
            if (handle == 0)
            {
                Log.Error(Scope, $"could not create the {what} (win32 {Marshal.GetLastWin32Error()})");
                return null;
            }

            return new DcHandle(handle);
        }

        protected override bool ReleaseHandle() => DeleteDC(handle);
    }

    /// <summary>A bitmap, brush or font, deleted with <c>DeleteObject</c>.</summary>
    private sealed class GdiObject : SafeHandleZeroOrMinusOneIsInvalid
    {
        private GdiObject(nint handle)
            : base(ownsHandle: true) => SetHandle(handle);

        public static GdiObject? Own(nint handle, string what)
        {
            if (handle == 0)
            {
                Log.Error(Scope, $"could not create the {what} (win32 {Marshal.GetLastWin32Error()})");
                return null;
            }

            return new GdiObject(handle);
        }

        protected override bool ReleaseHandle() => Win32.DeleteObject(handle);
    }

    /// <summary>The screen's DC, released with <c>ReleaseDC</c>.</summary>
    private sealed class ScreenDc : SafeHandleZeroOrMinusOneIsInvalid
    {
        private ScreenDc(nint handle)
            : base(ownsHandle: true) => SetHandle(handle);

        public static ScreenDc? Get()
        {
            var handle = GetDC(0);
            if (handle == 0)
            {
                Log.Error(Scope, "no screen DC to capture from");
                return null;
            }

            return new ScreenDc(handle);
        }

        protected override bool ReleaseHandle() => ReleaseDC(0, handle) == 1;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfoHeader
    {
        public uint Size;
        public int Width;
        public int Height;
        public ushort Planes;
        public ushort BitCount;
        public uint Compression;
        public uint SizeImage;
        public int XPelsPerMeter;
        public int YPelsPerMeter;
        public uint ClrUsed;
        public uint ClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfo
    {
        public BitmapInfoHeader Header;
        public uint Colors;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PaintStruct
    {
        public nint Hdc;
        public int Erase;
        public Win32.Rect Paint;
        public int Restore;
        public int IncUpdate;
        public fixed byte Reserved[32];
    }

    [LibraryImport("user32.dll")]
    private static partial nint GetDC(nint hwnd);

    [LibraryImport("user32.dll")]
    private static partial int ReleaseDC(nint hwnd, nint dc);

    [LibraryImport("user32.dll", EntryPoint = "LoadCursorW")]
    private static partial nint LoadCursorW(nint instance, nint name);

    [LibraryImport("user32.dll")]
    private static partial nint GetForegroundWindow();

    [LibraryImport("user32.dll")]
    private static partial nint SetCapture(nint hwnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ReleaseCapture();

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetCursorPos(int x, int y);

    [LibraryImport("user32.dll")]
    private static partial short GetKeyState(int vk);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool InvalidateRect(nint hwnd, Win32.Rect* rect, [MarshalAs(UnmanagedType.Bool)] bool erase);

    [LibraryImport("user32.dll")]
    private static partial nint BeginPaint(nint hwnd, PaintStruct* paint);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EndPaint(nint hwnd, PaintStruct* paint);

    [LibraryImport("user32.dll")]
    private static partial int FillRect(nint dc, Win32.Rect* rect, nint brush);

    [LibraryImport("user32.dll")]
    private static partial int DrawTextW(nint dc, char* text, int length, Win32.Rect* rect, uint format);

    [LibraryImport("gdi32.dll", SetLastError = true)]
    private static partial nint CreateCompatibleDC(nint dc);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DeleteDC(nint dc);

    [LibraryImport("gdi32.dll", SetLastError = true)]
    private static partial nint CreateCompatibleBitmap(nint dc, int width, int height);

    [LibraryImport("gdi32.dll", SetLastError = true)]
    private static partial nint CreateDIBSection(nint dc, BitmapInfo* info, uint usage, void** bits, nint section, uint offset);

    [LibraryImport("gdi32.dll")]
    private static partial nint SelectObject(nint dc, nint obj);

    [LibraryImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool BitBlt(nint dest, int x, int y, int width, int height, nint source, int sx, int sy, uint rop);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool StretchBlt(nint dest, int x, int y, int width, int height, nint source, int sx, int sy, int sw, int sh, uint rop);

    [LibraryImport("gdi32.dll")]
    private static partial int SetStretchBltMode(nint dc, int mode);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetViewportOrgEx(nint dc, int x, int y, Win32.Point* previous);

    [LibraryImport("gdi32.dll", SetLastError = true)]
    private static partial nint CreateSolidBrush(uint color);

    [LibraryImport("gdi32.dll", EntryPoint = "CreateFontW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint CreateFontW(
        int height,
        int width,
        int escapement,
        int orientation,
        int weight,
        uint italic,
        uint underline,
        uint strikeOut,
        uint charset,
        uint outPrecision,
        uint clipPrecision,
        uint quality,
        uint pitchAndFamily,
        string face);

    [LibraryImport("gdi32.dll")]
    private static partial int SetBkMode(nint dc, int mode);

    [LibraryImport("gdi32.dll")]
    private static partial uint SetTextColor(nint dc, uint color);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GdiFlush();

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmFlush();

    /// <summary>
    /// Wait for the desktop compositor's next frame: called once the colour flyout has closed and
    /// the settings window has drawn without it, so the capture shows the screen and not the menu.
    /// Event-driven (the compositor's own vblank), not a timer.
    /// </summary>
    public static void WaitForComposedFrame()
    {
        var hr = DwmFlush();
        if (hr != 0)
        {
            Log.Warn(Scope, $"DwmFlush failed (0x{hr:X8}); the capture may still show the closing menu");
        }
    }
}

/// <summary>The eyedropper as the SDK's colour rows ask for it (<see cref="Shell.ColorChoice"/>).</summary>
internal sealed class ScreenEyedropper : Shell.IScreenEyedropper
{
    public bool IsActive => ScreenColorPicker.IsActive;

    public void WaitForComposedFrame() => ScreenColorPicker.WaitForComposedFrame();

    public Task<ScreenPickResult> PickAsync(LoupeMetrics metrics, double labelFontDips) => ScreenColorPicker.PickAsync(metrics, labelFontDips);
}
