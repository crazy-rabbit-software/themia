using System.Globalization;
using Themia.Core.Localization;

namespace Themia.Core.Astronomy;

/// <summary>
/// The Sun and Moon widgets' words, from the string table and <see cref="LocaleFormat"/> rather
/// than culture patterns: the app builds with <c>InvariantGlobalization</c>. Pure, so every edge
/// (polar days, a shrinking day, the countdown's last minute) is a test rather than a screenshot.
/// Each text has an overload taking the language, so tests do not depend on the process-wide one.
/// </summary>
public static class SkyText
{
    /// <summary>"11h 33m"; "24h" in polar day and "0h" in polar night. [S-161]</summary>
    public static string Daylight(SunTimes times) => Daylight(Strings.Current, times);

    public static string Daylight(Locale locale, SunTimes times)
    {
        if (SolarPosition.Daylight(times) is not { } span)
        {
            return Hours(locale, times.Polar == PolarState.Day ? 24 : 0);
        }

        return Duration(locale, (int)Math.Round(span.TotalMinutes));
    }

    /// <summary>
    /// [S-1490] The day against yesterday: "+2m 14s", "−58s" (U+2212), "same as yesterday" when the
    /// change rounds to nothing, which it does for a day or two around each solstice.
    /// </summary>
    public static string Change(TimeSpan change) => Change(Strings.Current, change);

    public static string Change(Locale locale, TimeSpan change)
    {
        var seconds = (int)Math.Round(change.TotalSeconds);

        if (seconds == 0)
        {
            return Strings.Get(locale, Str.WidgetsC_SunMoon_SameAsYesterday);
        }

        var sign = seconds < 0 ? "−" : "+";
        var magnitude = Math.Abs(seconds);

        var amount = magnitude >= 60
            ? Strings.Format(locale, Str.WidgetsC_SunMoon_Duration_MinutesSeconds, ("minutes", magnitude / 60), ("seconds", magnitude % 60))
            : Strings.Format(locale, Str.WidgetsC_SunMoon_Duration_Seconds, ("seconds", magnitude));

        return sign + amount;
    }

    /// <summary>"in 6h 45m", "in 12m"; never negative. [S-163]</summary>
    public static string Countdown(TimeSpan remaining) => Countdown(Strings.Current, remaining);

    public static string Countdown(Locale locale, TimeSpan remaining)
    {
        var minutes = Math.Max(0, (int)Math.Round(remaining.TotalMinutes));
        return Strings.Format(locale, Str.SunMoon_InTime, ("n", Duration(locale, minutes)));
    }

    /// <summary>"Wed, Oct 7", for a moment already in the zone it should be read in.</summary>
    public static string ShortDate(DateTimeOffset local) => ShortDate(Strings.Current, local);

    public static string ShortDate(Locale locale, DateTimeOffset local)
        => LocaleFormat.Date(locale, DateOnly.FromDateTime(local.DateTime), DateStyle.WeekdayMonthDayShort);

    /// <summary>
    /// [S-3106] When a moonrise or moonset falls, read in the machine's zone: the time alone today
    /// ("07:28"), the short weekday before it on another day ("Tue 07:28"), because the next one is
    /// often tomorrow. An absent time is an em dash, as the sun's are ([S-161]).
    /// </summary>
    public static string EventTime(Locale locale, DateTimeOffset? at, DateTimeOffset now)
    {
        if (at is not { } moment)
        {
            return "—";
        }

        var local = moment.ToLocalTime();
        var time = LocaleFormat.Time(locale, TimeOnly.FromDateTime(local.DateTime));

        return local.Date == now.ToLocalTime().Date
            ? time
            : $"{LocaleFormat.WeekdayName(locale, local.DayOfWeek, NameWidth.Short)} {time}";
    }

    /// <summary>"34°": whole degrees, which is all a widget can show of a sun position good to a degree.</summary>
    public static string Degrees(double degrees)
        => $"{((int)Math.Round(degrees)).ToString(CultureInfo.InvariantCulture)}°"; // int: no "-0°"

    /// <summary>"6h 45m" from an hour on, "12m" under it.</summary>
    private static string Duration(Locale locale, int minutes)
        => minutes >= 60
            ? Strings.Format(locale, Str.WidgetsC_SunMoon_Duration_HoursMinutes, ("hours", minutes / 60), ("minutes", minutes % 60))
            : Strings.Format(locale, Str.WidgetsC_SunMoon_Duration_Minutes, ("minutes", minutes));

    private static string Hours(Locale locale, int hours)
        => Strings.Format(locale, Str.WidgetsC_SunMoon_Duration_Hours, ("hours", hours));
}
