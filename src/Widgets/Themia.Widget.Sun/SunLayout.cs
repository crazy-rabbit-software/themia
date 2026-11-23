using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-11111] The sun's graphic and its sizes. A picture says what it is, so a card is the graphic, a plot the card's whole
/// box in either style ([S-15812]): the arc 280 × 152 a new one, as flat or tall as the card; the day bar 280 × 36 a new
/// one (its dot in the inset's height). Both go down to 64 wide ([S-15808]).
/// Nothing else is drawn: the times are the sun times' atom.
/// </summary>
public static class SunLayout
{
    /// <summary>The sun's marker on the arc and on the bar, and the bar's height.</summary>
    public const double Dot = FamilySpace.S3;

    /// <summary>
    /// [S-15808] WR-14 (owner, 2026-11-19: "for the widgets that can scale there should basically be no lower limit except
    /// for one that makes sense so you can still see the widget"): the least width at which the day still reads, its 12
    /// marker (a glyph's floor, <see cref="ScaleFloor"/>) on a 40 track or a 64 arc.
    /// </summary>
    public const double Narrowest = 64;

    /// <summary>[S-15808] The arc's least height: its noon marker over a horizon, the night's dip under it (WR-14: over the grid's 32).</summary>
    public const double ArcLeast = 40;

    public const double Widest = 480;

    /// <summary>A new card's width.</summary>
    public const double Start = 280;

    /// <summary>[S-11111] The graphic's sizing.</summary>
    public static WidgetSizing Sizing(SunConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var inset = 2 * FamilySpace.Inset;
        if (config.Style == SunStyle.DayBar)
        {
            // [S-17101] The bar is one line: the card keeps its height.
            var bar = inset + Dot;
            return WidgetSizing.Range(new(Narrowest, bar), new(Start, bar), new(Widest, bar)) with { KeepsHeight = true };
        }

        double Arc(double width) => inset + Math.Floor((width - inset) / 2);
        // [S-17100] The arc is half as tall as wide: a taller card would stand room over and under it.
        return WidgetSizing.Range(new(Narrowest, ArcLeast), new(Start, Arc(Start)), new(Widest, Arc(Widest))).ScalesAsOne();
    }

    /// <summary>
    /// [S-15812] Where the day bar's dot stands across a plot <paramref name="width"/> wide at <paramref name="share"/> of
    /// the day: its share of the width, its centre kept a half dot inside either side so the dot is never cut.
    /// </summary>
    public static double DotAt(double share, double width)
    {
        var half = Dot / 2;
        var at = Math.Clamp(double.IsFinite(share) ? share : 0, 0, 1) * width;
        return width <= Dot ? width / 2 : Math.Clamp(at, half, width - half);
    }
}
