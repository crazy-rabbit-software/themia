#if DEBUG
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// [S-2377] Debug only: with <c>--icon-census</c>, every render writes beside its PNG (<c>&lt;png&gt;.icons.json</c>) each
/// icon it drew: what it is (a glyph's code point and size, a picture), its size as laid out, in the PNG's pixels its own
/// box, the box it is laid out in cut to every slot round it and the window it is seen through (a scroller's viewport), and
/// the key of the row it stands in.
/// <c>scripts/icon-clip-check.py</c> reads it with the PNG and says which icons' ink is cut by their box, measured on the
/// picture (owner, 2026-11-19: "in the settings pages the icons show clipped").
/// </summary>
internal static class IconCensus
{
    /// <summary>Whether this render was asked for the census.</summary>
    public static bool On { get; } = Environment.GetCommandLineArgs().Contains("--icon-census", StringComparer.Ordinal);

    /// <summary>An icon larger than this (a picture, a preview) is no icon.</summary>
    private const double Largest = 64;

    /// <summary>Write the census of what <paramref name="root"/> shows, for the PNG at <paramref name="png"/>.</summary>
    public static void Write(UIElement root, string png)
    {
        var scale = root.XamlRoot?.RasterizationScale is > 0 and var raster ? raster : 1.0;
        using var file = new FileStream(png + ".icons.json", FileMode.Create, FileAccess.Write, FileShare.None);
        using var json = new Utf8JsonWriter(file, new JsonWriterOptions { Indented = true });
        json.WriteStartObject();
        json.WriteNumber("scale", scale);
        json.WriteStartArray("icons");
        Walk(root, root, scale, json);
        json.WriteEndArray();
        json.WriteEndObject();
    }

    private static void Walk(DependencyObject node, UIElement root, double scale, Utf8JsonWriter json)
    {
        if (node is UIElement { Visibility: Visibility.Collapsed } or UIElement { Opacity: 0 })
        {
            return;
        }

        if (node is FrameworkElement element && Kind(element) is { } kind)
        {
            Note(element, kind, root, scale, json);

            // A glyph's own parts (its text) are the glyph.
            return;
        }

        var count = VisualTreeHelper.GetChildrenCount(node);
        for (var i = 0; i < count; i++)
        {
            Walk(VisualTreeHelper.GetChild(node, i), root, scale, json);
        }
    }

    /// <summary>What an element is as an icon: a glyph, a picture; null for none.</summary>
    private static string? Kind(FrameworkElement element) => element switch
    {
        FontIcon => "glyph",
        SymbolIcon => "symbol",
        ImageIcon or BitmapIcon or PathIcon => "picture",
        Image when element.ActualWidth is > 0 and <= Largest && element.ActualHeight is > 0 and <= Largest => "picture",

        // The icon picker's cells draw their glyph as text in the symbol font.
        TextBlock { FontFamily: { Source: var family }, Text.Length: > 0 and <= 2 } when family.Contains("Icons", StringComparison.OrdinalIgnoreCase) || family.Contains("MDL2", StringComparison.OrdinalIgnoreCase) => "glyph",
        _ => null,
    };

