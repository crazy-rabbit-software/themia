using System.Text;

namespace Themia.Core.Shell;

/// <summary>
/// [S-1754] A picture File Explorer draws in its own menus and command bar: Windows 11's two-tone
/// icons (a grey body, a blue part). Windows ships them as SVGs with Explorer, one per theme; Themia
/// draws a menu line with Explorer's picture where its glyph shows the same thing, so a Themia menu
/// carries the same icons as Explorer's.
/// </summary>
public enum MenuArt
{
    Cut,
    Copy,
    Paste,
    Rename,
    Share,
    Delete,
    Settings,
    Open,
    OpenWith,
    OpenNewWindow,
    OpenFileLocation,
    Pin,
    Compress,
    CopyAsPath,
    Link,
    Properties,
    ShowMoreOptions,
    New,
    NewFolder,
    SelectAll,
    AlignToGrid,
    Refresh,
    Undo,
    Redo,
    Sort,
    View,
}

/// <summary>[S-1754]..[S-1756] Which Explorer picture a menu glyph is, where it lives, and whether a file is one.</summary>
public static class MenuArtRules
{
    /// <summary>[S-1756] Explorer's pictures are a few hundred bytes; anything far larger is not one.</summary>
    public const int MaxSvgBytes = 64 * 1024;

    /// <summary>The menu icon slot in DIPs: Explorer's pictures are drawn on a 16×16 grid.</summary>
    public const double SlotDips = 16;

    /// <summary>The largest raster a picture gets, at 400 % scale: past that it is not a menu icon.</summary>
    public const int MaxRasterPixels = 64;

    /// <summary>
    /// [S-1757] The square raster, in physical pixels, a picture is drawn at for a screen at
    /// <paramref name="rasterizationScale"/>: the 16-DIP slot at that scale, rounded up, at least the
    /// slot and at most <see cref="MaxRasterPixels"/>. Explorer's SVGs carry only a viewBox, and an
    /// SVG source given no raster size draws at a screen-sized raster (2858² px, 32 MB each; user
    /// report 2026-10-17: 1 GB after some menus).
    /// </summary>
    public static int RasterPixels(double rasterizationScale)
    {
        if (!double.IsFinite(rasterizationScale) || rasterizationScale <= 1)
        {
            return (int)SlotDips;
        }

        return (int)Math.Min(MaxRasterPixels, Math.Ceiling(SlotDips * rasterizationScale - 1e-9));
    }

    /// <summary>
    /// [S-1754] The Explorer picture of a Segoe Fluent Icons glyph, or null for a glyph Explorer has
    /// no picture of (that line keeps its one-colour glyph, as Explorer's own lines without art do).
    /// By code point: a glyph is a picture, and Explorer's art is the same picture drawn in two tones.
    /// </summary>
    public static MenuArt? ForGlyph(int codePoint) => codePoint switch
    {
        0xE8C6 => MenuArt.Cut,
        0xE8C8 => MenuArt.Copy,
        0xE77F => MenuArt.Paste,
        0xE8AC => MenuArt.Rename,
        0xE72D => MenuArt.Share,
        0xE74D => MenuArt.Delete,
        0xE713 => MenuArt.Settings,
        0xE838 => MenuArt.Open,
        0xE7AC => MenuArt.OpenWith,
        0xE78B => MenuArt.OpenNewWindow,
        0xE8B7 => MenuArt.OpenFileLocation,
        0xE718 => MenuArt.Pin,
        0xF012 => MenuArt.Compress,
        0xE62F => MenuArt.CopyAsPath,
        0xE71B => MenuArt.Link,
        0xE90F => MenuArt.Properties,
        0xE8A7 => MenuArt.ShowMoreOptions,
        0xE710 => MenuArt.New,
        0xE8F4 => MenuArt.NewFolder,
        0xE8B3 => MenuArt.SelectAll,
        0xE80A => MenuArt.AlignToGrid,
        0xE72C => MenuArt.Refresh,
        0xE7A7 => MenuArt.Undo,
        0xE7A6 => MenuArt.Redo,
        0xE8CB => MenuArt.Sort,
        0xE7C4 => MenuArt.View,
        _ => null,
    };

