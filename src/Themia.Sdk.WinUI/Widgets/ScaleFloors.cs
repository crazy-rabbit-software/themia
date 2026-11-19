using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>[S-12833] The least scale of a face's parts: their smallest text and glyph at the legibility floor (<see cref="ScaleFloor"/>).</summary>
public static class ScaleFloors
{
    /// <summary>The least scale <paramref name="parts"/> may be drawn at.</summary>
    public static double Of(IEnumerable<UIElement> parts)
    {
        ArgumentNullException.ThrowIfNull(parts);
        double text = double.PositiveInfinity, glyph = double.PositiveInfinity;
        foreach (var part in parts)
        {
            Walk(part, ref text, ref glyph);
        }

        return ScaleFloor.Least(double.IsFinite(text) ? text : 0, double.IsFinite(glyph) ? glyph : 0);
    }

    private static void Walk(UIElement node, ref double text, ref double glyph)
    {
        if (node.Visibility != Visibility.Visible)
        {
            return;
        }

        switch (node)
        {
            case TextBlock block:
                if (block.Text.Length > 0 || block.Inlines.Count > 0)
                {
                    text = Math.Min(text, block.FontSize);
                    foreach (var inline in block.Inlines)
                    {
                        if (inline is Run { Text.Length: > 0 } run)
                        {
                            text = Math.Min(text, run.FontSize);
                        }
                    }
                }

                break;
            case FontIcon icon when icon.Glyph.Length > 0:
                glyph = Math.Min(glyph, icon.FontSize);
                break;
            case Panel panel:
                foreach (var child in panel.Children)
                {
                    Walk(child, ref text, ref glyph);
                }

                break;
            case Border { Child: { } child }:
                Walk(child, ref text, ref glyph);
                break;
            case ContentControl { Content: UIElement content }:
                Walk(content, ref text, ref glyph);
                break;
            case Viewbox { Child: { } child }:
                Walk(child, ref text, ref glyph);
                break;
        }
    }
}
