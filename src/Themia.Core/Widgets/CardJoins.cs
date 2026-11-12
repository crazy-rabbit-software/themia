using Themia.Core.Geometry;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-2241] What a card draws of its outline once its neighbours are known: which of its four corners
/// are round and which of its four border sides are drawn. Physical sides (left is the screen's
/// left); a card laid out right to left takes <see cref="Mirrored"/>.
/// </summary>
public readonly record struct CardOutline(
    bool RoundTopLeft,
    bool RoundTopRight,
    bool RoundBottomRight,
    bool RoundBottomLeft,
    bool DrawsLeft,
    bool DrawsTop,
    bool DrawsRight,
    bool DrawsBottom)
{
    /// <summary>A card joined to nothing: every corner round, the whole border drawn.</summary>
    public static CardOutline Free { get; } = new(true, true, true, true, true, true, true, true);

    /// <summary>The same outline seen from a right-to-left flow, where the start is the physical right.</summary>
    public CardOutline Mirrored() => new(
        RoundTopRight, RoundTopLeft, RoundBottomLeft, RoundBottomRight,
        DrawsRight, DrawsTop, DrawsLeft, DrawsBottom);

    /// <summary>
    /// [S-2243] The border's path for a card of <paramref name="width"/> × <paramref name="height"/>,
    /// a stroke of <paramref name="thickness"/> and a corner radius of <paramref name="radius"/>: the
    /// line runs half the stroke inside the card (as a shape strokes), a round corner's arc at the
    /// radius less that half, a square corner a point. A side not drawn breaks the path there, and the
    /// sides beside it run out to the card's edge, so they meet the neighbour's line with no notch.
    /// All four drawn is one closed figure; none drawn is none.
    /// </summary>
    public IReadOnlyList<OutlineFigure> Stroke(double width, double height, double thickness, double radius)
    {
        var t = double.IsFinite(thickness) && thickness > 0 ? thickness : 0;
        var half = t / 2;
        var x0 = half;
        var y0 = half;
        var x1 = Math.Max(x0, width - half);
        var y1 = Math.Max(y0, height - half);
        var r = double.IsFinite(radius) && radius > 0 ? radius : 0;
        var arc = Math.Min(Math.Max(0, r - half), Math.Min(x1 - x0, y1 - y0) / 2);

        // Clockwise: corner k (TL, TR, BR, BL) starts side k (top, right, bottom, left).
        bool[] round = [RoundTopLeft, RoundTopRight, RoundBottomRight, RoundBottomLeft];
        bool[] draws = [DrawsTop, DrawsRight, DrawsBottom, DrawsLeft];
        double A(int k) => round[k] ? arc : 0;

        // Where the side before corner k ends at it, and where the side after it starts.
        (double X, double Y) Entry(int k) => k switch
        {
            0 => (x0, y0 + A(0)),
            1 => (x1 - A(1), y0),
            2 => (x1, y1 - A(2)),
            _ => (x0 + A(3), y1),
        };
        (double X, double Y) Exit(int k) => k switch
        {
            0 => (x0 + A(0), y0),
            1 => (x1, y0 + A(1)),
            2 => (x1 - A(2), y1),
            _ => (x0, y1 - A(3)),
        };

        // An open end runs on to the card's edge along its side: a run starts by the corner its first
        // side leaves (OpenStart) and ends by the corner its last side reaches (OpenEnd).
        (double X, double Y) OpenStart(int k) => k switch
        {
            0 => (0, y0),
            1 => (x1, 0),
            2 => (width, y1),
            _ => (x0, height),
        };
        (double X, double Y) OpenEnd(int k) => k switch
        {
            0 => (x0, 0),
            1 => (width, y0),
            2 => (x1, height),
            _ => (0, y1),
        };

        if (t <= 0)
        {
            return [];
        }

        if (draws.All(d => d))
        {
            var (sx, sy) = Exit(0);
            var segments = new List<OutlineSegment>(8);
            for (var k = 0; k < 4; k++)
            {
                var next = (k + 1) % 4;
                var (ex, ey) = Entry(next);
                segments.Add(new OutlineSegment(ex, ey, 0));
                if (A(next) > 0)
                {
                    var (xx, xy) = Exit(next);
                    segments.Add(new OutlineSegment(xx, xy, A(next)));
                }
            }

            return [new OutlineFigure(sx, sy, segments, Closed: true)];
        }

        var figures = new List<OutlineFigure>(2);
        var gap = Array.IndexOf(draws, false);
        List<OutlineSegment>? run = null;
        (double X, double Y) start = default;

        for (var step = 1; step <= 4; step++)
        {
            var side = (gap + step) % 4;
            if (!draws[side])
            {
                if (run is not null)
                {
                    figures.Add(new OutlineFigure(start.X, start.Y, run, Closed: false));
                    run = null;
                }

                continue;
            }

            if (run is null)
            {
                run = new List<OutlineSegment>(6);
                start = OpenStart(side);
            }

            var next = (side + 1) % 4;
            if (draws[next])
            {
                var (ex, ey) = Entry(next);
                run.Add(new OutlineSegment(ex, ey, 0));
                if (A(next) > 0)
                {
                    var (xx, xy) = Exit(next);
                    run.Add(new OutlineSegment(xx, xy, A(next)));
                }
            }
            else
            {
                var (ox, oy) = OpenEnd(next);
                run.Add(new OutlineSegment(ox, oy, 0));
            }
        }

        if (run is not null)
        {
            figures.Add(new OutlineFigure(start.X, start.Y, run, Closed: false));
        }

        return figures;
    }
}

