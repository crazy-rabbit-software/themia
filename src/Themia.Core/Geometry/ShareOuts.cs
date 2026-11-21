using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Core.Geometry;

/// <summary>
/// [S-15026] [S-15027] The cards the load shared out among several kinds (<see cref="KindMerges"/>), laid on the display that
/// shows their view once its metrics have settled: their parts stood in fixed shares of the old card, which the load lays
/// without knowing a display, so a part could be under its minimum there, or between grid lines (a third of a card), and,
/// painted at whole cells, run over its sibling or a neighbour. Each card is laid once, on whole grid cells
/// (<see cref="Lay"/>), and forgotten; one whose parts were moved, resized or removed before then is forgotten as it is.
/// UI thread, as the host's mutations.
/// </summary>
public sealed class ShareOuts
{
    private readonly List<ShareOut> _waiting = [];

    /// <summary>[S-15026] Wait to lay <paramref name="sharedOut"/> (a load's, an import's).</summary>
    public void Add(IEnumerable<ShareOut> sharedOut)
    {
        ArgumentNullException.ThrowIfNull(sharedOut);
        _waiting.AddRange(sharedOut);
    }

    /// <summary>Forget every card waiting: the state they were read into is gone (a reset, an import).</summary>
    public void Clear() => _waiting.Clear();

    /// <summary>[S-15026] Whether a card waits whose view a settled surface shows: <see cref="Apply"/> would lay it.</summary>
    public bool Waits(IReadOnlyList<SurfaceMetrics> surfaces)
    {
        ArgumentNullException.ThrowIfNull(surfaces);
        return _waiting.Exists(shared => surfaces.Any(s => s.ViewId == shared.ViewId && s.Writable));
    }

    /// <summary>
    /// [S-15026] [S-15027] Lay every waiting card whose view the first of <paramref name="surfaces"/> showing it shows on settled
    /// metrics: its parts on that display's grid, at each part's own scale, clear of the view's other cards as they are
    /// painted (<see cref="Lay"/>). A card whose view is shown nowhere yet, or on a surface not settled, waits. Returns one
    /// note a card whose parts moved.
    /// </summary>
    public List<string> Apply(AppState state, IReadOnlyList<SurfaceMetrics> surfaces)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(surfaces);
        var notes = new List<string>();
        for (var i = _waiting.Count - 1; i >= 0; i--)
        {
            var shared = _waiting[i];
            if (state.FindView(shared.ViewId) is not { } view)
            {
                _waiting.RemoveAt(i);
                continue;
            }

            var at = -1;
            for (var s = 0; s < surfaces.Count && at < 0; s++)
            {
                at = surfaces[s].ViewId == shared.ViewId ? s : -1;
            }

            if (at < 0 || !surfaces[at].Writable)
            {
                continue;
            }

            _waiting.RemoveAt(i);
            if (LayOne(view, shared, surfaces[at]) is { } note)
            {
                notes.Add(note);
            }
        }

