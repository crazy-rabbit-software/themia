using Themia.Core.Model;

namespace Themia.Core.Geometry;

/// <summary>[S-14104] The cards that followed a refit card's moved edge: along which axis, by how much (fractions of the screen), which.</summary>
/// <param name="Vertical">The bottom edge moved (the cards under it followed); else the right edge (the cards beside it).</param>
/// <param name="Delta">How far the edge moved, as a fraction of the screen's height or width.</param>
/// <param name="Moved">The cards that moved with it, or none where the move was kept back.</param>
/// <param name="Kept">Why the cards stayed (a move past the allowed area or into a card they did not overlap); null when they moved.</param>
public sealed record JoinShift(bool Vertical, double Delta, IReadOnlyList<WidgetInstance> Moved, string? Kept = null);

/// <summary>
/// [S-14104] A refit keeps the cards it touched touching (owner, 2026-11-19: "the items have no spacing between them";
/// [S-2240]: cards whose cells meet join). A refit fits a card at its top-left ([S-2447]), so where an axis shrinks or grows
/// (a config that changed its least, or its rows and so its natural height, [S-14503]) its bottom or right edge moves: every card of the view that touched that edge, and every card that touched one of those along the same
/// axis, moves with it by the same amount, so what was joined stays joined. Nothing else moves. A move that would take a
/// card past the view's allowed area (the work area: never under a taskbar, [S-841]) or into a card it did not overlap
/// before is kept back whole: the cards stay where they were. Pure, so the refit and the tests share it.
/// </summary>
public static class RefitJoins
{
    /// <summary>[S-11893] The one tolerance: the lattice's.</summary>
    private const double Tolerance = Lattice.Epsilon;

    /// <summary>
    /// Move the cards of <paramref name="widgets"/> that touched <paramref name="refit"/>'s bottom or right edge as it was
    /// (<paramref name="before"/>) by how far that edge moved to its rect now, within <paramref name="allowed"/>.
    /// </summary>
    public static IReadOnlyList<JoinShift> Follow(IReadOnlyList<WidgetInstance> widgets, WidgetInstance refit, FracRect before, FracRect allowed)
    {
        ArgumentNullException.ThrowIfNull(widgets);
        ArgumentNullException.ThrowIfNull(refit);
        var after = refit.Rect;
        var shifts = new List<JoinShift>();
        if (Shift(widgets, refit, before, after.Bottom - before.Bottom, vertical: true, allowed) is { } down)
        {
            shifts.Add(down);
        }

        if (Shift(widgets, refit, before, after.Right - before.Right, vertical: false, allowed) is { } across)
        {
            shifts.Add(across);
        }

        return shifts;
    }

    private static JoinShift? Shift(IReadOnlyList<WidgetInstance> widgets, WidgetInstance refit, FracRect before, double delta, bool vertical, FracRect allowed)
    {
        if (!double.IsFinite(delta) || Math.Abs(delta) < Tolerance)
        {
            return null;
        }

        // The cards on the moved edge, then every card on the far edge of one of them, along the same axis.
        static bool Joins(WidgetInstance w) => w.Config is not IDesktopItemConfig;
        var group = new List<WidgetInstance>();
        var queue = new Queue<FracRect>();
        queue.Enqueue(before);
        while (queue.Count > 0)
        {
            var edge = queue.Dequeue();
            foreach (var w in widgets)
            {
                if (ReferenceEquals(w, refit) || group.Contains(w) || !Joins(w) || !Lattice.Follows(edge, w.Rect, vertical))
                {
                    continue;
                }

                group.Add(w);
                queue.Enqueue(w.Rect);
            }
        }

        if (group.Count == 0)
        {
            return null;
        }

        var moved = group.Select(w => vertical ? w.Rect with { Y = w.Rect.Y + delta } : w.Rect with { X = w.Rect.X + delta }).ToList();
        if (moved.Any(r => vertical ? r.Y < allowed.Y - Tolerance || r.Bottom > allowed.Bottom + Tolerance : r.X < allowed.X - Tolerance || r.Right > allowed.Right + Tolerance))
        {
            return new JoinShift(vertical, delta, [], "past the allowed area");
        }

        for (var i = 0; i < group.Count; i++)
        {
            foreach (var w in widgets)
            {
                if (group.Contains(w))
                {
                    continue;
                }

                var other = w.Rect;
                if (Lattice.Overlaps(moved[i], other) && !Lattice.Overlaps(group[i].Rect, ReferenceEquals(w, refit) ? before : other))
                {
                    return new JoinShift(vertical, delta, [], "into a card it did not overlap");
                }
            }
        }

        for (var i = 0; i < group.Count; i++)
        {
            group[i].Rect = moved[i];
        }

        return new JoinShift(vertical, delta, group);
    }
}
