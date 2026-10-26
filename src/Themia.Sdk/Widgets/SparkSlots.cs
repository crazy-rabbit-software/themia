namespace Themia.Core.Widgets;

/// <summary>
/// [S-6331] Where a live history chart draws its slots: the newest <b>finished</b> slot on the right
/// edge (half a stroke inside it, [S-1824]), each older one a whole slot further left, and nothing
/// moving between two points. A folded slot ([S-1121]) is drawn only once its last member has
/// arrived ([S-1829]): a slot still taking points would change value with each one at the right
/// edge. So a chart changes only when a slot finishes — once a sample unfolded, once every
/// <c>size</c> samples folded — and a point that finishes none draws nothing (<see cref="Moves"/>).
/// <para>
/// Positions are in DIPs. Pure, so the geometry is tested without a dispatcher.
/// </para>
/// </summary>
public static class SparkSlots
{
    /// <summary>
    /// [S-6331] Whether the point that made the series <paramref name="added"/> points long changes
    /// the drawn chart: only when it finishes a slot of <paramref name="size"/> points.
    /// </summary>
    public static bool Moves(long added, int size) => added > 0 && added % Math.Max(1, size) == 0;

    /// <summary>
    /// [S-6331] How far apart two slots of a <paramref name="window"/>-slot chart
    /// <paramref name="width"/> wide are, less a stroke ([S-1824]). Zero on an unmeasured chart.
    /// </summary>
    public static double Step(int window, double width, double stroke)
    {
        var span = Span(width, stroke);
        return span <= 0 ? 0 : span / (Math.Max(2, window) - 1.0);
    }

    /// <summary>
    /// [S-6331] Where each of <paramref name="buckets"/> folded slots is drawn (oldest first; the
    /// newest holds point <paramref name="added"/> − 1), and how many of the oldest to leave out:
    /// the newest finished slot at the right edge, the window's slots to its left, the oldest at half
    /// a stroke from the left edge. A slot still filling is left out ([S-1829]), and so is anything
    /// older than the window: <c>X</c> covers slots <c>Skip</c> to <c>Skip + X.Length − 1</c>. Empty
    /// on an unmeasured chart.
    /// </summary>
    public static (int Skip, double[] X) Layout(int buckets, long added, int size, int window, double width, double stroke)
    {
        if (buckets <= 0 || added <= 0 || Span(width, stroke) <= 0)
        {
            return (0, []);
        }

        var fold = Math.Max(1, size);
        var slots = Math.Max(2, window);
        var finished = added % fold == 0 ? buckets : buckets - 1;
        var shown = Math.Clamp(finished, 0, slots);
        var right = width - (Math.Max(0, stroke) / 2);
        var step = Step(slots, width, stroke);
        var x = new double[shown];

        for (var i = 0; i < shown; i++)
        {
            x[i] = right - ((shown - 1 - i) * step);
        }

        return (finished - shown, x);
    }

    /// <summary>
    /// [S-6331] How many more points than its span a chart holds: the slot still filling (up to
    /// <c>size − 1</c> points) and the oldest slot's members the span cuts off (up to as many). 0 unfolded.
    /// </summary>
    public static int Lead(int size) => 2 * (Math.Max(1, size) - 1);

    private static double Span(double width, double stroke)
        => double.IsFinite(width) ? Math.Max(0, width - Math.Max(0, stroke)) : 0;
}