        return notes;
    }

    private static string? LayOne(ViewState view, ShareOut shared, SurfaceMetrics surface)
    {
        var parts = shared.Parts.Select(p => view.Find(p.Id)).ToList();

        // Moved, resized or removed since the load: the user's, kept.
        if (parts.Exists(p => p is null)
            || parts.Where((p, k) => !Lattice.Same(p!.Rect, Placed(shared.Card, shared.Parts[k].Share))).Any())
        {
            return null;
        }

        var screen = surface.Screen;
        var grid = GridGeometry.GridDims(screen);
        var mins = new List<(int W, int H)>(parts.Count);
        foreach (var part in parts)
        {
            if (SizeFit.Limits(part!, screen, WidgetScale.For(part!, surface.DisplayScale)) is not { } limits)
            {
                return null;
            }

            mins.Add((Math.Max(1, GridSpace.Up(limits.Width.Min * grid.Cols)), Math.Max(1, GridSpace.Up(limits.Height.Min * grid.Rows))));
        }

        // The view's other cards as they are painted (fitted into their sizing), and the room new geometry may take.
        var others = view.Widgets
            .Where(w => !parts.Contains(w) && w.Rect.IsFinite)
            .Select(w => Cells(SizeFit.Shown(w.Rect, SizeFit.Limits(w, screen, WidgetScale.For(w, surface.DisplayScale)), screen), grid))
            .ToList();
        var allowed = screen.Allowed;
        var area = new CellBox(
            GridSpace.Up(allowed.X * grid.Cols),
            GridSpace.Up(allowed.Y * grid.Rows),
            GridSpace.Down(allowed.Right * grid.Cols) - GridSpace.Up(allowed.X * grid.Cols),
            GridSpace.Down(allowed.Bottom * grid.Rows) - GridSpace.Up(allowed.Y * grid.Rows));
        var card = new CellBox(grid.Col(shared.Card.X), grid.Row(shared.Card.Y), 0, 0);
        card = card with { W = Math.Max(1, grid.Col(shared.Card.Right) - card.X), H = Math.Max(1, grid.Row(shared.Card.Bottom) - card.Y) };

        if (Lay(card, [.. shared.Parts.Select(p => p.Share)], mins, box => !others.Exists(o => o.Overlaps(box)), area) is not { } boxes)
        {
            return null;
        }

        var moved = false;
        for (var k = 0; k < parts.Count; k++)
        {
            var rect = grid.Frac(boxes[k]);
            if (!Lattice.Same(parts[k]!.Rect, rect))
            {
                parts[k]!.Rect = rect;
                moved = true;
            }
        }

        return moved
            ? $"{shared.Kind}'s parts in view '{view.Name}' laid on the grid at their least: {string.Join(", ", shared.Parts.Select((p, k) => $"{p.Id} at cell {boxes[k].X},{boxes[k].Y} {boxes[k].W}x{boxes[k].H}"))}"
            : null;
    }

    /// <summary>The whole cells <paramref name="rect"/> reaches on <paramref name="grid"/>.</summary>
    private static CellBox Cells(FracRect rect, GridSpace grid)
    {
        var x = GridSpace.Down(rect.X * grid.Cols);
        var y = GridSpace.Down(rect.Y * grid.Rows);
        return new CellBox(x, y, GridSpace.Up(rect.Right * grid.Cols) - x, GridSpace.Up(rect.Bottom * grid.Rows) - y);
    }

    /// <summary>A share of <paramref name="card"/>, as the load placed it.</summary>
    public static FracRect Placed(FracRect card, MergeShare share)
        => new(card.X + (share.X * card.W), card.Y + (share.Y * card.H), share.Width * card.W, share.Height * card.H);

    /// <summary>
    /// [S-15026] [S-15027] The boxes of a card's parts on whole grid cells, each at least its minimum (<paramref name="mins"/>, in
    /// cells), none over another. The parts are a lead beside a stack (the shares: one part the card's whole height on its
    /// left, the others one above the other on its right; anything else is left as shared, null). An arrangement is the
    /// lead beside the stack (the line between them and the stack's lines moved as far as the minimums need) or the lead
    /// stacked on top, each part the block's width. Inside <paramref name="card"/> when one holds there, beside first; else
    /// the block grown over the card toward free cells (<paramref name="free"/>: no other card there, inside
    /// <paramref name="area"/>), the smaller arrangement first; else, last, the smaller one moved to the free cells nearest
    /// the card; else the smaller one at the card's corner.
    /// </summary>
    public static IReadOnlyList<CellBox>? Lay(
        CellBox card, IReadOnlyList<MergeShare> shares, IReadOnlyList<(int W, int H)> mins, Func<CellBox, bool> free, CellBox area)
    {
        ArgumentNullException.ThrowIfNull(shares);
        ArgumentNullException.ThrowIfNull(mins);
        ArgumentNullException.ThrowIfNull(free);
        if (shares.Count < 2 || shares.Count != mins.Count || card.W <= 0 || card.H <= 0)
        {
            return null;
        }

        var leads = Enumerable.Range(0, shares.Count).Where(i => Lattice.Same(shares[i].X, 0) && Lattice.Same(shares[i].Y, 0) && Lattice.Same(shares[i].Height, 1)).ToList();
        if (leads.Count != 1)
        {
            return null;
        }

        var lead = leads[0];
        var lw = shares[lead].Width;
        var stack = Enumerable.Range(0, shares.Count).Where(i => i != lead).OrderBy(i => shares[i].Y).ToList();
        if (stack.Exists(i => !Lattice.Same(shares[i].X, lw) || !Lattice.Same(shares[i].Width, 1 - lw)) || !Lattice.Same(stack.Sum(i => shares[i].Height), 1))
        {
            return null;
        }

        var stackMinW = stack.Max(i => mins[i].W);
        var beside = new Arrangement(
            Math.Max(card.W, mins[lead].W + stackMinW),
            Math.Max(card.H, Math.Max(mins[lead].H, stack.Sum(i => mins[i].H))),
            (w, h) => Beside(w, h, lead, lw, stack, shares, mins));
        var order = new List<int> { lead };
        order.AddRange(stack);
        var stacked = new Arrangement(
            Math.Max(card.W, order.Max(i => mins[i].W)),
            Math.Max(card.H, order.Sum(i => mins[i].H)),
            (w, h) => Stacked(w, h, order, shares.Count, mins));

        // In the card itself, beside first.
        foreach (var arrangement in new[] { beside, stacked })
        {
            if (arrangement.W == card.W && arrangement.H == card.H)
            {
                return arrangement.At(card.X, card.Y);
            }
        }

        var bySize = new[] { beside, stacked }.OrderBy(a => a.W * a.H).ToList();

        // Grown over the card toward free cells: every corner that keeps the card inside the block, nearest first.
        foreach (var arrangement in bySize)
        {
            var corners =
                from x in Enumerable.Range(card.X - (arrangement.W - card.W), arrangement.W - card.W + 1)
                from y in Enumerable.Range(card.Y - (arrangement.H - card.H), arrangement.H - card.H + 1)
                orderby Math.Abs(x - card.X) + Math.Abs(y - card.Y), card.X - x, card.Y - y
                select (x, y);
            foreach (var (x, y) in corners)
            {
                var block = new CellBox(x, y, arrangement.W, arrangement.H);
                if (area.Holds(block) && free(block))
                {
                    return arrangement.At(x, y);
                }
            }
        }

        // Last: the smaller arrangement moved to the free cells nearest the card.
        var smallest = bySize[0];
        var spots =
            from x in Enumerable.Range(area.X, Math.Max(0, area.W - smallest.W + 1))
            from y in Enumerable.Range(area.Y, Math.Max(0, area.H - smallest.H + 1))
            orderby ((x - card.X) * (x - card.X)) + ((y - card.Y) * (y - card.Y)), y, x
            select (x, y);
        foreach (var (x, y) in spots)
        {
            if (free(new CellBox(x, y, smallest.W, smallest.H)))
            {
                return smallest.At(x, y);
            }
        }

        return smallest.At(card.X, card.Y);
    }

    /// <summary>An arrangement of the parts in a block of <see cref="W"/> × <see cref="H"/> cells.</summary>
    private sealed record Arrangement(int W, int H, Func<int, int, CellBox[]> Boxes)
    {
        /// <summary>The parts' boxes with the block's corner at (<paramref name="x"/>, <paramref name="y"/>).</summary>
        public IReadOnlyList<CellBox> At(int x, int y) => [.. Boxes(W, H).Select(b => b with { X = b.X + x, Y = b.Y + y })];
    }

    /// <summary>The lead on the left, the stack beside it; each line moved as far as the minimums need.</summary>
    private static CellBox[] Beside(int w, int h, int lead, double leadShare, List<int> stack, IReadOnlyList<MergeShare> shares, IReadOnlyList<(int W, int H)> mins)
    {
        var across = Split(w, [leadShare, 1 - leadShare], [mins[lead].W, stack.Max(i => mins[i].W)]);
        var down = Split(h, [.. stack.Select(i => shares[i].Height)], [.. stack.Select(i => mins[i].H)]);

        var boxes = new CellBox[shares.Count];
        boxes[lead] = new CellBox(0, 0, across[0], h);
        var y = 0;
        for (var k = 0; k < stack.Count; k++)
        {
            boxes[stack[k]] = new CellBox(across[0], y, across[1], down[k]);
            y += down[k];
        }

        return boxes;
    }

    /// <summary>The lead on top and the stack under it, in <paramref name="order"/>, each the block's width.</summary>
    private static CellBox[] Stacked(int w, int h, List<int> order, int count, IReadOnlyList<(int W, int H)> mins)
    {
        var down = Split(h, [.. order.Select(_ => 1.0)], [.. order.Select(i => mins[i].H)]);
        var boxes = new CellBox[count];
        var y = 0;
        for (var k = 0; k < order.Count; k++)
        {
            boxes[order[k]] = new CellBox(0, y, w, down[k]);
            y += down[k];
        }

        return boxes;
    }

    /// <summary>
    /// <paramref name="total"/> whole cells (at least the minimums' sum) shared by <paramref name="weights"/>: a length under
    /// its minimum held at it and the rest shared again among the others; the cells left by rounding down go one each to the
    /// lengths that lost the most, the first on a tie.
    /// </summary>
    internal static int[] Split(int total, IReadOnlyList<double> weights, IReadOnlyList<int> mins)
    {
        var n = weights.Count;
        var exact = new double[n];
        var held = new bool[n];
        while (true)
        {
            var free = total - Enumerable.Range(0, n).Where(i => held[i]).Sum(i => mins[i]);
            var weight = Enumerable.Range(0, n).Where(i => !held[i]).Sum(i => weights[i]);
            for (var i = 0; i < n; i++)
            {
                exact[i] = held[i] ? mins[i] : weight > 0 ? free * weights[i] / weight : 0;
            }

            var under = Enumerable.Range(0, n).Where(i => !held[i] && exact[i] < mins[i] - Lattice.Epsilon).ToList();
            if (under.Count == 0)
            {
                break;
            }

            foreach (var i in under)
            {
                held[i] = true;
            }
        }

        var lengths = exact.Select((e, i) => Math.Max(mins[i], (int)Math.Floor(e + Lattice.Epsilon))).ToArray();
        var left = total - lengths.Sum();
        foreach (var i in Enumerable.Range(0, n).OrderByDescending(i => exact[i] - Math.Floor(exact[i] + Lattice.Epsilon)).ThenBy(i => i))
        {
            if (left <= 0)
            {
                break;
            }

            lengths[i]++;
            left--;
        }

        return lengths;
    }
}
