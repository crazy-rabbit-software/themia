#if DEBUG
using System.Globalization;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Themia.App.Widgets;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// [S-6731] Debug only: what a card draws, as the family's render walk reads it. Walks the visual tree
/// of the parts a widget is responsible for (the header, the view, the Set up row) and turns each
/// visible element into a <see cref="FamilyFact"/> for <see cref="FamilyCheck"/>. A control's own
/// template is the framework's, not the widget's: its internals are skipped, but what the widget put
/// in it (a button's content element, a scroll viewer's content) is walked again.
/// </summary>
internal static class FamilyWalk
{
    /// <summary>Visit every visible element under <paramref name="root"/> the widget drew, in tree order.</summary>
    public static void Visit(DependencyObject root, Action<FrameworkElement> visit)
    {
        ArgumentNullException.ThrowIfNull(visit);
        var own = new HashSet<DependencyObject>();
        Walk(root, own, inTemplate: false, visit);
    }

    private static void Walk(DependencyObject node, HashSet<DependencyObject> own, bool inTemplate, Action<FrameworkElement> visit)
    {
        if (node is UIElement { Visibility: not Visibility.Visible })
        {
            return;
        }

        if (own.Contains(node))
        {
            inTemplate = false;
        }

        if (node is FrameworkElement fe && !inTemplate && fe.ActualWidth > 0 && fe.ActualHeight > 0)
        {
            visit(fe);
        }

        // A glyph is judged as a glyph: its own text inside is the icon's, not a text of the card's.
        if (node is FontIcon)
        {
            return;
        }

        // A control's template is the framework's: walk past it to what the widget placed inside.
        if (node is Control control and not UserControl)
        {
            if (control is ContentControl { Content: DependencyObject content })
            {
                own.Add(content);
            }

            inTemplate = true;
        }

        var count = VisualTreeHelper.GetChildrenCount(node);
        for (var i = 0; i < count; i++)
        {
            Walk(VisualTreeHelper.GetChild(node, i), own, inTemplate, visit);
        }
    }

    /// <summary>The facts of an element: one for it, one more per run of a text with runs of their own size.</summary>
    public static IEnumerable<FamilyFact> Facts(FrameworkElement fe)
    {
        var lengths = new List<(string, double)>();
        Lengths(lengths, "margin", fe.Margin);

        switch (fe)
        {
            case TextBlock text:
                Lengths(lengths, "padding", text.Padding);
                yield return new FamilyFact(FamilyPartType.Text, nameof(TextBlock), text.FontSize, text.LineHeight, text.FontWeight.Weight, lengths, Colours: [Colour("fg", text.Foreground)]);
                foreach (var inline in text.Inlines)
                {
                    if (inline is Microsoft.UI.Xaml.Documents.Run run && (Math.Abs(run.FontSize - text.FontSize) > 0.01 || run.Foreground != text.Foreground))
                    {
                        yield return new FamilyFact(FamilyPartType.Text, "Run", run.FontSize, 0, run.FontWeight.Weight, Colours: [Colour("fg", run.Foreground ?? text.Foreground)]);
                    }
                }

                break;
            case FontIcon icon:
                yield return new FamilyFact(FamilyPartType.Glyph, nameof(FontIcon), icon.FontSize, Lengths: lengths, Colours: [Colour("fg", icon.Foreground)]);
                break;
            case Border border:
                Lengths(lengths, "padding", border.Padding);
                yield return Box(nameof(Border), fe, lengths, border.CornerRadius, border.Background, border.BorderBrush, border.BorderThickness);
                break;
            case Grid grid:
                Lengths(lengths, "padding", grid.Padding);
                Add(lengths, "spacing", grid.RowSpacing);
                Add(lengths, "spacing", grid.ColumnSpacing);
                yield return Box(nameof(Grid), fe, lengths, grid.CornerRadius, grid.Background, grid.BorderBrush, grid.BorderThickness);
                break;
            case StackPanel stack:
                Lengths(lengths, "padding", stack.Padding);
                Add(lengths, "spacing", stack.Spacing);
                yield return Box(nameof(StackPanel), fe, lengths, stack.CornerRadius, stack.Background, stack.BorderBrush, stack.BorderThickness);
                break;
            case Panel panel:
                yield return new FamilyFact(FamilyPartType.Box, panel.GetType().Name, Lengths: lengths, Colours: Colours(("bg", panel.Background)));
                break;
            case Shape shape:
                var radii = shape is Rectangle r ? new[] { r.RadiusX } : null;
                yield return new FamilyFact(FamilyPartType.Shape, shape.GetType().Name, Lengths: lengths, Radii: radii, Width: fe.ActualWidth, Height: fe.ActualHeight,
                    Stroke: shape.Stroke is null ? 0 : shape.StrokeThickness, Colours: Colours(("fill", shape.Fill), ("stroke", shape.Stroke)));
                break;
            case Control control:
                // A control's foreground is what its text inherits, not something it draws: its texts are judged themselves.
                Lengths(lengths, "padding", control.Padding);
                yield return new FamilyFact(FamilyPartType.Control, control.GetType().Name, control.FontSize, 0, control.FontWeight.Weight, lengths,
                    Radii(control.CornerRadius), fe.ActualWidth, fe.ActualHeight, Colours: Colours(("bg", control.Background)));
                break;
            default:
                if (lengths.Count > 0)
                {
                    yield return new FamilyFact(FamilyPartType.Box, fe.GetType().Name, Lengths: lengths);
                }

                break;
        }
    }

