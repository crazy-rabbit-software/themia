namespace Themia.Core.Astronomy;

/// <summary>A point in the drawing, in the widget's own pixels.</summary>
public readonly record struct PlotPoint(double X, double Y);

/// <summary>
/// Where the sun's path sits inside the drawing, and what its vertical axis means.
/// <para>
/// A value type with no drawing in it, so the layout can be tested without a window: the view asks
/// for a plot at its current size and then only pushes the points into shapes.
/// </para>
/// </summary>
/// <param name="X0">Left edge of the curve: sunrise.</param>
/// <param name="X1">Right edge: sunset.</param>
/// <param name="HorizonY">y of the horizon line, i.e. altitude zero.</param>
/// <param name="AboveHeight">Pixels the axis gives to <paramref name="AxisMaxDegrees"/> of altitude.</param>
/// <param name="AxisMaxDegrees">
/// [S-11806] Top of the altitude axis: the highest the sun gets on the day drawn (its noon altitude), so
/// the day's curve reaches the top of the box in December as in June and the box holds no empty sky
/// over a low winter arc. The sun's place along the curve is still the time of day; the curve's height
/// is the day's own shape, not the true elevation against another day's.
/// </param>
/// <param name="BelowHeight">
/// Pixels the night dip may use. The underside is compressed against the daylight scale on purpose:
/// nobody reads a number off it, and a true-to-scale winter midnight would be deeper than the whole
/// widget. [S-12]
/// </param>
/// <param name="NightDepthDegrees">Depth of the nadir, degrees, used to normalise that compression.</param>
public readonly record struct SunPathPlot(
    double X0,
    double X1,
    double HorizonY,
    double AboveHeight,
    double AxisMaxDegrees,
    double BelowHeight,
    double NightDepthDegrees)
{
    /// <summary>
    /// The plot for a drawing surface of this size at this place and date.
    /// <para>
    /// The curve is inset to 13%..87% of the width because the number strip anchors on the same two
    /// gridlines, so a time sits centred under the end of the curve it names. [S-16]
    /// </para>
    /// </summary>
    public static SunPathPlot For(double width, double height, double latitudeDegrees, double sunDeclinationDegrees)
    {
        var w = Math.Max(1, width);
        var h = Math.Max(1, height);

        var below = h * 0.16;

        // [S-11806] The noon point stands the marker's half under the box's top: the curve uses the height.
        var topPadding = Math.Min(h * 0.12, MarkerHalf);
        var horizon = h - below;

        return new SunPathPlot(
            X0: w * 0.13,
            X1: w * 0.87,
            HorizonY: horizon,
            AboveHeight: Math.Max(1, horizon - topPadding),
            AxisMaxDegrees: NoonAltitude(latitudeDegrees, sunDeclinationDegrees),
            BelowHeight: below,
            NightDepthDegrees: Math.Max(1, -SkyPath.AltitudeAtHourAngle(180, sunDeclinationDegrees, latitudeDegrees)));
    }

    /// <summary>[S-11806] Half the sun's marker (12): the room over the curve's top.</summary>
    public const double MarkerHalf = 6;

    /// <summary>
    /// [S-11806] The sun's highest altitude on a day of <paramref name="sunDeclinationDegrees"/> at
    /// <paramref name="latitudeDegrees"/>: 90 less their difference, at least 1 (a polar night has no day
    /// arc to scale) and at most 90.
    /// </summary>
    public static double NoonAltitude(double latitudeDegrees, double sunDeclinationDegrees)
        => Math.Clamp(90 - Math.Abs(latitudeDegrees - sunDeclinationDegrees), 1, 90);

    /// <summary>Place one reading. Never leaves the plot box, whatever altitude it is handed.</summary>
    public PlotPoint PointAt(SkySample sample)
    {
        var x = X0 + ((X1 - X0) * AstroMath.Clamp01(sample.Progress));

        if (sample.AltitudeDegrees >= 0)
        {
            var axis = AxisMaxDegrees > 0 ? AxisMaxDegrees : 1;
            return new PlotPoint(x, HorizonY - (AstroMath.Clamp01(sample.AltitudeDegrees / axis) * AboveHeight));
        }

        var depth = NightDepthDegrees > 0 ? NightDepthDegrees : 1;
        return new PlotPoint(x, HorizonY + (AstroMath.Clamp01(-sample.AltitudeDegrees / depth) * BelowHeight));
    }
}

/// <summary>Turns altitude readings into the points the view strokes and fills.</summary>
public static class SunPathGeometry
{
    /// <summary>The path through a run of readings.</summary>
    public static IReadOnlyList<PlotPoint> Curve(IReadOnlyList<SkySample> samples, SunPathPlot plot)
    {
        ArgumentNullException.ThrowIfNull(samples);

        var points = new PlotPoint[samples.Count];

        for (var i = 0; i < samples.Count; i++)
        {
            points[i] = plot.PointAt(samples[i]);
        }

        return points;
    }

    /// <summary>
    /// The same run, closed down to the horizon, so elapsed daylight can be filled in behind the
    /// sun. [S-11] Fewer than two readings fill nothing, rather than emitting a degenerate shape.
    /// </summary>
    public static IReadOnlyList<PlotPoint> Area(IReadOnlyList<SkySample> samples, SunPathPlot plot)
    {
        ArgumentNullException.ThrowIfNull(samples);

        if (samples.Count < 2)
        {
            return [];
        }

        var points = new PlotPoint[samples.Count + 2];

        for (var i = 0; i < samples.Count; i++)
        {
            points[i] = plot.PointAt(samples[i]);
        }

        points[samples.Count] = new PlotPoint(points[samples.Count - 1].X, plot.HorizonY);
        points[samples.Count + 1] = new PlotPoint(points[0].X, plot.HorizonY);

        return points;
    }
}
