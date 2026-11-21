using Themia.Core.Widgets;

namespace Themia.Core.Geometry;

/// <summary>
/// [S-11807] [S-11809] What a placement's new widgets are sized with on its display: the display's
/// content <see cref="Scale"/> ([S-6532]), with whole grid cells (the grid is always on, [S-06]).
/// </summary>
public readonly record struct PlacementSizes(double Scale);

/// <summary>
/// [S-11807] The snap grid's hold on a widget's size (owner's report 2026-11-15: "the widgets should
/// always perfectly align to the grid in width and height"). A kind's sizing stays its content's
/// ([S-2438], DIPs); with the grid on, the host fits it to the grid, so every widget's cell is whole grid
/// cells on both axes and, standing on a grid line, ends on one. Pure: the card, the resize, the
/// keyboard, the refit and the placement session ask it, and the tests pin it.
/// <list type="bullet">
/// <item>Min rounds <b>up</b> to whole cells (content never gets less than it asked for), never under one
/// cell.</item>
/// <item>Max rounds <b>down</b>, but never under the rounded Min, so a range narrower than a
/// cell becomes one fixed size of whole cells (its content gets up to a cell of spare room, laid out as
/// the kind lays out spare room).</item>
/// <item>[S-14500] Every whole cell between them is a size a resize lands on: no axis steps.</item>
/// </list>
/// The grid is the overlay's ([S-06]): <see cref="GridGeometry.GridDims"/> lines across the screen, so
/// a cell is <c>1 / lines</c> of the screen, which a card paints whole ([S-11884]).
/// </summary>
public static class GridFit
{
    /// <summary>
    /// [S-11807] Where grid line <paramref name="index"/> of <paramref name="count"/> across a screen of
    /// <paramref name="screenDip"/> is drawn: the whole DIP a card edge standing on that line is put on
    /// (<see cref="GridGeometry.PaintedCell"/> rounds every edge to a whole DIP first), so a card's edge and
    /// the line share their pixels on a screen whose DIPs are no whole multiple of the cell.
    /// </summary>
    public static double LineDip(int index, int count, double screenDip)
        => count > 0 ? GridGeometry.WholeDip(index / (double)count * screenDip) : 0;

    /// <summary>[S-11807] <paramref name="frac"/> rounded up to whole cells of <c>1 / lines</c>, at least one cell. No grid (lines ≤ 0): unchanged.</summary>
    public static double Up(double frac, int lines)
        => lines > 0 && double.IsFinite(frac) ? Cells(frac, lines) / (double)lines : frac;

    /// <summary>
    /// [S-11807] How many whole cells of <c>1 / lines</c> hold <paramref name="frac"/> of the screen along an axis: the
    /// grid's one count (<see cref="GridCells.Up"/>), what the host lays a widget's box in (<see cref="Up"/>).
    /// </summary>
    public static int Cells(double frac, int lines) => lines > 0 && double.IsFinite(frac) ? GridCells.Up(frac * lines) : 1;

    /// <summary>[S-11807] <paramref name="frac"/> rounded down to whole cells, at least one cell. No grid: unchanged.</summary>
    public static double Down(double frac, int lines)
        => lines > 0 && double.IsFinite(frac) ? GridCells.Down(frac * lines) / (double)lines : frac;

    /// <summary>
    /// [S-11807] One axis of a sizing, stated in content (<see cref="SizeFit.Limits(Sdk.WidgetSizing, ScreenMetrics, double, double, bool)"/>
    /// without the grid), fitted to <paramref name="lines"/> grid lines across the screen: Min up, Max down but never
    /// under Min. No grid (lines ≤ 0): the axis as it is.
    /// </summary>
    public static AxisLimits Axis(AxisLimits content, int lines)
    {
        if (lines <= 0)
        {
            return content;
        }

        var min = Up(content.Min, lines);
        return new AxisLimits(min, Math.Max(min, Down(content.Max, lines)), lines);
    }

    /// <summary>[S-11807] A sizing's limits on <paramref name="screen"/> fitted to its snap grid, both axes.</summary>
    public static SizeLimits OnGrid(SizeLimits content, ScreenMetrics screen)
    {
        ArgumentNullException.ThrowIfNull(content);
        var (cols, rows) = GridGeometry.GridDims(screen);
        return new SizeLimits(Axis(content.Width, cols), Axis(content.Height, rows));
    }

