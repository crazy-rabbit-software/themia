using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Sdk;

namespace Themia.Core.Desktop;

/// <summary>
/// [S-11875] [S-11876] Desktop icons placed as widgets are (owner, 2026-11-15: "the desktop icons should
/// follow the same behaviour, they dont have to follow how windows does it anymore"): on the one widget
/// grid ([S-06]), each icon a whole number of its cells, and every rule that puts an icon somewhere the
/// widget grid's. There is no icon grid of Explorer's: no column-first order, no cell an icon holds, no
/// pass that pulls icons back onto it. Pure; fractions of the display.
/// <list type="bullet">
/// <item><see cref="IconSize"/>: Explorer's cell at the icon size in force (<see cref="DesktopCell.Grid"/>)
/// plus the gap round every card, rounded <b>up</b> to whole cells on each axis, as any widget's sizing is
/// (<see cref="SizeFit.Limits(WidgetSizing, ScreenMetrics, double, double)"/>: an icon is drawn at scale 1).</item>
/// <item><see cref="FirstFree"/>: the first spot on the grid, in reading order (rows from the work area's
/// top, each from its left), where an icon's box overlaps no widget and no icon: where a new icon with
/// nowhere asked for arrives, and the order Auto arrange and Sort by pack in.</item>
/// <item><see cref="Arrive"/> at a point (a drop, a paste at the click): the icon's box centred on the
/// point, snapped to the grid, or the free spot nearest to it when that one is taken.</item>
/// <item>A drag of icons on the desktop is the widgets' move ([S-2008], [S-1584]); one dropped back on
/// the desktop from outside lands by <see cref="Drop"/>, the block's move. Neither looks for room: icons
/// and widgets may overlap there, as widgets do.</item>
/// </list>
/// </summary>
public static class DesktopGrid
{
    private const double Epsilon = 1e-9;

    /// <summary>
    /// [S-11875] An icon's box on <paramref name="screen"/> as fractions: <paramref name="cell"/> (the icon's
    /// cell in DIPs) rounded up to whole grid cells on each axis. Falls back to
    /// the cell itself on an unusable screen.
    /// </summary>
    public static (double W, double H) IconSize(SizeDip cell, ScreenMetrics screen)
    {
        if (!screen.IsUsable)
        {
            return (cell.Width, cell.Height);
        }

        return SizeFit.Limits(WidgetSizing.Fixed(cell), screen, 1) is { } limits
            ? (limits.Width.Min, limits.Height.Min)
            : (cell.Width / screen.Width, cell.Height / screen.Height);
    }

    /// <summary>Whether <paramref name="rect"/> overlaps none of <paramref name="occupied"/> (edges that only touch do not overlap).</summary>
    public static bool Free(FracRect rect, IReadOnlyList<FracRect> occupied)
    {
        ArgumentNullException.ThrowIfNull(occupied);
        return Blocker(rect, occupied) is null;
    }

    /// <summary>The widgets of <paramref name="view"/> as the room they take, leaving out <paramref name="except"/>.</summary>
    public static List<FracRect> Occupied(ViewState view, IReadOnlySet<string>? except = null)
    {
        ArgumentNullException.ThrowIfNull(view);
        return [.. view.Widgets.Where(w => except is null || !except.Contains(w.Id)).Select(w => w.Rect)];
    }

    /// <summary>
    /// [S-11876] The first free spot for a box of <paramref name="size"/> in reading order: grid rows from the
    /// allowed area's top, each from its left, the box whole inside the area. Null when none is free.
    /// </summary>
    public static FracRect? FirstFree((double W, double H) size, IReadOnlyList<FracRect> occupied, ScreenMetrics screen)
    {
        ArgumentNullException.ThrowIfNull(occupied);
        if (Range(size, screen) is not { } range)
        {
            return null;
        }

        var (c0, c1, r0, r1, cols, rows) = range;

        for (var r = r0; r <= r1; r++)
        {
            var c = c0;
            while (c <= c1)
            {
                var rect = new FracRect((double)c / cols, (double)r / rows, size.W, size.H);
                if (Blocker(rect, occupied) is not { } blocker)
                {
                    return rect;
                }

                // Past the widget in the way: the next line at or after its right edge.
                c = Math.Max(c + 1, (int)Math.Ceiling((blocker.Right * cols) - 1e-6));
            }
        }

        return null;
    }

