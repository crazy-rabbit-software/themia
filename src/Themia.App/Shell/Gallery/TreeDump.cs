#if DEBUG
using System.Text.Json;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Themia.Sdk.WinUI;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// [S-6732] Debug only: every drawn element of each card (its type, position and size in the card,
/// margin, padding, radius, text step, glyph and colours), as JSON beside a render
/// (<c>--widget-png … --tree</c>, <c>--family-blocks-png</c>). What a group phase compares with its
/// mockups' boxes (TestResults/family/match.py): a render matches when every text and glyph stands
/// within a DIP of the mockup's in the same colour role.
/// </summary>
internal static class TreeDump
{
    private static readonly string[] Keys =
    [
        "TextFillColorPrimaryBrush", "TextFillColorSecondaryBrush", "TextFillColorTertiaryBrush", "TextFillColorDisabledBrush",
        "AccentFillColorDefaultBrush", "AccentFillColorSecondaryBrush", "AccentFillColorTertiaryBrush", "AccentFillColorDisabledBrush",
        "AccentTextFillColorPrimaryBrush", "AccentTextFillColorTertiaryBrush", "TextOnAccentFillColorPrimaryBrush", "TextOnAccentFillColorSecondaryBrush",
        "SystemFillColorCriticalBrush", "SystemFillColorCautionBrush", "SystemFillColorSuccessBrush", "SystemFillColorCriticalBackgroundBrush",
        "SubtleFillColorTransparentBrush", "SubtleFillColorSecondaryBrush", "SubtleFillColorTertiaryBrush",
        "ControlFillColorDefaultBrush", "ControlFillColorSecondaryBrush", "ControlAltFillColorSecondaryBrush", "ControlAltFillColorTertiaryBrush",
        "ControlStrokeColorDefaultBrush", "ControlStrongStrokeColorDefaultBrush", "DividerStrokeColorDefaultBrush", "CardStrokeColorDefaultBrush",
        "CardBackgroundFillColorDefaultBrush", "SolidBackgroundFillColorBaseBrush", "SolidBackgroundFillColorTertiaryBrush", "ControlSolidFillColorDefaultBrush",
    ];

    public static void Write(IReadOnlyList<WidgetCard> cards, IReadOnlyList<Themia.Core.Widgets.WidgetTier> tiers, string path)
        => Write([.. cards.Select((card, i) => (i < tiers.Count ? tiers[i].Name : "?", (FrameworkElement)card, (DependencyObject?)card.View))], path);

