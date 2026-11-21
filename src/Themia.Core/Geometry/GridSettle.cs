using System.Globalization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Core.Geometry;

/// <summary>
/// [S-11892] Every card on its display's grid lines (owner, 2026-11-20: "there is a problem with the layouting in this
/// configuraiton. the borders dont nicely connect."): a card's edge that drifted off its line by less than a DIP is put
/// on it. Such an edge is drift, never a place anyone chose: a gesture lands on a line ([S-06], [S-11892]) or flush on
/// the work area's edge ([S-6609]), and a layout made on another display's grid is off this one's by more, or by
/// chance; until 2026-11-20 a move was put on whole pixels off its line, and fractions resolved against a window a
/// pixel short ([S-852]), so two blocks laid side by side stood a quarter DIP apart and their edges, each rounded to
/// its own pixel, met a pixel apart: one corner round and one square at the seam.
/// <para>
/// One translation of every equal edge: edges that are one value (a join, an alignment) move together or not at all,
/// so nothing joined is parted. A card moves only when both its edges on an axis are on lines or drifted, and its whole
/// cells there (what it is painted at, <see cref="AxisLimits.Range"/>) stay the same; a card that stays holds every
/// edge it shares. An edge flush on the allowed area's edge stays (the magnet's, a taskbar's). Pure: the host runs it
/// in every mutation on settled metrics ([S-854]), before the taskbars are cleared ([S-855]), and logs each card it
/// put on the lines.
/// </para>
/// </summary>
public static class GridSettle
{
    /// <summary>How far off its line, in DIPs, an edge may have drifted: less than one.</summary>
    public const double DriftDip = 1;

    /// <summary>[S-11893] Edges closer than this (a fraction of the screen) are one edge; an edge this near its line is on it: the lattice's.</summary>
    private const double Same = Lattice.Epsilon;

    /// <summary>
    /// [S-11892] <paramref name="rects"/> (one view's cards, in its order) with every drifted edge on <paramref name="screen"/>'s
    /// grid lines: the same list when nothing drifted. A rect <paramref name="stays"/> says stays where it is (a fence,
    /// laid by its own rule) and holds the edges it shares. A non-finite rect stays.
    /// </summary>
    public static IReadOnlyList<FracRect> Settle(IReadOnlyList<FracRect> rects, IReadOnlyList<bool> stays, ScreenMetrics screen)
    {
        ArgumentNullException.ThrowIfNull(rects);
        ArgumentNullException.ThrowIfNull(stays);
        if (stays.Count != rects.Count)
        {
            throw new ArgumentException("one stays per rect", nameof(stays));
        }

        if (!screen.IsUsable || rects.Count == 0)
        {
            return rects;
        }

        var (cols, rows) = GridGeometry.GridDims(screen);
        var a = screen.Allowed;
        var fixedAt = Enumerable.Range(0, rects.Count).Select(i => stays[i] || !rects[i].IsFinite).ToArray();
        var xs = Axis([.. rects.Select(r => (r.X, r.Right))], fixedAt, cols, screen.Width, a.X, a.Right);
        var ys = Axis([.. rects.Select(r => (r.Y, r.Bottom))], fixedAt, rows, screen.Height, a.Y, a.Bottom);
        if (xs is null && ys is null)
        {
            return rects;
        }

        var settled = rects.ToArray();
        for (var i = 0; i < settled.Length; i++)
        {
            if (xs?[i] is var (left, right))
            {
                settled[i] = settled[i] with { X = left, W = right - left };
            }

            if (ys?[i] is var (top, bottom))
            {
                settled[i] = settled[i] with { Y = top, H = bottom - top };
            }
        }

        return settled;
    }

    /// <summary>
    /// [S-11892] Settle every view a settled surface shows (on the first surface's grid, the area every display showing
    /// it holds, as <see cref="WorkAreaSettle.Shown"/>): its cards, a fence staying where its rule lays it, the desktop's
    /// icons left out (<c>DesktopGrid</c> lays them, [S-11875]). One note a card put on the lines, as the load's repairs are.
    /// </summary>
    public static List<string> Apply(AppState state, IReadOnlyList<SurfaceMetrics> surfaces)
    {
        ArgumentNullException.ThrowIfNull(state);
        var notes = new List<string>();
        foreach (var (view, screen) in WorkAreaSettle.Shown(state, surfaces))
        {
            var cards = view.Widgets.Where(w => w.Config is not IDesktopItemConfig).ToList();
            var settled = Settle([.. cards.Select(w => w.Rect)], [.. cards.Select(w => w.Config is IHoldsDesktopItems)], screen);
            for (var i = 0; i < cards.Count; i++)
            {
                if (settled[i] == cards[i].Rect)
                {
                    continue;
                }

                notes.Add($"widget '{cards[i].Id}' in view '{view.Name}' put on its display's grid lines: {Dips(cards[i].Rect, screen)} -> {Dips(settled[i], screen)} DIPs");
                cards[i].Rect = settled[i];
            }
        }

        return notes;
    }

