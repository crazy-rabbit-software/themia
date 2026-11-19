namespace Themia.Core.Widgets;

/// <summary>
/// [S-12833] How small a face that scales may draw (owner, 2026-11-19: "you should be able to make the clock even
/// smaller than it is right now. for the widgets that can scale there should basically be no lower limit except for one
/// that makes sense so you can still see the widget."): a face that scales as one (a clock, a date, a reading on
/// <c>ReadingFit</c> or <c>InkFit</c>) scales down as well as up, until its smallest text is <see cref="Text"/> DIP or
/// its smallest glyph <see cref="Glyph"/>; its kind's least size is its natural box at that scale, never under
/// <see cref="Themia.Sdk.WidgetSizing.LeastScaled"/>. One floor for every kind.
/// </summary>
public static class ScaleFloor
{
    /// <summary>The smallest a text is drawn, in DIPs: a caption still read at arm's length.</summary>
    public const double Text = 9;

    /// <summary>The smallest a glyph is drawn, in DIPs.</summary>
    public const double Glyph = 12;

    /// <summary>The least scale of a face whose smallest text is <paramref name="smallestText"/> and smallest glyph <paramref name="smallestGlyph"/> (0: none).</summary>
    public static double Least(double smallestText, double smallestGlyph = 0)
    {
        var byText = smallestText > 0 && double.IsFinite(smallestText) ? Text / smallestText : 0;
        var byGlyph = smallestGlyph > 0 && double.IsFinite(smallestGlyph) ? Glyph / smallestGlyph : 0;
        var least = Math.Max(byText, byGlyph);
        return least > 0 ? Math.Min(1, least) : 1;
    }

    /// <summary>A least scale held to (0, 1]: a degenerate one is 1 (never shrinks).</summary>
    public static double Clamp(double least) => least > 0 && double.IsFinite(least) ? Math.Min(1, least) : 1;
}