    /// <summary>Each named root's elements, positions relative to the root; <c>v</c> marks those under its view.</summary>
    public static void Write(IReadOnlyList<(string Name, FrameworkElement Root, DependencyObject? View)> cards, string path)
    {
        using var stream = File.Create(path);
        using var w = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false });
        w.WriteStartObject();
        w.WriteStartObject("palette");
        foreach (var key in Keys)
        {
            w.WriteString(key, Describe(ThemeBrush.Of(key, Colors.Transparent)));
        }

        w.WriteEndObject();
        w.WriteStartArray("cards");
        foreach (var (name, root, view) in cards)
        {
            w.WriteStartObject();
            w.WriteString("tier", name);
            w.WriteNumber("w", root.ActualWidth);
            w.WriteNumber("h", root.ActualHeight);
            var inView = new HashSet<DependencyObject>();
            if (view is not null)
            {
                Collect(view, inView);
            }

            w.WriteStartArray("els");
            Walk(root, root, inView, w, 0, 1.0);
            w.WriteEndArray();
            w.WriteEndObject();
        }

        w.WriteEndArray();
        w.WriteEndObject();
    }

    private static void Collect(DependencyObject node, HashSet<DependencyObject> into)
    {
        into.Add(node);
        var n = VisualTreeHelper.GetChildrenCount(node);
        for (var i = 0; i < n; i++)
        {
            Collect(VisualTreeHelper.GetChild(node, i), into);
        }
    }

    private static void Walk(UIElement card, DependencyObject node, HashSet<DependencyObject> inView, Utf8JsonWriter w, int depth, double opacity)
    {
        if (node is UIElement ui)
        {
            if (ui.Visibility != Visibility.Visible)
            {
                return;
            }

            opacity *= ui.Opacity;
        }

        if (node is FrameworkElement fe && fe.ActualWidth > 0 && fe.ActualHeight > 0)
        {
            Emit(card, fe, inView.Contains(node), w, depth, opacity);
        }

        var n = VisualTreeHelper.GetChildrenCount(node);
        for (var i = 0; i < n; i++)
        {
            Walk(card, VisualTreeHelper.GetChild(node, i), inView, w, depth + 1, opacity);
        }
    }

    private static void Emit(UIElement card, FrameworkElement fe, bool view, Utf8JsonWriter w, int depth, double opacity)
    {
        var interesting = fe is TextBlock or FontIcon or Border or Panel or Shape or ButtonBase or Image or TextBox or ContentPresenter
            || fe.Margin != default;
        if (!interesting)
        {
            return;
        }

        Windows.Foundation.Point at;
        try
        {
            at = fe.TransformToVisual(card).TransformPoint(new Windows.Foundation.Point(0, 0));
        }
        catch (ArgumentException)
        {
            // ignore: not in the card's tree any more (a closing popup's part); nothing to place
            return;
        }

        w.WriteStartObject();
        w.WriteString("t", fe.GetType().Name);
        w.WriteString("n", fe.Name);
        w.WriteBoolean("v", view);
        w.WriteNumber("d", depth);
        w.WriteNumber("x", Math.Round(at.X, 1));
        w.WriteNumber("y", Math.Round(at.Y, 1));
        w.WriteNumber("aw", Math.Round(fe.ActualWidth, 1));
        w.WriteNumber("ah", Math.Round(fe.ActualHeight, 1));
        if (opacity < 0.999)
        {
            w.WriteNumber("op", Math.Round(opacity, 3));
        }

        if (fe.Margin != default)
        {
            w.WriteString("m", T(fe.Margin));
        }

        switch (fe)
        {
            case TextBlock tb:
                w.WriteNumber("fs", Math.Round(tb.FontSize, 2));
                w.WriteNumber("fw", tb.FontWeight.Weight);
                w.WriteNumber("lh", Math.Round(tb.LineHeight, 2));
                w.WriteString("fg", Describe(tb.Foreground));
                w.WriteString("ff", tb.FontFamily?.Source ?? string.Empty);
                w.WriteString("tx", Cut(tb.Text));
                if (tb.Padding != default)
                {
                    w.WriteString("p", T(tb.Padding));
                }

                if (tb.Inlines.Count > 1 || (tb.Inlines.Count == 1 && tb.Inlines[0] is Microsoft.UI.Xaml.Documents.Run { FontSize: var one } && Math.Abs(one - tb.FontSize) > 0.01))
                {
                    w.WriteStartArray("runs");
                    foreach (var inline in tb.Inlines)
                    {
                        if (inline is Microsoft.UI.Xaml.Documents.Run run)
                        {
                            w.WriteStartObject();
                            w.WriteNumber("fs", Math.Round(run.FontSize, 2));
                            w.WriteNumber("fw", run.FontWeight.Weight);
                            w.WriteString("fg", Describe(run.Foreground ?? tb.Foreground));
                            w.WriteString("ff", run.FontFamily?.Source ?? string.Empty);
                            w.WriteString("tx", Cut(run.Text));
                            w.WriteEndObject();
                        }
                    }

                    w.WriteEndArray();
                }

                break;
            case FontIcon fi:
                w.WriteNumber("fs", Math.Round(fi.FontSize, 2));
                w.WriteString("fg", Describe(fi.Foreground));
                w.WriteString("gl", ((int)(fi.Glyph.Length > 0 ? fi.Glyph[0] : 0)).ToString("X4", System.Globalization.CultureInfo.InvariantCulture));
                break;
            case Border b:
                Box(w, b.Padding, b.CornerRadius, b.Background, b.BorderBrush, b.BorderThickness);
                break;
            case Grid g:
                Box(w, g.Padding, g.CornerRadius, g.Background, g.BorderBrush, g.BorderThickness);
                break;
            case StackPanel s:
                Box(w, s.Padding, s.CornerRadius, s.Background, s.BorderBrush, s.BorderThickness);
                w.WriteNumber("sp", s.Spacing);
                break;
            case Panel p when p.Background is not null:
                w.WriteString("bg", Describe(p.Background));
                break;
            case Shape sh:
                w.WriteString("fill", Describe(sh.Fill));
                w.WriteString("stroke", Describe(sh.Stroke));
                w.WriteNumber("st", sh.StrokeThickness);
                if (sh is Rectangle r && (r.RadiusX > 0 || r.RadiusY > 0))
                {
                    w.WriteNumber("rx", r.RadiusX);
                }

                break;
            case Control c:
                Box(w, c.Padding, c.CornerRadius, c.Background, c.BorderBrush, c.BorderThickness);
                w.WriteNumber("fs", Math.Round(c.FontSize, 2));
                w.WriteString("fg", Describe(c.Foreground));
                break;
        }

        w.WriteEndObject();
    }

    private static void Box(Utf8JsonWriter w, Thickness padding, CornerRadius radius, Brush? background, Brush? border, Thickness borderThickness)
    {
        if (padding != default)
        {
            w.WriteString("p", T(padding));
        }

        if (radius != default)
        {
            w.WriteString("r", $"{radius.TopLeft:0.##},{radius.TopRight:0.##},{radius.BottomRight:0.##},{radius.BottomLeft:0.##}");
        }

        if (background is not null)
        {
            w.WriteString("bg", Describe(background));
        }

        if (border is not null && borderThickness != default)
        {
            w.WriteString("bb", Describe(border));
            w.WriteString("bt", T(borderThickness));
        }
    }

    private static string T(Thickness t) => $"{t.Left:0.##},{t.Top:0.##},{t.Right:0.##},{t.Bottom:0.##}";

    private static string Cut(string? text) => text is null ? string.Empty : text.Length > 40 ? text[..40] : text;

    private static string Describe(Brush? brush) => brush switch
    {
        null => string.Empty,
        SolidColorBrush s => $"#{s.Color.A:X2}{s.Color.R:X2}{s.Color.G:X2}{s.Color.B:X2}" + (s.Opacity < 0.999 ? $"@{s.Opacity:0.###}" : string.Empty),
        _ => brush.GetType().Name,
    };
}
#endif
