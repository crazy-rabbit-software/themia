namespace Themia.Core.Widgets;

/// <summary>
/// [S-6302] A half-circle gauge in a box: the arc's centre-line radius and its stroke, the arc
/// sitting on the box's bottom edge (its round caps included) and centred across it.
/// </summary>
/// <param name="Width">The gauge's own box: the arc's outer width.</param>
/// <param name="Height">The arc's outer height, caps included.</param>
/// <param name="Radius">The stroke's centre line.</param>
/// <param name="Thickness">The stroke.</param>
public readonly record struct GaugeGeometry(double Width, double Height, double Radius, double Thickness)
{
    public double CenterX => Width / 2;

    /// <summary>The circle's centre: the outer edge's top is the box's top.</summary>
    public double CenterY => Radius + (Thickness / 2);

    /// <summary>The bowl's edge: what the reading inside must stay clear of.</summary>
    public double InnerRadius => Math.Max(0, Radius - (Thickness / 2));

    /// <summary>
    /// The point at <paramref name="fraction"/> along the arc: 0 the left end, 1 the right end,
    /// clockwise over the top, as a speedometer reads.
    /// </summary>
    public (double X, double Y) PointAt(double fraction)
    {
        var angle = Math.PI * (1 - Math.Clamp(double.IsFinite(fraction) ? fraction : 0, 0, 1));
        return (CenterX + (Radius * Math.Cos(angle)), CenterY - (Radius * Math.Sin(angle)));
    }
}

/// <summary>
/// [S-6302] The gauge's geometry and the reading inside its bowl. Shared by every meter that draws
/// a gauge (<c>GaugeDial</c> in the app), so a CPU and a memory gauge of one size are one size.
/// </summary>
public static class GaugeLayout
{
    /// <summary>The stroke as a share of the radius, before its clamp.</summary>
    public const double ThicknessShare = 0.12;

    public const double MinThickness = 4;

    public const double MaxThickness = 16;

    private static bool Fits(TypeStep step, double inner, HeroBound bound)
    {
        // The glyphs' tops sit about a font size above the centre line; the chord there is the
        // room, with a little air on each side to keep the number off the stroke.
        var rise = step.FontSize;
        if (rise >= inner)
        {
            return false;
        }

        var chord = 2 * Math.Sqrt((inner * inner) - (rise * rise));
        return HeroLayout.ValueWidth(step, bound.Value, bound.Unit) + (2 * Air) <= chord && step.LineHeight <= inner;
    }

    /// <summary>Under this radius a gauge is a squiggle: the reading stands alone.</summary>
    public const double MinRadius = 28;

    /// <summary>Between the reading and the bowl's edge on each side.</summary>
    public const double Air = 4;

    /// <summary>The largest gauge in a box, or null when the box holds none worth drawing.</summary>
    public static GaugeGeometry? Fit(double width, double height)
    {
        if (!double.IsFinite(width) || !double.IsFinite(height) || width <= 0 || height <= 0)
        {
            return null;
        }

        // Outer width 2r + t, outer height r + t (the round caps hang half a stroke under the centre).
        var radius = Math.Min(width / (2 + ThicknessShare), height / (1 + ThicknessShare));
        var thickness = Stroke(radius);
        radius = Math.Floor(Math.Min((width - thickness) / 2, height - thickness));

        if (radius < MinRadius)
        {
            return null;
        }

        thickness = Stroke(radius);
        return new GaugeGeometry((2 * radius) + thickness, radius + thickness, radius, thickness);
    }

    /// <summary>The stroke for a radius: a share of it, whole DIPs, within the clamp.</summary>
    public static double Stroke(double radius) => Math.Round(Math.Clamp(radius * ThicknessShare, MinThickness, MaxThickness));

    /// <summary>
    /// The reading's step inside the bowl: the largest from <paramref name="floor"/> to
    /// <paramref name="ceiling"/> whose line stands on the centre line and whose widest reading
    /// fits the bowl's chord at the top of its glyphs. Never under the floor: the reading is the
    /// point of the tile.
    /// </summary>
    public static TypeStep Reading(GaugeGeometry gauge, HeroBound bound, TypeStep floor, TypeStep ceiling)
    {
        var inner = gauge.InnerRadius;
        var chosen = floor;

        foreach (var step in FluentType.Ramp)
        {
            if (step.FontSize < floor.FontSize || step.FontSize > ceiling.FontSize)
            {
                continue;
            }

            if (Fits(step, inner, bound))
            {
                chosen = step;
            }
        }

        // A large gauge's number grows past Display as a large tile's reading does ([S-1762]),
        // to at most HeroLayout.MaxGrowth, while it still fits the bowl.
        if (chosen.FontSize >= FluentType.Display.FontSize && ceiling.FontSize >= FluentType.Display.FontSize)
        {
            for (var factor = HeroLayout.MaxGrowth; factor > 1.0001; factor -= 0.05)
            {
                var grown = FluentType.Scaled(FluentType.Display, factor);
                if (Fits(grown, inner, bound))
                {
                    return grown;
                }
            }
        }

        return chosen;
    }
}
