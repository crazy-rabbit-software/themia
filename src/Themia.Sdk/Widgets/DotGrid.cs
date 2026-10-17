namespace Themia.Core.Widgets;

/// <summary>
/// [S-6061] A grid of equal dots, reflowed to the box: as many columns as make the dots largest.
/// </summary>
/// <param name="Columns">Dots across.</param>
/// <param name="Rows">Dots down (the last row may be short).</param>
/// <param name="Pitch">From one dot's centre to the next, both ways, in DIPs.</param>
/// <param name="Diameter">A dot's own size: the pitch less the gap between dots.</param>
public readonly record struct DotGridPlan(int Columns, int Rows, double Pitch, double Diameter)
{
    /// <summary>The grid's own size, for centring it in the box.</summary>
    public double Width => Columns * Pitch;

    public double Height => Rows * Pitch;

    /// <summary>The column and row of dot <paramref name="index"/>, reading across then down.</summary>
    public (int Column, int Row) Cell(int index) => Columns <= 0 ? (0, 0) : (index % Columns, index / Columns);
}

/// <summary>
/// [S-6061] The reflow rule for every dot grid (Year progress's days, the Moon's month): try every
/// column count and keep the one whose square cells are largest; on a tie, the one with fewer empty
/// cells in the last row. The gap is a share of the pitch, so a larger tile spaces the dots out in
/// proportion rather than drawing larger dots in the same mesh.
/// </summary>
public static class DotGrid
{
    /// <summary>The gap between two dots as a share of the pitch.</summary>
    public const double GapShare = 0.25;

    /// <summary>
    /// The plan for <paramref name="count"/> dots in a <paramref name="width"/> × <paramref name="height"/> box.
    /// <paramref name="maxColumns"/> caps the columns (0 for none): a month reads as weeks of seven.
    /// </summary>
    public static DotGridPlan Fit(int count, double width, double height, int maxColumns = 0)
    {
        if (count <= 0 || !(width > 0) || !(height > 0) || !double.IsFinite(width) || !double.IsFinite(height))
        {
            return new DotGridPlan(0, 0, 0, 0);
        }

        var cap = maxColumns > 0 ? Math.Min(maxColumns, count) : count;
        var best = new DotGridPlan(1, count, 0, 0);
        var bestWaste = int.MaxValue;

        for (var columns = 1; columns <= cap; columns++)
        {
            var rows = (count + columns - 1) / columns;
            var pitch = Math.Min(width / columns, height / rows);
            var waste = (columns * rows) - count;

            if (pitch > best.Pitch + 1e-9 || (Math.Abs(pitch - best.Pitch) <= 1e-9 && waste < bestWaste))
            {
                best = new DotGridPlan(columns, rows, pitch, pitch * (1 - GapShare));
                bestWaste = waste;
            }
        }

        return best;
    }
}
