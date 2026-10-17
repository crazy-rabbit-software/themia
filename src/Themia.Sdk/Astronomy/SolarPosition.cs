namespace Themia.Core.Astronomy;

/// <summary>Whether the sun stays up or stays down all day at a place. [S-03]</summary>
public enum PolarState
{
    /// <summary>The sun rises and sets, as it does almost everywhere almost always.</summary>
    None = 0,

    /// <summary>Polar day: the sun never sets. The UI shows 24h of daylight and no times.</summary>
    Day = 1,

    /// <summary>Polar night: the sun never rises. The UI shows 0h of daylight and no times.</summary>
    Night = 2,
}

/// <summary>
/// Sunrise and sunset for one solar day, in UTC.
/// <para>
/// Both times are null when <see cref="Polar"/> is not <see cref="PolarState.None"/>: inside the
/// polar circles there is no event to report, and a placeholder time would be a lie rather than a
/// missing value.
/// </para>
/// </summary>
public readonly record struct SunTimes(DateTimeOffset? Sunrise, DateTimeOffset? Sunset, PolarState Polar);

/// <summary>Which of the two daily events this is. A closed set, so an enum. [S-07]</summary>
public enum SunEventKind
{
    Sunrise = 0,
    Sunset = 1,
}

/// <summary>[S-1490] Solar noon, in UTC, and the sun's altitude then.</summary>
public readonly record struct SolarNoon(DateTimeOffset At, double AltitudeDegrees);

/// <summary>One sunrise or sunset, in UTC.</summary>
public readonly record struct SunEvent(SunEventKind Kind, DateTimeOffset At);

/// <summary>
/// Sunrise, sunset and where the sun is, computed offline from a latitude and longitude.
/// <para>
/// The standard sunrise equation, accurate to a minute or two — plenty for a desktop widget, and it
/// needs no network, no ephemeris file and no time-zone database. Every function takes the instant
/// it should answer for, so nothing here reads the clock and every case is reproducible in a test.
/// </para>
/// </summary>
public static class SolarPosition
{
    /// <summary>Standard refraction-corrected altitude of the sun's upper limb at rise and set.</summary>
    private const double HorizonDegrees = -0.833;

    /// <summary>[S-1490] The sun's centre this far under the horizon is where civil twilight begins and ends.</summary>
    private const double CivilTwilightDegrees = -6;

    /// <summary>
    /// Sunrise and sunset for the solar day containing <paramref name="moment"/>.
    /// <para>
    /// The day is indexed by the <i>solar</i> day at <paramref name="longitudeDegrees"/>, not by the
    /// UTC day. [S-06] Solar noon at +151° (Sydney) falls about ten hours before noon UTC, so keying
    /// off the UTC day handed back the previous day's times right through the local morning — and
    /// the countdown built on them ran negative.
    /// </para>
    /// </summary>
    public static SunTimes TimesFor(DateTimeOffset moment, double latitudeDegrees, double longitudeDegrees)
        => Crossings(moment, latitudeDegrees, longitudeDegrees, HorizonDegrees);

    /// <summary>
    /// [S-1490] Civil dawn and dusk for the solar day containing <paramref name="moment"/>: when the
    /// sun's centre is 6° under the horizon, the moment there is light enough to read outside. In the
    /// returned <see cref="SunTimes"/>, <c>Sunrise</c> is dawn and <c>Sunset</c> is dusk.
    /// <see cref="PolarState.Day"/> means it never gets darker than civil twilight (white nights);
    /// <see cref="PolarState.Night"/> means it never gets that light.
    /// </summary>
    public static SunTimes CivilTwilightFor(DateTimeOffset moment, double latitudeDegrees, double longitudeDegrees)
        => Crossings(moment, latitudeDegrees, longitudeDegrees, CivilTwilightDegrees);

    /// <summary>
    /// [S-1490] Solar noon for the solar day containing <paramref name="moment"/>, and how high the
    /// sun stands then: <c>90° − |latitude − declination|</c>. Negative in polar night: the sun's
    /// highest point is still under the horizon.
    /// </summary>
    public static SolarNoon NoonFor(DateTimeOffset moment, double latitudeDegrees, double longitudeDegrees)
    {
        var day = DayOf(moment, longitudeDegrees);
        var declination = Math.Asin(day.SinDeclination) / AstroMath.Deg;

        return new SolarNoon(AstroMath.FromJulian(day.Transit), 90 - Math.Abs(latitudeDegrees - declination));
    }

    /// <summary>How long the sun is up, or null when it neither rises nor sets.</summary>
    public static TimeSpan? Daylight(SunTimes times)
        => times is { Sunrise: { } rise, Sunset: { } set } ? set - rise : null;

    /// <summary>
    /// [S-1490] Today's daylight against yesterday's, positive while the days lengthen. Null when either
    /// day has no sunrise or sunset to measure between (inside the polar circles).
    /// </summary>
    public static TimeSpan? DaylightChange(DateTimeOffset moment, double latitudeDegrees, double longitudeDegrees)
    {
        var today = Daylight(TimesFor(moment, latitudeDegrees, longitudeDegrees));
        var yesterday = Daylight(TimesFor(moment.AddDays(-1), latitudeDegrees, longitudeDegrees));

        return today is { } now && yesterday is { } before ? now - before : null;
    }

    /// <summary>The solar day's transit and the sun's declination then. [S-06]</summary>
    private readonly record struct SolarDay(double Transit, double SinDeclination);

