using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-11111] The sun's graphic and its sizes. A picture says what it is, so a card is the graphic: the arc the card's
/// whole box (a graph's plot, [S-15806]), 280 × 152 a new one, as flat or tall as the card; the day bar one dot tall in
/// the inset at a new card's height, growing as one with a larger card ([S-15807]). Both go down to 64 wide ([S-15808]).
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
            var bar = inset + Dot;
            return WidgetSizing.Range(new(Narrowest, bar), new(Start, bar), new(Widest, bar));
        }

        double Arc(double width) => inset + Math.Floor((width - inset) / 2);
        return WidgetSizing.Range(new(Narrowest, ArcLeast), new(Start, Arc(Start)), new(Widest, Arc(Widest)));
    }

    /// <summary>
    /// [S-15807] The day bar's scale in a content room of <paramref name="width"/> × <paramref name="height"/> (WR-7: a
    /// reading grows as one with its card): as a one-readout block, until its dot fills the room's height or the room's width
    /// is its natural width's share (a new card's, <see cref="Start"/> in the inset) of it; never under its natural size,
    /// whose dot is the glyph floor. Its track always runs the room's whole width.
    /// </summary>
    public static double BarScale(double width, double height)
    {
        if (!(width > 0) || !(height > 0) || !double.IsFinite(width) || !double.IsFinite(height))
        {
            return 1;
        }

        var natural = Start - (2 * FamilySpace.Inset);
        return Math.Max(1, Math.Min(height / Dot, width / natural));
    }

    /// <summary>
    /// [S-15807] Where the day bar's dot stands across a track <paramref name="width"/> long at <paramref name="share"/> of
    /// the day: its share of the track, its centre kept a half dot inside either end so the dot is never cut.
    /// </summary>
    public static double DotAt(double share, double width)
    {
        var half = Dot / 2;
        var at = Math.Clamp(double.IsFinite(share) ? share : 0, 0, 1) * width;
        return width <= Dot ? width / 2 : Math.Clamp(at, half, width - half);
    }
}
