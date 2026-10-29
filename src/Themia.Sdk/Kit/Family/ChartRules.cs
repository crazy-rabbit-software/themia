namespace Themia.Sdk.Kit;

/// <summary>[S-6717] A point of a chart's line, in the band's own DIPs.</summary>
public readonly record struct ChartPoint(double X, double Y);

/// <summary>
/// [S-6717] The Chart block: one series in the accent at 2 with a 16 % wash under it, a second series
/// tertiary; Caution or Critical stand in for the accent only while the reading is over its threshold.
/// A meter is a 4 track (round) with an accent fill, or a ring of stroke 2 over its track. Axis
/// labels are Caption tertiary, at the two ends at most. A history band runs edge to edge at the
/// card's bottom and takes what the Readout leaves; rings and bars grow with the box but keep their
/// stroke and track.
/// </summary>
public static class ChartRules
{
    /// <summary>
    /// The role of a reading's series: Critical at or past <paramref name="critical"/>, Caution at or
    /// past <paramref name="caution"/>, the accent below (no threshold, always the accent). No green: a
    /// healthy reading is the accent ([S-6706]).
    /// </summary>
    public static ColourRole Role(double reading, double? caution = null, double? critical = null)
    {
        if (critical is { } bad && reading >= bad)
        {
            return ColourRole.Critical;
        }

        return caution is { } warn && reading >= warn ? ColourRole.Caution : ColourRole.Accent;
    }

    /// <summary>
    /// The line through <paramref name="values"/> (each 0..1, clamped; not finite is 0) across a band
    /// <paramref name="width"/> × <paramref name="height"/>: evenly spaced, the stroke kept inside the
    /// band (a 0 sits half a stroke above the bottom, a 1 half a stroke under the top). Fewer than two
    /// values draw no line.
    /// </summary>
    public static IReadOnlyList<ChartPoint> Line(IReadOnlyList<double> values, double width, double height)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count < 2 || !(width > 0) || !(height > 0))
        {
            return [];
        }

        var points = new ChartPoint[values.Count];
        var rise = Math.Max(0, height - FamilyShape.Stroke);
        for (var i = 0; i < values.Count; i++)
        {
            var v = double.IsFinite(values[i]) ? Math.Clamp(values[i], 0, 1) : 0;
            points[i] = new ChartPoint(i * width / (values.Count - 1), height - (v * rise) - (FamilyShape.Stroke / 2));
        }

        return points;
    }

    /// <summary>
    /// A bar's fill on a track <paramref name="width"/> wide, from <paramref name="from"/> to
    /// <paramref name="to"/> (fractions, clamped and put in order): its left edge and width.
    /// </summary>
    public static (double Left, double Width) Bar(double width, double to, double from = 0)
    {
        if (!(width > 0))
        {
            return (0, 0);
        }

        var a = Math.Clamp(double.IsFinite(from) ? from : 0, 0, 1);
        var b = Math.Clamp(double.IsFinite(to) ? to : 0, 0, 1);
        var (lo, hi) = a <= b ? (a, b) : (b, a);
        return (lo * width, (hi - lo) * width);
    }

    /// <summary>A ring's sweep in degrees for <paramref name="fraction"/> (clamped; not finite is none), from twelve, clockwise.</summary>
    public static double Sweep(double fraction) => double.IsFinite(fraction) ? Math.Clamp(fraction, 0, 1) * 360 : 0;
}
