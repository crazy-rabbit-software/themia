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
/// </summary>
internal static class MenuArtIcons
{
    private const string Scope = "menu-art";

    /// <summary>The menu icon slot: Explorer's pictures are drawn on a 16×16 grid.</summary>
    private const double Size = 16;

    private static readonly Lock Gate = new();
    private static readonly Dictionary<(MenuArt Art, bool Dark), byte[]?> Cache = [];
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
        bool? shown = null;

        void Show(bool dark)
        {
            if (shown == dark || Bytes(art, dark) is not { } bytes)
            {
                return;
            }

            shown = dark;
            _ = LoadAsync(icon, art, dark, bytes);
        }

        // The app's theme until the icon is in its popup, then the theme it actually draws in.
        Show(Application.Current?.RequestedTheme == ApplicationTheme.Dark);
        icon.Loaded += (_, _) => Show(icon.ActualTheme == ElementTheme.Dark);
        icon.ActualThemeChanged += (_, _) => Show(icon.ActualTheme == ElementTheme.Dark);
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

    /// <summary>Decode into the icon; a picture that fails is dropped from the cache so later menus keep the glyph.</summary>
    private static async Task LoadAsync(ImageIcon icon, MenuArt art, bool dark, byte[] bytes)
    {
        try
        {
            using var stream = new Windows.Storage.Streams.InMemoryRandomAccessStream();
            await stream.WriteAsync(bytes.AsBuffer());
            stream.Seek(0);

            // No raster size: the image draws the vector at its own size and the screen's scale.
            var source = new SvgImageSource();
            var status = await source.SetSourceAsync(stream);
            if (status != SvgImageSourceLoadStatus.Success)
            {
                Log.Warn(Scope, $"{MenuArtRules.FileName(art)} did not decode ({status}); later menus keep the glyph");
                lock (Gate)
                {
                    Cache[(art, dark)] = null;
                }

                return;
            }

            icon.Source = source;
        }
        catch (Exception ex)
        {
            // Fire-and-forget from a menu being built: nothing may escape.
            Log.Warn(Scope, "a menu picture could not be drawn", ex);
        }
    }
}
