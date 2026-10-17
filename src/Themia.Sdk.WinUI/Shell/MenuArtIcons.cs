using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Shell;

namespace Themia.App.Shell;

/// <summary>
/// [S-1754]..[S-1756] File Explorer's two-tone menu pictures, read from the Windows install (never
/// shipped with Themia) and drawn as a menu line's icon in place of its one-colour glyph. Each
/// picture is read and checked once per theme and kept; a Windows without them, high contrast, a
/// disabled line and a glyph that turns round right to left keep the glyph.
/// <para>
/// [S-1757] Each picture is decoded once per theme and raster size into one shared
/// <see cref="SvgImageSource"/> that every menu line showing it reuses, rasterized at the icon slot
/// on its screen: the SVGs carry only a viewBox, and a source with no raster size draws at a
/// screen-sized raster (32 MB per menu line, held until the next collection).
/// </para>
/// </summary>
public static class MenuArtIcons
{
    private const string Scope = "menu-art";

    /// <summary>The menu icon slot: Explorer's pictures are drawn on a 16×16 grid.</summary>
    private const double Size = 16;

    private static readonly Lock Gate = new();
    private static readonly Dictionary<(MenuArt Art, bool Dark), byte[]?> Cache = [];

    /// <summary>The decoded pictures, shared by every line; UI thread only (all of Themia's windows are on it).</summary>
    private static readonly Dictionary<(MenuArt Art, bool Dark, int Pixels), Task<SvgImageSource?>> Sources = [];
    private static readonly Windows.UI.ViewManagement.AccessibilitySettings Accessibility = new();
    private static bool _absentLogged;

    /// <summary>Explorer's picture of <paramref name="glyph"/> as an icon, or null to keep the glyph.</summary>
    public static IconElement? Create(string glyph)
    {
        ArgumentNullException.ThrowIfNull(glyph);

        if (glyph.Length == 0 || char.IsLowSurrogate(glyph[0]) || (char.IsHighSurrogate(glyph[0]) && (glyph.Length < 2 || !char.IsLowSurrogate(glyph[1]))))
        {
            return null;
        }

        if (MenuArtRules.ForGlyph(char.ConvertToUtf32(glyph, 0)) is not { } art)
        {
            return null;
        }

        // High contrast: the glyph takes the user's contrast colours; Explorer's art would not.
        // Right to left: a glyph that turns round has art that would not.
        if (Accessibility.HighContrast
            || (TextDirection.IsRightToLeft(Strings.Current) && TextDirection.MirrorsGlyph(glyph))
            || Bytes(art, dark: false) is null
            || Bytes(art, dark: true) is null)
        {
            return null;
        }

        var icon = new ImageIcon { Width = Size, Height = Size };
        (bool Dark, int Pixels)? shown = null;

        // Drawn once it is in its popup: only then are the theme it draws in and its screen's scale known.
        void Show()
        {
            var dark = icon.ActualTheme == ElementTheme.Dark;
            var pixels = MenuArtRules.RasterPixels(icon.XamlRoot?.RasterizationScale ?? 1);
            if (shown == (dark, pixels) || Bytes(art, dark) is not { } bytes)
            {
                return;
            }

            shown = (dark, pixels);
            _ = ShowAsync(icon, art, dark, pixels, bytes);
        }

        icon.Loaded += (_, _) => Show();
        icon.ActualThemeChanged += (_, _) => Show();
        return icon;
    }

    /// <summary>A menu line's icon for <paramref name="glyph"/>: Explorer's picture of it, else the glyph.</summary>
    public static IconElement IconOrGlyph(string glyph) => Create(glyph) ?? UiLanguage.Mirrored(new FontIcon { Glyph = glyph });

    /// <summary>The checked picture, read once; null when Windows has none or it is not one.</summary>
    private static byte[]? Bytes(MenuArt art, bool dark)
    {
        lock (Gate)
        {
            if (Cache.TryGetValue((art, dark), out var cached))
            {
                return cached;
            }

            var bytes = Read(art, dark);
            Cache[(art, dark)] = bytes;
            return bytes;
        }
    }

    private static byte[]? Read(MenuArt art, bool dark)
    {
        var folder = MenuArtRules.Folder(Environment.GetFolderPath(Environment.SpecialFolder.Windows), dark);
        var path = Path.Combine(folder, MenuArtRules.FileName(art));

        try
        {
            var info = new FileInfo(path);
            if (!info.Exists)
            {
                // Absent, not broken: an older Windows, or one whose Explorer has other art.
                if (!_absentLogged)
                {
                    _absentLogged = true;
                    Log.Info(Scope, $"Explorer's menu pictures are not all on this Windows ({MenuArtRules.FileName(art)} missing); those lines keep their glyphs");
                }

                return null;
            }

            if (info.Length > MenuArtRules.MaxSvgBytes)
            {
                Log.Warn(Scope, $"{path} is {info.Length} bytes, not a menu picture; the glyph stays");
                return null;
            }

            var bytes = File.ReadAllBytes(path);
            if (!MenuArtRules.IsPlausibleSvg(bytes))
            {
                Log.Warn(Scope, $"{path} is not a self-contained SVG; the glyph stays");
                return null;
            }

            return bytes;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Warn(Scope, $"{path} could not be read; the glyph stays", ex);
            return null;
        }
    }

    /// <summary>The shared picture into the icon, decoding it on first use.</summary>
    private static async Task ShowAsync(ImageIcon icon, MenuArt art, bool dark, int pixels, byte[] bytes)
    {
        try
        {
            var key = (art, dark, pixels);
            if (!Sources.TryGetValue(key, out var pending))
            {
                pending = DecodeAsync(art, dark, pixels, bytes);
                Sources[key] = pending;
            }

            if (await pending.ConfigureAwait(true) is { } source)
            {
                icon.Source = source;
            }
        }
        catch (Exception ex)
        {
            // Fire-and-forget from a menu being shown: nothing may escape.
            Log.Warn(Scope, "a menu picture could not be drawn", ex);
        }
    }

    /// <summary>Decode once at the slot's raster; a picture that fails is dropped so later menus keep the glyph.</summary>
    private static async Task<SvgImageSource?> DecodeAsync(MenuArt art, bool dark, int pixels, byte[] bytes)
    {
        try
        {
            using var stream = new Windows.Storage.Streams.InMemoryRandomAccessStream();
            await stream.WriteAsync(bytes.AsBuffer());
            stream.Seek(0);

            // [S-1757] Set before the source: without it the viewBox-only SVG draws at a screen-sized raster.
            var source = new SvgImageSource { RasterizePixelWidth = pixels, RasterizePixelHeight = pixels };
            var status = await source.SetSourceAsync(stream);
            if (status == SvgImageSourceLoadStatus.Success)
            {
                return source;
            }

            Log.Warn(Scope, $"{MenuArtRules.FileName(art)} did not decode ({status}); later menus keep the glyph");
        }
        catch (Exception ex)
        {
            Log.Warn(Scope, $"{MenuArtRules.FileName(art)} could not be decoded; later menus keep the glyph", ex);
        }

        lock (Gate)
        {
            Cache[(art, dark)] = null;
        }

        return null;
    }
}
