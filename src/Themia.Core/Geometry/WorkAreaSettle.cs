using Themia.Core.Model;
using Themia.Sdk;

namespace Themia.Core.Geometry;

/// <summary>
/// [S-855] Every widget of a view clear of the taskbars of every display that shows it (owner, 2026-11-20: "when i
/// show the same view on two screens it calculates the center without the taskbar this leads to hidden widgets on
/// both screens"). Stored fractions are of the whole monitor ([S-840]) and resolve on each display against its own
/// monitor, so a view shown on a display whose taskbar is on the left and on one whose taskbar is on the right holds
/// what both show only in the part of the screen both work areas hold (<see cref="GridGeometry.SharedAllowed"/>).
/// A widget reaching past a taskbar's edge of that part (one laid out against the other display's work area, or
/// before the view was shown on two displays, or under a taskbar that moved, grew or stopped hiding) is moved in as
/// little as it takes: the whole view by one translation where that holds everything (so a layout made against one
/// work area keeps its joins, gaps and alignment), else each set of touching widgets by its own, carrying along a
/// widget it would come to lie over. What hangs over the monitor's own edge (Over the edge, [S-6608]) is left.
/// Pure: the host runs it in every mutation, on settled metrics ([S-854]).
/// </summary>
public static class WorkAreaSettle
{
    private const double Eps = 1e-9;

    /// <summary>
    /// [S-855] <paramref name="rects"/> (one view's widgets, in its order) settled clear of the strips
    /// <paramref name="screen"/>'s allowed area leaves (the area every display showing the view holds): the same list
    /// where every rect is clear already, each moved rect where it lands otherwise. A non-finite rect is left as it is.
    /// </summary>
    public static IReadOnlyList<FracRect> Settle(IReadOnlyList<FracRect> rects, ScreenMetrics screen)
    {
        ArgumentNullException.ThrowIfNull(rects);
        var a = screen.Allowed;
        var strips = GridGeometry.Strips(a);
        if (strips.None || rects.All(r => Clear(r, a, strips)))
        {
            return rects;
        }

        var all = Enumerable.Range(0, rects.Count).Where(i => rects[i].IsFinite).ToList();
        var settled = rects.ToArray();

        // The whole view by one translation, where one holds every widget.
        if (Shift(rects, all, a, strips) is { } whole)
        {
            foreach (var i in all)
            {
                settled[i] = Moved(rects[i], whole);
            }

            return settled;
        }

        // Else each set of touching widgets on its own ([S-2240]: a join is kept), what it comes over carried along.
        var groups = SizeFit.Touching([.. all.Select(i => rects[i])]).Select(g => g.Select(k => all[k]).ToList()).ToList();
        var moved = new bool[groups.Count];
        var queue = new Queue<(int Group, (double Dx, double Dy) By)>();
        for (var g = 0; g < groups.Count; g++)
        {
            if (groups[g].All(i => Clear(rects[i], a, strips)))
            {
                continue;
            }

            moved[g] = true;
            if (Shift(rects, groups[g], a, strips) is { } by)
            {
                foreach (var i in groups[g])
                {
                    settled[i] = Moved(rects[i], by);
                }

                queue.Enqueue((g, by));
            }
            else
            {
                // Wider than the room on an axis: each widget kept in it on its own, as a gesture's clamp does.
                foreach (var i in groups[g])
                {
                    settled[i] = GridGeometry.Clamp(rects[i], screen with { HoldsWhole = false });
                }
            }
        }

        while (queue.Count > 0)
        {
            var (from, by) = queue.Dequeue();
            for (var g = 0; g < groups.Count; g++)
            {
                if (moved[g] || !Comes(groups[from], groups[g], rects, settled))
                {
                    continue;
                }

                // As far as the widget that came over it moved, or as far as it may.
                moved[g] = true;
                if (Carry(rects, groups[g], a, strips, by) is { } carried)
                {
                    foreach (var i in groups[g])
                    {
                        settled[i] = Moved(rects[i], carried);
                    }

                    queue.Enqueue((g, carried));
                }
            }
        }

        return settled;
    }

    /// <summary>
    /// [S-855] Settle every view a surface shows, in the state, on the area all the displays showing it hold (each
    /// surface's <see cref="SurfaceMetrics.Screen"/>, intersected): the desktop's icons are left to their own rules
    /// ([S-11887], <c>DesktopGrid.Settle</c>); a view one of whose surfaces has not settled waits ([S-854]). Returns
    /// how many widgets moved.
    /// </summary>
    public static int Apply(AppState state, IReadOnlyList<SurfaceMetrics> surfaces)
    {
        ArgumentNullException.ThrowIfNull(state);
        var moved = 0;
        foreach (var (view, screen) in Shown(state, surfaces))
        {
            var widgets = view.Widgets.Where(w => w.Config is not IDesktopItemConfig).ToList();
            var settled = Settle([.. widgets.Select(w => w.Rect)], screen);
            for (var i = 0; i < widgets.Count; i++)
            {
                if (settled[i] != widgets[i].Rect)
                {
                    widgets[i].Rect = settled[i];
                    moved++;
                }
            }
        }

        return moved;
    }

