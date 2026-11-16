using System.Runtime.InteropServices;
using Themia.Core.Diagnostics;
using Themia.Core.Files;

namespace Themia.App.Interop;

/// <summary>
/// [S-5565] A few characters of real text as coverage (one byte a pixel), drawn at once with GDI: the
/// number in the badge of a drag's ghost. XAML can only render text asynchronously and from a live
/// tree, and a drag's picture has to be there when the drag starts, so the badge's number is drawn here:
/// white on black into a small bitmap of our own, smoothed in grey (never ClearType, whose colour
/// fringes are no coverage), and its green channel read back. Every handle is released on every path.
/// </summary>
internal static unsafe partial class TextInk
{
    private const string Scope = "text-ink";
    private const string Face = "Segoe UI";
    private const int SemiBold = 600;
    private const uint DefaultCharset = 1;
    private const uint AntialiasedQuality = 4;
    private const int Transparent = 1;
    private const uint White = 0x00FFFFFF;

    /// <summary>The most characters and the tallest text drawn: a badge's number, never a sentence.</summary>
    private const int MaxLength = 8;
    private const int MaxHeight = 96;

    /// <summary>
    /// <paramref name="text"/> in the UI font, semibold, its character height <paramref name="pixelHeight"/>
    /// pixels, tight round its ink sideways; an undrawable ink (logged) when it is too long, too tall or
    /// GDI refuses. Never throws.
    /// </summary>
    public static GhostInk Draw(string text, int pixelHeight)
    {
        if (string.IsNullOrEmpty(text) || text.Length > MaxLength || pixelHeight is < 1 or > MaxHeight)
        {
            return default;
        }

        nint dc = 0;
        nint font = 0;
        nint bitmap = 0;
        nint oldFont = 0;
        nint oldBitmap = 0;
        try
        {
            dc = CreateCompatibleDC(0);

            // Negative height: the character height, which is what a font size means.
            font = dc == 0 ? 0 : CreateFontW(-pixelHeight, 0, 0, 0, SemiBold, 0, 0, 0, DefaultCharset, 0, 0, AntialiasedQuality, 0, Face);
            if (dc == 0 || font == 0)
            {
                Log.Warn(Scope, $"no font or device context for the badge's number (win32 {Marshal.GetLastWin32Error()})");
                return default;
            }

            oldFont = SelectObject(dc, font);
            Size size;
            fixed (char* chars = text)
            {
                if (!GetTextExtentPoint32W(dc, chars, text.Length, &size) || size.Width <= 0 || size.Height <= 0 || size.Width > MaxHeight * MaxLength || size.Height > 2 * MaxHeight)
                {
                    return default;
                }

                var info = new BitmapInfo { Size = (uint)sizeof(BitmapInfo), Width = size.Width, Height = -size.Height, Planes = 1, BitCount = 32 };
                void* bits = null;
                bitmap = CreateDIBSection(dc, &info, 0, &bits, 0, 0);
                if (bitmap == 0 || bits is null)
                {
                    Log.Warn(Scope, $"no bitmap for the badge's number (win32 {Marshal.GetLastWin32Error()})");
                    return default;
                }

                oldBitmap = SelectObject(dc, bitmap);
                new Span<byte>(bits, size.Width * size.Height * 4).Clear();
                _ = SetBkMode(dc, Transparent);
                _ = SetTextColor(dc, White);
                if (!TextOutW(dc, 0, 0, chars, text.Length) || !GdiFlush())
                {
                    return default;
                }

                var drawn = new ReadOnlySpan<byte>(bits, size.Width * size.Height * 4);
                var coverage = new byte[size.Width * size.Height];
                for (var i = 0; i < coverage.Length; i++)
                {
                    coverage[i] = drawn[(i * 4) + 1];
                }

                return new GhostInk(size.Width, size.Height, coverage);
            }
        }
        catch (Exception ex)
        {
            Log.Warn(Scope, "the badge's number could not be drawn", ex);
            return default;
        }
        finally
        {
            if (dc != 0)
            {
                if (oldBitmap != 0)
                {
                    _ = SelectObject(dc, oldBitmap);
                }

                if (oldFont != 0)
                {
                    _ = SelectObject(dc, oldFont);
                }
            }

            if (bitmap != 0)
            {
                _ = Win32.DeleteObject(bitmap);
            }

            if (font != 0)
            {
                _ = Win32.DeleteObject(font);
            }

            if (dc != 0)
            {
                _ = DeleteDC(dc);
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Size
    {
        public int Width;
        public int Height;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfo
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

    [LibraryImport("gdi32.dll", SetLastError = true)]
    private static partial nint CreateCompatibleDC(nint dc);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DeleteDC(nint dc);

    [LibraryImport("gdi32.dll", SetLastError = true)]
    private static partial nint CreateDIBSection(nint dc, BitmapInfo* info, uint usage, void** bits, nint section, uint offset);

    [LibraryImport("gdi32.dll")]
    private static partial nint SelectObject(nint dc, nint obj);

    [LibraryImport("gdi32.dll")]
    private static partial int SetBkMode(nint dc, int mode);

    [LibraryImport("gdi32.dll")]
    private static partial uint SetTextColor(nint dc, uint color);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetTextExtentPoint32W(nint dc, char* text, int length, Size* size);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool TextOutW(nint dc, int x, int y, char* text, int length);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GdiFlush();

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
}