    /// <summary>
    /// The colours a card may draw in, in the current theme: every role's, the card's own, and the
    /// item layer's for each role and state, as <c>#AARRGGBB</c> to the name it stands for.
    /// </summary>
    public static Dictionary<string, string> Palette(FrameworkElement card)
    {
        var palette = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var role in ColourRoles.All)
        {
            Name(palette, Family.Brush(role), role.ToString());
        }

        foreach (var key in ColourRoles.CardKeys)
        {
            Name(palette, ThemeBrush.Of(key, Colors.Transparent), key);
        }

        foreach (var role in Enum.GetValues<ItemRole>())
        {
            foreach (var state in Enum.GetValues<ItemState>())
            {
                Name(palette, ItemFill.For(card, role, state), $"item {role} {state}");
            }
        }

        return palette;
    }

    /// <summary>The accent role's colour now.</summary>
    public static string Accent() => Argb(Family.Brush(ColourRole.Accent));

    private static void Name(Dictionary<string, string> palette, Brush brush, string name)
    {
        var argb = Argb(brush);
        if (argb.Length > 0)
        {
            palette.TryAdd(argb, name);
        }
    }

    private static FamilyFact Box(string type, FrameworkElement fe, List<(string, double)> lengths, CornerRadius radius, Brush? background, Brush? border, Thickness borderThickness)
        => new(FamilyPartType.Box, type, Lengths: lengths, Radii: Radii(radius), Width: fe.ActualWidth, Height: fe.ActualHeight,
            Stroke: border is null ? 0 : Math.Max(Math.Max(borderThickness.Left, borderThickness.Right), Math.Max(borderThickness.Top, borderThickness.Bottom)),
            Colours: Colours(("bg", background), ("border", border)));

    private static double[] Radii(CornerRadius r) => [r.TopLeft, r.TopRight, r.BottomRight, r.BottomLeft];

    private static void Lengths(List<(string, double)> into, string name, Thickness t)
    {
        Add(into, name, t.Left);
        Add(into, name, t.Top);
        Add(into, name, t.Right);
        Add(into, name, t.Bottom);
    }

    private static void Add(List<(string, double)> into, string name, double value)
    {
        if (double.IsFinite(value) && Math.Abs(value) > 0.001)
        {
            into.Add((name, value));
        }
    }

    private static FamilyColour[] Colours(params (string Slot, Brush? Brush)[] brushes)
        => [.. brushes.Where(b => b.Brush is SolidColorBrush).Select(b => Colour(b.Slot, b.Brush))];

    private static FamilyColour Colour(string slot, Brush? brush) => new(slot, Argb(brush), Family.IsData(brush));

    /// <summary>A solid brush's colour as <c>#AARRGGBB</c> (its own opacity folded into the alpha); empty for any other brush.</summary>
    public static string Argb(Brush? brush)
    {
        if (brush is not SolidColorBrush solid)
        {
            return string.Empty;
        }

        var c = solid.Color;
        var alpha = (byte)Math.Round(c.A * Math.Clamp(solid.Opacity, 0, 1));
        return string.Create(CultureInfo.InvariantCulture, $"#{alpha:X2}{c.R:X2}{c.G:X2}{c.B:X2}");
    }
}
#endif
