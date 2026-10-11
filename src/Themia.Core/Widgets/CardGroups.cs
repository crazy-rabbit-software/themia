using Themia.Core.Geometry;
using Themia.Core.Model;

namespace Themia.Core.Widgets;

/// <summary>
/// One card on a surface, as grouping sees it: the cell it owns (in the surface's DIPs, as
/// <see cref="GridGeometry.ToPixels"/> resolves it) and the card it paints, or null for one that
/// paints none (a transparent widget, a view hiding itself, [S-873]).
/// </summary>
public readonly record struct GroupCandidate(PixelRect Cell, WidgetSurface? Surface);

/// <summary>[S-2100] Cards that share one background: indexes into the candidate list, ascending.</summary>
public sealed record CardGroup(IReadOnlyList<int> Members);

/// <summary>
/// [S-2103] One corner of a group's outline, ready to draw: a straight line arrives at
/// <see cref="InX"/>,<see cref="InY"/>, an arc of <see cref="Radius"/> leaves at
/// <see cref="OutX"/>,<see cref="OutY"/>. <see cref="Convex"/> corners turn clockwise on screen
/// (y down); concave ones turn the other way. A radius of zero is a sharp corner (In = Out).
/// </summary>
public readonly record struct OutlineCorner(double X, double Y, bool Convex, double Radius, double InX, double InY, double OutX, double OutY);

/// <summary>
/// [S-2103] A group's painted shape: closed loops of corners (the outer edge clockwise, any hole
/// counter-clockwise, so even-odd and non-zero filling agree) and the rectangles that cover exactly
/// that shape, for the window's hit region ([S-2105]).
/// </summary>
public sealed record CardOutline(IReadOnlyList<IReadOnlyList<OutlineCorner>> Loops, IReadOnlyList<PixelRect> Region);

/// <summary>
/// [S-2100]..[S-2106] Widgets that touch share one card. Pure: which cards form a group, and the
/// outline the group's background follows. The shell draws what this answers and nothing else, so
/// the grouping rule, its tolerance and the corner shapes are all tested here.
/// </summary>
public static class CardGroups
{
    /// <summary>
    /// [S-2101] How far apart two edges may be and still meet: half a snap cell. Snapped neighbours
    /// meet exactly (or within a rounding pixel); one empty cell between them is a whole cell, so it
    /// is never mistaken for a touch.
    /// </summary>
    public static double Tolerance(ScreenMetrics screen)
    {
        if (!screen.IsUsable)
        {
            return GridGeometry.MinCellPx / 2;
        }

        var (cols, rows) = GridGeometry.GridDims(screen);
        return Math.Min(screen.Width / cols, screen.Height / rows) / 2;
    }

    /// <summary>
    /// [S-2100]..[S-2102] The groups of two or more cards on one surface. Two cards join when both
    /// paint the same kind of card, neither overlaps any other card, and they share an edge: the
    /// facing edges within <paramref name="tolerance"/> of each other and the shared run longer than
    /// it (a corner alone is not a touch). Joining is transitive. A card in no group is not listed.
    /// </summary>
    public static IReadOnlyList<CardGroup> Find(IReadOnlyList<GroupCandidate> cards, double tolerance)
    {
        ArgumentNullException.ThrowIfNull(cards);
        var tol = double.IsFinite(tolerance) && tolerance >= 0 ? tolerance : 0;
        var n = cards.Count;

        // [S-2102] A card overlapping another keeps its own card: the group's fill is painted under
        // every widget, so a member stacked on (or under) another would show it through.
        var overlaps = new bool[n];
        for (var i = 0; i < n; i++)
        {
            for (var j = i + 1; j < n; j++)
            {
                if (Overlap(cards[i].Cell, cards[j].Cell, tol))
                {
                    overlaps[i] = overlaps[j] = true;
                }
            }
        }

        var parent = new int[n];
        for (var i = 0; i < n; i++)
        {
            parent[i] = i;
        }

        int Root(int i)
        {
            while (parent[i] != i)
            {
                parent[i] = parent[parent[i]];
                i = parent[i];
            }

            return i;
        }

        for (var i = 0; i < n; i++)
        {
            if (!Joins(cards[i], overlaps[i]))
            {
                continue;
            }

            for (var j = i + 1; j < n; j++)
            {
                if (Joins(cards[j], overlaps[j]) && cards[i].Surface == cards[j].Surface && Touch(cards[i].Cell, cards[j].Cell, tol))
                {
                    parent[Root(j)] = Root(i);
                }
            }
        }

        var byRoot = new Dictionary<int, List<int>>();
        for (var i = 0; i < n; i++)
        {
            if (!Joins(cards[i], overlaps[i]))
            {
                continue;
            }

            var root = Root(i);
            if (!byRoot.TryGetValue(root, out var members))
            {
                byRoot[root] = members = [];
            }

            members.Add(i);
        }

        return [.. byRoot.Values.Where(m => m.Count > 1).OrderBy(m => m[0]).Select(m => new CardGroup(m))];
    }

