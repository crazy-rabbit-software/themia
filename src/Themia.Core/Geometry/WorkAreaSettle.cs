using Themia.Core.Model;
using Themia.Sdk;

namespace Themia.Core.Geometry;

/// <summary>
/// [S-855] [S-6608] What is stored of a view against the taskbars of every display that shows it. Stored fractions are of
/// the whole monitor ([S-840]) and resolve on each display against its own monitor, so a view shown on a display whose
/// taskbar is on the left and on one whose taskbar is on the right holds what both show only in the part of the screen
/// both work areas hold (<see cref="GridGeometry.SharedAllowed"/>, the view's area). Two rules, nothing else:
/// <list type="number">
/// <item><b>Nothing is lost</b>: every widget keeps the visible minimum on the view's area on each axis
/// (<see cref="GridGeometry.MinVisibleDip"/>), sliding back just that far, as a gesture keeps it. A widget the user hung over
/// a taskbar's edge stays there (owner, 2026-11-21: "the widgets should be able to go over the screen"): the part over the
/// taskbar is behind it, the surface clipping at the work area ([S-851]).</item>
/// <item><b>Nothing lands hidden by a change of the area</b> (owner, 2026-11-20: "when i show the same view on two
/// screens it calculates the center without the taskbar this leads to hidden widgets on both screens"): the view
/// remembers the area its widgets were laid on (<see cref="ViewState.WorkArea"/>; none yet: the whole monitor, laid on no
/// work area). When it is shown in another (on a second display, another display alone, a taskbar that moved, grew or
/// stopped hiding), a widget that lay clear of a side there and would now reach behind a taskbar on it
/// (<see cref="GridGeometry.ClearSides"/>) moves in as little as it takes: the whole view by one translation where that
/// holds everything (so a layout made against one work area keeps its joins, gaps and alignment), else each set of
/// touching widgets by its own, carrying along a widget it would come to lie over. A side a widget already reached past
/// where it was laid on (hung over by the user, a taskbar's or the monitor's own) is left to the first rule.</item>
/// </list>
/// Nothing is ever resized by it but a set wider than the area. Pure: the host runs it in every mutation, on settled
/// metrics ([S-854]); a placement lands clear by its own rule (<see cref="GridGeometry.Land"/>).
/// </summary>
public static class WorkAreaSettle
{
    /// <summary>[S-11893] The one tolerance: the lattice's.</summary>
    private const double Eps = Lattice.Epsilon;

    /// <summary>
    /// [S-855] <paramref name="rects"/> (one view's widgets, in its order), laid on <paramref name="laidOn"/>, settled on
    /// <paramref name="screen"/>'s allowed area (the area every display showing the view holds): the same list where
    /// nothing moves, each moved rect where it lands otherwise. A non-finite rect is left as it is.
    /// </summary>
    public static IReadOnlyList<FracRect> Settle(IReadOnlyList<FracRect> rects, FracRect laidOn, ScreenMetrics screen)
    {
        ArgumentNullException.ThrowIfNull(rects);
        var a = screen.Allowed;
        var held = rects.Select(r => GridGeometry.ClearSides(r, laidOn, a)).ToArray();
        var arrived = Arrive(rects, held, screen);
        var settled = Reachable(arrived, screen);
        return Enumerable.Range(0, rects.Count).All(i => settled[i] == rects[i]) ? rects : settled;
    }

    /// <summary>
    /// [S-855] Settle every view a surface shows, in the state, on the area all the displays showing it hold (each
    /// surface's <see cref="SurfaceMetrics.Screen"/>, intersected), and remember that area as the one the view is laid on:
    /// the desktop's icons are left to their own rules ([S-11887], <c>DesktopGrid.Settle</c>); a view one of whose surfaces
    /// has not settled waits ([S-854]). Returns how many widgets moved.
    /// </summary>
    public static int Apply(AppState state, IReadOnlyList<SurfaceMetrics> surfaces)
    {
        ArgumentNullException.ThrowIfNull(state);
        var moved = 0;
        foreach (var (view, screen) in Shown(state, surfaces))
        {
            var widgets = view.Widgets.Where(w => w.Config is not IDesktopItemConfig).ToList();
            var settled = Settle([.. widgets.Select(w => w.Rect)], view.WorkArea ?? FracRect.Full, screen);
            for (var i = 0; i < widgets.Count; i++)
            {
                if (settled[i] != widgets[i].Rect)
                {
                    widgets[i].Rect = settled[i];
                    moved++;
                }
            }

            if (view.WorkArea is not { } known || !Lattice.Same(known, screen.Allowed))
            {
                view.WorkArea = screen.Allowed;
            }
        }

        return moved;
    }