    /// <summary>
    /// [S-11808] [S-11809] Several widgets laid out together (a ready-made group's boxes, a group's
    /// members) fitted as one block, never box by box: every edge they share stays shared, so cards that
    /// touch still touch ([S-2240], [S-2248]) and none comes to overlap another; each box gets at least
    /// the size its own limits hold it to (<see cref="AxisLimits.Range"/>: what the desktop then paints it at,
    /// [S-2446]); the order of edges is kept. Every edge
    /// is a grid line (the limits are on the grid, <see cref="SizeFit.Limits(WidgetInstance, ScreenMetrics, double, double)"/>):
    /// space between boxes keeps its nearest whole cells and the block's top-left corner lands on the
    /// nearest line. <paramref name="asked"/>, when given, is the size (W, H, fractions)
    /// each box asks for in place of its own (a recipe's box is its card's DIPs: its cell at the
    /// display's content scale plus the gap is bigger, [S-11809]). A box with no limits keeps what it
    /// asks (whole cells of it on the grid). No usable screen: the boxes as they are.
    /// </summary>
    public static IReadOnlyList<FracRect> Layout(IReadOnlyList<FracRect> boxes, IReadOnlyList<SizeLimits?> limits, ScreenMetrics screen, IReadOnlyList<(double W, double H)>? asked = null)
    {
        ArgumentNullException.ThrowIfNull(boxes);
        ArgumentNullException.ThrowIfNull(limits);
        if (!screen.IsUsable || boxes.Count == 0 || boxes.Any(b => !b.IsFinite))
        {
            return boxes;
        }

        (double W, double H) Asked(int i) => asked is not null && i < asked.Count ? asked[i] : (boxes[i].W, boxes[i].H);
        var (cols, rows) = GridGeometry.GridDims(screen);
        var xs = LayoutAxis([.. boxes.Select((b, i) => (b.X, b.Right, Asked(i).W))], [.. boxes.Select((_, i) => i < limits.Count ? limits[i]?.Width : null)], cols);
        var ys = LayoutAxis([.. boxes.Select((b, i) => (b.Y, b.Bottom, Asked(i).H))], [.. boxes.Select((_, i) => i < limits.Count ? limits[i]?.Height : null)], rows);
        return [.. boxes.Select((_, i) => new FracRect(xs[i].Start, ys[i].Start, xs[i].Size, ys[i].Size))];
    }

    /// <summary>
    /// One axis of <see cref="Layout"/>, in cells on the grid (<paramref name="lines"/> &gt; 0) or in
    /// fractions off it: each distinct edge, in order, gets a place no nearer the one before than the
    /// space between them (its nearest whole cells on the grid), and far enough on for every box ending
    /// there to get its own size (a longest path over the edges). A box then spans from its start's place
    /// to its end's, held to its limits where they allow less, never past its end's place.
    /// </summary>
    private static (double Start, double Size)[] LayoutAxis((double Start, double End, double Asked)[] spans, AxisLimits?[] limits, int lines)
    {
        static double Key(double v) => Math.Round(v, 9);
        var grid = lines > 0;
        double unit = grid ? lines : 1;
        double Snap(double v) => grid ? GridSpace.Nearest(v) : v;

        // A box's own size for a span (in fractions), in units: what its limits hold it to.
        double Own(int i, double span) => Snap((limits[i] is { } l ? l.Range(span) : grid ? Up(span, lines) : span) * unit);

        var edges = spans.SelectMany(s => new[] { Key(s.Start), Key(s.End) }).Distinct().Order().ToArray();
        var line = new Dictionary<double, double>(edges.Length) { [edges[0]] = Snap(edges[0] * unit) };
        var own = spans.Select((s, i) => Own(i, s.Asked)).ToArray();

        for (var k = 1; k < edges.Length; k++)
        {
            var at = line[edges[k - 1]] + Math.Max(0, Snap((edges[k] - edges[k - 1]) * unit));
            for (var i = 0; i < spans.Length; i++)
            {
                if (Key(spans[i].End) == edges[k])
                {
                    at = Math.Max(at, line[Key(spans[i].Start)] + own[i]);
                }
            }

            line[edges[k]] = at;
        }

        return [.. spans.Select((s, i) =>
        {
            var start = line[Key(s.Start)];
            var span = line[Key(s.End)] - start;
            return (start / unit, Math.Min(span, Own(i, span / unit)) / unit);
        })];
    }
}