    /// <summary>
    /// [S-11876] The free spot for a box of <paramref name="size"/> nearest to the corner
    /// (<paramref name="x"/>, <paramref name="y"/>): that corner on its nearest grid line when the box is free
    /// there, else the free spot whose corner is the fewest cells away (among equals the one further up, then
    /// further left, so the answer never depends on the order things were asked in). Null when none is free.
    /// </summary>
    public static FracRect? NearestFree((double W, double H) size, double x, double y, IReadOnlyList<FracRect> occupied, ScreenMetrics screen)
    {
        ArgumentNullException.ThrowIfNull(occupied);
        if (Range(size, screen) is not { } range || !double.IsFinite(x) || !double.IsFinite(y))
        {
            return null;
        }

        var (c0, c1, r0, r1, cols, rows) = range;

        var wc = Math.Clamp((int)Math.Round(x * cols, MidpointRounding.AwayFromZero), c0, c1);
        var wr = Math.Clamp((int)Math.Round(y * rows, MidpointRounding.AwayFromZero), r0, r1);
        FracRect? best = null;
        var bestD = long.MaxValue;
        var (bestC, bestR) = (0, 0);
        var reach = Math.Max(Math.Max(wc - c0, c1 - wc), Math.Max(wr - r0, r1 - wr));
        for (var ring = 0; ring <= reach; ring++)
        {
            if (best is not null && (long)ring * ring > bestD)
            {
                break;
            }

            for (var r = wr - ring; r <= wr + ring; r++)
            {
                for (var c = wc - ring; c <= wc + ring; c++)
                {
                    if (Math.Max(Math.Abs(c - wc), Math.Abs(r - wr)) != ring || c < c0 || c > c1 || r < r0 || r > r1)
                    {
                        continue;
                    }

                    long dc = c - wc;
                    long dr = r - wr;
                    var d = (dc * dc) + (dr * dr);
                    if (d > bestD || (d == bestD && (r > bestR || (r == bestR && c >= bestC))))
                    {
                        continue;
                    }

                    var rect = new FracRect((double)c / cols, (double)r / rows, size.W, size.H);
                    if (Blocker(rect, occupied) is null)
                    {
                        (best, bestD, bestC, bestR) = (rect, d, c, r);
                    }
                }
            }
        }

        return best;
    }

    /// <summary>
    /// [S-11876] Where an icon that arrives lands (a new file, a paste, New ›, a drop from outside, a stand-in):
    /// with a point (<paramref name="cx"/>, <paramref name="cy"/>), its box centred there and on the grid, or
    /// the nearest free spot; with none, the first free spot (<see cref="FirstFree"/>). The spot is added to
    /// <paramref name="occupied"/>, so the arrivals of one pass never share one. Null when nothing is free.
    /// </summary>
    public static FracRect? Arrive(List<FracRect> occupied, (double W, double H) size, double? cx, double? cy, ScreenMetrics screen)
    {
        ArgumentNullException.ThrowIfNull(occupied);
        var spot = cx is { } px && cy is { } py
            ? NearestFree(size, px - (size.W / 2), py - (size.H / 2), occupied, screen)
            : FirstFree(size, occupied, screen);
        if (spot is { } placed)
        {
            occupied.Add(placed);
        }

        return spot;
    }