    /// <summary>[S-855] Whether <see cref="Apply"/> would change anything: move a widget, or remember another area.</summary>
    public static bool Waits(AppState state, IReadOnlyList<SurfaceMetrics> surfaces)
    {
        ArgumentNullException.ThrowIfNull(state);
        return Shown(state, surfaces).Any(shown =>
        {
            if (shown.View.WorkArea is not { } known || !Lattice.Same(known, shown.Screen.Allowed))
            {
                return true;
            }

            IReadOnlyList<FracRect> rects = [.. shown.View.Widgets.Where(w => w.Config is not IDesktopItemConfig).Select(w => w.Rect)];
            return !ReferenceEquals(Settle(rects, known, shown.Screen), rects);
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

    /// <summary>
    /// The second rule: every rect of <paramref name="rects"/> inside its sides in <paramref name="held"/>, moved as
    /// little as it takes (the whole view by one translation, else each touching set, what it comes over carried).
    /// </summary>
    private static FracRect[] Arrive(IReadOnlyList<FracRect> rects, EdgeHolds[] held, ScreenMetrics screen)
    {
        var a = screen.Allowed;
        var settled = rects.ToArray();
        if (Enumerable.Range(0, rects.Count).All(i => Clear(rects[i], a, held[i])))
        {
            return settled;
        }

        var all = Enumerable.Range(0, rects.Count).Where(i => rects[i].IsFinite).ToList();

        // The whole view by one translation, where one holds every widget.
        if (Shift(rects, all, a, held) is { } whole)
        {
            foreach (var i in all)
            {
                settled[i] = Moved(rects[i], whole);
            }

            return settled;
        }

        // Else each set of touching widgets on its own ([S-2240]: a join is kept), what it comes over carried along.
        var groups = Lattice.Touching([.. all.Select(i => rects[i])]).Select(g => g.Select(k => all[k]).ToList()).ToList();
        var moved = new bool[groups.Count];
        var queue = new Queue<(int Group, (double Dx, double Dy) By)>();
        for (var g = 0; g < groups.Count; g++)
        {
            if (groups[g].All(i => Clear(rects[i], a, held[i])))
            {
                continue;
            }

            moved[g] = true;
            if (Shift(rects, groups[g], a, held) is { } by)
            {
                foreach (var i in groups[g])
                {
                    settled[i] = Moved(rects[i], by);
                }

                queue.Enqueue((g, by));
            }
            else
            {
                // Wider than the room on an axis: each widget kept in it on its own, as a placement's clamp does.
                foreach (var i in groups[g])
                {
                    settled[i] = GridGeometry.Clamp(rects[i], screen, held[i]);
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
                if (Carry(rects, groups[g], a, held, by) is { } carried)
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
    /// The first rule: every rect keeps the visible minimum on the area ([S-6608], <see cref="GridGeometry.Reach"/> of its
    /// own size: nothing is resized). A set of touching rects out of reach moves by one translation where one brings
    /// every member in reach (its joins kept), else each member on its own.
    /// </summary>
    private static FracRect[] Reachable(FracRect[] rects, ScreenMetrics screen)
    {
        bool InReach(FracRect r)
        {
            var (loX, hiX, loY, hiY) = GridGeometry.Reach(screen, r.W, r.H);
            return !r.IsFinite || (r.X >= loX - Eps && r.X <= hiX + Eps && r.Y >= loY - Eps && r.Y <= hiY + Eps);
        }

        if (rects.All(InReach))
        {
            return rects;
        }

        var settled = rects.ToArray();
        var all = Enumerable.Range(0, rects.Length).Where(i => rects[i].IsFinite).ToList();
        foreach (var group in Lattice.Touching([.. all.Select(i => rects[i])]).Select(g => g.Select(k => all[k]).ToList()))
        {
            if (group.All(i => InReach(rects[i])))
            {
                continue;
            }

            double loX = double.NegativeInfinity, hiX = double.PositiveInfinity;
            double loY = double.NegativeInfinity, hiY = double.PositiveInfinity;
            foreach (var i in group)
            {
                var r = rects[i];
                var (rLoX, rHiX, rLoY, rHiY) = GridGeometry.Reach(screen, r.W, r.H);
                loX = Math.Max(loX, rLoX - r.X);
                hiX = Math.Min(hiX, rHiX - r.X);
                loY = Math.Max(loY, rLoY - r.Y);
                hiY = Math.Min(hiY, rHiY - r.Y);
            }

            foreach (var i in group)
            {
                var r = rects[i];
                if (loX <= hiX + Eps && loY <= hiY + Eps)
                {
                    settled[i] = Moved(r, (Nearest(loX, hiX), Nearest(loY, hiY)));
                }
                else
                {
                    var (rLoX, rHiX, rLoY, rHiY) = GridGeometry.Reach(screen, r.W, r.H);
                    settled[i] = r with { X = GridGeometry.Between(r.X, rLoX, rHiX), Y = GridGeometry.Between(r.Y, rLoY, rHiY) };
                }
            }
        }

        return settled;
    }

    /// <summary>Whether a rect lies inside the area on each side it is held on.</summary>
    private static bool Clear(FracRect r, FracRect a, EdgeHolds held)
        => !r.IsFinite
            || ((!held.Left || r.X >= a.X - Eps) && (!held.Right || r.Right <= a.Right + Eps)
                && (!held.Top || r.Y >= a.Y - Eps) && (!held.Bottom || r.Bottom <= a.Bottom + Eps));

    /// <summary>
    /// The translation nearest none that puts every rect of <paramref name="at"/> inside its held sides without moving one
    /// hung over another edge along that axis; null where none does on an axis.
    /// </summary>
    private static (double Dx, double Dy)? Shift(IReadOnlyList<FracRect> rects, IReadOnlyList<int> at, FracRect a, EdgeHolds[] held)
        => Range(rects, at, a, held) is var (loX, hiX, loY, hiY) && loX <= hiX + Eps && loY <= hiY + Eps
            ? (Nearest(loX, hiX), Nearest(loY, hiY))
            : null;

    /// <summary>
    /// <paramref name="by"/> as far as the rects of <paramref name="at"/> may go each way (the same rules as
    /// <see cref="Shift"/>, from where they stand); null where they may not move at all.
    /// </summary>
    private static (double Dx, double Dy)? Carry(IReadOnlyList<FracRect> rects, IReadOnlyList<int> at, FracRect a, EdgeHolds[] held, (double Dx, double Dy) by)
    {
        var (loX, hiX, loY, hiY) = Range(rects, at, a, held);
        if (loX > hiX + Eps || loY > hiY + Eps)
        {
            return null;
        }

        var carried = (Math.Clamp(by.Dx, Math.Min(loX, hiX), hiX), Math.Clamp(by.Dy, Math.Min(loY, hiY), hiY));
        return Math.Abs(carried.Item1) < Eps && Math.Abs(carried.Item2) < Eps ? null : carried;
    }

    /// <summary>
    /// The translations the rects of <paramref name="at"/> may take together: on a side it is held on each stays inside
    /// it; on any other side none goes further over it than it already was, and one that is over an edge (the user's
    /// overhang, [S-6608]) does not move along that axis at all.
    /// </summary>
    private static (double LoX, double HiX, double LoY, double HiY) Range(IReadOnlyList<FracRect> rects, IReadOnlyList<int> at, FracRect a, EdgeHolds[] held)
    {
        double loX = double.NegativeInfinity, hiX = double.PositiveInfinity;
        double loY = double.NegativeInfinity, hiY = double.PositiveInfinity;
        foreach (var i in at)
        {
            var (r, h) = (rects[i], held[i]);
            var overX = (!h.Left && r.X < a.X - Eps) || (!h.Right && r.Right > a.Right + Eps);
            var overY = (!h.Top && r.Y < a.Y - Eps) || (!h.Bottom && r.Bottom > a.Bottom + Eps);
            loX = Math.Max(loX, overX ? 0 : h.Left ? a.X - r.X : Math.Min(0, a.X - r.X));
            hiX = Math.Min(hiX, overX ? 0 : h.Right ? a.Right - r.Right : Math.Max(0, a.Right - r.Right));
            loY = Math.Max(loY, overY ? 0 : h.Top ? a.Y - r.Y : Math.Min(0, a.Y - r.Y));
            hiY = Math.Min(hiY, overY ? 0 : h.Bottom ? a.Bottom - r.Bottom : Math.Max(0, a.Bottom - r.Bottom));
        }

        return (loX, hiX, loY, hiY);
    }

    /// <summary>The value of [lo, hi] nearest none: a rect already where it may be on an axis is not moved along it.</summary>
    private static double Nearest(double lo, double hi)
    {
        var at = Math.Clamp(0, Math.Min(lo, hi), hi);
        return Math.Abs(at) < Eps ? 0 : at;
    }

    private static FracRect Moved(FracRect r, (double Dx, double Dy) by) => r with { X = r.X + by.Dx, Y = r.Y + by.Dy };

    /// <summary>Whether a rect of <paramref name="mover"/>, where it settled, now lies over a rect of <paramref name="other"/> it did not lie over before.</summary>
    private static bool Comes(List<int> mover, List<int> other, IReadOnlyList<FracRect> rects, FracRect[] settled)
        => mover.Any(m => other.Any(o => Lattice.Overlaps(settled[m], rects[o]) && !Lattice.Overlaps(rects[m], rects[o])));
}
