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
        var (text, glyph) = Smallest(parts);
        return ScaleFloor.Least(double.IsFinite(text) ? text : 0, double.IsFinite(glyph) ? glyph : 0);
    }

    /// <summary>
    /// [S-12833] The least scale <paramref name="parts"/> may be drawn at, a face stating its main reading's size
    /// (<paramref name="main"/>, <c>FaceParts.Main</c>): their smallest text's or glyph's, else, with none drawn (a picture),
    /// the main reading's.
    /// </summary>
    public static double Of(IEnumerable<UIElement> parts, double main)
    {
        var (text, glyph) = Smallest(parts);
        return double.IsFinite(text) || double.IsFinite(glyph)
            ? ScaleFloor.Least(double.IsFinite(text) ? text : 0, double.IsFinite(glyph) ? glyph : 0)
            : ScaleFloor.Least(main);
    }

    private static (double Text, double Glyph) Smallest(IEnumerable<UIElement> parts)
    {
        ArgumentNullException.ThrowIfNull(parts);
        double text = double.PositiveInfinity, glyph = double.PositiveInfinity;
        foreach (var part in parts)
        {
            Walk(part, ref text, ref glyph);
        }

        return (text, glyph);
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
