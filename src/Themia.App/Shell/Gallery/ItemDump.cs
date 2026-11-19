#if DEBUG
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Themia.App.Widgets;
using Windows.Foundation;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// [S-12831] Debug only: every card's item boxes beside a render (<c>--widget-png … --layout-json &lt;out.json&gt;</c>), for
/// the item check (<c>scripts/item-check.py</c>, the owner's WR-2..WR-6): the card's box and its grid pitch, each item
/// (<see cref="ItemMark"/>) as the box it shows in the card (cut by the scroll viewer or clip it stands in), grouped by
/// the panel that lays it, and the ink that stands in the card outside every item (texts, glyphs, pictures, shapes),
/// so the check can tell what stands over a list from a band over it.
/// </summary>
internal static class ItemDump
{
    public static void Write(IReadOnlyList<WidgetCard> cards, IReadOnlyList<Themia.Core.Geometry.CheckedSize> tiers, string kind, string? state, string path)
    {
        using var stream = File.Create(path);
        using var w = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false });
        w.WriteStartObject();
        w.WriteString("kind", kind);
        w.WriteString("state", state ?? string.Empty);
        w.WriteStartArray("cards");
        for (var i = 0; i < cards.Count; i++)
        {
            var card = cards[i];
            w.WriteStartObject();
            w.WriteString("tier", i < tiers.Count ? tiers[i].Name : "?");
            w.WriteNumber("w", Math.Round(card.ActualWidth, 2));
            w.WriteNumber("h", Math.Round(card.ActualHeight, 2));
            w.WriteNumber("cell", Math.Round(card.CellPitch, 4));
            w.WriteNumber("scale", card.XamlRoot?.RasterizationScale ?? 1);

            var items = new List<(FrameworkElement Item, Rect Box, bool Cut)>();
            var ink = new List<(FrameworkElement Part, Rect Box)>();
            var texts = new List<(FrameworkElement Part, Rect Box)>();
            if (card.View is { } view)
            {
                Walk(card, view, new Rect(0, 0, card.ActualWidth, card.ActualHeight), insideItem: false, items, ink, texts);
            }

            var groups = new Dictionary<DependencyObject, int>();
            w.WriteStartArray("items");
            foreach (var (item, box, cut) in items)
            {
                var parent = VisualTreeHelper.GetParent(item) ?? item;
                if (!groups.TryGetValue(parent, out var group))
                {
                    group = groups.Count;
                    groups[parent] = group;
                }

                w.WriteStartObject();
                w.WriteString("t", item.GetType().Name);
                w.WriteNumber("g", group);
                Box(w, box);
                if (cut)
                {
                    w.WriteBoolean("cut", true);
                }

                w.WriteEndObject();
            }

            w.WriteEndArray();
            w.WriteStartArray("ink");
            foreach (var (part, box) in ink)
            {
                w.WriteStartObject();
                w.WriteString("t", part.GetType().Name);
                Box(w, box);
                w.WriteEndObject();
            }

            w.WriteEndArray();

            // The texts inside the items, for a mark that stands over one (a status glyph over a row's value).
            w.WriteStartArray("texts");
            foreach (var (part, box) in texts)
            {
                w.WriteStartObject();
                w.WriteString("t", part.GetType().Name);
                Box(w, box);
                w.WriteEndObject();
            }

            w.WriteEndArray();
            w.WriteEndObject();
        }

        w.WriteEndArray();
        w.WriteEndObject();
    }

    private static void Box(Utf8JsonWriter w, Rect box)
    {
        w.WriteNumber("x", Math.Round(box.X, 2));
        w.WriteNumber("y", Math.Round(box.Y, 2));
        w.WriteNumber("w", Math.Round(box.Width, 2));
        w.WriteNumber("h", Math.Round(box.Height, 2));
    }

    /// <summary>The items and the free ink under <paramref name="node"/>, each as it shows inside <paramref name="clip"/> (card coordinates).</summary>
    private static void Walk(UIElement card, DependencyObject node, Rect clip, bool insideItem, List<(FrameworkElement, Rect, bool)> items, List<(FrameworkElement, Rect)> ink, List<(FrameworkElement, Rect)> texts)
    {
        if (node is UIElement { Visibility: not Visibility.Visible } || node is UIElement { Opacity: <= 0 })
        {
            return;
        }

        if (node is FrameworkElement fe && fe.ActualWidth > 0 && fe.ActualHeight > 0 && Place(card, fe) is { } box)
        {
            var shown = Intersect(box, clip);
            if (shown is null)
            {
                return;
            }

            if (!insideItem && ItemMark.IsItem(fe))
            {
                var cut = shown.Value.Width < box.Width - 0.5 || shown.Value.Height < box.Height - 0.5;
                items.Add((fe, shown.Value, cut));
                insideItem = true;
            }
            else if (!insideItem && fe is TextBlock { Text.Length: > 0 } or FontIcon or Image or Shape or ButtonBase or TextBox or ProgressRing)
            {
                ink.Add((fe, shown.Value));
            }
            else if (insideItem && fe is TextBlock text && (text.Text.Length > 0 || text.Inlines.Count > 0))
            {
                texts.Add((fe, shown.Value));
            }

            // What a scroll viewer or a clip shows of its content is its own box.
            if (fe is ScrollViewer or ScrollContentPresenter || fe.Clip is not null)
            {
                clip = shown.Value;
            }
        }

        var n = VisualTreeHelper.GetChildrenCount(node);
        for (var i = 0; i < n; i++)
        {
            Walk(card, VisualTreeHelper.GetChild(node, i), clip, insideItem, items, ink, texts);
        }
    }

    private static Rect? Place(UIElement card, FrameworkElement fe)
    {
        try
        {
            return fe.TransformToVisual(card).TransformBounds(new Rect(0, 0, fe.ActualWidth, fe.ActualHeight));
        }
        catch (ArgumentException)
        {
            // ignore: not in the card's tree any more (a closing popup's part); nothing to place
            return null;
        }
    }

    private static Rect? Intersect(Rect a, Rect b)
    {
        var x = Math.Max(a.X, b.X);
        var y = Math.Max(a.Y, b.Y);
        var r = Math.Min(a.X + a.Width, b.X + b.Width);
        var bottom = Math.Min(a.Y + a.Height, b.Y + b.Height);
        return r - x > 0.01 && bottom - y > 0.01 ? new Rect(x, y, r - x, bottom - y) : null;
    }
}
#endif
