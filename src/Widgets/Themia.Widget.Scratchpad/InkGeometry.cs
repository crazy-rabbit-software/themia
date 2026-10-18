namespace Themia.Core.Widgets;

/// <summary>
/// A point on a scratchpad, in the pad's own unit square.
/// <para>
/// Normalised rather than in pixels so a drawing survives a resize: the widget is dragged from
/// 180×120 to 800×600 and the sketch grows with it instead of sitting in the corner or being
/// cropped. It also means the stored drawing says nothing about the monitor it was drawn on.
/// </para>
/// </summary>
public readonly record struct InkPoint(double X, double Y);

/// <summary>
/// The scratchpad's geometry: packing a stroke for storage, thinning it while it is drawn, and
/// deciding what the eraser has touched.
/// <para>
/// The whole of the scratchpad's correctness lives here, as pure functions on numbers, because a
/// drawing surface in a view is the one thing in this app that cannot be tested at all.
/// </para>
/// </summary>
public static class InkGeometry
{
    /// <summary>The thinnest pen. Below this a stroke reads as an artefact rather than a line.</summary>
    public const double MinWidth = 1;

    /// <summary>The fattest pen. Past this a stroke is a fill, and the pad is a colour swatch.</summary>
    public const double MaxWidth = 24;

    /// <summary>The pen a new pad starts with.</summary>
    public const double DefaultWidth = 3;

    /// <summary>
    /// How far the pointer must travel, in pad units, before another point is kept.
    /// <para>
    /// A pointer at 240Hz on a 300px pad produces hundreds of points an inch, nearly all of them
    /// inside the width of the line they describe. Keeping them would put a megabyte of noise in a
    /// state file that is rewritten whole on every unrelated save, and would draw exactly the same
    /// picture.
    /// </para>
    /// </summary>
    public const double MinSegment = 0.004;

    /// <summary>A pad with this many strokes has been drawn on for a very long time. It bounds the state file.</summary>
    public const int MaxStrokes = 400;

    /// <summary>One stroke's point budget. A stroke this long has already crossed the pad many times.</summary>
    public const int MaxPointsPerStroke = 600;

    /// <summary>The eraser is fatter than the pen, the way a real one is. The original used the same ratio.</summary>
    public const double EraserScale = 2.5;

    /// <summary>Clamp a stored or typed pen width into the range the pad can actually draw.</summary>
    public static double ClampWidth(double width)
        => double.IsFinite(width) ? Math.Clamp(width, MinWidth, MaxWidth) : DefaultWidth;

    /// <summary>
    /// Flatten points for storage: x, y, x, y. A flat list of numbers rather than a list of objects
    /// because the state file is written indented, and <c>{ "x": 0.5, "y": 0.5 }</c> per point costs
    /// several times what the number itself does.
    /// </summary>
    public static List<double> Pack(IReadOnlyList<InkPoint>? points)
    {
        var packed = new List<double>((points?.Count ?? 0) * 2);

        if (points is null)
        {
            return packed;
        }

        foreach (var point in points)
        {
            packed.Add(Round(point.X));
            packed.Add(Round(point.Y));
        }

        return packed;
    }

    /// <summary>
    /// Read points back. This is a trust boundary: the numbers come off disk, and a hand-edited or
    /// truncated file must produce a shorter drawing, never a crash or a stroke through infinity.
    /// <para>
    /// A pair with a non-finite number is dropped, because there is no sensible place to put it. A
    /// pair outside the unit square is clamped, because there is: the pad's edge, which is where a
    /// captured pointer dragged past the widget was heading anyway. A trailing odd number is an
    /// incomplete pair and is ignored.
    /// </para>
    /// </summary>
    public static List<InkPoint> Unpack(IReadOnlyList<double>? packed)
    {
        var points = new List<InkPoint>();

        if (packed is null)
        {
            return points;
        }

        for (var i = 0; i + 1 < packed.Count; i += 2)
        {
            var x = packed[i];
            var y = packed[i + 1];

            if (!double.IsFinite(x) || !double.IsFinite(y))
            {
                continue;
            }

            points.Add(new InkPoint(Math.Clamp(x, 0, 1), Math.Clamp(y, 0, 1)));

            if (points.Count >= MaxPointsPerStroke)
            {
                break;
            }
        }

        return points;
    }

    /// <summary>
    /// Whether a new pointer sample is far enough from the last kept one to be worth storing.
    /// See <see cref="MinSegment"/> for why this exists at all.
    /// </summary>
    public static bool ShouldKeep(InkPoint last, InkPoint next)
    {
        if (!IsUsable(next))
        {
            return false;
        }

        var dx = next.X - last.X;
        var dy = next.Y - last.Y;
        return ((dx * dx) + (dy * dy)) >= MinSegment * MinSegment;
    }

    /// <summary>Clamp a raw pointer position into the pad. A captured pointer routinely leaves it.</summary>
    public static InkPoint Clamp(InkPoint point)
        => IsUsable(point)
            ? new InkPoint(Math.Clamp(point.X, 0, 1), Math.Clamp(point.Y, 0, 1))
            : new InkPoint(0, 0);

    /// <summary>
    /// Whether the eraser at <paramref name="probe"/> has touched this stroke.
    /// <para>
    /// Measured against the stroke's <i>segments</i>, not only its stored points: thinning leaves
    /// long straight runs, and testing the endpoints alone would let the eraser pass straight
    /// through the middle of a line without cutting it.
    /// </para>
    /// <para>
    /// Both the points and the radius are in the same space; the caller chooses which. The view
    /// works in pixels here, so that the eraser is the size it looks on screen whatever shape the
    /// widget has been dragged into.
    /// </para>
    /// </summary>
    public static bool Touches(IReadOnlyList<InkPoint>? points, InkPoint probe, double radius)
    {
        if (points is null || points.Count == 0 || !double.IsFinite(radius) || radius <= 0)
        {
            return false;
        }

        if (points.Count == 1)
        {
            return Distance(points[0], probe) <= radius;
        }

        for (var i = 0; i + 1 < points.Count; i++)
        {
            if (DistanceToSegment(points[i], points[i + 1], probe) <= radius)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The shortest distance from <paramref name="probe"/> to the segment a–b.</summary>
    public static double DistanceToSegment(InkPoint a, InkPoint b, InkPoint probe)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var lengthSquared = (dx * dx) + (dy * dy);

        if (lengthSquared <= 0)
        {
            return Distance(a, probe);
        }

        // Where the perpendicular from the probe lands along the segment, clamped to its ends so a
        // probe past either end measures to that end rather than to the infinite line.
        var t = Math.Clamp((((probe.X - a.X) * dx) + ((probe.Y - a.Y) * dy)) / lengthSquared, 0, 1);

        return Distance(new InkPoint(a.X + (t * dx), a.Y + (t * dy)), probe);
    }

    private static double Distance(InkPoint a, InkPoint b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    private static bool IsUsable(InkPoint point) => double.IsFinite(point.X) && double.IsFinite(point.Y);

    /// <summary>
    /// Four decimals is a ten-thousandth of the pad — well under a pixel on any widget anyone will
    /// ever draw on — and it halves what a point costs in the file.
    /// </summary>
    private static double Round(double value)
        => double.IsFinite(value) ? Math.Round(Math.Clamp(value, 0, 1), 4) : 0;
}