    /// <summary>
    /// [S-2101] Whether two cells share an edge: facing edges within the tolerance, and a shared run
    /// longer than it. Overlap is not a touch ([S-2102]); a corner alone is not one either.
    /// </summary>
    public static bool Touch(PixelRect a, PixelRect b, double tolerance)
    {
        var overlapX = Math.Min(a.X + a.Width, b.X + b.Width) - Math.Max(a.X, b.X);
        var overlapY = Math.Min(a.Y + a.Height, b.Y + b.Height) - Math.Max(a.Y, b.Y);

        var meetsX = overlapX > -tolerance && overlapX <= tolerance;
        var meetsY = overlapY > -tolerance && overlapY <= tolerance;

        return (meetsX && overlapY > tolerance) || (meetsY && overlapX > tolerance);
    }

    /// <summary>[S-2102] Whether two cells overlap by more than the tolerance on both axes.</summary>
    public static bool Overlap(PixelRect a, PixelRect b, double tolerance)
    {
        var overlapX = Math.Min(a.X + a.Width, b.X + b.Width) - Math.Max(a.X, b.X);
        var overlapY = Math.Min(a.Y + a.Height, b.Y + b.Height) - Math.Max(a.Y, b.Y);
        return overlapX > tolerance && overlapY > tolerance;
    }

    private static bool Joins(GroupCandidate card, bool overlaps)
        => !overlaps
            && card.Surface is WidgetSurface.Card or WidgetSurface.Translucent
            && card.Cell.Width > 0 && card.Cell.Height > 0
            && double.IsFinite(card.Cell.X + card.Cell.Y + card.Cell.Width + card.Cell.Height);

    /// <summary>
    /// [S-2103] The outline of the members' cells taken together, every edge pulled in by
    /// <paramref name="inset"/> (half the widget gap, so the group sits in its cells exactly as a lone
    /// card does), convex corners rounded by <paramref name="convexRadius"/> and concave ones by
    /// <paramref name="concaveRadius"/>, each capped at half of either edge beside it. Edges within the
    /// tolerance of each other are welded first, so an unsnapped neighbour a pixel off leaves no notch.
    /// Null when the inset swallows the shape (a gap wider than a member): the members then paint
    /// their own cards.
    /// </summary>
    public static CardOutline? Outline(
        IReadOnlyList<PixelRect> cells,
        double tolerance,
        double inset,
        double convexRadius,
        double concaveRadius)
    {
        ArgumentNullException.ThrowIfNull(cells);
        if (cells.Count == 0)
        {
            return null;
        }

        var tol = double.IsFinite(tolerance) && tolerance >= 0 ? tolerance : 0;
        var h = double.IsFinite(inset) && inset > 0 ? inset : 0;
        var rOut = double.IsFinite(convexRadius) && convexRadius > 0 ? convexRadius : 0;
        var rIn = double.IsFinite(concaveRadius) && concaveRadius > 0 ? concaveRadius : 0;

        var weldX = Weld(cells.SelectMany(c => new[] { c.X, c.X + c.Width }), tol);
        var weldY = Weld(cells.SelectMany(c => new[] { c.Y, c.Y + c.Height }), tol);
        var rects = cells
            .Select(c => (L: weldX(c.X), T: weldY(c.Y), R: weldX(c.X + c.Width), B: weldY(c.Y + c.Height)))
            .Where(c => c.R > c.L && c.B > c.T)
            .ToList();
        if (rects.Count == 0)
        {
            return null;
        }

        var xs = rects.SelectMany(r => new[] { r.L, r.R }).Distinct().Order().ToArray();
        var ys = rects.SelectMany(r => new[] { r.T, r.B }).Distinct().Order().ToArray();
        var cols = xs.Length - 1;
        var rows = ys.Length - 1;

        bool Filled(int i, int j)
        {
            if (i < 0 || j < 0 || i >= cols || j >= rows)
            {
                return false;
            }

            var cx = (xs[i] + xs[i + 1]) / 2;
            var cy = (ys[j] + ys[j + 1]) / 2;
            return rects.Exists(r => cx > r.L && cx < r.R && cy > r.T && cy < r.B);
        }

        // Directed boundary edges, interior on the right (y down): top edges run +x, right edges +y,
        // bottom edges -x, left edges -y. So the outside loop runs clockwise and a hole the other way.
        var edges = new List<(int Ax, int Ay, int Bx, int By)>();
        for (var i = 0; i < cols; i++)
        {
            for (var j = 0; j < rows; j++)
            {
                if (!Filled(i, j))
                {
                    continue;
                }

                if (!Filled(i, j - 1))
                {
                    edges.Add((i, j, i + 1, j));
                }

                if (!Filled(i + 1, j))
                {
                    edges.Add((i + 1, j, i + 1, j + 1));
                }

                if (!Filled(i, j + 1))
                {
                    edges.Add((i + 1, j + 1, i, j + 1));
                }

                if (!Filled(i - 1, j))
                {
                    edges.Add((i, j + 1, i, j));
                }
            }
        }

        var loops = new List<IReadOnlyList<OutlineCorner>>();
        foreach (var loop in Trace(edges))
        {
            if (Corners(loop, xs, ys, h, rOut, rIn) is not { } corners)
            {
                return null;
            }

            loops.Add(corners);
        }

        return new CardOutline(loops, Region(loops));
    }