/// <summary>A piece of a border's path: a line to (X, Y), or with an arc radius a clockwise quarter arc to it.</summary>
public readonly record struct OutlineSegment(double X, double Y, double ArcRadius);

/// <summary>One unbroken run of a border's path, from (StartX, StartY).</summary>
public sealed record OutlineFigure(double StartX, double StartY, IReadOnlyList<OutlineSegment> Segments, bool Closed);

/// <summary>
/// [S-2240] [S-2241] [S-2242] Cards that touch become one surface. Two cards whose painted boxes meet
/// along a side are joined there: the corners a joined neighbour covers are square, and where two
/// cards join exactly one of them draws the line between. Pure: boxes in, an outline per card out,
/// so the desktop and the scene renderer agree on every case.
/// </summary>
public static class CardJoins
{
    /// <summary>[S-2240] The tolerance two edges meet within: half a device pixel, in DIPs.</summary>
    public static double Tolerance(double rasterScale)
        => 0.5 / (double.IsFinite(rasterScale) && rasterScale > 0 ? rasterScale : 1);

    private enum Side
    {
        Left,
        Top,
        Right,
        Bottom,
    }

    /// <summary>A join: <paramref name="First"/> is the upper or the left card, <paramref name="Second"/> the lower or the right.</summary>
    private readonly record struct Join(int First, int Second, bool Stacked)
    {
        public Side FirstSide => Stacked ? Side.Bottom : Side.Right;

        public Side SecondSide => Stacked ? Side.Top : Side.Left;
    }

