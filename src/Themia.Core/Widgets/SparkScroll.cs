namespace Themia.Core.Widgets;

/// <summary>
/// [S-1826] Where a live history chart draws its points so it scrolls instead of stepping. The chart
/// is drawn one sample behind: the newest point sits one sample step beyond the right edge, and the
/// drawn geometry is then translated left by exactly one step over one sample period (a composition
/// animation, not a redraw). When the next point arrives the geometry is rebuilt at the same lag and
/// the translation reset, and because a point's place depends only on its distance from the newest,
/// the rebuilt geometry lies exactly where the translated one ended: no visible jump.
/// <para>
/// Positions are in DIPs, measured against the newest point minus <c>lag</c> samples (1 just after a
/// point arrives, 0 once the step has played out or when nothing animates). A folded slot ([S-1121])
/// stands at the time of its last member and is drawn only once that member has arrived: a slot still
/// filling changes value with every point, and the segment into it would show that at the right edge.
/// So a folded chart is a further slot less one sample behind, which puts a slot that just finished a
/// whole slot beyond the right edge, from where it slides in over its points' periods ([S-1829]).
/// </para>
/// </summary>
public static class SparkScroll
{
    /// <summary>
    /// [S-1826] How far the chart moves per sample, in DIPs: one slot of a <paramref name="window"/>-slot
    /// chart <paramref name="width"/> wide (less a stroke, [S-1824]) over the <paramref name="size"/>
    /// samples a slot folds. Zero on an unmeasured chart.
    /// </summary>
    public static double Step(int size, int window, double width, double stroke)
    {
        var span = Span(width, stroke);
        if (span <= 0)
        {
            return 0;
        }

        return span / (Math.Max(1, size) * (Math.Max(2, window) - 1.0));
    }

    /// <summary>
    /// [S-1826] The x of folded slot <paramref name="bucket"/> (its absolute id, [S-1121]) when the
    /// series' newest point is number <paramref name="newest"/> (0-based) and the chart is
    /// <paramref name="lag"/> samples behind it. With no lag and no folding this is
    /// <see cref="Sparkline.PlotX"/>: the newest point half a stroke inside the right edge. [S-1829] A
    /// folded chart stands <c>size − 1</c> samples further behind, so the newest finished slot is never
    /// left of the right edge and one that just finished is a whole slot beyond it.
    /// </summary>
    public static double PlotX(long bucket, long newest, int size, int window, double width, double stroke, double lag)
    {
        var fold = Math.Max(1, size);
        var right = width - (Math.Max(0, stroke) / 2);
        var behind = newest - lag - (fold - 1) - (((bucket + 1) * fold) - 1);
        return right - (behind * Step(fold, window, width, stroke));
    }

    /// <summary>
    /// [S-1826] Where each of <paramref name="buckets"/> folded slots is drawn (oldest first; the
    /// newest holds point <paramref name="added"/> − 1), and how many of the oldest to leave out: every
    /// slot from the last one at or left of the chart's left edge is kept, so the line reaches the
    /// edge while it slides, and anything older is not drawn. [S-1829] The newest slot is left out
    /// too while it is still filling: <c>X</c> covers slots <c>Skip</c> to <c>Skip + X.Length − 1</c>.
    /// Empty on an unmeasured chart.
    /// </summary>
    public static (int Skip, double[] X) Layout(int buckets, long added, int size, int window, double width, double stroke, double lag)
    {
        if (buckets <= 0 || added <= 0 || Span(width, stroke) <= 0)
        {
            return (0, []);
        }

        var fold = Math.Max(1, size);
        var newest = added - 1;
        var last = newest / fold;
        var finished = added % fold == 0 ? buckets : buckets - 1;
        var x = new double[Math.Max(0, finished)];
        var skip = 0;

        for (var i = 0; i < x.Length; i++)
        {
            x[i] = PlotX(last - (buckets - 1 - i), newest, fold, window, width, stroke, lag);

            if (x[i] <= 0)
            {
                skip = i;
            }
        }

        return (skip, skip == 0 ? x : x[skip..]);
    }

    /// <summary>
    /// [S-1826] How many more points than its span a chart holds: the slot sliding out at the left
    /// edge, the newest beyond the right one and, folded ([S-1829]), the slot still filling and the
    /// slot's further lag. At least 2.
    /// </summary>
    public static int Lead(int size) => (3 * Math.Max(1, size)) - 1;

    /// <summary>
    /// [S-1827] [S-1828] How a chart draws after <paramref name="change"/>: a new point with animation
    /// effects on is drawn a sample behind and slid over one period; anything else (a history loaded,
    /// a resize, a span or scale change, a clear, animation effects off) is drawn settled — the newest
    /// point at the right edge, still — so the next point slides on from exactly there.
    /// </summary>
    public static SparkFrame Plan(SparkChange change, bool animates)
        => change == SparkChange.Point && animates ? new SparkFrame(1, true) : new SparkFrame(0, false);

    private static double Span(double width, double stroke)
        => double.IsFinite(width) ? Math.Max(0, width - Math.Max(0, stroke)) : 0;
}

/// <summary>What made a history chart draw again ([S-1827]).</summary>
public enum SparkChange
{
    /// <summary>A new point arrived on the series' cadence.</summary>
    Point,

    /// <summary>The history was loaded, cleared, re-spanned or rescaled, or the chart was resized.</summary>
    Settle,
}

/// <summary>
/// [S-1827] One drawing of a history chart: how many samples behind the newest its geometry is
/// placed (<see cref="Lag"/>, 0 or 1), and whether it then slides left by that lag over one period.
/// </summary>
public readonly record struct SparkFrame(double Lag, bool Slides);
