namespace Themia.Core.Geometry;

/// <summary>
/// [S-11893] The one set of rules Core compares geometry by: whether two edges are one edge, two spans lie across each
/// other, two rects overlap, two rects touch (meet side to side) or are the same rect. Every rule that places, packs,
/// joins, settles or selects (the joins, the refit, a block's resize, the work-area settle, the grid settle, the
/// desktop grid, the fences, the displays' stand-ins, the lasso) asks these and keeps no rule or tolerance of its own.
/// <para>
/// Every edge Core compares stands on a lattice: a fraction of its screen on the grid's lines ([S-06], at least a
/// thousandth of the screen apart) or flush on the work area's edge, a device pixel's edge (<see cref="Device"/>), a
/// whole number of cells. Two edges that are not one are at least a line, a pixel or a cell apart, orders of magnitude
/// more than <see cref="Epsilon"/>: the one tolerance absorbs the float noise of fractions added and multiplied, nothing
/// else (until 2026-11-21 six rules for "touching" and four for "the same rect" held nine tolerances between none and
/// half a device pixel, so a lasso, a join and a packed icon could disagree about one pair of edges).
/// </para>
/// </summary>
public static class Lattice
{
    /// <summary>
    /// [S-11893] How far apart two values may be and still be one: float noise, in the unit compared (a fraction of the
    /// screen, a device pixel, a cell). The grid's own count rounds by it too (<see cref="Themia.Core.Widgets.GridCells.Noise"/>).
    /// </summary>
    public const double Epsilon = Themia.Core.Widgets.GridCells.Noise;

    /// <summary>[S-11893] Two values are one edge.</summary>
    public static bool Same(double a, double b) => Math.Abs(a - b) <= Epsilon;

    /// <summary>[S-11893] Two rects are the same rect: every edge one.</summary>
    public static bool Same(FracRect a, FracRect b)
        => Same(a.X, b.X) && Same(a.Y, b.Y) && Same(a.W, b.W) && Same(a.H, b.H);

    /// <summary>How far two spans lie across each other: negative where they are apart.</summary>
    public static double Overlap(double a0, double a1, double b0, double b1) => Math.Min(a1, b1) - Math.Max(a0, b0);

    /// <summary>[S-11893] Two spans lie across each other by more than float noise: ends that only meet do not.</summary>
    public static bool Across(double a0, double a1, double b0, double b1) => Overlap(a0, a1, b0, b1) > Epsilon;

    /// <summary>[S-11893] Two rects share area: rects that only touch do not, nor a sliver of float noise.</summary>
    public static bool Overlaps(FracRect a, FracRect b)
        => a.IsFinite && b.IsFinite && Across(a.X, a.Right, b.X, b.Right) && Across(a.Y, a.Bottom, b.Y, b.Bottom);

    /// <summary>[S-11893] <see cref="Overlaps(FracRect, FracRect)"/> on the device lattice.</summary>
    public static bool Overlaps(DeviceRect a, DeviceRect b)
        => Across(a.X, a.Right, b.X, b.Right) && Across(a.Y, a.Bottom, b.Y, b.Bottom);

    /// <summary>
    /// [S-11893] [S-2248] Whether two rects meet: a side of one on the opposite side of the other, the two lying across
    /// each other along it (a corner alone is no meeting). Null when they do not meet; else whether they are stacked (one
    /// above the other) and whether <paramref name="a"/> is the first (the upper or the left one).
    /// </summary>
    public static (bool Stacked, bool AFirst)? Meeting(FracRect a, FracRect b)
        => Meeting(a.X, a.Y, a.Right, a.Bottom, b.X, b.Y, b.Right, b.Bottom);

    /// <summary>[S-11893] <see cref="Meeting(FracRect, FracRect)"/> on the device lattice: edges in whole device pixels.</summary>
    public static (bool Stacked, bool AFirst)? Meeting(DeviceRect a, DeviceRect b)
        => Meeting(a.X, a.Y, a.Right, a.Bottom, b.X, b.Y, b.Right, b.Bottom);

    /// <summary>[S-11893] Two rects meet side to side (<see cref="Meeting(FracRect, FracRect)"/>).</summary>
    public static bool Touches(FracRect a, FracRect b) => a.IsFinite && b.IsFinite && Meeting(a, b) is not null;

    /// <summary>
    /// [S-11893] Whether <paramref name="next"/> stands on <paramref name="edge"/>'s far side along an axis: its top on
    /// <paramref name="edge"/>'s bottom (<paramref name="vertical"/>), else its left on its right, the two across each other.
    /// </summary>
    public static bool Follows(FracRect edge, FracRect next, bool vertical)
        => Meeting(edge, next) is { AFirst: true } m && m.Stacked == vertical;

    /// <summary>
    /// [S-11893] [S-11809] The rects that touch one another, as sets of indexes: two that meet (<see cref="Touches"/>) are in
    /// one set, and so is everything that touches either. A rect touching nothing is a set of its own. In first-index order.
    /// </summary>
    public static List<List<int>> Touching(IReadOnlyList<FracRect> rects)
    {
        ArgumentNullException.ThrowIfNull(rects);
        var root = Enumerable.Range(0, rects.Count).ToArray();
        int Find(int i) => root[i] == i ? i : root[i] = Find(root[i]);
        for (var i = 0; i < rects.Count; i++)
        {
            for (var j = i + 1; j < rects.Count; j++)
            {
                if (Touches(rects[i], rects[j]))
                {
                    root[Find(i)] = Find(j);
                }
            }
        }

        return [.. Enumerable.Range(0, rects.Count).GroupBy(Find).Select(g => g.ToList())];
    }

    /// <summary>
    /// [S-11893] [S-11873] A painted cell (DIPs on device pixels, <see cref="GridGeometry.PaintedCell"/>) on the device lattice:
    /// each edge the whole device pixel it stands on at <paramref name="rasterScale"/>. Two edges a card shares are one
    /// pixel there, two that are not at least a pixel apart, at 100 % as at 150 %; a non-positive scale counts as 1.
    /// </summary>
    public static DeviceRect Device(DipRect cell, double rasterScale)
    {
        var s = double.IsFinite(rasterScale) && rasterScale > 0 ? rasterScale : 1;
        static double Pixel(double device) => Math.Floor(device + 0.5);
        return DeviceRect.FromEdges(Pixel(cell.X * s), Pixel(cell.Y * s), Pixel(cell.Right * s), Pixel(cell.Bottom * s));
    }

    private static (bool Stacked, bool AFirst)? Meeting(double aL, double aT, double aR, double aB, double bL, double bT, double bR, double bB)
    {
        var acrossX = Across(aL, aR, bL, bR);
        var acrossY = Across(aT, aB, bT, bB);
        return acrossX && Same(aB, bT) ? (true, true)
            : acrossX && Same(bB, aT) ? (true, false)
            : acrossY && Same(aR, bL) ? (false, true)
            : acrossY && Same(bR, aL) ? (false, false)
            : null;
    }
}