    private static SolarDay DayOf(DateTimeOffset moment, double longitudeDegrees)
    {
        var julian = AstroMath.ToJulian(moment);
        var dayIndex = Math.Round(julian - AstroMath.J2000 + 0.0008 + (longitudeDegrees / 360.0));
        var meanSolarTime = dayIndex - (longitudeDegrees / 360.0);

        var meanAnomaly = AstroMath.Norm360(357.5291 + (0.98560028 * meanSolarTime));
        var anomaly = meanAnomaly * AstroMath.Deg;

        var centre = (1.9148 * Math.Sin(anomaly)) + (0.02 * Math.Sin(2 * anomaly)) + (0.0003 * Math.Sin(3 * anomaly));
        var eclipticLongitude = AstroMath.Norm360(meanAnomaly + centre + 180 + 102.9372) * AstroMath.Deg;

        var transit = AstroMath.J2000 + meanSolarTime
            + (0.0053 * Math.Sin(anomaly))
            - (0.0069 * Math.Sin(2 * eclipticLongitude));

        var sinDeclination = Math.Sin(eclipticLongitude) * Math.Sin(AstroMath.ObliquityDegrees * AstroMath.Deg);

        return new SolarDay(transit, sinDeclination);
    }

    /// <summary>When the sun's centre crosses <paramref name="altitudeDegrees"/> going up and coming down.</summary>
    private static SunTimes Crossings(DateTimeOffset moment, double latitudeDegrees, double longitudeDegrees, double altitudeDegrees)
    {
        var day = DayOf(moment, longitudeDegrees);
        var sinDeclination = day.SinDeclination;
        var cosDeclination = Math.Cos(Math.Asin(sinDeclination));
        var latitude = latitudeDegrees * AstroMath.Deg;

        var cosHourAngle =
            (Math.Sin(altitudeDegrees * AstroMath.Deg) - (Math.Sin(latitude) * sinDeclination)) /
            (Math.Cos(latitude) * cosDeclination);

        if (double.IsNaN(cosHourAngle))
        {
            // Degenerate coordinates only (an exactly-zero cosine at a pole). The sun is up all day
            // when it is in the same hemisphere as the observer, and down all day when it is not.
            return new SunTimes(null, null, sinDeclination * latitudeDegrees > 0 ? PolarState.Day : PolarState.Night);
        }

        if (cosHourAngle > 1)
        {
            return new SunTimes(null, null, PolarState.Night);
        }

        if (cosHourAngle < -1)
        {
            return new SunTimes(null, null, PolarState.Day);
        }

        var hourAngle = Math.Acos(cosHourAngle) / AstroMath.Deg;

        return new SunTimes(
            AstroMath.FromJulian(day.Transit - (hourAngle / 360.0)),
            AstroMath.FromJulian(day.Transit + (hourAngle / 360.0)),
            PolarState.None);
    }

    /// <summary>
    /// The next sunrise or sunset strictly after <paramref name="moment"/>. [S-07]
    /// <para>
    /// It collects every event from the solar day before, of and after the moment and takes the
    /// earliest still ahead, so the answer can never be an event that has already happened however
    /// the solar day lines up with the local clock — which is what kept the countdown pinned at
    /// <c>0m</c>. Null inside the polar circles, where the window holds no event at all.
    /// </para>
    /// </summary>
    public static SunEvent? NextEvent(DateTimeOffset moment, double latitudeDegrees, double longitudeDegrees)
    {
        SunEvent? earliest = null;

        for (var day = -1; day <= 1; day++)
        {
            var times = TimesFor(moment.AddDays(day), latitudeDegrees, longitudeDegrees);
            Consider(times.Sunrise, SunEventKind.Sunrise, moment, ref earliest);
            Consider(times.Sunset, SunEventKind.Sunset, moment, ref earliest);
        }

        return earliest;
    }

    /// <summary>
    /// The point the sun is directly over, from its instantaneous right ascension and declination
    /// against sidereal time.
    /// <para>
    /// Deliberately not the rounded day index <see cref="TimesFor"/> uses: that is right for
    /// "which day is it", and wrong for "where is the sun now" — it would freeze the marker for a
    /// whole day. [S-08]
    /// </para>
    /// </summary>
    public static GroundPoint SubsolarPoint(DateTimeOffset moment)
    {
        var days = AstroMath.DaysSinceJ2000(moment);
        var obliquity = AstroMath.ObliquityDegrees * AstroMath.Deg;

        var meanAnomaly = AstroMath.Norm360(357.5291 + (0.98560028 * days)) * AstroMath.Deg;
        var centre = ((1.9148 * Math.Sin(meanAnomaly))
            + (0.02 * Math.Sin(2 * meanAnomaly))
            + (0.0003 * Math.Sin(3 * meanAnomaly))) * AstroMath.Deg;

        var eclipticLongitude = meanAnomaly + centre + ((180 + 102.9372) * AstroMath.Deg);

        var rightAscension = Math.Atan2(Math.Cos(obliquity) * Math.Sin(eclipticLongitude), Math.Cos(eclipticLongitude))
            / AstroMath.Deg;
        var declination = Math.Asin(Math.Sin(obliquity) * Math.Sin(eclipticLongitude)) / AstroMath.Deg;

        return AstroMath.GroundPointOf(rightAscension, declination, days);
    }

    private static void Consider(DateTimeOffset? at, SunEventKind kind, DateTimeOffset after, ref SunEvent? earliest)
    {
        if (at is not { } instant || instant <= after)
        {
            return;
        }

        if (earliest is null || instant < earliest.Value.At)
        {
            earliest = new SunEvent(kind, instant);
        }
    }
}
