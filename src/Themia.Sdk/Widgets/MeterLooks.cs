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

/// <summary>[S-6319] What one Core graphs tile shows besides its history: its load's figure, at a step, or none.</summary>
/// <param name="Figure">The figure's step; null when the tile is too small for one.</param>
/// <param name="Inset">Between the figure and the tile's top and leading edges.</param>
public readonly record struct CoreGraphTilePlan(TypeStep? Figure, double Inset);

/// <summary>
/// [S-6317] [S-6319] The Core graphs style: the tiles are the Core grid's ([S-6305],
/// <see cref="BarLayout.Tiles"/>), each a core's history edge to edge with its load in a line above
/// it at the top left. Room at L/XL is a larger figure, nothing more ([S-666]).
/// </summary>
public static class CoreGraphLayout
{
    /// <summary>Between the figure and the tile's edges.</summary>
    public const double Inset = 4;

    /// <summary>The most of a tile's height the figure's line may take: the rest is the history's.</summary>
    public const double FigureShare = 0.4;

    /// <summary>The steps a figure climbs, smallest first.</summary>
    private static readonly TypeStep[] Steps = [FluentType.Caption, FluentType.Body, FluentType.BodyLarge, FluentType.Subtitle];

    /// <summary>
    /// [S-6319] A tile's figure: the largest step whose line takes at most <see cref="FigureShare"/>
    /// of the tile's height and whose widest figure (<paramref name="figureCharacters"/>, "100%")
    /// fits the width inside the inset; none when even a caption does not, and the tile is its history alone.
    /// </summary>
    public static CoreGraphTilePlan Tile(double width, double height, double figureCharacters)
    {
        TypeStep? chosen = null;

        if (double.IsFinite(width) && double.IsFinite(height))
        {
            foreach (var step in Steps)
            {
                if (step.LineHeight <= height * FigureShare
                    && FluentType.EstimateWidth(step, figureCharacters) + (2 * Inset) <= width)
                {
                    chosen = step;
                }
            }
        }

        return new CoreGraphTilePlan(chosen, Inset);
    }
}
