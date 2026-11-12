using System.Runtime.InteropServices;
using Themia.Core.Desktop;
using Themia.Core.Diagnostics;
using Themia.Core.Geometry;
using Themia.Core.Widgets;

namespace Themia.App.Interop;

/// <summary>[S-2116] What Windows shows on one display: the picture's file (empty for none), its fit, the colour round it, and every display's bounds together (for Span).</summary>
internal readonly record struct WallpaperSetting(string Path, WallpaperFit Fit, RgbColor Background, PixelRect Desktop);

/// <summary>
/// [S-2116] Windows' wallpaper, read through <c>IDesktopWallpaper</c> (shobjidl_core.h): per monitor
/// <c>GetWallpaper</c>, and <c>GetPosition</c>, <c>GetBackgroundColor</c>, <c>GetMonitorRECT</c>.
/// Function-pointer vtables as in the Core Audio interop, so it stays Native AOT clean (no built-in COM
/// interop, no reflection, no <c>Marshal.GetActiveObject</c>); every interface pointer and every
/// string the call allocates is released by a <c>using</c> on every path. Blocking: call it off the UI thread.
/// </summary>
internal static unsafe partial class DesktopWallpaper
{
    private const string Scope = "wallpaper";

    // IDesktopWallpaper: IUnknown (3), then SetWallpaper (3), GetWallpaper (4), GetMonitorDevicePathAt (5),
    // GetMonitorDevicePathCount (6), GetMonitorRECT (7), SetBackgroundColor (8), GetBackgroundColor (9),
    // SetPosition (10), GetPosition (11), ...
    private const int SlotRelease = 2;
    private const int SlotGetWallpaper = 4;
    private const int SlotGetMonitorDevicePathAt = 5;
    private const int SlotGetMonitorDevicePathCount = 6;
    private const int SlotGetMonitorRect = 7;
    private const int SlotGetBackgroundColor = 9;
    private const int SlotGetPosition = 11;

    private const uint ClsCtxAll = 0x17;

    private static readonly Guid ClsidDesktopWallpaper = new("C2CF3110-460E-4FC1-B9D0-8A1C0C9CC4BD");
    private static readonly Guid IidDesktopWallpaper = new("B92B56A9-8B55-4E14-9A89-0199BBB6F93B");

