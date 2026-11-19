using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;

namespace Themia.App.Widgets;

/// <summary>
/// [S-12807] [S-12808] Where a measured part's ink starts and ends inside its box (<see cref="LineInk"/>): a family
/// text's leading over its capitals and under its baseline, a row's (<see cref="IInked"/>), and for a panel
/// the part of it that reaches furthest up and down, as it stands in the panel. A glyph, a chart, a bar, anything
/// else inks its whole box. What a <see cref="SpreadStack"/> stands on the insets.
/// </summary>
public static class PartInk
{
    /// <summary>The room in <paramref name="element"/>'s box (its margin included) over its first ink and under its last; measured parts only.</summary>
    public static LineInk Of(UIElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (element.Visibility != Visibility.Visible)
        {
            return LineInk.None;
        }

        var margin = element is FrameworkElement fe ? fe.Margin : default;
        var inner = element switch
        {
            IInked part => part.Ink,
            TextBlock text => text.LineHeight > 0 ? LineInk.Of(new TypeStep(text.FontSize, text.LineHeight, false)) : LineInk.None,
            Border border when border.Child is { } child => Add(Of(child), border.Padding.Top + border.BorderThickness.Top, border.Padding.Bottom + border.BorderThickness.Bottom),
            StackPanel { Orientation: Orientation.Vertical } stack => Add(Stacked(stack.Children), stack.Padding.Top, stack.Padding.Bottom),
            SpreadStack { Orientation: Orientation.Vertical } spread => Stacked(spread.Children),
            Panel panel => Beside(panel),
            _ => LineInk.None,
        };

        return Add(inner, margin.Top, margin.Bottom);
    }

    /// <summary>A vertical stack: its first shown part's room over and its last's under.</summary>
    private static LineInk Stacked(UIElementCollection children)
    {
        var shown = children.Where(c => c.Visibility == Visibility.Visible).ToList();
        return shown.Count == 0 ? LineInk.None : new LineInk(Of(shown[0]).Over, Of(shown[^1]).Under);
    }

    /// <summary>Parts side by side (a grid, a row): the least room over and under any of them, where each stands in the panel's height.</summary>
    private static LineInk Beside(Panel panel)
    {
        // Its own box: what it measured less its own margin (each part's slot holds the part's).
        var own = panel is FrameworkElement self ? self.Margin.Top + self.Margin.Bottom : 0;
        var height = Math.Max(0, panel.DesiredSize.Height - own);
        var rows = panel is Grid grid ? Math.Max(1, grid.RowDefinitions.Count) : 1;
        double over = double.PositiveInfinity, under = double.PositiveInfinity;

        // A picture or a glyph beside a text draws inside its box (its own margins): the text says where the ink is.
        var shown = panel.Children.OfType<FrameworkElement>().Where(c => c.Visibility == Visibility.Visible).ToList();
        var pictures = shown.Where(c => c is Image or FontIcon).ToList();
        if (pictures.Count < shown.Count)
        {
            shown = [.. shown.Except(pictures)];
        }

        foreach (var part in shown)
        {

            // In a grid of rows only the first row's parts can ink its top and only the last row's its foot.
            var row = panel is Grid ? Grid.GetRow(part) : 0;
            var span = panel is Grid ? Grid.GetRowSpan(part) : 1;
            // Only a text's place in the panel is read; any other part (a button, a ring, a canvas the panel sizes) is
            // taken to ink the panel's whole height.
            var texty = part is IInked or TextBlock or Panel or Border;
            var room = texty && part.DesiredSize.Height > 0 ? Math.Max(0, height - part.DesiredSize.Height) : 0;
            // A part shorter than its slot stands centred when it stretches (its own height holds it).
            var top = part.VerticalAlignment switch
            {
                VerticalAlignment.Top => 0,
                VerticalAlignment.Bottom => room,
                _ => room / 2,
            };
            var ink = Of(part);
            if (row == 0)
            {
                over = Math.Min(over, top + ink.Over);
            }

            if (row + span >= rows)
            {
                under = Math.Min(under, room - top + ink.Under);
            }
        }

        return double.IsFinite(over) && double.IsFinite(under) ? new LineInk(over, under) : LineInk.None;
    }

    private static LineInk Add(LineInk ink, double over, double under) => new(ink.Over + over, ink.Under + under);
}