    /// <summary>
    /// [S-5594] [S-11876] Auto arrange icons, once: every icon packed by <see cref="FirstFree"/> round the
    /// other widgets, in the reading order of where they stand now (by grid row, then left to right; two in
    /// one place keep the order they are listed in), each at <paramref name="size"/>. Returns, per icon, its
    /// new rectangle, or null where it already stands there (or no room is left for it): packing a packed
    /// desktop changes nothing.
    /// </summary>
    public static IReadOnlyList<FracRect?> Pack(IReadOnlyList<FracRect> icons, IReadOnlyList<FracRect> others, (double W, double H) size, ScreenMetrics screen)
    {
        ArgumentNullException.ThrowIfNull(icons);
        ArgumentNullException.ThrowIfNull(others);
        var result = new FracRect?[icons.Count];
        var (_, rows) = GridGeometry.GridDims(screen);
        var occupied = new List<FracRect>(others);
        foreach (var i in Enumerable.Range(0, icons.Count)
            .OrderBy(i => (int)Math.Round(icons[i].Y * rows, MidpointRounding.AwayFromZero))
            .ThenBy(i => Math.Round(icons[i].X, 6))
            .ThenBy(i => i))
        {
            if (Arrive(occupied, size, null, null, screen) is not { } packed)
            {
                break;
            }

            result[i] = Same(packed, icons[i]) ? null : packed;
        }

        return result;
    }

