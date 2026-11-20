using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Themia.Core.Diagnostics;

namespace Themia.App.Interop;

/// <summary>
/// GDI, declared once: the imports the eyedropper (<see cref="ScreenColorPicker"/>), a drag badge's number
/// (<see cref="TextInk"/>) and the shell's icons (<c>AppIcons</c>) draw and read with, and the handles they own as
/// <see cref="SafeHandle"/>s (<see cref="MemoryDc"/>, <see cref="GdiObject"/>, <see cref="ScreenDc"/>), released on
/// every path; an object selected into a DC is put back by <see cref="Selected"/>. Never a raw handle freed by hand.
/// </summary>
internal static unsafe partial class Gdi
{
    /// <summary>A memory DC (<c>CreateCompatibleDC</c>), deleted with <c>DeleteDC</c>.</summary>
    internal sealed class MemoryDc : SafeHandleZeroOrMinusOneIsInvalid
    {
        private MemoryDc(nint handle)
            : base(ownsHandle: true) => SetHandle(handle);

        /// <summary>A memory DC compatible with <paramref name="dc"/> (0: the screen); null, logged under <paramref name="scope"/>, when GDI refuses.</summary>
        public static MemoryDc? Create(nint dc, string scope, string what)
        {
            var handle = CreateCompatibleDC(dc);
            if (handle == 0)
            {
                Log.Error(scope, $"could not create the {what} (win32 {Marshal.GetLastWin32Error()})");
                return null;
            }

            return new MemoryDc(handle);
        }

        protected override bool ReleaseHandle() => DeleteDC(handle);
    }

    /// <summary>A bitmap, brush or font, deleted with <c>DeleteObject</c>.</summary>
    internal sealed class GdiObject : SafeHandleZeroOrMinusOneIsInvalid
    {
        private GdiObject(nint handle)
            : base(ownsHandle: true) => SetHandle(handle);

        /// <summary>Own <paramref name="handle"/> as GDI made it; null, logged under <paramref name="scope"/>, when it is none.</summary>
        public static GdiObject? Own(nint handle, string scope, string what)
        {
            if (handle == 0)
            {
                Log.Error(scope, $"could not create the {what} (win32 {Marshal.GetLastWin32Error()})");
                return null;
            }

            return new GdiObject(handle);
        }

        protected override bool ReleaseHandle() => DeleteObject(handle);
    }

    /// <summary>The screen's DC (<c>GetDC(0)</c>), released with <c>ReleaseDC</c>.</summary>
    internal sealed class ScreenDc : SafeHandleZeroOrMinusOneIsInvalid
    {
        private ScreenDc(nint handle)
            : base(ownsHandle: true) => SetHandle(handle);

        /// <summary>The screen's DC; null, logged under <paramref name="scope"/>, when there is none.</summary>
        public static ScreenDc? Get(string scope)
        {
            var handle = GetDC(0);
            if (handle == 0)
            {
                Log.Error(scope, "no screen DC");
                return null;
            }

            return new ScreenDc(handle);
        }

        protected override bool ReleaseHandle() => ReleaseDC(0, handle) == 1;
    }

    /// <summary>Selects a GDI object into a DC for a scope and puts the previous one back.</summary>
    internal readonly ref struct Selected
    {
        private readonly nint _dc;
        private readonly nint _old;

        public Selected(nint dc, nint selected)
        {
            _dc = dc;
            _old = SelectObject(dc, selected);
        }

        /// <summary>Whether the object went in (GDI handed back the one it replaced).</summary>
        public bool In => _old != 0;

        public void Dispose()
        {
            if (_old != 0)
            {
                _ = SelectObject(_dc, _old);
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct BitmapInfoHeader
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

    /// <summary>A BITMAPINFO: the header and room for one colour (a 32-bit BI_RGB bitmap has no table, but the documented buffer has one entry).</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct BitmapInfo
    {
        public BitmapInfoHeader Header;
        public uint Color0;

        /// <summary>A top-down (row 0 at the top) 32-bit BI_RGB bitmap of <paramref name="width"/> × <paramref name="height"/>.</summary>
        public static BitmapInfo TopDown32(int width, int height) => new()
        {
            Header = new BitmapInfoHeader
            {
                Size = (uint)sizeof(BitmapInfoHeader),
                Width = width,
                Height = -height,
                Planes = 1,
                BitCount = 32,
            },
        };
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Size
    {
        public int Width;
        public int Height;
    }

    [LibraryImport("user32.dll")]
    private static partial nint GetDC(nint hwnd);

    [LibraryImport("user32.dll")]
    private static partial int ReleaseDC(nint hwnd, nint dc);

    [LibraryImport("gdi32.dll", SetLastError = true)]
    private static partial nint CreateCompatibleDC(nint dc);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DeleteDC(nint dc);

    [LibraryImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool DeleteObject(nint obj);

    [LibraryImport("gdi32.dll")]
    internal static partial nint SelectObject(nint dc, nint obj);

    /// <summary>A rectangular region (a surface cut to its work area): the window owns it once <c>SetWindowRgn</c> took it.</summary>
    [LibraryImport("gdi32.dll", SetLastError = true)]
    internal static partial nint CreateRectRgn(int left, int top, int right, int bottom);

    [LibraryImport("gdi32.dll", SetLastError = true)]
    internal static partial nint CreateCompatibleBitmap(nint dc, int width, int height);

    [LibraryImport("gdi32.dll", SetLastError = true)]
    internal static partial nint CreateDIBSection(nint dc, BitmapInfo* info, uint usage, void** bits, nint section, uint offset);

    [LibraryImport("gdi32.dll", EntryPoint = "GetObjectW")]
    internal static partial int GetObject(nint handle, int size, void* buffer);

    [LibraryImport("gdi32.dll")]
    internal static partial int GetDIBits(nint dc, nint bitmap, uint start, uint lines, void* bits, BitmapInfo* info, uint usage);

    [LibraryImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool BitBlt(nint dest, int x, int y, int width, int height, nint source, int sx, int sy, uint rop);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool StretchBlt(nint dest, int x, int y, int width, int height, nint source, int sx, int sy, int sw, int sh, uint rop);

    [LibraryImport("gdi32.dll")]
    internal static partial int SetStretchBltMode(nint dc, int mode);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetViewportOrgEx(nint dc, int x, int y, Win32.Point* previous);

    [LibraryImport("gdi32.dll", SetLastError = true)]
    internal static partial nint CreateSolidBrush(uint color);

    [LibraryImport("gdi32.dll", EntryPoint = "CreateFontW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    internal static partial nint CreateFont(
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
    internal static partial int SetBkMode(nint dc, int mode);

    [LibraryImport("gdi32.dll")]
    internal static partial uint SetTextColor(nint dc, uint color);

    [LibraryImport("gdi32.dll", EntryPoint = "GetTextExtentPoint32W")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetTextExtentPoint32(nint dc, char* text, int length, Size* size);

    [LibraryImport("gdi32.dll", EntryPoint = "TextOutW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool TextOut(nint dc, int x, int y, char* text, int length);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GdiFlush();
}
