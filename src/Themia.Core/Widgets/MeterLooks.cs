namespace Themia.Core.Widgets;

/// <summary>
/// [S-6301] How strongly one item's load is drawn (a core's tile): the usage pressure's colour
/// ([S-1123]) at an opacity that rises with the load, so a grid of cores reads as a heat map in the
/// theme's own accent, caution and critical colours, never a colour of its own.
/// </summary>
/// <param name="Pressure">The colour: accent, caution from 70 %, critical from 90 %.</param>
/// <param name="Strength">The opacity, <see cref="LoadTint.Faintest"/> at idle to 1 at full load.</param>
/// <param name="Known">False for an item with no reading: drawn in the disabled colour.</param>
public readonly record struct LoadShade(MeterPressure Pressure, double Strength, bool Known);

/// <summary>[S-6301] The shade of a load.</summary>
public static class LoadTint
{
    /// <summary>An idle item is still a tile you can see, not a hole.</summary>
    public const double Faintest = 0.22;

    /// <summary>The shade of <paramref name="fraction"/> (0..1); unknown when there is none.</summary>
    public static LoadShade Of(double? fraction)
    {
        if (fraction is not { } f || !double.IsFinite(f))
        {
            return new LoadShade(MeterPressure.Normal, Faintest, false);
        }

        var clamped = Math.Clamp(f, 0, 1);
        return new LoadShade(MeterPressures.For(PressureScale.Usage, clamped), Faintest + ((1 - Faintest) * clamped), true);
    }
}

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

/// <summary>[S-6303] Vertical bars side by side, one per item (a core): each bar's width and the gap between.</summary>
/// <param name="Bar">One bar's width, at least 1.</param>
/// <param name="Gap">Between two bars: 4, then 2, then 1, then 0 as the items crowd.</param>
public readonly record struct BarColumnsPlan(double Bar, double Gap);

/// <summary>[S-6304] Labelled horizontal bars, one row per item (an engine, memory used and free).</summary>
/// <param name="Shown">How many rows fit, from the first.</param>
/// <param name="Label">The label's and value's step on the row's line.</param>
/// <param name="Thickness">The bar under the line.</param>
/// <param name="Spacing">Between two rows.</param>
public readonly record struct BarRowsPlan(int Shown, TypeStep Label, double Thickness, double Spacing)
{
    /// <summary>The line, the gap under it and the bar.</summary>
    public double RowHeight => Label.LineHeight + BarLayout.LineGap + Thickness;
}

/// <summary>[S-6305] A grid of tiles, one per item (a core), filling the box.</summary>
public readonly record struct TileGridPlan(int Columns, int Rows, double TileWidth, double TileHeight, double Gap);

/// <summary>
/// [S-6303]..[S-6305] The layouts of the shared bar list and tile grid. Room left at a large size
/// goes into the items themselves (thicker bars, a larger label), never into more parts ([S-666]).
/// </summary>
public static class BarLayout
{
    /// <summary>Between a row's line and its bar.</summary>
    public const double LineGap = 4;

    /// <summary>Between a row's label and its value on the line.</summary>
    public const double LineSpace = 8;

    /// <summary>The bar thicknesses a row climbs, thinnest first: a progress bar's 4 up to 12.</summary>
    public static IReadOnlyList<double> Thicknesses { get; } = [4, 6, 8, 12];

    /// <summary>[S-6303] Columns for <paramref name="count"/> items across <paramref name="width"/>.</summary>
    public static BarColumnsPlan Columns(double width, int count)
    {
        if (count <= 0 || !double.IsFinite(width) || width <= 0)
        {
            return new BarColumnsPlan(0, 0);
        }

        // The widest gap that still leaves each bar at least twice as wide as the gap.
        foreach (var gap in (ReadOnlySpan<double>)[4, 2, 1])
        {
            var bar = (width - (gap * (count - 1))) / count;
            if (bar >= 2 * gap)
            {
                return new BarColumnsPlan(Math.Floor(bar * 100) / 100, gap);
            }
        }

        return new BarColumnsPlan(Math.Max(1, width / count), 0);
    }