    /// <summary>[S-1755] The file name Explorer gives the picture, in each theme's folder.</summary>
    public static string FileName(MenuArt art) => art switch
    {
        MenuArt.Cut => "windows.cut.svg",
        MenuArt.Copy => "windows.copy.svg",
        MenuArt.Paste => "windows.paste.svg",
        MenuArt.Rename => "windows.rename.svg",
        MenuArt.Share => "Windows.ModernShare.svg",
        MenuArt.Delete => "windows.ribbondelete.svg",
        MenuArt.Settings => "windows.folderoptions.svg",
        MenuArt.Open => "windows.openfolder.svg",
        MenuArt.OpenWith => "windows.openwith.svg",
        MenuArt.OpenNewWindow => "windows.opennewwindow.svg",
        MenuArt.OpenFileLocation => "windows.shortcut.opencontaining.svg",
        MenuArt.Pin => "windows.pintohome.svg",
        MenuArt.Compress => "Windows.Zip.Action.svg",
        MenuArt.CopyAsPath => "windows.copyaspath.svg",
        MenuArt.Link => "link.svg",
        MenuArt.Properties => "windows.properties.svg",
        MenuArt.ShowMoreOptions => "expandtoclassic.svg",
        MenuArt.New => "windows.newitem.svg",
        MenuArt.NewFolder => "newfolder.svg",
        MenuArt.SelectAll => "windows.selectall.svg",
        MenuArt.AlignToGrid => "windows.aligngrid.svg",
        MenuArt.Refresh => "windows.refresh.svg",
        MenuArt.Undo => "windows.undo.svg",
        MenuArt.Redo => "windows.redo.svg",
        MenuArt.Sort => "SortBy.svg",
        MenuArt.View => "View.svg",
        _ => throw new ArgumentOutOfRangeException(nameof(art), art, null),
    };

    /// <summary>
    /// [S-1755] Where Windows keeps Explorer's pictures for a theme, under the Windows folder. Only the
    /// standard-contrast sets: in high contrast the menus keep their glyphs, which follow the user's
    /// contrast colours.
    /// </summary>
    public static string Folder(string windowsFolder, bool dark)
    {
        ArgumentNullException.ThrowIfNull(windowsFolder);
        return Path.Combine(windowsFolder, "SystemApps", "MicrosoftWindows.Client.FileExp_cw5n1h2txyewy", "FileExplorerExtensions",
            "Assets", "images", "contrast-standard", dark ? "theme-dark" : "theme-light");
    }

    /// <summary>
    /// [S-1756] Whether <paramref name="bytes"/> are a picture to draw: a small, self-contained SVG
    /// document — UTF-8, an <c>&lt;svg</c> root, no script and nothing fetched from elsewhere. Anything
    /// else is not Explorer's picture, and the line keeps its glyph.
    /// </summary>
    public static bool IsPlausibleSvg(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length is 0 or > MaxSvgBytes)
        {
            return false;
        }

        string text;
        try
        {
            text = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return false;
        }

        var start = text.AsSpan().TrimStart('﻿').TrimStart();
        if (!start.StartsWith("<svg", StringComparison.Ordinal) || !text.TrimEnd().EndsWith("</svg>", StringComparison.Ordinal))
        {
            return false;
        }

        return !text.Contains("<script", StringComparison.OrdinalIgnoreCase)
            && !text.Contains("href=\"http", StringComparison.OrdinalIgnoreCase)
            && !text.Contains("href='http", StringComparison.OrdinalIgnoreCase)
            && !text.Contains("<!ENTITY", StringComparison.OrdinalIgnoreCase)
            && !text.Contains("<foreignObject", StringComparison.OrdinalIgnoreCase);
    }
}