    /// <summary>
    /// [S-2240]..[S-2242] The outline of every card, in the order given. A null box is a card that
    /// draws no card surface (a bare widget, a hidden one) or one being dragged: it is
    /// <see cref="CardOutline.Free"/>, and joins nothing.
    /// </summary>
    public static CardOutline[] Outlines(IReadOnlyList<PixelRect?> boxes, double tolerance)
    {
        ArgumentNullException.ThrowIfNull(boxes);
        var tol = double.IsFinite(tolerance) && tolerance > 0 ? tolerance : 0;
        var n = boxes.Count;
        var joins = new List<Join>();

        for (var i = 0; i < n; i++)
        {
            if (boxes[i] is not { } a)
            {
                continue;
            }

            for (var j = i + 1; j < n; j++)
            {
                if (boxes[j] is not { } b)
                {
                    continue;
                }

                var acrossX = Overlap(a.X, a.X + a.Width, b.X, b.X + b.Width) > tol;
                var acrossY = Overlap(a.Y, a.Y + a.Height, b.Y, b.Y + b.Height) > tol;

                if (acrossX && Meets(a.Y + a.Height, b.Y, tol))
                {
                    joins.Add(new Join(i, j, Stacked: true));
                }
                else if (acrossX && Meets(b.Y + b.Height, a.Y, tol))
                {
                    joins.Add(new Join(j, i, Stacked: true));
                }
                else if (acrossY && Meets(a.X + a.Width, b.X, tol))
                {
                    joins.Add(new Join(i, j, Stacked: false));
                }
                else if (acrossY && Meets(b.X + b.Width, a.X, tol))
                {
                    joins.Add(new Join(j, i, Stacked: false));
                }
            }
        }

        var outlines = new CardOutline[n];
        if (joins.Count == 0)
        {
            Array.Fill(outlines, CardOutline.Free);
            return outlines;
        }

        // The spans of joined neighbours along each side of each card, in the side's own axis.
        var spans = new List<(double From, double To)>[n, 4];
        void Add(int card, Side side, int other)
        {
            var o = boxes[other]!.Value;
            var span = side is Side.Top or Side.Bottom ? (o.X, o.X + o.Width) : (o.Y, o.Y + o.Height);
            (spans[card, (int)side] ??= []).Add(span);
        }

        foreach (var join in joins)
        {
            Add(join.First, join.FirstSide, join.Second);
            Add(join.Second, join.SecondSide, join.First);
        }

        // [S-2242] One line where two cards join: the lower or right card leaves out a side its
        // neighbours cover whole; else the upper or left one does, when every neighbour along that
        // side draws its own; else both draw (staggered).
        var dropped = new bool[n, 4];
        foreach (var join in joins)
        {
            if (Covered(boxes[join.Second]!.Value, join.SecondSide, spans[join.Second, (int)join.SecondSide], tol))
            {
                dropped[join.Second, (int)join.SecondSide] = true;
            }
        }

        foreach (var join in joins)
        {
            var side = join.FirstSide;
            if (dropped[join.First, (int)side]
                || !Covered(boxes[join.First]!.Value, side, spans[join.First, (int)side], tol))
            {
                continue;
            }

            var partnersDraw = joins.TrueForAll(other => other.First != join.First || other.FirstSide != side
                || !dropped[other.Second, (int)other.SecondSide]);
            if (partnersDraw)
            {
                dropped[join.First, (int)side] = true;
            }
        }

        for (var c = 0; c < n; c++)
        {
            if (boxes[c] is not { } box)
            {
                outlines[c] = CardOutline.Free;
                continue;
            }

            var left = box.X;
            var top = box.Y;
            var right = box.X + box.Width;
            var bottom = box.Y + box.Height;
            var l = spans[c, (int)Side.Left];
            var t = spans[c, (int)Side.Top];
            var r = spans[c, (int)Side.Right];
            var b = spans[c, (int)Side.Bottom];

            // [S-2241] A corner is square where a joined neighbour's span along either side reaches it.
            bool ReachesStart(List<(double From, double To)>? s, double at) => s is not null && s.Exists(x => x.From <= at + tol);
            bool ReachesEnd(List<(double From, double To)>? s, double at) => s is not null && s.Exists(x => x.To >= at - tol);

            outlines[c] = new CardOutline(
                RoundTopLeft: !(ReachesStart(t, left) || ReachesStart(l, top)),
                RoundTopRight: !(ReachesEnd(t, right) || ReachesStart(r, top)),
                RoundBottomRight: !(ReachesEnd(b, right) || ReachesEnd(r, bottom)),
                RoundBottomLeft: !(ReachesStart(b, left) || ReachesEnd(l, bottom)),
                DrawsLeft: !dropped[c, (int)Side.Left],
                DrawsTop: !dropped[c, (int)Side.Top],
                DrawsRight: !dropped[c, (int)Side.Right],
                DrawsBottom: !dropped[c, (int)Side.Bottom]);
        }

        return outlines;
    }

    private static double Overlap(double a0, double a1, double b0, double b1) => Math.Min(a1, b1) - Math.Max(a0, b0);

    private static bool Meets(double edge, double opposite, double tol) => Math.Abs(edge - opposite) <= tol;

    /// <summary>Whether <paramref name="spans"/> together cover the whole of the card's side, within the tolerance.</summary>
    private static bool Covered(PixelRect box, Side side, List<(double From, double To)>? spans, double tol)
    {
        if (spans is null || spans.Count == 0)
        {
            return false;
        }

        var (from, to) = side is Side.Top or Side.Bottom ? (box.X, box.X + box.Width) : (box.Y, box.Y + box.Height);
        var reached = from + tol;
        foreach (var (a, b) in spans.OrderBy(s => s.From))
        {
            if (a > reached)
            {
                return false;
            }

            reached = Math.Max(reached, b + tol);
            if (reached >= to)
            {
                return true;
            }
        }

        return reached >= to;
    }
}
