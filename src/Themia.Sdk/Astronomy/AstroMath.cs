namespace Themia.Core.Astronomy;

/// <summary>
/// The arithmetic every body in this namespace shares: Julian dates, angle wrapping and sidereal
/// time.
/// <para>
/// Internal on purpose. It is not a public astronomy library — it is the handful of conversions
/// <see cref="SolarPosition"/> and <see cref="MoonPhase"/> both need, kept in one place so the two
/// cannot disagree about what day it is.
/// </para>
/// <para>
/// Everything here works in UTC. The app builds with <c>InvariantGlobalization</c>, so IANA
/// time-zone ids do not resolve; a caller that wants a wall-clock time converts the returned
/// instant with the machine's own local zone.
/// </para>
/// </summary>
internal static class AstroMath
{
    /// <summary>Radians per degree.</summary>
    public const double Deg = Math.PI / 180.0;

    /// <summary>Obliquity of the ecliptic, degrees. Good enough for a widget for the next century.</summary>
    public const double ObliquityDegrees = 23.4397;

    /// <summary>Julian day number of the Unix epoch, 1970-01-01T00:00Z.</summary>
    public const double UnixEpochJulian = 2440587.5;

    /// <summary>Julian day number of J2000.0, 2000-01-01T12:00Z.</summary>
    public const double J2000 = 2451545.0;

    public static double ToJulian(DateTimeOffset moment)
        => ((moment.UtcDateTime - DateTime.UnixEpoch).TotalDays) + UnixEpochJulian;

    public static DateTimeOffset FromJulian(double julian)
        => DateTimeOffset.UnixEpoch.AddDays(julian - UnixEpochJulian);

    /// <summary>Days since J2000.0, the argument every series below is written in.</summary>
    public static double DaysSinceJ2000(DateTimeOffset moment) => ToJulian(moment) - J2000;

    /// <summary>Wrap to 0..360.</summary>
    public static double Norm360(double degrees)
    {
        var wrapped = degrees % 360.0;
        return wrapped < 0 ? wrapped + 360.0 : wrapped;
    }

    /// <summary>Wrap to -180..180, which is how a longitude and an hour angle are read.</summary>
    public static double Norm180(double degrees)
    {
        var wrapped = Norm360(degrees);
        return wrapped > 180.0 ? wrapped - 360.0 : wrapped;
    }

    /// <summary>Greenwich mean sidereal time in degrees (IAU low-precision expression).</summary>
    public static double Gmst(double daysSinceJ2000)
        => Norm360(280.46061837 + (360.98564736629 * daysSinceJ2000));

    /// <summary>
    /// Equatorial coordinates to the point that body is directly over: the declination <i>is</i> the
    /// latitude, and the longitude is the right ascension measured back from where the Greenwich
    /// meridian currently points.
    /// </summary>
    public static GroundPoint GroundPointOf(double rightAscensionDegrees, double declinationDegrees, double daysSinceJ2000)
        => new(declinationDegrees, Norm180(rightAscensionDegrees - Gmst(daysSinceJ2000)));

    public static double Clamp01(double value) => value < 0 ? 0 : value > 1 ? 1 : value;
}
