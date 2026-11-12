using Themia.Core.Geometry;

namespace Themia.Core.Widgets;

/// <summary>[S-2241] A card's side, physical: left is the screen's left.</summary>
public enum CardSide
{
    Left,
    Top,
    Right,
    Bottom,
}

/// <summary>
/// [S-11870] A stretch of a side the card leaves without a line because a member of its own group lies
/// across it: <paramref name="From"/> and <paramref name="To"/> are fractions of the side's length
/// from its physical start (the left end of the top and the bottom, the top end of the left and the
/// right), so the outline needs no size and mirrors without one.
/// </summary>
public readonly record struct OutlineSeam(CardSide Side, double From, double To);

/// <summary>
/// [S-2241] What a card draws of its outline once its neighbours are known: which of its four corners
/// are round and which of its four border sides are drawn. Physical sides (left is the screen's
/// left); a card laid out right to left takes <see cref="Mirrored"/>. [S-11870] A side drawn may still
/// leave out the stretches in <see cref="Seams"/>, where a member of the card's own group lies across.
/// </summary>
public readonly record struct CardOutline(
    bool RoundTopLeft,
    bool RoundTopRight,
    bool RoundBottomRight,
    bool RoundBottomLeft,
    bool DrawsLeft,
    bool DrawsTop,
    bool DrawsRight,
    bool DrawsBottom,
    IReadOnlyList<OutlineSeam>? Seams = null)
{
    /// <summary>Lengths closer than this are equal: a seam is a fraction times a side.</summary>
    private const double Epsilon = 1e-6;

    /// <summary>A card joined to nothing: every corner round, the whole border drawn.</summary>
    public static CardOutline Free { get; } = new(true, true, true, true, true, true, true, true);

    /// <summary>The seams, none when the outline has none.</summary>
    private IReadOnlyList<OutlineSeam> SeamList => Seams ?? [];

    /// <summary>The same outline seen from a right-to-left flow, where the start is the physical right.</summary>
    public CardOutline Mirrored() => new(
        RoundTopRight, RoundTopLeft, RoundBottomLeft, RoundBottomRight,
        DrawsRight, DrawsTop, DrawsLeft, DrawsBottom,
        Seams is null ? null : [.. Seams.Select(s => s.Side switch
        {
            CardSide.Left => s with { Side = CardSide.Right },
            CardSide.Right => s with { Side = CardSide.Left },
            _ => s with { From = 1 - s.To, To = 1 - s.From },
        })]);

    /// <summary>Two outlines are the same when they round, draw and leave out the same; the seams by value.</summary>
    public bool Equals(CardOutline other)
        => RoundTopLeft == other.RoundTopLeft && RoundTopRight == other.RoundTopRight
        && RoundBottomRight == other.RoundBottomRight && RoundBottomLeft == other.RoundBottomLeft
        && DrawsLeft == other.DrawsLeft && DrawsTop == other.DrawsTop
        && DrawsRight == other.DrawsRight && DrawsBottom == other.DrawsBottom
        && SeamList.SequenceEqual(other.SeamList);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = HashCode.Combine(RoundTopLeft, RoundTopRight, RoundBottomRight, RoundBottomLeft, DrawsLeft, DrawsTop, DrawsRight, DrawsBottom);
        foreach (var seam in SeamList)
        {
            hash = HashCode.Combine(hash, seam);
        }

        return hash;
    }

    /// <summary>
    /// [S-2243] The border's path for a card of <paramref name="width"/> × <paramref name="height"/>,
    /// a stroke of <paramref name="thickness"/> and a corner radius of <paramref name="radius"/>: the
    /// line runs half the stroke inside the card (as a shape strokes), a round corner's arc at the
    /// radius less that half, a square corner a point. A side not drawn breaks the path there, and the
    /// sides beside it run out to the card's edge, so they meet the neighbour's line with no notch.
    /// All four drawn whole is one closed figure; none drawn is none.
    /// <para>
    /// [S-11871] A seam breaks its side into runs. A run that stops at a seam runs on into it by the
    /// stroke, under the end of the line the group mate across draws down its own side to that edge,
    /// so the two meet with no notch.
    /// </para>
    /// </summary>
    public IReadOnlyList<OutlineFigure> Stroke(double width, double height, double thickness, double radius)
    {
        var t = double.IsFinite(thickness) && thickness > 0 ? thickness : 0;
        if (t <= 0)
        {
            return [];
        }

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
        CardSide[] sides = [CardSide.Top, CardSide.Right, CardSide.Bottom, CardSide.Left];
        double A(int k) => round[k] ? arc : 0;
        double Length(int k) => k % 2 == 0 ? width : height;

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

        // A point of side k's line at a clockwise distance d from the corner it starts at.
        (double X, double Y) At(int k, double d) => k switch
        {
            0 => (d, y0),
            1 => (x1, d),
            2 => (width - d, y1),
            _ => (x0, height - d),
        };

        // [S-11871] The runs each side draws, clockwise along it: the whole side less its seams.
        var runs = new List<(double From, double To)>[4];
        for (var k = 0; k < 4; k++)
        {
            runs[k] = [];
            if (!draws[k])
            {
                continue;
            }

            var length = Length(k);
            var side = sides[k];
            var cuts = SeamList.Where(s => s.Side == side)
                .Select(s => k < 2 ? (From: s.From * length, To: s.To * length) : (From: (1 - s.To) * length, To: (1 - s.From) * length))
                .OrderBy(c => c.From);
            var at = 0.0;
            foreach (var (from, to) in cuts)
            {
                if (from - at > Epsilon)
                {
                    runs[k].Add((at, from));
                }

                at = Math.Max(at, to);
            }

            if (length - at > Epsilon)
            {
                runs[k].Add((at, length));
            }
        }

        bool StartsAtCorner(int k) => runs[k].Count > 0 && runs[k][0].From <= Epsilon;
        bool EndsAtCorner(int k) => runs[k].Count > 0 && runs[k][^1].To >= Length(k) - Epsilon;

        // Corner k turns from the run that ends side k - 1 into the run that starts side k.
        bool Turns(int corner) => EndsAtCorner((corner + 3) % 4) && StartsAtCorner(corner);

        if (Enumerable.Range(0, 4).All(k => runs[k].Count == 1 && Turns(k)))
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
        for (var k = 0; k < 4; k++)
        {
            for (var i = 0; i < runs[k].Count; i++)
            {
                // A figure starts at a run no corner turns into, and follows the runs the corners turn
                // into until one stops: at a seam (on into it by the stroke) or where a side is not drawn.
                var from = runs[k][i].From;
                if (i == 0 && from <= Epsilon && Turns(k))
                {
                    continue;
                }

                var start = from <= Epsilon ? OpenStart(k) : At(k, Math.Max(0, from - t));
                var segments = new List<OutlineSegment>(6);
                var (side, index) = (k, i);
                while (true)
                {
                    var to = runs[side][index].To;
                    if (to < Length(side) - Epsilon)
                    {
                        var (px, py) = At(side, Math.Min(Length(side), to + t));
                        segments.Add(new OutlineSegment(px, py, 0));
                        break;
                    }

                    var next = (side + 1) % 4;
                    if (!Turns(next))
                    {
                        var (ox, oy) = OpenEnd(next);
                        segments.Add(new OutlineSegment(ox, oy, 0));
                        break;
                    }

                    var (ex, ey) = Entry(next);
                    segments.Add(new OutlineSegment(ex, ey, 0));
                    if (A(next) > 0)
                    {
                        var (xx, xy) = Exit(next);
                        segments.Add(new OutlineSegment(xx, xy, A(next)));
                    }

                    (side, index) = (next, 0);
                }

                figures.Add(new OutlineFigure(start.X, start.Y, segments, Closed: false));
            }
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
/// cards join exactly one of them draws the line between. [S-11870] Between two members of one group
/// neither draws it. Pure: boxes in, an outline per card out, so the desktop and the scene renderer
/// agree on every case.
/// </summary>
public static class CardJoins
{
    /// <summary>[S-2240] The tolerance two edges meet within: half a device pixel, in DIPs.</summary>
    public static double Tolerance(double rasterScale)
        => 0.5 / (double.IsFinite(rasterScale) && rasterScale > 0 ? rasterScale : 1);

    /// <summary>A join: <paramref name="First"/> is the upper or the left card, <paramref name="Second"/> the lower or the right.</summary>
    private readonly record struct Join(int First, int Second, bool Stacked)
    {
        public CardSide FirstSide => Stacked ? CardSide.Bottom : CardSide.Right;

        public CardSide SecondSide => Stacked ? CardSide.Top : CardSide.Left;
    }

    /// <summary>
    /// [S-2240]..[S-2242] The outline of every card, in the order given. A null box is a card that
    /// draws no card surface (a bare widget, a hidden one) or one being dragged: it is
    /// <see cref="CardOutline.Free"/>, and joins nothing.
    /// <para>
    /// [S-2457] <paramref name="sets"/>, when given, says which set each card is in: two cards join only
    /// within one set. The cards a drag carries are a set of their own, so a dragged group (or any
    /// selection dragged together) stays one panel while it moves, free of the cards standing still.
    /// </para>
    /// <para>
    /// [S-11870] <paramref name="groups"/>, when given, says which group each card is a member of (null:
    /// none). Where two joined cards are of one group neither draws the line between: a side wholly
    /// across members of its own group is not drawn, a side partly across them leaves those stretches
    /// out (<see cref="CardOutline.Seams"/>). Every other join keeps its one line ([S-2242]).
    /// </para>
    /// </summary>
    public static CardOutline[] Outlines(IReadOnlyList<PixelRect?> boxes, double tolerance, IReadOnlyList<int>? sets = null, IReadOnlyList<string?>? groups = null)
    {
        ArgumentNullException.ThrowIfNull(boxes);
        if (sets is not null && sets.Count != boxes.Count)
        {
            throw new ArgumentException("one set per box", nameof(sets));
        }

        if (groups is not null && groups.Count != boxes.Count)
        {
            throw new ArgumentException("one group per box", nameof(groups));
        }

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
                if (boxes[j] is not { } b || (sets is not null && sets[i] != sets[j]))
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

        bool Mates(int a, int b) => groups is not null && groups[a] is { } g && string.Equals(g, groups[b], StringComparison.Ordinal);

        // The spans of joined neighbours along each side of each card, in the side's own axis; and of
        // those, the spans of the card's own group mates ([S-11870]).
        var spans = new List<(double From, double To)>[n, 4];
        var mateSpans = new List<(double From, double To)>[n, 4];
        void Add(int card, CardSide side, int other)
        {
            var o = boxes[other]!.Value;
            var span = side is CardSide.Top or CardSide.Bottom ? (o.X, o.X + o.Width) : (o.Y, o.Y + o.Height);
            (spans[card, (int)side] ??= []).Add(span);
            if (Mates(card, other))
            {
                (mateSpans[card, (int)side] ??= []).Add(span);
            }
        }

        foreach (var join in joins)
        {
            Add(join.First, join.FirstSide, join.Second);
            Add(join.Second, join.SecondSide, join.First);
        }

        // [S-2242] One line where two cards join: the lower or right card leaves out a side its
        // neighbours cover whole; else the upper or left one does, when every neighbour along that
        // side draws its own; else both draw (staggered). [S-11870] Group mates count as neighbours
        // here; the stretches across them are then left out of whatever side is drawn.
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
            var l = spans[c, (int)CardSide.Left];
            var t = spans[c, (int)CardSide.Top];
            var r = spans[c, (int)CardSide.Right];
            var b = spans[c, (int)CardSide.Bottom];

            // [S-2241] A corner is square where a joined neighbour's span along either side reaches it.
            bool ReachesStart(List<(double From, double To)>? s, double at) => s is not null && s.Exists(x => x.From <= at + tol);
            bool ReachesEnd(List<(double From, double To)>? s, double at) => s is not null && s.Exists(x => x.To >= at - tol);

            // [S-11870] A side drawn leaves out what lies across its group mates; wholly across them it is not drawn.
            var seams = new List<OutlineSeam>();
            bool Draws(CardSide side)
            {
                if (dropped[c, (int)side])
                {
                    return false;
                }

                var mates = mateSpans[c, (int)side];
                if (mates is null)
                {
                    return true;
                }

                if (Covered(box, side, mates, tol))
                {
                    return false;
                }

                seams.AddRange(Seams(box, side, mates, tol));
                return true;
            }

            var drawsLeft = Draws(CardSide.Left);
            var drawsTop = Draws(CardSide.Top);
            var drawsRight = Draws(CardSide.Right);
            var drawsBottom = Draws(CardSide.Bottom);

            outlines[c] = new CardOutline(
                RoundTopLeft: !(ReachesStart(t, left) || ReachesStart(l, top)),
                RoundTopRight: !(ReachesEnd(t, right) || ReachesStart(r, top)),
                RoundBottomRight: !(ReachesEnd(b, right) || ReachesEnd(r, bottom)),
                RoundBottomLeft: !(ReachesStart(b, left) || ReachesEnd(l, bottom)),
                DrawsLeft: drawsLeft,
                DrawsTop: drawsTop,
                DrawsRight: drawsRight,
                DrawsBottom: drawsBottom,
                Seams: seams.Count > 0 ? seams : null);
        }

        return outlines;
    }

    private static double Overlap(double a0, double a1, double b0, double b1) => Math.Min(a1, b1) - Math.Max(a0, b0);

    private static bool Meets(double edge, double opposite, double tol) => Math.Abs(edge - opposite) <= tol;

    /// <summary>The card's side as a stretch of its own axis.</summary>
    private static (double From, double To) Extent(PixelRect box, CardSide side)
        => side is CardSide.Top or CardSide.Bottom ? (box.X, box.X + box.Width) : (box.Y, box.Y + box.Height);

    /// <summary>Whether <paramref name="spans"/> together cover the whole of the card's side, within the tolerance.</summary>
    private static bool Covered(PixelRect box, CardSide side, List<(double From, double To)>? spans, double tol)
    {
        if (spans is null || spans.Count == 0)
        {
            return false;
        }

        var (from, to) = Extent(box, side);
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

    /// <summary>
    /// [S-11870] The stretches of the card's side across <paramref name="spans"/>, merged, as fractions
    /// of the side; an end within the tolerance of the side's own end is that end.
    /// </summary>
    private static IEnumerable<OutlineSeam> Seams(PixelRect box, CardSide side, List<(double From, double To)> spans, double tol)
    {
        var (from, to) = Extent(box, side);
        var length = to - from;
        if (length <= 0)
        {
            yield break;
        }

        double Fraction(double at) => at <= from + tol ? 0 : at >= to - tol ? 1 : (at - from) / length;

        (double From, double To)? open = null;
        foreach (var (a, b) in spans.Select(s => (Math.Max(from, s.From), Math.Min(to, s.To))).Where(s => s.Item2 > s.Item1).OrderBy(s => s.Item1))
        {
            if (open is { } o && a <= o.To + tol)
            {
                open = (o.From, Math.Max(o.To, b));
                continue;
            }

            if (open is { } done)
            {
                yield return new OutlineSeam(side, Fraction(done.From), Fraction(done.To));
            }

            open = (a, b);
        }

        if (open is { } last)
        {
            yield return new OutlineSeam(side, Fraction(last.From), Fraction(last.To));
        }
    }
}