    /// <summary>Snap every value to the first of a run of values each within the tolerance of that first one.</summary>
    private static Func<double, double> Weld(IEnumerable<double> values, double tol)
    {
        var sorted = values.Order().ToArray();
        var map = new Dictionary<double, double>();
        double? anchor = null;
        foreach (var v in sorted)
        {
            if (anchor is not { } a || v - a > tol)
            {
                anchor = v;
            }

            map[v] = anchor.Value;
        }

        return v => map.TryGetValue(v, out var w) ? w : v;
    }

    /// <summary>
    /// Chain the directed edges into closed loops of grid vertices, collinear runs merged. Where two
    /// edges leave one vertex (two members meeting only at a corner inside the group) the right turn
    /// is taken, so the two stay separate corners rather than one pinched outline.
    /// </summary>
    private static List<List<(int X, int Y)>> Trace(List<(int Ax, int Ay, int Bx, int By)> edges)
    {
        var outgoing = new Dictionary<(int, int), List<int>>();
        for (var e = 0; e < edges.Count; e++)
        {
            var key = (edges[e].Ax, edges[e].Ay);
            if (!outgoing.TryGetValue(key, out var list))
            {
                outgoing[key] = list = [];
            }

            list.Add(e);
        }

        var used = new bool[edges.Count];
        var loops = new List<List<(int X, int Y)>>();

        for (var start = 0; start < edges.Count; start++)
        {
            if (used[start])
            {
                continue;
            }

            var points = new List<(int X, int Y)>();
            var e = start;
            while (!used[e])
            {
                used[e] = true;
                var (ax, ay, bx, by) = edges[e];
                points.Add((ax, ay));

                var dx = Math.Sign(bx - ax);
                var dy = Math.Sign(by - ay);
                var next = -1;
                var best = int.MinValue;
                foreach (var candidate in outgoing[(bx, by)])
                {
                    if (used[candidate] && candidate != start)
                    {
                        continue;
                    }

                    var (cx, cy, cbx, cby) = edges[candidate];
                    var ndx = Math.Sign(cbx - cx);
                    var ndy = Math.Sign(cby - cy);

                    // Right turn (cross > 0 with y down) beats straight beats left.
                    var turn = (dx * ndy) - (dy * ndx);
                    var rank = turn > 0 ? 2 : turn == 0 ? 1 : 0;
                    if (rank > best)
                    {
                        best = rank;
                        next = candidate;
                    }
                }

                if (next < 0)
                {
                    break;
                }

                e = next;
            }

            loops.Add(Simplify(points));
        }

        return loops;
    }

    /// <summary>Drop the vertices that sit on a straight run.</summary>
    private static List<(int X, int Y)> Simplify(List<(int X, int Y)> points)
    {
        var kept = new List<(int X, int Y)>();
        var n = points.Count;
        for (var i = 0; i < n; i++)
        {
            var p = points[(i + n - 1) % n];
            var v = points[i];
            var q = points[(i + 1) % n];
            var cross = ((v.X - p.X) * (q.Y - v.Y)) - ((v.Y - p.Y) * (q.X - v.X));
            if (cross != 0)
            {
                kept.Add(v);
            }
        }

        return kept;
    }

