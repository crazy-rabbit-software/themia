using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-11111] The sun's graphic and its sizes. It starts headerless (a picture says what it is), so a
/// card is the graphic in the inset: the arc as wide and as tall as its box (200..480 wide; its height
/// half its content's width at the natural size, a free range round it: a flatter or taller sky), the
/// day bar one dot tall at one height. Nothing else is drawn: the times are the sun times' atom.
/// </summary>
public static class SunLayout
{
    /// <summary>The sun's marker on the arc and on the bar, and the bar's height.</summary>
    public const double Dot = FamilySpace.S3;

    public const double Narrowest = 200;

    public const double Widest = 480;

    /// <summary>A new card's width.</summary>
    public const double Start = 280;

    /// <summary>[S-11111] The graphic's sizing (headerless: the inset round the graphic).</summary>
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
        return WidgetSizing.Range(new(Narrowest, Arc(Narrowest)), new(Start, Arc(Start)), new(Widest, Arc(Widest)));
    }
}
