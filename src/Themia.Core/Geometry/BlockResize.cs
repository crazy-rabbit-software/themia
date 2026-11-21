using Themia.Core.Shell;

namespace Themia.Core.Geometry;

/// <summary>
/// [S-2460]..[S-2465] Several selected widgets resized as one block (owner, 2026-11-20: "add the possibility to resize
/// multiple widgets with each other. they should still snap to the grid of course. this means in some cases you need to
/// work with common denominators and stuff"). The selection's frame dragged by an edge or a corner scales every member
/// about the opposite edge or corner, positions and sizes alike, and settles them so every edge stands on a grid line.
/// <list type="bullet">
/// <item>Each axis the handle moves is laid on its own (<see cref="Axis"/>). The members' distinct edges, measured in
/// whole cells from the anchored edge, are scaled by the block's new length over its old, each to its nearest line. A
/// member takes the size its own sizing gives the room between its two edges (<see cref="AxisLimits.Range"/>: its Min,
/// Max and steps), and an edge a member ends on stands where that member's size puts it, so members that touch stay
/// touching. Edges keep their order, and two that were apart stay at least a cell apart, so members that neither
/// touched nor overlapped never come to.</item>
/// <item>The block's length moves on its common step (<see cref="Axis.Step"/>): the fewest whole cells at which every
/// edge scales to a whole cell exactly (the block's length over the greatest common divisor of the edges' distances),
/// when that is at most <see cref="MostCommonStep"/> cells; so two equal widgets side by side grow a cell each, together.
/// Otherwise cell by cell, each edge on its nearest line.</item>
/// <item>The block stops at the last length at which every member stays inside its sizing (one member at its Min or Max
/// stops them all), stays in reach as a single widget does (over any edge, a taskbar's as the monitor's, by all but the
/// visible minimum, [S-6608]): on the common step where one was passed, else (a limit nearer than one step) that last
/// length itself. It may come over a widget that stays: nothing keeps widgets apart ([S-2465] retired with [S-11959]).</item>
/// <item>A member fixed on an axis (a desktop icon) keeps its size there and moves with its nearer edge; an axis every
/// member is fixed on does not resize (<see cref="Free"/>).</item>
/// </list>
/// Pure: the frame's drag, its preview and the keys share it.
/// </summary>
public static class BlockResize
{
    /// <summary>[S-2461] The largest common step, in grid cells, the block's length moves by; past it, cell by cell.</summary>
    public const int MostCommonStep = 8;

    /// <summary>How far a size may be off a whole cell and still count as on it (float noise).</summary>
    private const double Eps = 1e-6;

    /// <summary>
    /// [S-2460] The rects a drag of the block's <paramref name="handle"/> by (<paramref name="dxFrac"/>,
    /// <paramref name="dyFrac"/>) of the screen leaves, from <paramref name="start"/>; each member held to its own
    /// <paramref name="limits"/> (null: none known). A handle on an axis no member may resize leaves that axis.
    /// </summary>
    public static IReadOnlyList<FracRect> Resize(IReadOnlyList<FracRect> start, IReadOnlyList<SizeLimits?> limits, ResizeHandle handle, double dxFrac, double dyFrac, ScreenMetrics screen)
    {
        ArgumentNullException.ThrowIfNull(start);
        ArgumentNullException.ThrowIfNull(limits);
        if (!Workable(start, screen) || !double.IsFinite(dxFrac) || !double.IsFinite(dyFrac))
        {
            return [.. start];
        }

        var (freeW, freeH) = Free(limits, start.Count);
        var horizontal = freeW && (GridGeometry.MovesLeft(handle) || GridGeometry.MovesRight(handle));
        var vertical = freeH && (GridGeometry.MovesTop(handle) || GridGeometry.MovesBottom(handle));
        var x = horizontal ? Axis.Of(start, limits, true, GridGeometry.MovesLeft(handle), screen) : null;
        var y = vertical ? Axis.Of(start, limits, false, GridGeometry.MovesTop(handle), screen) : null;
        var wantX = x is null ? 0 : x.Length + ((GridGeometry.MovesLeft(handle) ? -dxFrac : dxFrac) * x.Lines);
        var wantY = y is null ? 0 : y.Length + ((GridGeometry.MovesTop(handle) ? -dyFrac : dyFrac) * y.Lines);
        return Solve(start, x, wantX, y, wantY, untilChanged: false);
    }