    /// <summary>[S-11892] Whether <see cref="Apply"/> would put anything on the lines.</summary>
    public static bool Waits(AppState state, IReadOnlyList<SurfaceMetrics> surfaces)
    {
        ArgumentNullException.ThrowIfNull(state);
        return WorkAreaSettle.Shown(state, surfaces).Any(shown =>
        {
            var cards = shown.View.Widgets.Where(w => w.Config is not IDesktopItemConfig).ToList();
            var rects = cards.Select(w => w.Rect).ToList();
            var settled = Settle(rects, [.. cards.Select(w => w.Config is IHoldsDesktopItems)], shown.Screen);
            return !ReferenceEquals(settled, rects) && !settled.SequenceEqual(rects);
        });
    }

    /// <summary>
    /// One axis: the (start, end) each span that moves lands at (null for one that stays), or null when nothing on this
    /// axis moves. An edge is on a line,
    /// drifted (under <see cref="DriftDip"/> off one, not flush on the allowed area), or stays; a span moves when both its
    /// edges are on lines or drifted, its whole cells kept, and no span that stays shares a drifted edge with it (followed
    /// through: a span held by one holds the edges it shares in turn).
    /// </summary>
    private static (double Start, double End)?[]? Axis((double Start, double End)[] spans, bool[] fixedAt, int lines, double length, double areaLo, double areaHi)
    {
        var n = spans.Length;
        var value = new double[2 * n];
        var to = new double?[2 * n];
        for (var i = 0; i < n; i++)
        {
            (value[2 * i], value[(2 * i) + 1]) = spans[i];
            to[2 * i] = Snap(spans[i].Start);
            to[(2 * i) + 1] = Snap(spans[i].End);
        }

        bool Moves(int edge) => to[edge] is { } t && t != value[edge];
        var moving = new bool[n];
        for (var i = 0; i < n; i++)
        {
            moving[i] = !fixedAt[i] && to[2 * i] is { } start && to[(2 * i) + 1] is { } end && (Moves(2 * i) || Moves((2 * i) + 1))
                && GridCells.Up((spans[i].End - spans[i].Start) * lines) == GridCells.Up((end - start) * lines);
        }

        if (!moving.Contains(true))
        {
            return null;
        }

        // A span that stays holds every edge it shares: the span on the other side of it stays too, and so on.
        for (var changed = true; changed;)
        {
            changed = false;
            for (var k = 0; k < 2 * n; k++)
            {
                if (moving[k / 2])
                {
                    continue;
                }

                for (var j = 0; j < 2 * n; j++)
                {
                    if (moving[j / 2] && Moves(j) && Math.Abs(value[j] - value[k]) <= Same)
                    {
                        moving[j / 2] = false;
                        changed = true;
                    }
                }
            }
        }

        if (!moving.Contains(true))
        {
            return null;
        }

        return [.. Enumerable.Range(0, n).Select(i => moving[i] ? ((double Start, double End)?)(to[2 * i]!.Value, to[(2 * i) + 1]!.Value) : null)];

        // The line an edge goes to: its own value on a line, its line drifted, null where it stays.
        double? Snap(double v)
        {
            if (!double.IsFinite(v))
            {
                return null;
            }

            var line = Math.Round(v * lines) / lines;
            var off = Math.Abs(v - line);
            if (off <= Same)
            {
                return v;
            }

            var flush = Math.Abs(v - areaLo) <= Same || Math.Abs(v - areaHi) <= Same;
            return !flush && off * length < DriftDip ? line : null;
        }
    }

    private static string Dips(FracRect r, ScreenMetrics screen)
        => string.Create(CultureInfo.InvariantCulture, $"{r.X * screen.Width:0.##},{r.Y * screen.Height:0.##} {r.W * screen.Width:0.##}x{r.H * screen.Height:0.##}");
}