    /// <summary>
    /// The loop's corners in DIPs: each vertex moved in by the inset along both of its edges' inward
    /// normals, then rounded. Null when an edge no longer has positive length after the inset.
    /// </summary>
    private static List<OutlineCorner>? Corners(List<(int X, int Y)> loop, double[] xs, double[] ys, double h, double rOut, double rIn)
    {
        var n = loop.Count;
        if (n < 4)
        {
            return null;
        }

        var moved = new (double X, double Y, bool Convex, double Dx1, double Dy1, double Dx2, double Dy2)[n];
        for (var i = 0; i < n; i++)
        {
            var p = loop[(i + n - 1) % n];
            var v = loop[i];
            var q = loop[(i + 1) % n];
            double dx1 = Math.Sign(v.X - p.X), dy1 = Math.Sign(v.Y - p.Y);
            double dx2 = Math.Sign(q.X - v.X), dy2 = Math.Sign(q.Y - v.Y);

            // Inward normal of a direction with the interior on its right (y down): (-dy, dx).
            var x = xs[v.X] + (h * (-dy1 + -dy2));
            var y = ys[v.Y] + (h * (dx1 + dx2));
            var convex = ((dx1 * dy2) - (dy1 * dx2)) > 0;
            moved[i] = (x, y, convex, dx1, dy1, dx2, dy2);
        }

        var lengths = new double[n];
        for (var i = 0; i < n; i++)
        {
            var a = moved[i];
            var b = moved[(i + 1) % n];
            lengths[i] = Math.Abs(b.X - a.X) + Math.Abs(b.Y - a.Y);

            // The edge must still run the way it did: an inset wider than the shape turns it round.
            var sx = Math.Sign(b.X - a.X);
            var sy = Math.Sign(b.Y - a.Y);
            if (lengths[i] < 1 || sx != (int)a.Dx2 || sy != (int)a.Dy2)
            {
                return null;
            }
        }

        var corners = new List<OutlineCorner>(n);
        for (var i = 0; i < n; i++)
        {
            var c = moved[i];
            var before = lengths[(i + n - 1) % n];
            var after = lengths[i];
            var radius = Math.Min(c.Convex ? rOut : rIn, Math.Min(before, after) / 2);
            corners.Add(new OutlineCorner(
                c.X,
                c.Y,
                c.Convex,
                radius,
                c.X - (c.Dx1 * radius),
                c.Y - (c.Dy1 * radius),
                c.X + (c.Dx2 * radius),
                c.Y + (c.Dy2 * radius)));
        }

        return corners;
    }

    /// <summary>
    /// [S-2105] Rectangles covering the painted shape: the inset outline's interior (even-odd over
    /// every loop, so a hole stays the desktop's) plus the square each rounded concave corner fills
    /// beyond it. A rounded convex corner is covered by its square too, as a lone card's is.
    /// </summary>
    private static List<PixelRect> Region(List<IReadOnlyList<OutlineCorner>> loops)
    {
        var xs = loops.SelectMany(l => l).Select(c => c.X).Distinct().Order().ToArray();
        var ys = loops.SelectMany(l => l).Select(c => c.Y).Distinct().Order().ToArray();

        bool Inside(double px, double py)
        {
            var inside = false;
            foreach (var loop in loops)
            {
                for (var i = 0; i < loop.Count; i++)
                {
                    var a = loop[i];
                    var b = loop[(i + 1) % loop.Count];

                    // Vertical edges only cross a horizontal ray.
                    if (a.X == b.X && a.X > px && (a.Y > py) != (b.Y > py))
                    {
                        inside = !inside;
                    }
                }
            }

            return inside;
        }

        var region = new List<PixelRect>();
        for (var j = 0; j + 1 < ys.Length; j++)
        {
            var cy = (ys[j] + ys[j + 1]) / 2;
            double? runStart = null;
            for (var i = 0; i + 1 < xs.Length; i++)
            {
                var filled = Inside((xs[i] + xs[i + 1]) / 2, cy);
                if (filled && runStart is null)
                {
                    runStart = xs[i];
                }

                var closes = runStart is { } s && (!filled || i + 2 == xs.Length);
                if (closes)
                {
                    var end = filled ? xs[i + 1] : xs[i];
                    region.Add(PixelRect.FromEdges(runStart!.Value, ys[j], end, ys[j + 1]));
                    runStart = null;
                }
            }
        }

        // A rounded concave corner fills the square between the corner and its two tangent points,
        // outside the inset outline (but inside the members' cells, [S-2104]).
        foreach (var c in loops.SelectMany(l => l).Where(c => !c.Convex && c.Radius > 0))
        {
            var left = Math.Min(c.X, Math.Min(c.InX, c.OutX));
            var right = Math.Max(c.X, Math.Max(c.InX, c.OutX));
            var top = Math.Min(c.Y, Math.Min(c.InY, c.OutY));
            var bottom = Math.Max(c.Y, Math.Max(c.InY, c.OutY));
            region.Add(PixelRect.FromEdges(left, top, right, bottom));
        }

        return region;
    }
}