    /// <summary>
    /// [S-2464] One Shift+arrow press on a selection of several: the block resized by its right or bottom edge (Right and
    /// Down grow, Left and Up shrink), its top-left corner put, by one step: its common step, at least one cell (ten with
    /// Ctrl, on the common step); a press that leaves every member as it was (a list between two of its rows) goes on to
    /// the next length that changes one. It stops where a drag would.
    /// </summary>
    public static IReadOnlyList<FracRect> Nudge(IReadOnlyList<FracRect> start, IReadOnlyList<SizeLimits?> limits, NudgeDirection direction, NudgeStep step, ScreenMetrics screen)
    {
        ArgumentNullException.ThrowIfNull(start);
        ArgumentNullException.ThrowIfNull(limits);
        if (!Workable(start, screen))
        {
            return [.. start];
        }

        var across = direction is NudgeDirection.Left or NudgeDirection.Right;
        var grow = direction is NudgeDirection.Right or NudgeDirection.Down;
        var (freeW, freeH) = Free(limits, start.Count);
        if (across ? !freeW : !freeH)
        {
            return [.. start];
        }

        var axis = Axis.Of(start, limits, across, fromEnd: false, screen);
        var cells = step == NudgeStep.Far ? KeyboardNudgeDefaults.FarCells : 1;
        var press = Math.Ceiling(cells / (double)axis.Step) * axis.Step;
        var want = axis.Length + ((grow ? 1 : -1) * press);
        var moved = across ? Solve(start, axis, want, null, 0, untilChanged: false) : Solve(start, null, 0, axis, want, untilChanged: false);
        if (!Same(moved, start))
        {
            return moved;
        }

        // A press that changed nothing (a member's next whole cell not yet reached): on to the first length that does.
        var far = grow ? double.PositiveInfinity : double.NegativeInfinity;
        return across ? Solve(start, axis, far, null, 0, untilChanged: true) : Solve(start, null, 0, axis, far, untilChanged: true);
    }

    /// <summary>
    /// [S-2460] Which axes the block resizes on: one that a member may change its size on (no limits known, or not fixed
    /// there). On an axis every member is fixed on, the frame has no handle, as a single card's fixed axis has none ([S-2444]).
    /// </summary>
    public static (bool Width, bool Height) Free(IReadOnlyList<SizeLimits?> limits, int count)
    {
        ArgumentNullException.ThrowIfNull(limits);
        bool width = false, height = false;
        for (var i = 0; i < count; i++)
        {
            var l = i < limits.Count ? limits[i] : null;
            width |= l is not { Width.Fixed: true };
            height |= l is not { Height.Fixed: true };
        }

        return (width, height);
    }

    /// <summary>
    /// [S-2461] The block's common step on one axis, in grid cells, for the rects as they stand: the fewest whole cells
    /// its length can change by with every member's edge on a whole cell; 1 where that is more than
    /// <see cref="MostCommonStep"/> or an edge is off the whole cells.
    /// </summary>
    public static int CommonStep(IReadOnlyList<FracRect> rects, bool horizontal, ScreenMetrics screen)
    {
        ArgumentNullException.ThrowIfNull(rects);
        return Workable(rects, screen) ? Axis.Of(rects, [], horizontal, fromEnd: false, screen).Step : 1;
    }

    private static bool Workable(IReadOnlyList<FracRect> rects, ScreenMetrics screen)
        => rects.Count > 0 && screen.IsUsable && rects.All(r => r.IsFinite && r.W > 0 && r.H > 0);

