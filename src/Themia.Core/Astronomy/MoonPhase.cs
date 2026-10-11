using Themia.Core.Localization;

namespace Themia.Core.Astronomy;

/// <summary>
/// The eight phase buckets the widget names. [S-04]
/// <para>
/// A closed set, so an enum rather than a string: the view maps it to a label, and a value outside
/// the set would be a bug rather than a new phase of the moon.
/// </para>
/// </summary>
public enum MoonPhaseName
{
    NewMoon = 0,
    WaxingCrescent = 1,
    FirstQuarter = 2,
    WaxingGibbous = 3,
    FullMoon = 4,
    WaningGibbous = 5,
    LastQuarter = 6,
    WaningCrescent = 7,
}

/// <summary>What the moon looks like at one instant.</summary>
/// <param name="Phase">0..1 through the lunar cycle: 0 new, 0.5 full.</param>
/// <param name="Illumination">0..1 fraction of the disc lit.</param>
/// <param name="Name">Which of the eight buckets the phase falls in.</param>
public readonly record struct MoonInfo(double Phase, double Illumination, MoonPhaseName Name);

/// <summary>[S-1491] A full or new moon, and when.</summary>
public readonly record struct MoonEvent(MoonPhaseName Name, DateTimeOffset At);

/// <summary>
/// [S-3104] The next moonrise and the next moonset after an instant, each null when the moon does
/// not cross the horizon in the window searched (it stays up, or down, near the poles).
/// </summary>
public readonly record struct MoonTimes(DateTimeOffset? Rise, DateTimeOffset? Set)
{
    /// <summary>Whichever of the two comes first, and whether it is the rise.</summary>
    public (DateTimeOffset At, bool IsRise)? Next => (Rise, Set) switch
    {
        ({ } rise, { } set) => rise <= set ? (rise, true) : (set, false),
        ({ } rise, null) => (rise, true),
        (null, { } set) => (set, false),
        _ => null,
    };
}

/// <summary>
/// Moon phase, illumination and where the moon is, computed offline.
/// <para>
/// The phase comes from the synodic month counted off a known new moon: a linear model, good to
/// well under a day, which is all a phase glyph and a percentage can show. The sublunar point is a
/// truncated lunar series instead, because that one has to move between ticks.
/// </para>
/// </summary>
public static class MoonPhase
{
    /// <summary>Mean length of a lunation, days.</summary>
    private const double SynodicMonth = 29.53058867;

    /// <summary>The new moon of 2000-01-06 18:14 UTC, as a Julian day. The anchor of the model.</summary>
    private const double KnownNewMoon = 2451550.1;

    /// <summary>Phase, illumination and name at <paramref name="moment"/>. [S-04]</summary>
    public static MoonInfo At(DateTimeOffset moment)
    {
        var phase = (AstroMath.ToJulian(moment) - KnownNewMoon) / SynodicMonth % 1.0;

        if (phase < 0)
        {
            phase += 1;
        }

        var illumination = (1 - Math.Cos(2 * Math.PI * phase)) / 2;
        var bucket = (int)Math.Round(phase * 8, MidpointRounding.AwayFromZero) % 8;

        return new MoonInfo(phase, illumination, (MoonPhaseName)bucket);
    }

    /// <summary>
    /// The point the moon is directly over, from its instantaneous right ascension and declination
    /// against sidereal time. [S-08]
    /// <para>
    /// Meeus's truncated lunar series: good to a fraction of a degree, which is finer than the disc
    /// the widget draws. Crucially it is a function of the instant, not of a rounded day index —
    /// the moon has to be seen to move, and the day index used for sunrise would freeze it for
    /// twenty-four hours at a time.
    /// </para>
    /// </summary>
    public static GroundPoint SublunarPoint(DateTimeOffset moment)
    {
        var days = AstroMath.DaysSinceJ2000(moment);
        var obliquity = AstroMath.ObliquityDegrees * AstroMath.Deg;

        var meanLongitude = (218.316 + (13.176396 * days)) * AstroMath.Deg;
        var meanAnomaly = (134.963 + (13.064993 * days)) * AstroMath.Deg;
        var argumentOfLatitude = (93.272 + (13.229350 * days)) * AstroMath.Deg;

        var eclipticLongitude = meanLongitude + (6.289 * AstroMath.Deg * Math.Sin(meanAnomaly));
        var eclipticLatitude = 5.128 * AstroMath.Deg * Math.Sin(argumentOfLatitude);

        var rightAscension = Math.Atan2(
            (Math.Sin(eclipticLongitude) * Math.Cos(obliquity)) - (Math.Tan(eclipticLatitude) * Math.Sin(obliquity)),
            Math.Cos(eclipticLongitude)) / AstroMath.Deg;

        var declination = Math.Asin(
            (Math.Sin(eclipticLatitude) * Math.Cos(obliquity)) +
            (Math.Cos(eclipticLatitude) * Math.Sin(obliquity) * Math.Sin(eclipticLongitude))) / AstroMath.Deg;

        return AstroMath.GroundPointOf(rightAscension, declination, days);
    }