    /// <summary>
    /// [S-6304] Rows for <paramref name="count"/> items in <paramref name="height"/>: every row at
    /// the largest label step (Caption, Body, BodyLarge) and the thickest bar that let all of them
    /// fit; when even the smallest does not, as many smallest rows as fit, from the first. A label
    /// step is taken only when the widest line (<paramref name="lineCharacters"/>, the label and the
    /// value together) fits <paramref name="width"/>, so a narrow tile writes smaller rather than
    /// cutting the label.
    /// </summary>
    public static BarRowsPlan Rows(double height, int count, double spacing, double width = double.PositiveInfinity, double lineCharacters = 0)
    {
        var smallest = new BarRowsPlan(0, FluentType.Caption, Thicknesses[0], spacing);

        if (count <= 0 || !double.IsFinite(height) || height <= 0)
        {
            return smallest;
        }

        BarRowsPlan? best = null;

        foreach (var label in (TypeStep[])[FluentType.Caption, FluentType.Body, FluentType.BodyLarge])
        {
            foreach (var thickness in Thicknesses)
            {
                // A bar is never more than half its label's line: it underlines the figure, it is not a slab.
                if (thickness > label.LineHeight / 2
                    || (label != FluentType.Caption && FluentType.EstimateWidth(label, lineCharacters) + LineSpace > width))
                {
                    continue;
                }

                var plan = new BarRowsPlan(count, label, thickness, spacing);
                if (Total(plan, count) <= height + 1e-9)
                {
                    best = plan;
                }
            }
        }

        if (best is { } all)
        {
            return all;
        }

        var shown = (int)Math.Floor((height + spacing) / (smallest.RowHeight + spacing));
        return smallest with { Shown = Math.Clamp(shown, 0, count) };
    }

    /// <summary>The height <paramref name="count"/> rows of <paramref name="plan"/> take.</summary>
    public static double Total(BarRowsPlan plan, int count)
        => count <= 0 ? 0 : (count * plan.RowHeight) + ((count - 1) * plan.Spacing);

    /// <summary>
    /// [S-6305] Tiles for <paramref name="count"/> items filling <paramref name="width"/> ×
    /// <paramref name="height"/>: the column count whose tiles are the largest and closest to square
    /// (the smaller side the biggest), the last row left short. The gap shrinks from 4 to 2 to 1 as
    /// the tiles get small.
    /// </summary>
    public static TileGridPlan Tiles(double width, double height, int count)
    {
        if (count <= 0 || !double.IsFinite(width) || !double.IsFinite(height) || width <= 0 || height <= 0)
        {
            return new TileGridPlan(0, 0, 0, 0, 0);
        }

        foreach (var gap in (ReadOnlySpan<double>)[4, 2, 1])
        {
            var plan = BestGrid(width, height, count, gap);
            if (Math.Min(plan.TileWidth, plan.TileHeight) >= 3 * gap)
            {
                return plan;
            }
        }

        return BestGrid(width, height, count, 0);
    }

    /// <summary>A grid whose tiles are at least this share of the largest grid's is as good, if it leaves fewer holes.</summary>
    public const double EvenGridShare = 0.8;

    private static TileGridPlan BestGrid(double width, double height, int count, double gap)
    {
        var grids = new List<(TileGridPlan Plan, double Side, int Empty)>();

        for (var columns = 1; columns <= count; columns++)
        {
            var rows = (count + columns - 1) / columns;
            var tileWidth = (width - (gap * (columns - 1))) / columns;
            var tileHeight = (height - (gap * (rows - 1))) / rows;

            if (tileWidth > 0 && tileHeight > 0)
            {
                grids.Add((new TileGridPlan(columns, rows, tileWidth, tileHeight, gap), Math.Min(tileWidth, tileHeight), (columns * rows) - count));
            }
        }

        if (grids.Count == 0)
        {
            return new TileGridPlan(count, 1, 0, 0, gap);
        }

        // The largest tile, unless a grid nearly as large fills its last row (16 cores as 4 × 4,
        // not 6 + 6 + 4): the fewest holes among those, then the larger tile, then fewer columns.
        var largest = grids.Max(g => g.Side);
        return grids
            .Where(g => g.Side >= (largest * EvenGridShare) - 1e-9)
            .OrderBy(g => g.Empty)
            .ThenByDescending(g => g.Side)
            .ThenBy(g => g.Plan.Columns)
            .First().Plan;
    }
}
