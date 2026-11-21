using Themia.Core.Geometry;

namespace Themia.Core.Desktop;

/// <summary>
/// [S-15667] The room taken on a display's grid, looked up by cell: every rect added is filed under the grid cells it
/// reaches (one cell more on each side), so whether a box is free asks only the rects filed under the cells it covers
/// instead of every rect there is. The answer is the one rule's (<see cref="DesktopGrid"/>: edges that only touch do not
/// overlap; the first rect added that overlaps is the one in the way). A free spot searched over the whole grid
/// (<see cref="DesktopGrid.NearestFree(ValueTuple{double, double}, double, double, Occupancy, ScreenMetrics)"/>) costs the
/// cells it covers per spot, not the widgets.
/// </summary>
public sealed class Occupancy
{
    private readonly int _cols;
    private readonly int _rows;
    private readonly List<FracRect> _rects = [];
    private readonly List<int>?[] _cells;

    /// <summary>An empty grid of <paramref name="screen"/>'s cells.</summary>
    public Occupancy(ScreenMetrics screen)
    {
        var (cols, rows) = GridGeometry.GridDims(screen);
        _cols = Math.Max(1, cols);
        _rows = Math.Max(1, rows);
        _cells = new List<int>?[_cols * _rows];
    }

    /// <summary><paramref name="rects"/> on <paramref name="screen"/>'s grid, in their order.</summary>
    public static Occupancy Of(IEnumerable<FracRect> rects, ScreenMetrics screen)
    {
        ArgumentNullException.ThrowIfNull(rects);
        var occupancy = new Occupancy(screen);
        foreach (var rect in rects)
        {
            occupancy.Add(rect);
        }

        return occupancy;
    }

    /// <summary>The rects added, in their order.</summary>
    public IReadOnlyList<FracRect> Rects => _rects;

    /// <summary>Take <paramref name="rect"/>'s room. A rect that is not finite takes none (it overlaps nothing).</summary>
    public void Add(FracRect rect)
    {
        var index = _rects.Count;
        _rects.Add(rect);
        if (!rect.IsFinite || Span(rect) is not var (c0, c1, r0, r1))
        {
            return;
        }

        for (var r = r0; r <= r1; r++)
        {
            for (var c = c0; c <= c1; c++)
            {
                (_cells[(r * _cols) + c] ??= []).Add(index);
            }
        }
    }

    /// <summary>The first rect added that overlaps <paramref name="rect"/> (edges that only touch do not), or null.</summary>
    public FracRect? Blocker(FracRect rect)
    {
        if (!rect.IsFinite || Span(rect) is not var (c0, c1, r0, r1))
        {
            return null;
        }

        var first = int.MaxValue;
        for (var r = r0; r <= r1; r++)
        {
            for (var c = c0; c <= c1; c++)
            {
                if (_cells[(r * _cols) + c] is not { } filed)
                {
                    continue;
                }

                foreach (var i in filed)
                {
                    if (i < first && Overlaps(rect, _rects[i]))
                    {
                        first = i;
                    }
                }
            }
        }

        return first == int.MaxValue ? null : _rects[first];
    }

    /// <summary>The one overlap rule, the lattice's ([S-11893]): the two share more than an edge.</summary>
    internal static bool Overlaps(FracRect rect, FracRect other) => Lattice.Overlaps(rect, other);

    /// <summary>
    /// The cells <paramref name="rect"/> reaches, one more on each side, kept on the grid (a rect past the display's edge is
    /// filed at the edge): two rects that overlap always share one.
    /// </summary>
    private (int C0, int C1, int R0, int R1)? Span(FracRect rect)
    {
        static int Clamp(double v, int n) => v <= 0 ? 0 : v >= n - 1 ? n - 1 : (int)v;

        var c0 = Clamp(Math.Floor(rect.X * _cols) - 1, _cols);
        var c1 = Clamp(Math.Ceiling(rect.Right * _cols) + 1, _cols);
        var r0 = Clamp(Math.Floor(rect.Y * _rows) - 1, _rows);
        var r1 = Clamp(Math.Ceiling(rect.Bottom * _rows) + 1, _rows);
        return c1 < c0 || r1 < r0 ? null : (c0, c1, r0, r1);
    }
}