    /// <summary>
    /// [S-1491] The next full or new moon after <paramref name="moment"/>, whichever comes first, from
    /// the same linear lunation as <see cref="At"/>, so the date agrees with the phase drawn beside it.
    /// </summary>
    public static MoonEvent NextMajor(DateTimeOffset moment)
    {
        var phase = At(moment).Phase;

        // Strictly after: a phase of exactly 0.5 is full now, and the next full moon is a month away.
        var toFull = phase < 0.5 ? 0.5 - phase : 1.5 - phase;
        var toNew = 1 - phase;

        return toFull < toNew
            ? new MoonEvent(MoonPhaseName.FullMoon, moment.AddDays(toFull * SynodicMonth))
            : new MoonEvent(MoonPhaseName.NewMoon, moment.AddDays(toNew * SynodicMonth));
    }

    /// <summary>
    /// The geocentric altitude of the moon's centre at the moment it is seen to rise or set: its
    /// horizontal parallax (about 0.95 degrees) lifts it, refraction and the semi-diameter lower it,
    /// netting about +0.125 degrees (Meeus, ch. 15).
    /// </summary>
    public const double RiseAltitudeDegrees = 0.125;

    /// <summary>How far ahead <see cref="NextRiseSet"/> looks: two lunar days, so both events are found wherever they exist.</summary>
    public static readonly TimeSpan RiseSetWindow = TimeSpan.FromHours(50);

    private static readonly TimeSpan ScanStep = TimeSpan.FromMinutes(10);

    /// <summary>
    /// [S-3104] The next moonrise and moonset strictly after <paramref name="moment"/> for an observer
    /// at the given place. The moon's altitude is stepped every ten minutes from the same sublunar
    /// point that draws its position, and each horizon crossing is bisected to well under a second
    /// of model time, so a rise is never missed between steps (the moon moves half a degree in an
    /// hour against the sky, far slower than a step can skip). Good to a few minutes against an
    /// almanac: the series is truncated.
    /// </summary>
    public static MoonTimes NextRiseSet(DateTimeOffset moment, double latitudeDegrees, double longitudeDegrees)
    {
        double Above(DateTimeOffset at)
            => SkyPath.PositionOf(SublunarPoint(at), latitudeDegrees, longitudeDegrees).AltitudeDegrees - RiseAltitudeDegrees;

        DateTimeOffset? rise = null;
        DateTimeOffset? set = null;

        var from = moment;
        var before = Above(from);
        var end = moment + RiseSetWindow;

        while (from < end && (rise is null || set is null))
        {
            var to = from + ScanStep;
            var after = Above(to);

            if (before < 0 && after >= 0)
            {
                rise ??= Crossing(from, to, Above);
            }
            else if (before >= 0 && after < 0)
            {
                set ??= Crossing(from, to, Above);
            }

            from = to;
            before = after;
        }

        return new MoonTimes(rise, set);
    }

    /// <summary>Bisect a sign change of <paramref name="above"/> between two instants.</summary>
    private static DateTimeOffset Crossing(DateTimeOffset low, DateTimeOffset high, Func<DateTimeOffset, double> above)
    {
        var lowAbove = above(low) >= 0;

        for (var i = 0; i < 12; i++)
        {
            var middle = low + ((high - low) / 2);

            if ((above(middle) >= 0) == lowAbove)
            {
                low = middle;
            }
            else
            {
                high = middle;
            }
        }

        return high;
    }

    /// <summary>The display label for a phase bucket, in the app's language.</summary>
    public static string Label(MoonPhaseName name) => Label(Strings.Current, name);

    /// <summary>The display label for a phase bucket. Written out rather than derived from the enum name.</summary>
    public static string Label(Locale locale, MoonPhaseName name) => Strings.Get(locale, name switch
    {
        MoonPhaseName.NewMoon => Str.WidgetsC_SunMoon_Phase_NewMoon,
        MoonPhaseName.WaxingCrescent => Str.WidgetsC_SunMoon_Phase_WaxingCrescent,
        MoonPhaseName.FirstQuarter => Str.WidgetsC_SunMoon_Phase_FirstQuarter,
        MoonPhaseName.WaxingGibbous => Str.WidgetsC_SunMoon_Phase_WaxingGibbous,
        MoonPhaseName.FullMoon => Str.WidgetsC_SunMoon_Phase_FullMoon,
        MoonPhaseName.WaningGibbous => Str.WidgetsC_SunMoon_Phase_WaningGibbous,
        MoonPhaseName.LastQuarter => Str.WidgetsC_SunMoon_Phase_LastQuarter,
        MoonPhaseName.WaningCrescent => Str.WidgetsC_SunMoon_Phase_WaningCrescent,
        _ => Str.WidgetsC_SunMoon_Phase_Moon,
    });
}
