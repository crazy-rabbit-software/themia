namespace Themia.Core.Widgets;

/// <summary>
/// [S-6060] The filled part of a progress ring, as numbers a view turns into an arc: clockwise from
/// twelve o'clock, on the circle through the middle of the stroke. One rule for every ring (the
/// Pomodoro's, the Countdown's, Year progress's), so they cannot disagree on where 25 % ends.
/// </summary>
/// <param name="Centre">The ring's centre on both axes: half its diameter.</param>
/// <param name="Radius">The radius of the stroke's centre line, never under 1.</param>
/// <param name="Fraction">The share drawn, clamped to 0..1 (NaN is 0).</param>
/// <param name="EndX">Where the arc ends, across.</param>
/// <param name="EndY">Where the arc ends, down.</param>
/// <param name="IsLarge">More than half the ring: the arc takes the long way round.</param>
public readonly record struct RingArc(double Centre, double Radius, double Fraction, double EndX, double EndY, bool IsLarge)
{
    /// <summary>Below this nothing is drawn: a dot of round cap reads as progress that is not there.</summary>
    public const double EmptyBelow = 0.0005;

    /// <summary>From this the ring is whole: an arc from a point back to itself draws nothing, so a view draws two halves.</summary>
    public const double FullFrom = 0.9995;

    /// <summary>Where the arc starts: twelve o'clock.</summary>
    public double StartX => Centre;

    public double StartY => Centre - Radius;

    /// <summary>Nothing to draw.</summary>
    public bool Empty => Fraction < EmptyBelow;

    /// <summary>The whole ring.</summary>
    public bool Full => Fraction >= FullFrom;

    /// <summary>The arc for <paramref name="fraction"/> of a ring <paramref name="diameter"/> across, drawn <paramref name="stroke"/> thick.</summary>
    public static RingArc Of(double diameter, double stroke, double fraction)
    {
        var centre = Math.Max(0, diameter) / 2;
        var radius = Math.Max(1, (Math.Max(0, diameter) - Math.Max(0, stroke)) / 2);
        var share = double.IsNaN(fraction) ? 0 : Math.Clamp(fraction, 0, 1);
        var angle = share * 2 * Math.PI;

        return new RingArc(
            centre,
            radius,
            share,
            centre + (radius * Math.Sin(angle)),
            centre - (radius * Math.Cos(angle)),
            share > 0.5);
    }

    /// <summary>[S-6060] A ring's stroke: a twentieth of its diameter, between 3 and 12 DIPs.</summary>
    public static double Stroke(double diameter) => Math.Clamp(diameter * 0.05, 3, 12);
}