    /// <summary>
    /// The setting for the display whose bounds (virtual screen pixels) are <paramref name="display"/>,
    /// or null when Windows cannot say (the interface is missing, no monitor of Windows' matches): logged.
    /// </summary>
    public static WallpaperSetting? Read(PixelRect display)
    {
        using var apartment = Apartment.Enter();
        if (!apartment.Ready)
        {
            return null;
        }

        nint raw = 0;
        var clsid = ClsidDesktopWallpaper;
        var iid = IidDesktopWallpaper;
        var hr = Win32.CoCreateInstance(&clsid, 0, ClsCtxAll, &iid, &raw);
        using var wallpaper = new ComRef(raw);
        if (hr < 0 || raw == 0)
        {
            Log.Warn(Scope, $"no IDesktopWallpaper (0x{hr:X8}); the cards draw without the blurred wallpaper");
            return null;
        }

        uint count;
        hr = ((delegate* unmanaged[Stdcall]<nint, uint*, int>)Slot(raw, SlotGetMonitorDevicePathCount))(raw, &count);
        if (hr < 0)
        {
            Log.Warn(Scope, $"the wallpaper's monitors could not be counted (0x{hr:X8})");
            return null;
        }

        string? match = null;
        var desktop = default(PixelRect);
        var any = false;
        for (uint i = 0; i < count; i++)
        {
            char* path;
            if (((delegate* unmanaged[Stdcall]<nint, uint, char**, int>)Slot(raw, SlotGetMonitorDevicePathAt))(raw, i, &path) < 0)
            {
                continue;
            }

            using var id = new CoTaskString(path);
            Win32.Rect rect;

            // A monitor Windows remembers but is not attached has no rectangle: skipped.
            if (((delegate* unmanaged[Stdcall]<nint, char*, Win32.Rect*, int>)Slot(raw, SlotGetMonitorRect))(raw, path, &rect) < 0 || rect.Width <= 0)
            {
                continue;
            }

            var bounds = new PixelRect(rect.Left, rect.Top, rect.Width, rect.Height);
            desktop = any ? Union(desktop, bounds) : bounds;
            any = true;
            if (bounds == display)
            {
                match = id.Text;
            }
        }

        if (match is null)
        {
            Log.Debug(Scope, $"no monitor of the wallpaper's is at {display}");
            return null;
        }

        string file;
        fixed (char* monitor = match)
        {
            char* text = null;
            hr = ((delegate* unmanaged[Stdcall]<nint, char*, char**, int>)Slot(raw, SlotGetWallpaper))(raw, monitor, &text);
            using var owned = new CoTaskString(hr >= 0 ? text : null);
            file = owned.Text ?? string.Empty;
        }

        if (hr < 0)
        {
            Log.Warn(Scope, $"the wallpaper of a display could not be read (0x{hr:X8})");
            return null;
        }

        int position;
        if (((delegate* unmanaged[Stdcall]<nint, int*, int>)Slot(raw, SlotGetPosition))(raw, &position) < 0 || !Enum.IsDefined((WallpaperFit)position))
        {
            position = (int)WallpaperFit.Fill;
        }

        uint colour;
        if (((delegate* unmanaged[Stdcall]<nint, uint*, int>)Slot(raw, SlotGetBackgroundColor))(raw, &colour) < 0)
        {
            colour = 0;
        }

        // A COLORREF is 0x00BBGGRR.
        var background = new RgbColor((byte)(colour & 0xFF), (byte)((colour >> 8) & 0xFF), (byte)((colour >> 16) & 0xFF));
        return new WallpaperSetting(file, (WallpaperFit)position, background, desktop);
    }

    private static PixelRect Union(PixelRect a, PixelRect b)
        => PixelRect.FromEdges(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X + a.Width, b.X + b.Width), Math.Max(a.Y + a.Height, b.Y + b.Height));

    private static nint Slot(nint com, int slot) => (*(nint**)com)[slot];

    [LibraryImport("ole32.dll")]
    private static partial void CoTaskMemFree(nint memory);

    /// <summary>An interface pointer, released once.</summary>
    private readonly struct ComRef(nint pointer) : IDisposable
    {
        public void Dispose()
        {
            if (pointer != 0)
            {
                _ = ((delegate* unmanaged[Stdcall]<nint, uint>)Slot(pointer, SlotRelease))(pointer);
            }
        }
    }

    /// <summary>A string the callee allocated with <c>CoTaskMemAlloc</c>, freed once.</summary>
    private readonly struct CoTaskString(char* text) : IDisposable
    {
        public string? Text => text is null ? null : new string(text);

        public void Dispose()
        {
            if (text is not null)
            {
                CoTaskMemFree((nint)text);
            }
        }
    }

    /// <summary>COM on this thread for the call: entered (or already entered by someone else) and left again.</summary>
    private readonly struct Apartment : IDisposable
    {
        private readonly bool _leave;

        private Apartment(bool ready, bool leave)
        {
            Ready = ready;
            _leave = leave;
        }

        public bool Ready { get; }

        public static Apartment Enter()
        {
            var hr = Win32.CoInitializeEx(0, Win32.CoInitMultithreaded);
            if (hr >= 0)
            {
                return new Apartment(true, true);
            }

            if (hr == Win32.RpcEChangedMode)
            {
                // ignore: the thread is in another apartment already, which serves the call as well.
                return new Apartment(true, false);
            }

            Log.Warn(Scope, $"CoInitializeEx failed (0x{hr:X8})");
            return new Apartment(false, false);
        }

        public void Dispose()
        {
            if (_leave)
            {
                Win32.CoUninitialize();
            }
        }
    }
}
