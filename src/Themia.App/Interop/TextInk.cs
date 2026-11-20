using Themia.Core.Diagnostics;
using Themia.Core.Files;

namespace Themia.App.Interop;

/// <summary>
/// [S-5565] A few characters of real text as coverage (one byte a pixel), drawn at once with GDI: the
/// number in the badge of a drag's ghost. XAML can only render text asynchronously and from a live
/// tree, and a drag's picture has to be there when the drag starts, so the badge's number is drawn here:
/// white on black into a small bitmap of our own, smoothed in grey (never ClearType, whose colour
/// fringes are no coverage), and its green channel read back. Every handle is a <see cref="Gdi"/> SafeHandle, every
/// selection a scope: released and put back on every path, in reverse order.
/// </summary>
internal static unsafe class TextInk
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

        try
        {
            using var dc = Gdi.MemoryDc.Create(0, Scope, "badge's device context");

            // Negative height: the character height, which is what a font size means.
            using var font = dc is null ? null : Gdi.GdiObject.Own(
                Gdi.CreateFont(-pixelHeight, 0, 0, 0, SemiBold, 0, 0, 0, DefaultCharset, 0, 0, AntialiasedQuality, 0, Face), Scope, "badge's font");
            if (dc is null || font is null)
            {
                return default;
            }

            var hdc = dc.DangerousGetHandle();
            using var withFont = new Gdi.Selected(hdc, font.DangerousGetHandle());
            Gdi.Size size;
            fixed (char* chars = text)
            {
                if (!Gdi.GetTextExtentPoint32(hdc, chars, text.Length, &size) || size.Width <= 0 || size.Height <= 0 || size.Width > MaxHeight * MaxLength || size.Height > 2 * MaxHeight)
                {
                    return default;
                }

                var info = Gdi.BitmapInfo.TopDown32(size.Width, size.Height);
                void* bits = null;
                using var bitmap = Gdi.GdiObject.Own(Gdi.CreateDIBSection(hdc, &info, 0, &bits, 0, 0), Scope, "badge's bitmap");
                if (bitmap is null || bits is null)
                {
                    return default;
                }

                using var withBitmap = new Gdi.Selected(hdc, bitmap.DangerousGetHandle());
                new Span<byte>(bits, size.Width * size.Height * 4).Clear();
                _ = Gdi.SetBkMode(hdc, Transparent);
                _ = Gdi.SetTextColor(hdc, White);
                if (!Gdi.TextOut(hdc, 0, 0, chars, text.Length) || !Gdi.GdiFlush())
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
    }
}
