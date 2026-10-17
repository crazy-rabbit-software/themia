namespace Themia.Core.Astronomy;

/// <summary>A point on the Earth's surface: degrees north, degrees east.</summary>
/// <param name="Latitude">-90..90, north positive.</param>
/// <param name="Longitude">-180..180, east positive.</param>
public readonly record struct GroundPoint(double Latitude, double Longitude);

/// <summary>Where a body is in an observer's sky right now.</summary>
/// <param name="Up">Above the horizon?</param>
/// <param name="Progress">
/// 0 at rise, 0.5 at the meridian, 1 at set; then 0 at set, 0.5 at the nadir, 1 back at rise.
/// </param>
/// <param name="AltitudeDegrees">Degrees above the horizon; negative below it.</param>
public readonly record struct SkyPosition(bool Up, double Progress, double AltitudeDegrees);

/// <summary>One altitude reading along a path.</summary>
/// <param name="Progress">0..1 along the current stretch.</param>
/// <param name="AltitudeDegrees">Degrees above the horizon; negative below it.</param>
public readonly record struct SkySample(double Progress, double AltitudeDegrees);

/// <summary>
/// The shape a body actually traces across the sky: altitude against hour angle. [S-11]
/// <para>
/// It is <b>not</b> a semicircle. How high the curve climbs depends on the observer's latitude and
/// the body's declination, so it flattens in winter, towers in summer, and is barely a bump inside
/// the polar circles. Everything here is a pure function of an angle — no clock, no location
/// lookup — so the widget and its tests see exactly the same curve.
/// </para>
/// </summary>
public static class SkyPath
{
    /// <summary>Altitude above the horizon, degrees, at hour angle <paramref name="hourAngleDegrees"/>.</summary>
    public static double AltitudeAtHourAngle(double hourAngleDegrees, double declinationDegrees, double latitudeDegrees)
    {
        var lat = latitudeDegrees * AstroMath.Deg;
        var dec = declinationDegrees * AstroMath.Deg;

        return Math.Asin(
            (Math.Sin(lat) * Math.Sin(dec)) +
            (Math.Cos(lat) * Math.Cos(dec) * Math.Cos(hourAngleDegrees * AstroMath.Deg))) / AstroMath.Deg;
    }

    /// <summary>
    /// Half the time a body spends above the horizon, as an hour angle in degrees: 0 when it never
    /// rises, 180 when it never sets. This is what makes the ends of the drawn arc land exactly on
    /// rise and set rather than near them.
    /// </summary>
    public static double SemiDiurnalArc(double declinationDegrees, double latitudeDegrees)
    {
        var cosine = -Math.Tan(latitudeDegrees * AstroMath.Deg) * Math.Tan(declinationDegrees * AstroMath.Deg);

        if (cosine <= -1)
        {
            return 180; // circumpolar: never sets
        }

        if (cosine >= 1)
        {
            return 0; // never rises
        }

        return Math.Acos(cosine) / AstroMath.Deg;
    }

    /// <summary>
    /// The hour angle at a point of progress along one of the two stretches.
    /// <para>
    /// Above the horizon, progress 0..1 runs rise → meridian → set, so the hour angle runs
    /// -arc..+arc. Below, it carries on the same way round the sky — set → nadir → rise, i.e.
    /// +arc..180 / -180..-arc — because a body sets on the right, passes underneath and comes back
    /// up on the left. [S-12]
    /// </para>
    /// </summary>
    public static double HourAngleAt(double progress, double semiDiurnalArcDegrees, bool below)
    {
        var clamped = AstroMath.Clamp01(progress);

        if (!below)
        {
            return ((2 * clamped) - 1) * semiDiurnalArcDegrees;
        }

        var half = 180 - semiDiurnalArcDegrees;
        return clamped <= 0.5
            ? semiDiurnalArcDegrees + (2 * clamped * half)
            : -180 + ((clamped - 0.5) * 2 * half);
    }

    /// <summary>
    /// Where a body actually is in the observer's sky, from the point it is currently overhead.
    /// [S-10]
    /// </summary>
    public static SkyPosition PositionOf(GroundPoint body, double latitudeDegrees, double longitudeDegrees)
    {
        // Hour angle: how far west of the observer's meridian the body is. Negative means it is
        // still east, i.e. on its way up.
        var hourAngle = AstroMath.Norm180(longitudeDegrees - body.Longitude);
        var altitude = AltitudeAtHourAngle(hourAngle, body.Latitude, latitudeDegrees);
        var arc = SemiDiurnalArc(body.Latitude, latitudeDegrees);
        var up = altitude >= 0;

        if (up)
        {
            // An arc of 0 means the body never rises, which contradicts being up; guard anyway
            // rather than dividing by zero at the exact moment of a polar sunrise.
            var progress = arc > 0 ? AstroMath.Clamp01(0.5 + (hourAngle / (2 * arc))) : 0.5;
            return new SkyPosition(true, progress, altitude);
        }

        var half = 180 - arc;

        if (half <= 0)
        {
            return new SkyPosition(false, 0.5, altitude);
        }

        var belowProgress = hourAngle >= 0
            ? (hourAngle - arc) / (2 * half)
            : 0.5 + ((hourAngle + 180) / (2 * half));

        return new SkyPosition(false, AstroMath.Clamp01(belowProgress), altitude);
    }

    /// <summary>
    /// The path itself: altitude sampled evenly along one stretch, ready to draw.
    /// <para>
    /// <paramref name="from"/>/<paramref name="to"/> narrow it to part of the stretch — the
    /// elapsed-daylight fill is the same curve stopped at the sun, so there is one curve, not two
    /// that could drift apart.
    /// </para>
    /// </summary>
    public static IReadOnlyList<SkySample> Samples(
        double declinationDegrees,
        double latitudeDegrees,
        bool below,
        int steps = 48,
        double from = 0,
        double to = 1)
    {
        var count = Math.Max(1, steps);
        var arc = SemiDiurnalArc(declinationDegrees, latitudeDegrees);
        var samples = new SkySample[count + 1];

        for (var i = 0; i <= count; i++)
        {
            var progress = from + ((to - from) * i / count);
            var hourAngle = HourAngleAt(progress, arc, below);
            samples[i] = new SkySample(progress, AltitudeAtHourAngle(hourAngle, declinationDegrees, latitudeDegrees));
        }

        return samples;
    }
}
