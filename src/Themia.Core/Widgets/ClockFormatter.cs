using System.Globalization;
using Themia.Core.Localization;
using Themia.Core.Model;

namespace Themia.Core.Widgets;

/// <summary>What a clock widget shows at one instant. Plain data, so the view is a pure projection.</summary>
public readonly record struct ClockReading(string Time, string? Meridiem, string? Date, double HourAngle, double MinuteAngle, double SecondAngle);

/// <summary>
/// Turns an instant plus a <see cref="ClockConfig"/> into display strings and hand angles.
/// <para>
/// Formatting is done explicitly rather than with culture patterns: the app builds with
/// <c>InvariantGlobalization</c> (no ICU, a large part of why the AOT binary stays small), so
/// "h:mm tt" would render with invariant-culture quirks instead of following the config.
/// </para>
/// <para>
/// [S-1106] Names, dates and the meridiem come from the locale the caller passes (<see cref="LocaleFormat"/>),
/// not from <see cref="Strings.Current"/>, so a test pins the language. The
/// digits stay the clock's own: its 12/24-hour switch is a widget setting, not the language's.
/// </para>
/// </summary>
public static class ClockFormatter
{
    public static ClockReading Read(DateTimeOffset utcNow, ClockConfig config, Locale locale)
    {
        ArgumentNullException.ThrowIfNull(config);

        var zone = config.ResolveTimeZone();
        var local = TimeZoneInfo.ConvertTime(utcNow, zone);

        var hour24 = local.Hour;
        var hour = config.Use24Hour ? hour24 : ToTwelveHour(hour24);

        var time = config.ShowSeconds
            ? $"{Pad(hour, config.Use24Hour)}:{local.Minute:D2}:{local.Second:D2}"
            : $"{Pad(hour, config.Use24Hour)}:{local.Minute:D2}";

        var data = LocaleData.For(locale);
        var meridiem = config.Use24Hour ? null : (hour24 < 12 ? data.Am : data.Pm);
        var date = config.ShowDate ? FormatDate(local, locale) : null;

        // Hands sweep rather than step, so an analog face reads correctly between ticks.
        var secondAngle = local.Second * 6.0;
        var minuteAngle = (local.Minute * 6.0) + (local.Second * 0.1);
        var hourAngle = ((hour24 % 12) * 30.0) + (local.Minute * 0.5);

        return new ClockReading(time, meridiem, date, hourAngle, minuteAngle, secondAngle);
    }

    /// <summary>
    /// [S-665] "Sun, Oct 4" / "So., 4. Okt.": the date for a tile too narrow for the long one. No
    /// week, day of the year or time zone line (retired, concise widgets).
    /// </summary>
    public static string ShortDate(DateTimeOffset utcNow, ClockConfig config, Locale locale)
    {
        ArgumentNullException.ThrowIfNull(config);

        var local = TimeZoneInfo.ConvertTime(utcNow, config.ResolveTimeZone());
        return LocaleFormat.Date(locale, DateOnly.FromDateTime(local.DateTime), DateStyle.WeekdayMonthDayShort);
    }

    /// <summary>
    /// [S-1482] The label over the time: the custom label when there is one, else the city of a chosen
    /// zone ("Tokyo"), else nothing: a clock on the machine's own zone stays label-free, the classic
    /// look. (The original also dropped the label when the chosen zone <i>was</i> the local one; a
    /// Windows zone covers many cities, so here choosing any city labels the clock with it.)
    /// </summary>
    public static string? Label(ClockConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var custom = config.CustomLabel?.Trim();

        if (!string.IsNullOrEmpty(custom))
        {
            return custom;
        }

        return config.IsWorldClock && ZoneCatalog.Find(config.TimeZoneId) is { } entry ? entry.City : null;
    }

    /// <summary>[S-1485] The offset line for this instant, or null when the mode is None.</summary>
    public static string? OffsetLine(DateTimeOffset utcNow, ClockConfig config, Locale locale)
    {
        ArgumentNullException.ThrowIfNull(config);

        return config.OffsetMode == ClockOffsetMode.None
            ? null
            : OffsetText(config.ResolveTimeZone().GetUtcOffset(utcNow), TimeZoneInfo.Local.GetUtcOffset(utcNow), config.OffsetMode, locale);
    }

    /// <summary>
    /// [S-1485] "UTC+2", "UTC+5:30", "UTC−3" from UTC; "+8h local", "−5:30h local", "same as local"
    /// from the machine's zone. Minus is U+2212, the typographic sign, as the original wrote it.
    /// </summary>
    public static string? OffsetText(TimeSpan zoneOffset, TimeSpan localOffset, ClockOffsetMode mode, Locale locale)
    {
        switch (mode)
        {
            case ClockOffsetMode.Utc:
                return zoneOffset == TimeSpan.Zero ? "UTC" : $"UTC{Signed(zoneOffset)}";

            case ClockOffsetMode.Local:
                var difference = zoneOffset - localOffset;
                return difference == TimeSpan.Zero
                    ? Strings.Get(locale, Str.WidgetsA_Clock_Offset_SameAsLocal)
                    : Strings.Format(locale, Str.WidgetsA_Clock_Offset_FromLocal, ("offset", Signed(difference)));

            default:
                return null;
        }
    }

    /// <summary>"+2", "−3", "+5:30": whole hours bare, minutes only when there are some.</summary>
    private static string Signed(TimeSpan offset)
    {
        var sign = offset < TimeSpan.Zero ? "−" : "+";
        var magnitude = offset.Duration();
        var hours = (int)magnitude.TotalHours;

        return magnitude.Minutes == 0
            ? $"{sign}{hours}"
            : $"{sign}{hours}:{magnitude.Minutes:D2}";
    }

    /// <summary>12-hour clocks show 12, not 0, for both midnight and noon.</summary>
    public static int ToTwelveHour(int hour24)
    {
        var hour = hour24 % 12;
        return hour == 0 ? 12 : hour;
    }

    /// <summary>24-hour time is zero-padded; 12-hour time is not, which is what both conventions expect.</summary>
    private static string Pad(int hour, bool use24Hour)
        => use24Hour ? hour.ToString("D2", CultureInfo.InvariantCulture) : hour.ToString(CultureInfo.InvariantCulture);

    /// <summary>"Monday, March 9" / "Montag, 9. März": the weekday and the day, in the language's order.</summary>
    private static string FormatDate(DateTimeOffset local, Locale locale)
        => Strings.Format(locale, Str.WidgetsA_Clock_Date,
            ("weekday", LocaleFormat.WeekdayName(locale, local.DayOfWeek)),
            ("date", LocaleFormat.Date(locale, DateOnly.FromDateTime(local.DateTime), DateStyle.MonthDay)));
}