    /// <summary>
    /// [S-5594] Auto arrange over every view <paramref name="surfaces"/> show: each view once, for the first
    /// writable surface showing it (list the primary first; [S-854]: a surface not settled is left alone),
    /// its icons packed (<see cref="Pack"/>) at <paramref name="cell"/>. Pure.
    /// </summary>
    public static List<(WidgetInstance Widget, FracRect Rect)> Packs(AppState state, IReadOnlyList<SurfaceMetrics> surfaces, SizeDip cell)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(surfaces);
        var moves = new List<(WidgetInstance Widget, FracRect Rect)>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var surface in surfaces)
        {
            if (!seen.Add(surface.ViewId) || !surface.Writable || state.FindView(surface.ViewId) is not { } view)
            {
                continue;
            }

            var icons = view.Widgets.Where(w => w.Config is IDesktopItemConfig).ToList();
            if (icons.Count == 0)
            {
                continue;
            }

            var others = view.Widgets.Where(w => w.Config is not IDesktopItemConfig).Select(w => w.Rect).ToList();
            var packed = Pack([.. icons.Select(w => w.Rect)], others, IconSize(cell, surface.Screen), surface.Screen);
            for (var i = 0; i < icons.Count; i++)
            {
                if (packed[i] is { } rect)
                {
                    moves.Add((icons[i], rect));
                }
            }
        }

        return moves;
    }

    /// <summary>
    /// [S-5593] [S-11875] [S-11887] The icons of one view when the icon size changes their box from
    /// <paramref name="from"/> to <paramref name="to"/>. Icons that stand together (within half a box of each
    /// other: a column, a row, a block) are one arrangement, and each icon's distance from its arrangement's
    /// top-left corner grows or shrinks with the box (its anchor), so the arrangement keeps its shape and its
    /// corner, and a size and back puts it where it was. Then all are laid at the new size by the one collision
    /// rule (<see cref="Settle"/>'s): in reading order of the anchors, each on its anchor's grid spot inside the
    /// inset area ([S-11888]), pushed down its column or along its row past the icons laid before it, else the
    /// nearest free spot. So a column stays a column in the same order, no two icons overlap and none lands on
    /// a widget of <paramref name="others"/>. Returns every icon's new rectangle (where no room is left, its
    /// anchor's spot at the new size). The same box both ways changes nothing on a valid layout.
    /// </summary>
    public static IReadOnlyList<FracRect> Rescale(IReadOnlyList<FracRect> icons, IReadOnlyList<FracRect> others, (double W, double H) from, (double W, double H) to, ScreenMetrics screen)
    {
        ArgumentNullException.ThrowIfNull(icons);
        ArgumentNullException.ThrowIfNull(others);
        var corner = Arrangements(icons, from);
        var anchors = icons.Select((r, i) => (
            X: from.W > 0 ? corner[i].X + ((r.X - corner[i].X) * to.W / from.W) : r.X,
            Y: from.H > 0 ? corner[i].Y + ((r.Y - corner[i].Y) * to.H / from.H) : r.Y)).ToList();
        var laid = Lay([.. ReadingOrder(anchors, screen)], anchors, [], others, to, screen);
        return [.. icons.Select((r, i) => laid[i] ?? Fallback(anchors[i], to, screen))];
    }

    /// <summary>
    /// [S-11887] Per icon, the top-left corner of the arrangement it is part of: the icons joined by standing
    /// within half a box (<paramref name="box"/>) of one another, on both axes.
    /// </summary>
    private static (double X, double Y)[] Arrangements(IReadOnlyList<FracRect> icons, (double W, double H) box)
    {
        var group = Enumerable.Range(0, icons.Count).ToArray();
        int Root(int i)
        {
            while (group[i] != i)
            {
                i = group[i] = group[group[i]];
            }

            return i;
        }

        var (hw, hh) = (box.W / 2, box.H / 2);
        for (var i = 0; i < icons.Count; i++)
        {
            for (var j = i + 1; j < icons.Count; j++)
            {
                var (a, b) = (icons[i], icons[j]);
                if (a.IsFinite && b.IsFinite
                    && a.X < b.Right + hw && b.X < a.Right + hw && a.Y < b.Bottom + hh && b.Y < a.Bottom + hh)
                {
                    group[Root(i)] = Root(j);
                }
            }
        }

        var corners = new Dictionary<int, (double X, double Y)>();
        for (var i = 0; i < icons.Count; i++)
        {
            var root = Root(i);
            var r = icons[i];
            corners[root] = corners.TryGetValue(root, out var c) ? (Math.Min(c.X, r.X), Math.Min(c.Y, r.Y)) : (r.X, r.Y);
        }

        return [.. Enumerable.Range(0, icons.Count).Select(i => corners[Root(i)])];
    }

    /// <summary>
    /// [S-11887] The settle pass's rule for one view's icons (at start, on a display or work-area change, after
    /// the icon size changed): an icon is <b>valid</b> when its rect is the icon's box (<paramref name="size"/>),
    /// whole inside the work area, and overlaps no icon before it in reading order; icons never overlap. Valid
    /// icons stay exactly where they are (no write: a valid layout is left alone; an icon the user put on a
    /// widget or against the work area's edge stays). The others (an icon of another box, a stored size of
    /// an older build, one under the taskbar, the later of two that overlap) are laid at the box by the one
    /// collision rule round the valid icons and <paramref name="others"/> (the view's widgets): in reading
    /// order, on their own corner's grid spot inside the inset area ([S-11888]), pushed down a column (or
    /// along a row) past the icons in the way, else the nearest free spot, so a column stays a column in the
    /// same order and nothing jumps across the screen while a near spot is free. Returns, per icon, its new
    /// rectangle, or null where it stays (or no room is left for it).
    /// </summary>
    public static IReadOnlyList<FracRect?> Settle(IReadOnlyList<FracRect> icons, IReadOnlyList<FracRect> others, (double W, double H) size, ScreenMetrics screen)
    {
        ArgumentNullException.ThrowIfNull(icons);
        ArgumentNullException.ThrowIfNull(others);
        var none = new FracRect?[icons.Count];
        if (!screen.IsUsable || icons.Count == 0)
        {
            return none;
        }

        var a = screen.Allowed;
        var anchors = icons.Select(r => (r.X, r.Y)).ToList();
        var kept = new List<FracRect>();
        var moving = new List<int>();
        foreach (var i in ReadingOrder(anchors, screen))
        {
            var r = icons[i];
            var valid = r.IsFinite
                && Math.Abs(r.W - size.W) < 1e-6 && Math.Abs(r.H - size.H) < 1e-6
                && r.X >= a.X - 1e-6 && r.Y >= a.Y - 1e-6 && r.Right <= a.Right + 1e-6 && r.Bottom <= a.Bottom + 1e-6
                && Blocker(r, kept) is null;
            if (valid)
            {
                kept.Add(r);
            }
            else
            {
                moving.Add(i);
            }
        }

        if (moving.Count == 0)
        {
            return none;
        }

        var laid = Lay(moving, anchors, kept, others, size, screen);
        for (var i = 0; i < laid.Length; i++)
        {
            if (laid[i] is { } rect && Same(rect, icons[i]))
            {
                laid[i] = null;
            }
        }

        return laid;
    }

    /// <summary>
    /// [S-11887] The one collision rule: the icons <paramref name="order"/> names laid one by one at
    /// <paramref name="size"/>, each on its anchor's grid spot (clamped into the inset area, [S-11888]); where an
    /// icon already there (<paramref name="kept"/>, or one laid before it) is in the way, pushed past it down
    /// the column (when they stand more above each other than beside) or along the row, as long as no widget of
    /// <paramref name="others"/> is in the way; else the free spot nearest the anchor. Null for an icon with no
    /// room.
    /// </summary>
    private static FracRect?[] Lay(IReadOnlyList<int> order, IReadOnlyList<(double X, double Y)> anchors, List<FracRect> kept, IReadOnlyList<FracRect> others, (double W, double H) size, ScreenMetrics screen)
    {
        var result = new FracRect?[anchors.Count];
        if (Range(size, screen) is not { } range)
        {
            return result;
        }

        var (c0, c1, r0, r1, cols, rows) = range;
        var wCells = Math.Max(1, size.W * cols);
        var hCells = Math.Max(1, size.H * rows);
        var icons = new List<FracRect>(kept);
        foreach (var i in order)
        {
            var (ax, ay) = anchors[i];
            if (!double.IsFinite(ax) || !double.IsFinite(ay))
            {
                continue;
            }

            var c = Math.Clamp((int)Math.Round(ax * cols, MidpointRounding.AwayFromZero), c0, c1);
            var r = Math.Clamp((int)Math.Round(ay * rows, MidpointRounding.AwayFromZero), r0, r1);
            FracRect? spot = null;
            bool? down = null;
            for (var step = 0; step <= cols + rows; step++)
            {
                var rect = new FracRect((double)c / cols, (double)r / rows, size.W, size.H);
                if (Blocker(rect, others) is not null)
                {
                    break;
                }

                if (Blocker(rect, icons) is not { } icon)
                {
                    spot = rect;
                    break;
                }

                // The way the first icon in the way says (more above each other: down the column; more beside
                // each other: along the row), kept for the rest of this icon's push.
                down ??= Math.Abs(r - (icon.Y * rows)) / hCells >= Math.Abs(c - (icon.X * cols)) / wCells;
                if (down.Value)
                {
                    r = Math.Max(r + 1, (int)Math.Ceiling((icon.Bottom * rows) - 1e-6));
                }
                else
                {
                    c = Math.Max(c + 1, (int)Math.Ceiling((icon.Right * cols) - 1e-6));
                }

                if (r > r1 || c > c1)
                {
                    break;
                }
            }

            spot ??= NearestFree(size, ax, ay, [.. others, .. icons], screen);
            if (spot is { } placed)
            {
                icons.Add(placed);
                result[i] = placed;
            }
        }

        return result;
    }

    /// <summary>Indices of <paramref name="corners"/> in reading order: by grid row, then left to right, then as listed.</summary>
    private static IEnumerable<int> ReadingOrder(IReadOnlyList<(double X, double Y)> corners, ScreenMetrics screen)
    {
        var (_, rows) = GridGeometry.GridDims(screen);
        return Enumerable.Range(0, corners.Count)
            .OrderBy(i => double.IsFinite(corners[i].Y) ? (int)Math.Round(corners[i].Y * rows, MidpointRounding.AwayFromZero) : int.MaxValue)
            .ThenBy(i => double.IsFinite(corners[i].X) ? Math.Round(corners[i].X, 6) : double.MaxValue)
            .ThenBy(i => i);
    }

    /// <summary>An anchor with no room left: its grid spot at <paramref name="size"/>, kept inside the allowed area.</summary>
    private static FracRect Fallback((double X, double Y) anchor, (double W, double H) size, ScreenMetrics screen)
    {
        var a = screen.Allowed;
        var (cols, rows) = GridGeometry.GridDims(screen);
        var x = double.IsFinite(anchor.X) ? Math.Round(anchor.X * cols, MidpointRounding.AwayFromZero) / cols : a.X;
        var y = double.IsFinite(anchor.Y) ? Math.Round(anchor.Y * rows, MidpointRounding.AwayFromZero) / rows : a.Y;
        return new FracRect(Math.Clamp(x, a.X, Math.Max(a.X, a.Right - size.W)), Math.Clamp(y, a.Y, Math.Max(a.Y, a.Bottom - size.H)), size.W, size.H);
    }

    /// <summary>
    /// [S-5546] [S-1584] Icons dragged out and let go back on the desktop: <paramref name="moving"/> (in the
    /// target display's fractions) move as one block, the held one's corner at (<paramref name="x"/>,
    /// <paramref name="y"/>), the block snapped to the grid and kept in reach as a widget move is
    /// (<see cref="BlockGeometry.Move"/>). Each keeps its size and its offset to the others; nothing is pushed
    /// aside. An unknown held index moves nothing.
    /// </summary>
    public static IReadOnlyList<FracRect> Drop(IReadOnlyList<FracRect> moving, int held, double x, double y, ScreenMetrics screen)
    {
        ArgumentNullException.ThrowIfNull(moving);
        if (held < 0 || held >= moving.Count || !screen.IsUsable)
        {
            return moving;
        }

        var delta = BlockGeometry.Move(moving, x - moving[held].X, y - moving[held].Y, screen);
        return [.. moving.Select(r => BlockGeometry.Apply(r, delta, screen))];
    }

    /// <summary>
    /// [S-11888] How many grid cells a rule's placement keeps clear of the work area's edges: one, as
    /// Explorer keeps its icons off the screen's edge. A user's own move may still go to the edge.
    /// </summary>
    public const int EdgeInset = 1;

    /// <summary>
    /// The grid lines a box of <paramref name="size"/> may start on with the whole box inside the allowed area
    /// less <see cref="EdgeInset"/> cells on every side ([S-11888]); the allowed area itself where the inset
    /// leaves no room; null when not even that holds the box.
    /// </summary>
    private static (int C0, int C1, int R0, int R1, int Cols, int Rows)? Range((double W, double H) size, ScreenMetrics screen)
    {
        if (!screen.IsUsable || !(size.W > 0) || !(size.H > 0))
        {
            return null;
        }

        var a = screen.Allowed;
        var (cols, rows) = GridGeometry.GridDims(screen);
        var c0 = (int)Math.Ceiling((a.X * cols) - 1e-6);
        var r0 = (int)Math.Ceiling((a.Y * rows) - 1e-6);
        var c1 = (int)Math.Floor(((a.Right - size.W) * cols) + 1e-6);
        var r1 = (int)Math.Floor(((a.Bottom - size.H) * rows) + 1e-6);
        if (c1 < c0 || r1 < r0)
        {
            return null;
        }

        return c1 - c0 >= 2 * EdgeInset && r1 - r0 >= 2 * EdgeInset
            ? (c0 + EdgeInset, c1 - EdgeInset, r0 + EdgeInset, r1 - EdgeInset, cols, rows)
            : (c0, c1, r0, r1, cols, rows);
    }

    /// <summary>The first of <paramref name="occupied"/> that overlaps <paramref name="rect"/>, or null.</summary>
    private static FracRect? Blocker(FracRect rect, IReadOnlyList<FracRect> occupied)
    {
        foreach (var other in occupied)
        {
            if (other.IsFinite
                && rect.X < other.Right - Epsilon && other.X < rect.Right - Epsilon
                && rect.Y < other.Bottom - Epsilon && other.Y < rect.Bottom - Epsilon)
            {
                return other;
            }
        }

        return null;
    }

    private static bool Same(FracRect a, FracRect b)
        => Math.Abs(a.X - b.X) < Epsilon && Math.Abs(a.Y - b.Y) < Epsilon && Math.Abs(a.W - b.W) < Epsilon && Math.Abs(a.H - b.H) < Epsilon;
}
