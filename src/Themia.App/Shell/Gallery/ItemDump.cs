#if DEBUG
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Themia.App.Widgets;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI.Kit;
using Windows.Foundation;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// [S-12831] Debug only: every card's item boxes beside a render (<c>--widget-png … --layout-json &lt;out.json&gt;</c>), for
/// the item check (<c>scripts/item-check.py</c>, the owner's WR-2..WR-6): the card's box and its grid pitch, each item
/// (<see cref="ItemMark"/>) as the box it shows in the card (cut by the scroll viewer or clip it stands in), grouped by
/// the panel that lays it, [S-14500] a family row's optimal height on the card's grid (<c>o</c>: its natural in whole
/// cells, what it may stretch from and never shrink under), and the ink that stands in the card outside every item (texts,
/// glyphs, pictures, shapes), so the check can tell what stands over a list from a band over it. [S-6754] And every graph's plot (the one
/// <see cref="Chart"/>'s box), for the gap check (<c>scripts/gap-check.py</c>): a plot reaches the card's edges.
/// </summary>
internal static class ItemDump
{
    public static void Write(IReadOnlyList<WidgetCard> cards, IReadOnlyList<Themia.Core.Geometry.CheckedSize> tiers, string kind, string? state, UIElement page, string path)
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

            // [S-14300] Where the card stands in the picture (the page's DIPs), so a check crops the card itself: never a
            // card found by its alpha, which merges a size label over it and skips a card under 20 px.
            var at = card.TransformToVisual(page).TransformPoint(default);
            w.WriteNumber("x", Math.Round(at.X, 2));
            w.WriteNumber("y", Math.Round(at.Y, 2));
            w.WriteNumber("w", Math.Round(card.ActualWidth, 2));
            w.WriteNumber("h", Math.Round(card.ActualHeight, 2));
            w.WriteNumber("cell", Math.Round(card.CellPitch, 4));
            w.WriteNumber("scale", card.XamlRoot?.RasterizationScale ?? 1);

            // [S-12835] [S-6754] The card's compact Set up line over the view: a plot stands right under it.
            w.WriteNumber("setup", Math.Round(card.SetupBand, 2));

            // [S-14301] Where the card's content stands across: the alignment it draws where its kind aligns in its style
            // ([S-6583]), else centred (the owner's "Centred is fine": the leftover shared alike on both sides).
            w.WriteString("align", Across(card.Model));

            var items = new List<(FrameworkElement Item, Rect Box, bool Cut)>();
            var ink = new List<(FrameworkElement Part, Rect Box)>();
            var texts = new List<(FrameworkElement Part, Rect Box)>();
            var plots = new List<Rect>();
            var fields = new List<Rect>();
            var marks = new List<Rect>();
            var cuts = new List<Rect>();
            var heads = new List<Rect>();
            var holds = new List<Rect>();
            if (card.View is { } view)
            {
                Walk(card, view, new Rect(0, 0, card.ActualWidth, card.ActualHeight), insideItem: false, items, ink, texts, plots, fields, marks, cuts, heads, holds);
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

                // [S-14500] A family row's optimal height: its lines' natural in whole cells of the card's grid.
                if ((item as RowBlock ?? (item as Border)?.Child as RowBlock) is { } row)
                {
                    w.WriteNumber("o", Math.Round(ItemStretch.Optimal(Themia.Sdk.Kit.FamilyShape.RowHeight(row.Lines), new ItemGrid(card.CellPitch)), 2));
                }

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

            // The texts inside the items, for a mark that stands over one (a status glyph over a row's value). [S-14317] With
            // each its letters' own box (`ink`: its box less its side bearings and the leading over and under its line), what
            // a mark may not stand over: a reading placed by its ink reaches past it with its box alone.
            w.WriteStartArray("texts");
            foreach (var (part, box) in texts)
            {
                w.WriteStartObject();
                w.WriteString("t", part.GetType().Name);
                Box(w, box);
                if (Letters(card, part) is { } letters && Intersect(letters, box) is { } shown)
                {
                    w.WritePropertyName("ink");
                    w.WriteStartObject();
                    Box(w, shown);
                    w.WriteEndObject();
                }

                w.WriteEndObject();
            }

            w.WriteEndArray();

            // [S-6754] Each chart's plot as it shows in the card.
            w.WriteStartArray("plots");
            foreach (var box in plots)
            {
                w.WriteStartObject();
                Box(w, box);
                w.WriteEndObject();
            }

            w.WriteEndArray();

            // [S-14303] Each free field (an editor's box, a drawing pad) as it shows in the card.
            w.WriteStartArray("fields");
            foreach (var box in fields)
            {
                w.WriteStartObject();
                Box(w, box);
                w.WriteEndObject();
            }

            w.WriteEndArray();

            // [S-14302] Each mark over the content (the sources' problem glyph, KitStatus): no ink of the content's own.
            w.WriteStartArray("marks");
            foreach (var box in marks)
            {
                w.WriteStartObject();
                Box(w, box);
                w.WriteEndObject();
            }

            w.WriteEndArray();

            // [S-14304] Each text cut with its ellipsis at the width it was given, as it shows in the card.
            w.WriteStartArray("cuts");
            foreach (var box in cuts)
            {
                w.WriteStartObject();
                Box(w, box);
                w.WriteEndObject();
            }

            w.WriteEndArray();

            // [S-14319] Each held reading's box (its template's ink across).
            w.WriteStartArray("holds");
            foreach (var box in holds)
            {
                w.WriteStartObject();
                Box(w, box);
                w.WriteEndObject();
            }

            w.WriteEndArray();

            // [S-14314] Each column's head (the month's weekday letters' box, the column's across).
            w.WriteStartArray("heads");
            foreach (var box in heads)
            {
                w.WriteStartObject();
                Box(w, box);
                w.WriteEndObject();
            }

            w.WriteEndArray();
            w.WriteEndObject();
        }