    private static bool Same(IReadOnlyList<FracRect> a, IReadOnlyList<FracRect> b)
    {
        for (var i = 0; i < a.Count; i++)
        {
            if (Math.Abs(a[i].X - b[i].X) > 1e-9 || Math.Abs(a[i].Y - b[i].Y) > 1e-9 || Math.Abs(a[i].W - b[i].W) > 1e-9 || Math.Abs(a[i].H - b[i].H) > 1e-9)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Both axes laid, each on its own.</summary>
    private static IReadOnlyList<FracRect> Solve(IReadOnlyList<FracRect> start, Axis? x, double wantX, Axis? y, double wantY, bool untilChanged)
        => Compose(start, x?.Walk(wantX, untilChanged), y?.Walk(wantY, untilChanged));

    private static List<FracRect> Compose(IReadOnlyList<FracRect> start, Laid? x, Laid? y)
    {
        var rects = new List<FracRect>(start.Count);
        for (var i = 0; i < start.Count; i++)
        {
            var r = start[i];
            if (x is not null)
            {
                r = r with { X = x.Spans[i].Start, W = x.Spans[i].Size };
            }

            if (y is not null)
            {
                r = r with { Y = y.Spans[i].Start, H = y.Spans[i].Size };
            }

            rects.Add(r);
        }

        return rects;
    }

    /// <summary>One member on one axis, in fractions of the screen.</summary>
    private readonly record struct Span(double Start, double Size);

    /// <summary>An axis laid at a length (in cells): every member's span.</summary>
    private sealed record Laid(double Length, Span[] Spans);

    /// <summary>
    /// [S-2460] One axis of the block: its members' distinct edges as distances in cells from the anchored edge (the
    /// block's start, or its end where the handle moves the start), which member ends where, and each member's sizing in
    /// cells.
    /// </summary>
    private sealed class Axis
    {
        private readonly double _lo;
        private readonly double _hi;
        private readonly bool _fromEnd;
        private readonly double[] _d;
        private readonly int[] _near;
        private readonly int[] _far;
        private readonly List<int>[] _endsAt;
        private readonly AxisLimits?[] _limits;
        private readonly double[] _size;
        private readonly double[] _least;
        private readonly double[] _most;
        private readonly bool[] _fixed;
        private readonly bool[] _reachedAtStart;
        private readonly ScreenMetrics _screen;
        private readonly bool _horizontal;

        private Axis(double lo, double hi, bool fromEnd, int lines, double[] d, int[] near, int[] far, AxisLimits?[] limits, double[] size, ScreenMetrics screen, bool horizontal)
        {
            (_lo, _hi, _fromEnd, Lines, _d, _near, _far, _limits, _size, _screen, _horizontal) = (lo, hi, fromEnd, lines, d, near, far, limits, size, screen, horizontal);
            var a = screen.Allowed;
            var (areaLo, areaHi) = horizontal ? (a.X, a.Right) : (a.Y, a.Bottom);
            Length = d[^1];
            _endsAt = [.. d.Select(_ => new List<int>())];
            _least = new double[near.Length];
            _most = new double[near.Length];
            _fixed = new bool[near.Length];
            _reachedAtStart = new bool[near.Length];
            var area = (areaHi - areaLo) * lines;
            for (var i = 0; i < near.Length; i++)
            {
                _endsAt[far[i]].Add(i);
                _fixed[i] = limits[i] is { Fixed: true };

                // A member already outside its sizing (a hand edit) may stay where it is, never go further out.
                _least[i] = Math.Min(size[i], limits[i] is { } l ? l.Min * lines : 1);
                _most[i] = Math.Max(size[i], limits[i] is { } m ? Math.Min(m.Max * lines, area) : area);
                _reachedAtStart[i] = Reached(Position(d[near[i]], i, size[i]));
            }

            Step = CommonStep();
        }

        /// <summary>The grid's lines across the screen on this axis: a cell is <c>1 / Lines</c>.</summary>
        public int Lines { get; }

        /// <summary>The block's length now, in cells.</summary>
        public double Length { get; }

        /// <summary>[S-2461] The common step, in cells: 1 where none is fine enough.</summary>
        public int Step { get; }

        public static Axis Of(IReadOnlyList<FracRect> rects, IReadOnlyList<SizeLimits?> limits, bool horizontal, bool fromEnd, ScreenMetrics screen)
        {
            var (cols, rows) = GridGeometry.GridDims(screen);
            var lines = horizontal ? cols : rows;
            var spans = rects.Select(r => horizontal ? (Start: r.X, End: r.Right) : (Start: r.Y, End: r.Bottom)).ToArray();
            var lo = spans.Min(s => s.Start);
            var hi = spans.Max(s => s.End);
            double Distance(double edge) => Math.Round((fromEnd ? hi - edge : edge - lo) * lines, 6);

            var d = spans.SelectMany(s => new[] { Distance(s.Start), Distance(s.End) }).Distinct().Order().ToArray();
            int Index(double edge) => Array.IndexOf(d, Distance(edge));
            var near = spans.Select(s => Index(fromEnd ? s.End : s.Start)).ToArray();
            var far = spans.Select(s => Index(fromEnd ? s.Start : s.End)).ToArray();
            AxisLimits? Own(int i) => i < limits.Count && limits[i] is { } l ? (horizontal ? l.Width : l.Height) : null;
            var own = rects.Select((_, i) => Own(i)).ToArray();
            // In whole cells where it is on them: a fixed member keeps this size, and float noise in it would land the member
            // off the size its sizing paints.
            var size = spans.Select(s => Whole((s.End - s.Start) * lines)).ToArray();

            return new Axis(lo, hi, fromEnd, lines, d, near, far, own, size, screen, horizontal);
        }

        /// <summary>
        /// [S-2460] The block's length nearest <paramref name="want"/> cells that it may take, walked from its length now:
        /// on the common step, or the last length allowed where it stops first; null when it does not change. With
        /// <paramref name="untilChanged"/>, the first length (towards <paramref name="want"/>) that changes a member.
        /// </summary>
        public Laid? Walk(double want, bool untilChanged)
        {
            var n = Length;
            if (!double.IsFinite(n) || n <= 0 || double.IsNaN(want) || Math.Abs(want - n) < Eps)
            {
                return null;
            }

            var dir = want > n ? 1 : -1;
            var exact = Step > 1 ? n + (Math.Round((want - n) / Step, MidpointRounding.AwayFromZero) * Step) : double.NaN;
            var goal = untilChanged ? (dir > 0 ? 4.0 * Lines : 1) : Math.Round(Clamp(want), MidpointRounding.AwayFromZero);
            if (!double.IsNaN(exact) && !untilChanged)
            {
                goal = dir > 0 ? Math.Max(goal, exact) : Math.Min(goal, exact);
            }

            var first = dir > 0 ? Math.Floor(n + Eps) + 1 : Math.Ceiling(n - Eps) - 1;
            Laid? last = null;
            Laid? lastExact = null;
            var stopped = false;
            for (var c = first; dir > 0 ? c <= goal + Eps : c >= goal - Eps; c += dir)
            {
                var laid = c >= 1 && c <= 4 * Lines ? Lay(c) : null;
                if (laid is null)
                {
                    stopped = true;
                    break;
                }

                if (untilChanged && Changes(laid))
                {
                    return laid;
                }

                last = laid;
                if (Step > 1 && Math.Abs(Math.IEEERemainder(c - n, Step)) < Eps)
                {
                    lastExact = laid;
                }

                if (!double.IsNaN(exact) && !untilChanged && Math.Abs(c - exact) < Eps)
                {
                    break;
                }
            }

            if (untilChanged)
            {
                return null;
            }

            // [S-2462] A stop: the last length on the common step before it, else (a limit nearer than one step) the last
            // length allowed.
            if (stopped || double.IsNaN(exact))
            {
                return lastExact ?? last;
            }

            return Math.Abs(exact - n) < Eps ? null : last;
        }

        private double Clamp(double want) => Math.Clamp(want, -4.0 * Lines, 8.0 * Lines);

        /// <summary>Whether a layout moved any member from where it started.</summary>
        private bool Changes(Laid laid)
        {
            for (var i = 0; i < _near.Length; i++)
            {
                var was = Position(_d[_near[i]], i, _size[i]);
                if (Math.Abs(was.Start - laid.Spans[i].Start) > 1e-9 || Math.Abs(was.Size - laid.Spans[i].Size) > 1e-9)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// [S-2460] The members laid for a block <paramref name="length"/> cells long; null where a member would leave its
        /// sizing or the visible minimum ([S-6608]).
        /// </summary>
        private Laid? Lay(double length)
        {
            var scale = length / Length;
            var t = new double[_d.Length];
            for (var j = 1; j < _d.Length; j++)
            {
                var target = Math.Round(_d[j] * scale, MidpointRounding.AwayFromZero);
                double rigid = double.NegativeInfinity, free = double.NegativeInfinity, least = double.NegativeInfinity;
                foreach (var i in _endsAt[j])
                {
                    var near = t[_near[i]];
                    var room = target - near;

                    // [S-2462] A member at its Min or Max stops the whole block.
                    if (!_fixed[i] && (room < _least[i] - Eps || room > _most[i] + Eps))
                    {
                        return null;
                    }

                    // [S-2462] A member that only takes one size (fixed) decides where an edge it shares lands;
                    // the free ones there take the room it leaves them, so they stay joined to it.
                    var at = near + Want(i, room);
                    if (Rigid(i))
                    {
                        rigid = Math.Max(rigid, at);
                    }
                    else
                    {
                        free = Math.Max(free, at);
                        least = Math.Max(least, near + Want(i, 0));
                    }
                }

                var edge = !double.IsNegativeInfinity(rigid) ? Math.Max(rigid, least) : !double.IsNegativeInfinity(free) ? free : target;

                // [S-2463] Edges keep their order, and edges that were apart a cell between them at least.
                t[j] = Math.Max(edge, t[j - 1] + Math.Min(1, _d[j] - _d[j - 1]));
            }

            var spans = new Span[_near.Length];
            for (var i = 0; i < spans.Length; i++)
            {
                var room = t[_far[i]] - t[_near[i]];
                var size = Fit(i, room);
                spans[i] = Position(t[_near[i]], i, size);

                // [S-2462] A free member a rigid neighbour left more room than its Max stops the block too.
                if ((_reachedAtStart[i] && !Reached(spans[i])) || size > _most[i] + Eps || (!Rigid(i) && room > _most[i] + Eps))
                {
                    return null;
                }
            }

            return new Laid(length, spans);
        }

        /// <summary>A member that takes only one size on this axis: fixed ([S-14500]: no member steps).</summary>
        private bool Rigid(int i) => _fixed[i];

        /// <summary>The size a member takes in <paramref name="room"/> cells: its sizing's (its own size on a fixed axis).</summary>
        private double Want(int i, double room)
        {
            if (_fixed[i])
            {
                return _size[i];
            }

            if (_limits[i] is not { } l)
            {
                return Math.Max(1, room);
            }

            return Whole(l.Range(Math.Max(0, room) / Lines) * Lines);
        }

        /// <summary>
        /// The size a member ends at with <paramref name="room"/> cells between its edges: <see cref="Want"/>, never more
        /// than the room.
        /// </summary>
        private double Fit(int i, double room)
        {
            var want = Want(i, room);
            return _fixed[i] || want <= room + Eps ? want : room;
        }

        private static double Whole(double cells) => Math.Abs(cells - Math.Round(cells)) < Eps ? Math.Round(cells) : cells;

        /// <summary>A member's span with its near edge <paramref name="near"/> cells from the anchor, <paramref name="size"/> cells long.</summary>
        private Span Position(double near, int i, double size)
        {
            if (!_fromEnd)
            {
                return new Span(_lo + (near / Lines), size / Lines);
            }

            var end = _hi - (near / Lines);
            return new Span(end - (size / Lines), size / Lines);
        }

        /// <summary>
        /// [S-6608] The span is in reach as a single widget's gesture keeps it (<see cref="GridGeometry.Reach"/>):
        /// over any edge of the view's allowed area, a taskbar's as the monitor's, by all but the visible minimum.
        /// </summary>
        private bool Reached(Span span)
        {
            var (loX, hiX, loY, hiY) = GridGeometry.Reach(_screen, span.Size, span.Size);
            var (lo, hi) = _horizontal ? (loX, hiX) : (loY, hiY);
            return span.Start >= lo - 1e-9 && span.Start <= hi + 1e-9;
        }

        /// <summary>
        /// [S-2461] The fewest whole cells the block's length can change by with every edge on a whole cell: its length over
        /// the greatest common divisor of the edges' distances from the anchor. 1 when an edge is off the whole cells or
        /// the step would be more than <see cref="MostCommonStep"/>.
        /// </summary>
        private int CommonStep()
        {
            if (_d.Any(d => Math.Abs(d - Math.Round(d)) > Eps))
            {
                return 1;
            }

            long g = 0;
            foreach (var d in _d)
            {
                g = Gcd(g, (long)Math.Round(d));
            }

            var n = (long)Math.Round(Length);
            if (g <= 0 || n <= 0)
            {
                return 1;
            }

            var step = n / g;
            return step >= 1 && step <= MostCommonStep ? (int)step : 1;
        }

        private static long Gcd(long a, long b)
        {
            while (b != 0)
            {
                (a, b) = (b, a % b);
            }

            return Math.Abs(a);
        }
    }
}
