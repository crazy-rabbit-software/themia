namespace Themia.Core.Widgets;

/// <summary>
/// Where a sample sits along a sparkline's width.
/// <para>
/// The obvious mapping — spread the <c>n</c> samples you have across the full width — is wrong, and
/// wrong in a way that only shows up while a widget is filling. A two-sample graph drawn that way
/// spans edge to edge, and every new sample squeezes the whole trace leftward, so for the first
/// minute of its life the graph is visibly rescaling rather than reporting. The original app carries
/// the same note against the same bug.
/// </para>
/// <para>
/// Instead the window is a fixed number of slots. The newest sample is pinned to the right edge and
/// each older one steps left by exactly one slot, so the x-axis means the same thing from the first
/// frame: the trace grows in from the right and reaches the left edge only when the window is full.
/// </para>
/// </summary>
public static class Sparkline
{
    /// <summary>
    /// The fraction of the width, 0 to 1, for sample <paramref name="index"/> of
    /// <paramref name="count"/> held, in a window of <paramref name="capacity"/> slots.
    /// </summary>
    /// <param name="index">Position in the buffer, oldest first.</param>
    /// <param name="count">How many samples are held right now.</param>
    /// <param name="capacity">How many the window holds when full. Below 2 there is no line to draw.</param>
    public static double SampleX(int index, int count, int capacity)
    {
        var slots = Math.Max(2, capacity);
        var step = 1.0 / (slots - 1);
        var x = 1.0 - ((count - 1 - index) * step);

        // A buffer that is briefly longer than its window would otherwise emit a negative x.
        return Math.Clamp(x, 0, 1);
    }

    /// <summary>
    /// The fraction of the height, 0 at the top and 1 at the bottom, for a reading in 0..1.
    /// <para>
    /// The axis is the full range, not the range actually observed. A meter rescaled to its own
    /// recent minimum and maximum turns a steady 47% into a dramatic zigzag of noise, which is the
    /// opposite of what a glanceable readout is for.
    /// </para>
    /// </summary>
    public static double SampleY(double fraction) => 1.0 - Math.Clamp(fraction, 0, 1);

    /// <summary>
    /// [S-1824] Where sample <paramref name="index"/> is drawn across a chart <paramref name="width"/>
    /// DIPs wide with a <paramref name="stroke"/>-thick line: <see cref="SampleX"/> across the width
    /// less half a stroke at each end, so the newest point's stroke is never cut by the chart's edge.
    /// </summary>
    public static double PlotX(int index, int count, int capacity, double width, double stroke)
    {
        var half = Math.Max(0, stroke) / 2;
        var span = Math.Max(0, width - (2 * half));
        return half + (SampleX(index, count, capacity) * span);
    }
}