    private static void Note(FrameworkElement icon, string kind, UIElement root, double scale, Utf8JsonWriter json)
    {
        if (icon.ActualWidth <= 0 || icon.ActualHeight <= 0)
        {
            return;
        }

        Rect own;
        Rect slot;
        (Rect Clip, Rect View) cut;
        try
        {
            own = icon.TransformToVisual(root).TransformBounds(new Rect(0, 0, icon.ActualWidth, icon.ActualHeight));
            slot = SlotOf(icon, root) ?? own;
            cut = ClipOf(icon, root, slot);
        }
        catch (ArgumentException)
        {
            // ignore: not under the root's visual (a popup's own tree); the census is of the picture only.
            return;
        }

        json.WriteStartObject();
        json.WriteString("kind", kind);
        json.WriteString("type", icon.GetType().Name);
        switch (icon)
        {
            case FontIcon glyph:
                json.WriteNumber("glyph", char.ConvertToUtf32(glyph.Glyph, 0));
                json.WriteNumber("size", glyph.FontSize);
                break;
            case TextBlock text:
                json.WriteNumber("glyph", char.ConvertToUtf32(text.Text, 0));
                json.WriteNumber("size", text.FontSize);
                break;
        }

        // Its own size in DIPs, as laid out (a glyph cut by its box says the box's): what it is scaled by, its box over this.
        json.WriteStartArray("actual");
        json.WriteNumberValue(Math.Round(icon.ActualWidth, 2));
        json.WriteNumberValue(Math.Round(icon.ActualHeight, 2));
        json.WriteEndArray();
        Box(json, "own", own, scale);
        Box(json, "slot", slot, scale);
        Box(json, "clip", cut.Clip, scale);
        Box(json, "view", cut.View, scale);
        json.WriteString("row", RowOf(icon));
        json.WriteEndObject();
    }

    /// <summary>The box <paramref name="icon"/> is laid out in (its layout slot), in the root's coordinates.</summary>
    private static Rect? SlotOf(FrameworkElement icon, UIElement root)
    {
        if (VisualTreeHelper.GetParent(icon) is not UIElement parent)
        {
            return null;
        }

        var slot = LayoutInformation.GetLayoutSlot(icon);
        return parent.TransformToVisual(root).TransformBounds(slot);
    }

    /// <summary>
    /// The box the icon's ink may show in (<c>Clip</c>): its slot, cut to every slot round it up to the root (a box with a
    /// fixed size clips what is laid out larger in it); and apart, the window it is seen through (<c>View</c>): every
    /// scroller's viewport and every clip set on the way, which hide what is scrolled away, not what is too large.
    /// </summary>
    private static (Rect Clip, Rect View) ClipOf(FrameworkElement icon, UIElement root, Rect slot)
    {
        var clip = slot;
        var view = new Rect(0, 0, root.ActualSize.X, root.ActualSize.Y);
        for (var node = VisualTreeHelper.GetParent(icon); node is not null && !ReferenceEquals(node, root); node = VisualTreeHelper.GetParent(node))
        {
            if (node is not FrameworkElement element || element.ActualWidth <= 0 || element.ActualHeight <= 0)
            {
                continue;
            }

            if (element is ScrollViewer or ScrollContentPresenter || element.Clip is not null)
            {
                view = Cut(view, element.TransformToVisual(root).TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight)));
            }

            // A scroller's content and a moved element stand where their transform puts them, not in their slot.
            if (VisualTreeHelper.GetParent(element) is UIElement parent and not (ScrollContentPresenter or ScrollViewer) && element.RenderTransform is null)
            {
                clip = Cut(clip, parent.TransformToVisual(root).TransformBounds(LayoutInformation.GetLayoutSlot(element)));
            }
        }

        return (clip, view);
    }

    private static Rect Cut(Rect a, Rect b)
    {
        a.Intersect(b);
        return a.IsEmpty ? new Rect(0, 0, 0, 0) : a;
    }

    /// <summary>The key of the nearest tagged element round the icon (a row's), for naming it.</summary>
    private static string RowOf(FrameworkElement icon)
    {
        for (DependencyObject? node = icon; node is not null; node = VisualTreeHelper.GetParent(node))
        {
            if (node is FrameworkElement { Tag: string tag } && tag.Length > 0)
            {
                return tag;
            }
        }

        return string.Empty;
    }

    private static void Box(Utf8JsonWriter json, string name, Rect rect, double scale)
    {
        json.WriteStartArray(name);
        json.WriteNumberValue(Math.Round(rect.X * scale, 2));
        json.WriteNumberValue(Math.Round(rect.Y * scale, 2));
        json.WriteNumberValue(Math.Round(rect.Width * scale, 2));
        json.WriteNumberValue(Math.Round(rect.Height * scale, 2));
        json.WriteEndArray();
    }
}
#endif
