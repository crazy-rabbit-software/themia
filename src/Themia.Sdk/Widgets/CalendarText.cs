using Themia.Core.Localization;

namespace Themia.Core.Widgets;

/// <summary>
/// The day and month names the calendar, mood and habit widgets write.
/// <para>
/// [S-1106] The app builds with <c>InvariantGlobalization</c>: there is no ICU data in the process, so
/// <c>DateTime.ToString("ddd")</c> could only ever answer in English. The names come from
/// <see cref="LocaleFormat"/> (and the two-letter column initials from the string tables) for the
/// language asked for, or the app's current one when none is passed.
/// </para>
/// </summary>
public static class CalendarText
{
    /// <summary>
    /// Two letters, the width a heatmap column header wants: a fixed width makes the headers a column,
    /// where "Mo" on one row and "Mon" on the next would jitter. Each language lists its seven
    /// (Sunday first) in one string, so a translator picks the abbreviations people actually use.
    /// </summary>
    public static string WeekdayInitial(DayOfWeek day, Locale? locale = null)
    {
        var language = locale ?? Strings.Current;
        var initials = Strings.Get(language, Str.WidgetsA_Calendar_WeekdayInitials).Split(',', StringSplitOptions.TrimEntries);

        // A translation with the wrong number of names must not throw out of a render loop.
        return initials.Length == 7
            ? initials[Index(day)]
            : LocaleFormat.WeekdayName(language, (DayOfWeek)Index(day), NameWidth.Narrow);
    }

    /// <summary>The full weekday name, for a tooltip or a screen reader.</summary>
    public static string WeekdayName(DayOfWeek day, Locale? locale = null)
        => LocaleFormat.WeekdayName(locale ?? Strings.Current, (DayOfWeek)Index(day));

    /// <summary>The abbreviated weekday ("Mon", "Mo.", "月"), never cut out of the full name.</summary>
    public static string WeekdayShort(DayOfWeek day, Locale? locale = null)
        => LocaleFormat.WeekdayName(locale ?? Strings.Current, (DayOfWeek)Index(day), NameWidth.Short);

    /// <summary>
    /// A <see cref="DayOfWeek"/> is an enum, and an enum in C# holds whatever integer was cast into
    /// it — including one read back out of a settings file. Folded into range rather than throwing
    /// an <see cref="IndexOutOfRangeException"/> out of a render loop.
    /// </summary>
    private static int Index(DayOfWeek day) => (int)day is var raw && raw is >= 0 and < 7 ? raw : 0;

    /// <summary>The seven column headers, in the order a week starting on <paramref name="weekStart"/> runs.</summary>
    public static IReadOnlyList<string> WeekdayHeaders(DayOfWeek weekStart, Locale? locale = null)
    {
        var headers = new string[7];
        var first = Index(weekStart);

        for (var i = 0; i < 7; i++)
        {
            headers[i] = WeekdayInitial((DayOfWeek)((first + i) % 7), locale);
        }

        return headers;
    }

    /// <summary>A short date: <c>Feb 3</c> / <c>3. Feb.</c>. The year is left off, because a heatmap only ever spans one.</summary>
    public static string ShortDate(LocalDay day, Locale? locale = null)
        => LocaleFormat.Date(locale ?? Strings.Current, day.Date, DateStyle.MonthDayShort);

    /// <summary>
    /// The whole day, as a sentence a screen reader reads correctly: <c>Tuesday, February 3, 2026</c>.
    /// Carries the year, because this is the form used where there is nothing else on screen to supply it.
    /// </summary>
    public static string FullDate(LocalDay day, Locale? locale = null)
        => LocaleFormat.Date(locale ?? Strings.Current, day.Date, DateStyle.Full);
}