    /// <summary>[S-855] Whether <see cref="Apply"/> would move anything.</summary>
    public static bool Waits(AppState state, IReadOnlyList<SurfaceMetrics> surfaces)
    {
        ArgumentNullException.ThrowIfNull(state);
        return Shown(state, surfaces).Any(shown =>
        {
            var a = shown.Screen.Allowed;
            var strips = GridGeometry.Strips(a);
            return shown.View.Widgets.Any(w => w.Config is not IDesktopItemConfig && w.Rect.IsFinite && !Clear(w.Rect, a, strips));
        });
    }

    /// <summary>
    /// [S-855] The views the surfaces show, each with the screen of its first surface narrowed to the area every
    /// surface showing it holds; a view one of whose surfaces is not writable ([S-854]) left out.
    /// </summary>
    public static IEnumerable<(ViewState View, ScreenMetrics Screen)> Shown(AppState state, IReadOnlyList<SurfaceMetrics> surfaces)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(surfaces);
        foreach (var showing in surfaces.GroupBy(s => s.ViewId, StringComparer.Ordinal))
        {
            if (!showing.All(s => s.Writable) || state.FindView(showing.Key) is not { } view)
            {
                continue;
            }

            var first = showing.First().Screen;
            yield return (view, first with { Allowed = GridGeometry.SharedAllowed(showing.Select(s => s.Screen.Allowed)) });
        }
    }

    /// <summary>Whether a rect lies clear of every strip: inside the area on each side that ends at one.</summary>
    private static bool Clear(FracRect r, FracRect a, EdgeHolds strips)
        => !r.IsFinite
            || ((!strips.Left || r.X >= a.X - Eps) && (!strips.Right || r.Right <= a.Right + Eps)
                && (!strips.Top || r.Y >= a.Y - Eps) && (!strips.Bottom || r.Bottom <= a.Bottom + Eps));

    /// <summary>
    /// The translation nearest none that puts every rect of <paramref name="at"/> clear of the strips without taking any
    /// further over a monitor edge than it was; null where none does on an axis.
    /// </summary>
    private static (double Dx, double Dy)? Shift(IReadOnlyList<FracRect> rects, IReadOnlyList<int> at, FracRect a, EdgeHolds strips)
        => Range(rects, at, a, strips) is var (loX, hiX, loY, hiY) && loX <= hiX + Eps && loY <= hiY + Eps
            ? (Nearest(loX, hiX), Nearest(loY, hiY))
            : null;

    /// <summary>
    /// <paramref name="by"/> as far as the rects of <paramref name="at"/> may go each way (the same rules as
    /// <see cref="Shift"/>, from where they stand); null where they may not move at all.
    /// </summary>
    private static (double Dx, double Dy)? Carry(IReadOnlyList<FracRect> rects, IReadOnlyList<int> at, FracRect a, EdgeHolds strips, (double Dx, double Dy) by)
    {
        var (loX, hiX, loY, hiY) = Range(rects, at, a, strips);
        if (loX > hiX + Eps || loY > hiY + Eps)
        {
            return null;
        }

        var carried = (Math.Clamp(by.Dx, Math.Min(loX, hiX), hiX), Math.Clamp(by.Dy, Math.Min(loY, hiY), hiY));
        return Math.Abs(carried.Item1) < Eps && Math.Abs(carried.Item2) < Eps ? null : carried;
    }

    /// <summary>
    /// The translations the rects of <paramref name="at"/> may take together: on a side that ends at a strip each
    /// stays inside it; on the monitor's own edge none goes further over it than it already was.
    /// </summary>
    private static (double LoX, double HiX, double LoY, double HiY) Range(IReadOnlyList<FracRect> rects, IReadOnlyList<int> at, FracRect a, EdgeHolds strips)
    {
        double loX = double.NegativeInfinity, hiX = double.PositiveInfinity;
        double loY = double.NegativeInfinity, hiY = double.PositiveInfinity;
        foreach (var i in at)
        {
            var r = rects[i];
            loX = Math.Max(loX, strips.Left ? a.X - r.X : Math.Min(0, a.X - r.X));
            hiX = Math.Min(hiX, strips.Right ? a.Right - r.Right : Math.Max(0, a.Right - r.Right));
            loY = Math.Max(loY, strips.Top ? a.Y - r.Y : Math.Min(0, a.Y - r.Y));
            hiY = Math.Min(hiY, strips.Bottom ? a.Bottom - r.Bottom : Math.Max(0, a.Bottom - r.Bottom));
        }

        return (loX, hiX, loY, hiY);
    }

    /// <summary>The value of [lo, hi] nearest none: a rect already clear on an axis is not moved along it.</summary>
    private static double Nearest(double lo, double hi)
    {
        var at = Math.Clamp(0, Math.Min(lo, hi), hi);
        return Math.Abs(at) < Eps ? 0 : at;
    }

    private static FracRect Moved(FracRect r, (double Dx, double Dy) by) => r with { X = r.X + by.Dx, Y = r.Y + by.Dy };

    /// <summary>Whether a rect of <paramref name="mover"/>, where it settled, now lies over a rect of <paramref name="other"/> it did not lie over before.</summary>
    private static bool Comes(List<int> mover, List<int> other, IReadOnlyList<FracRect> rects, FracRect[] settled)
        => mover.Any(m => other.Any(o => BlockGeometry.Over(settled[m], rects[o]) && !BlockGeometry.Over(rects[m], rects[o])));
}
