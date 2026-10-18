using System.Globalization;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Widget.Clock;

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
/// not from <see cref="Strings.Current"/>, so a test pins the language. [S-3773] The 12/24-hour
/// choice is the shared regional setting (<see cref="RegionFormat"/>), passed in for the same reason.
/// </para>
/// </summary>
public static class ClockFormatter
{
    public static ClockReading Read(DateTimeOffset utcNow, ClockConfig config, Locale locale, bool use24Hour)
    {
        ArgumentNullException.ThrowIfNull(config);

        var zone = config.ResolveTimeZone();
        var local = TimeZoneInfo.ConvertTime(utcNow, zone);

        var hour24 = local.Hour;
        var hour = use24Hour ? hour24 : ToTwelveHour(hour24);

        // [S-1115] In the language's own digits (Arabic ٠٩:٤١), one for one, so the readout keeps
        // the character count the clock's type ladder sizes it by.
        var time = LocaleFormat.Digits(locale, config.DrawsSeconds
            ? $"{Pad(hour, use24Hour)}:{local.Minute:D2}:{local.Second:D2}"
            : $"{Pad(hour, use24Hour)}:{local.Minute:D2}");

        var meridiem = use24Hour ? null : (hour24 < 12 ? LocaleFormat.Am(locale) : LocaleFormat.Pm(locale));
        var date = config.ShowDate ? FormatDate(local, locale) : null;

        // Hands sweep rather than step, so an analog face reads correctly between ticks.
        var secondAngle = local.Second * 6.0;
        var minuteAngle = (local.Minute * 6.0) + (local.Second * 0.1);
        var hourAngle = ((hour24 % 12) * 30.0) + (local.Minute * 0.5);

        return new ClockReading(time, meridiem, date, hourAngle, minuteAngle, secondAngle);
    }

    /// <summary>
    /// [S-1483] [S-659] The wider of the language's AM and PM markers, which the clock's row is
    /// measured with (<see cref="HeroFit.Ems"/>): Tamil's "பிற்பகல்" is four times as wide as "PM"
    /// and clipped a tall clock while the ladder still counted it as two letters.
    /// </summary>
    public static string WidestMeridiem(Locale locale)
    {
        var (am, pm) = (LocaleFormat.Am(locale), LocaleFormat.Pm(locale));
        return HeroFit.Ems(pm) > HeroFit.Ems(am) ? pm : am;
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

    /// <summary>[S-6013] The word clock's sentence for this instant, in the clock's zone and <paramref name="locale"/>.</summary>
    public static string Words(DateTimeOffset utcNow, ClockConfig config, Locale locale)
    {
        ArgumentNullException.ThrowIfNull(config);

        var local = TimeZoneInfo.ConvertTime(utcNow, config.ResolveTimeZone());
        return WordClock.Compose(locale, local.Hour, local.Minute);
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
    /// The UTC form stays Latin beside "UTC"; the local one takes the language's digits ([S-1115]).
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
                    ? ClockStr.WidgetsA_Clock_Offset_SameAsLocal.Text(locale)
                    : ClockStr.WidgetsA_Clock_Offset_FromLocal.Format(locale, ("offset", LocaleFormat.Digits(locale, Signed(difference))));

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
        => ClockStr.WidgetsA_Clock_Date.Format(locale,
            ("weekday", LocaleFormat.WeekdayName(locale, local.DayOfWeek)),
            ("date", LocaleFormat.Date(locale, DateOnly.FromDateTime(local.DateTime), DateStyle.MonthDay)));
}
