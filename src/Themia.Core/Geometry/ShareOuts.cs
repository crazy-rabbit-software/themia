using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Sdk;

namespace Themia.Core.Geometry;

/// <summary>
/// [S-15026] The cards the load shared out among several kinds (<see cref="KindMerges"/>), laid on the display that shows
/// their view once its metrics have settled: their parts stood in fixed shares of the old card, which the load lays
/// without knowing a display, so a part could be under its minimum there and, painted at it, run past the old card over
/// its neighbours. Each card is laid once (<see cref="Lay(FracRect, IReadOnlyList{MergeShare}, IReadOnlyList{ValueTuple{double, double}})"/>)
/// and forgotten; one whose parts were moved, resized or removed before then is forgotten as it is. UI thread, as the
/// host's mutations.
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
    /// [S-15026] Lay every waiting card whose view the first of <paramref name="surfaces"/> showing it shows on settled
    /// metrics: its parts' rects as <see cref="Lay(FracRect, IReadOnlyList{MergeShare}, IReadOnlyList{ValueTuple{double, double}})"/>
    /// gives them on that display, at each part's own scale. A card whose view is shown nowhere yet, or on a surface not
    /// settled, waits. Returns one note a card whose parts moved.
    /// </summary>
    public List<string> Apply(AppState state, IReadOnlyList<SurfaceMetrics> surfaces)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(surfaces);
        var notes = new List<string>();
        for (var i = _waiting.Count - 1; i >= 0; i--)
        {
            var shared = _waiting[i];
            var at = surfaces.ToList().FindIndex(s => s.ViewId == shared.ViewId);
            if (state.FindView(shared.ViewId) is not { } view)
            {
                _waiting.RemoveAt(i);
                continue;
            }

            if (at < 0 || !surfaces[at].Writable)
            {
                continue;
            }

            _waiting.RemoveAt(i);
            var surface = surfaces[at];
            var parts = shared.Parts.Select(p => view.Find(p.Id)).ToList();

            // Moved, resized or removed since the load: the user's, kept.
            if (parts.Exists(p => p is null)
                || parts.Where((p, k) => !Lattice.Same(p!.Rect, Placed(shared.Card, shared.Parts[k].Share))).Any())
            {
                continue;
            }

            var mins = new List<(double, double)>(parts.Count);
            foreach (var part in parts)
            {
                if (SizeFit.Limits(part!, surface.Screen, WidgetScale.For(part!, surface.DisplayScale)) is not { } limits)
                {
                    break;
                }

                mins.Add((limits.Width.Min, limits.Height.Min));
            }

            if (mins.Count < parts.Count || Lay(shared.Card, [.. shared.Parts.Select(p => p.Share)], mins) is not { } rects)
            {
                continue;
            }

            var moved = false;
            for (var k = 0; k < parts.Count; k++)
            {
                if (!Lattice.Same(parts[k]!.Rect, rects[k]))
                {
                    parts[k]!.Rect = rects[k];
                    moved = true;
                }
            }

            if (moved)
            {
                notes.Add($"{shared.Kind}'s parts in view '{view.Name}' laid at their least on its display: {string.Join(", ", shared.Parts.Select(p => p.Id))}");
            }
        }

        return notes;
    }

    /// <summary>A share of <paramref name="card"/>, as the load placed it.</summary>
    public static FracRect Placed(FracRect card, MergeShare share)
        => new(card.X + (share.X * card.W), card.Y + (share.Y * card.H), share.Width * card.W, share.Height * card.H);

    /// <summary>
    /// [S-15026] The rects of a card's parts that keep each at least its minimum (<paramref name="mins"/>, fractions of the
    /// screen as the card's), the card grown only where no arrangement holds them. The parts are a lead beside a stack (the
    /// shares: one part the card's whole height on its left, the others one above the other on its right; anything else is
    /// laid as shared, null). Beside first, the line between lead and stack and the stack's lines moved as far as the
    /// minimums need; else stacked, the lead on top and the stack under it, each the card's width; else the arrangement
    /// that grows the card least, to the right and down from its corner, never one part over another.
    /// </summary>
    public static IReadOnlyList<FracRect>? Lay(FracRect card, IReadOnlyList<MergeShare> shares, IReadOnlyList<(double W, double H)> mins)
    {
        ArgumentNullException.ThrowIfNull(shares);
        ArgumentNullException.ThrowIfNull(mins);
        if (shares.Count < 2 || shares.Count != mins.Count || !card.IsFinite)
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

        var beside = Beside(card, lead, lw, stack, shares, mins);
        if (Fits(card, beside.Grown))
        {
            return beside.Rects;
        }

        var stacked = Stacked(card, [lead, .. stack], shares.Count, mins);
        if (Fits(card, stacked.Grown))
        {
            return stacked.Rects;
        }

        return Area(stacked.Grown) < Area(beside.Grown) - Lattice.Epsilon ? stacked.Rects : beside.Rects;
    }

    private static bool Fits(FracRect card, (double W, double H) grown)
        => grown.W <= card.W + Lattice.Epsilon && grown.H <= card.H + Lattice.Epsilon;

    private static double Area((double W, double H) size) => size.W * size.H;

    /// <summary>The lead on the left, the stack beside it; each line moved as far as the minimums need.</summary>
    private static (FracRect[] Rects, (double W, double H) Grown) Beside(
        FracRect card, int lead, double leadShare, List<int> stack, IReadOnlyList<MergeShare> shares, IReadOnlyList<(double W, double H)> mins)
    {
        var stackMinW = stack.Max(i => mins[i].W);
        var w = Math.Max(card.W, mins[lead].W + stackMinW);
        var h = Math.Max(card.H, Math.Max(mins[lead].H, stack.Sum(i => mins[i].H)));
        var across = Split(w, [leadShare, 1 - leadShare], [mins[lead].W, stackMinW]);
        var down = Split(h, [.. stack.Select(i => shares[i].Height)], [.. stack.Select(i => mins[i].H)]);

        var rects = new FracRect[shares.Count];
        rects[lead] = new FracRect(card.X, card.Y, across[0], h);
        var y = card.Y;
        for (var k = 0; k < stack.Count; k++)
        {
            rects[stack[k]] = new FracRect(card.X + across[0], y, across[1], down[k]);
            y += down[k];
        }

        return (rects, (w, h));
    }

    /// <summary>The lead on top and the stack under it, in <paramref name="order"/>, each the card's width.</summary>
    private static (FracRect[] Rects, (double W, double H) Grown) Stacked(FracRect card, List<int> order, int count, IReadOnlyList<(double W, double H)> mins)
    {
        var w = Math.Max(card.W, order.Max(i => mins[i].W));
        var h = Math.Max(card.H, order.Sum(i => mins[i].H));
        var down = Split(h, [.. order.Select(_ => 1.0)], [.. order.Select(i => mins[i].H)]);

        var rects = new FracRect[count];
        var y = card.Y;
        for (var k = 0; k < order.Count; k++)
        {
            rects[order[k]] = new FracRect(card.X, y, w, down[k]);
            y += down[k];
        }

        return (rects, (w, h));
    }

    /// <summary>
    /// <paramref name="total"/> (at least the minimums' sum) shared by <paramref name="weights"/>, a length under its minimum
    /// held at it and the rest shared again among the others.
    /// </summary>
    internal static double[] Split(double total, IReadOnlyList<double> weights, IReadOnlyList<double> mins)
    {
        var lengths = new double[weights.Count];
        var held = new bool[weights.Count];
        while (true)
        {
            var free = total - Enumerable.Range(0, weights.Count).Where(i => held[i]).Sum(i => mins[i]);
            var weight = Enumerable.Range(0, weights.Count).Where(i => !held[i]).Sum(i => weights[i]);
            var changed = false;
            for (var i = 0; i < weights.Count; i++)
            {
                if (held[i])
                {
                    lengths[i] = mins[i];
                    continue;
                }

                lengths[i] = weight > 0 ? free * weights[i] / weight : 0;
            }

            for (var i = 0; i < weights.Count; i++)
            {
                if (!held[i] && lengths[i] < mins[i] - Lattice.Epsilon)
                {
                    held[i] = true;
                    changed = true;
                }
            }

            if (!changed)
            {
                return lengths;
            }
        }
    }
}