        w.WriteEndArray();
        w.WriteEndObject();
    }

    /// <summary>[S-14301] The card's content alignment as the check reads it: left, center or right.</summary>
    private static string Across(WidgetInstance widget)
        => !ContentAlignment.Applies(widget) ? "center" : ContentAlignment.Shown(widget) switch
        {
            ContentAlign.Right => "right",
            ContentAlign.Center => "center",
            _ => "left",
        };

    private static void Box(Utf8JsonWriter w, Rect box)
    {
        w.WriteNumber("x", Math.Round(box.X, 2));
        w.WriteNumber("y", Math.Round(box.Y, 2));
        w.WriteNumber("w", Math.Round(box.Width, 2));
        w.WriteNumber("h", Math.Round(box.Height, 2));
    }

    /// <summary>The items and the free ink under <paramref name="node"/>, each as it shows inside <paramref name="clip"/> (card coordinates).</summary>
    private static void Walk(UIElement card, DependencyObject node, Rect clip, bool insideItem, List<(FrameworkElement, Rect, bool)> items, List<(FrameworkElement, Rect)> ink, List<(FrameworkElement, Rect)> texts, List<Rect> plots, List<Rect> fields, List<Rect> marks, List<Rect> cuts, List<Rect> heads, List<Rect> holds)
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

            // A curve its owner laid out on the chart's parts (the forecast's, over its day columns) is no plot of its own;
            // a plot drawn off the chart (the screen-time chart's bars) is marked one.
            if (fe is Chart { Model.Laid: false } || PlotMark.IsPlot(fe))
            {
                plots.Add(shown.Value);
            }

            if (FieldMark.IsField(fe))
            {
                fields.Add(shown.Value);
            }

            if (fe is KitStatus)
            {
                marks.Add(shown.Value);
            }

            if (HeadMark.IsHead(fe))
            {
                heads.Add(shown.Value);
            }

            if (HoldMark.IsHeld(fe))
            {
                holds.Add(shown.Value);
            }

            if (fe is TextBlock { IsTextTrimmed: true })
            {
                cuts.Add(shown.Value);
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
            Walk(card, VisualTreeHelper.GetChild(node, i), clip, insideItem, items, ink, texts, plots, fields, marks, cuts, heads, holds);
        }
    }

    /// <summary>
    /// [S-14317] A text's letters as they show in the card: its box less its side bearings (<see cref="PartInk.Across"/>) and
    /// the leading over its first ink and under its last (<see cref="PartInk.Of"/>), both read without its margin; null for a
    /// part that is no plain text.
    /// </summary>
    private static Rect? Letters(UIElement card, FrameworkElement part)
    {
        if (part is not TextBlock text || !(text.ActualWidth > 0) || !(text.ActualHeight > 0))
        {
            return null;
        }

        var margin = text.Margin;
        var (left, right) = PartInk.Across(text);
        var line = PartInk.Of(text);
        var l = Math.Clamp(left - margin.Left, 0, text.ActualWidth);
        var r = Math.Clamp(right - margin.Right, 0, text.ActualWidth - l);
        var o = Math.Clamp(line.Over - margin.Top, 0, text.ActualHeight);
        var u = Math.Clamp(line.Under - margin.Bottom, 0, text.ActualHeight - o);
        try
        {
            return text.TransformToVisual(card).TransformBounds(new Rect(l, o, text.ActualWidth - l - r, text.ActualHeight - o - u));
        }
        catch (ArgumentException)
        {
            // ignore: not in the card's tree any more (a closing popup's part); its box stands for it
            return null;
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
