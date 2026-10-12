using Themia.Core.Model;
using Themia.Core.Office;

namespace Themia.Core.Localization;

/// <summary>[S-3770] The regional formats in force: concrete values, every choice made.</summary>
/// <param name="Use24Hour">Times on the 24-hour clock.</param>
/// <param name="WeekStart">The first column of every calendar.</param>
/// <param name="Temperature">The scale weather is asked for and shown in.</param>
public sealed record RegionValues(bool Use24Hour, WeekStartDay WeekStart, TemperatureUnit Temperature);

/// <summary>
/// [S-3770] One place for the formats every widget shares — the 12- or 24-hour clock, the first day
/// of the week, the temperature scale — so that no widget carries its own copy.
/// <para>
/// Each format follows Windows' regional settings (<see cref="Windows"/>, read by the app at start
/// and whenever the setting may have changed) until the user picks a value in Settings › Widgets.
/// Until the app has read Windows, and in the tests, a format follows the app's language
/// (<see cref="Language"/>), which is what <see cref="LocaleFormat"/> did before there was a
/// setting. A change raises <see cref="Strings.Changed"/>: everything that shows text already
/// redraws on it, and a time format is text.
/// </para>
/// </summary>
public static class RegionFormat
{
    private static volatile RegionValues? _windows;
    private static volatile RegionValues? _current;

    /// <summary>What Windows' regional settings say, or null before the app has read them.</summary>
    public static RegionValues? Windows => _windows;

    /// <summary>The formats in force: chosen, else Windows', else the language's.</summary>
    public static RegionValues Current => _current ?? _windows ?? Language(Strings.Current);

    /// <summary>[S-3773] Whether times use the 24-hour clock: the setting when one is in force, else <paramref name="locale"/>'s habit.</summary>
    internal static bool Uses24Hour(Locale locale) => (_current ?? _windows)?.Use24Hour ?? LocaleFormat.Uses24Hour(locale);

    /// <summary>[S-3772] A language's own habits: its hour cycle and week start, Celsius except in English.</summary>
    public static RegionValues Language(Locale locale) => new(
        LocaleFormat.Uses24Hour(locale),
        CalendarRules.WeekStartOf(LocaleFormat.FirstDayOfWeek(locale)),
        TemperatureUnit.Celsius);

    /// <summary>
    /// [S-3772] The formats for <paramref name="preferences"/>: each chosen value as chosen, each
    /// "follow Windows" from <paramref name="windows"/>. Pure.
    /// </summary>
    public static RegionValues Resolve(RegionPreferences preferences, RegionValues windows)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        ArgumentNullException.ThrowIfNull(windows);

        return new RegionValues(
            preferences.Clock switch
            {
                ClockHours.Twelve => false,
                ClockHours.TwentyFour => true,
                _ => windows.Use24Hour,
            },
            preferences.WeekStart switch
            {
                FirstWeekday.Monday => WeekStartDay.Monday,
                FirstWeekday.Sunday => WeekStartDay.Sunday,
                FirstWeekday.Saturday => WeekStartDay.Saturday,
                _ => windows.WeekStart,
            },
            preferences.Temperature switch
            {
                TemperatureScale.Celsius => TemperatureUnit.Celsius,
                TemperatureScale.Fahrenheit => TemperatureUnit.Fahrenheit,
                _ => windows.Temperature,
            });
    }

    /// <summary>The formats <paramref name="preferences"/> resolve to on this machine right now.</summary>
    public static RegionValues For(RegionPreferences preferences) => Resolve(preferences, _windows ?? Language(Strings.Current));

    /// <summary>
    /// [S-3772] What Windows' regional settings say, from the raw values the app reads: the short
    /// time pattern ("HH:mm", "h:mm tt"), the first day of the week (0 = Monday … 6 = Sunday, as
    /// <c>LOCALE_IFIRSTDAYOFWEEK</c> counts) and the measurement system (0 metric, 1 US). A value
    /// that cannot be read falls back to <paramref name="fallback"/>'s.
    /// </summary>
    public static RegionValues FromWindows(string? timePattern, int? firstDay, int? measure, RegionValues fallback)
    {
        ArgumentNullException.ThrowIfNull(fallback);

        // "H" is the 24-hour hour in Windows' patterns, "h" the 12-hour one; a pattern with neither is unreadable.
        var use24 = string.IsNullOrEmpty(timePattern)
            ? fallback.Use24Hour
            : timePattern.Contains('H', StringComparison.Ordinal) || (!timePattern.Contains('h', StringComparison.Ordinal) && fallback.Use24Hour);

        // LOCALE_IFIRSTDAYOFWEEK counts from Monday (0) to Sunday (6). Monday, Sunday and Saturday
        // (fa-IR) are the weeks calendars start on; any other day starts on Monday.
        var weekStart = firstDay switch
        {
            6 => WeekStartDay.Sunday,
            5 => WeekStartDay.Saturday,
            >= 0 and <= 4 => WeekStartDay.Monday,
            _ => fallback.WeekStart,
        };

        var temperature = measure switch
        {
            1 => TemperatureUnit.Fahrenheit,
            0 => TemperatureUnit.Celsius,
            _ => fallback.Temperature,
        };

        return new RegionValues(use24, weekStart, temperature);
    }

    /// <summary>Record what Windows says. Raises <see cref="Strings.Changed"/> only when the formats in force moved.</summary>
    public static void SetWindows(RegionValues windows)
    {
        ArgumentNullException.ThrowIfNull(windows);

        var before = Current;
        _windows = windows;
        Announce(before);
    }

    /// <summary>Put <paramref name="values"/> in force. Raises <see cref="Strings.Changed"/> only on a real change.</summary>
    public static void Apply(RegionValues values)
    {
        ArgumentNullException.ThrowIfNull(values);

        var before = Current;
        _current = values;
        Announce(before);
    }

    private static void Announce(RegionValues before)
    {
        if (before != Current)
        {
            Strings.NotifyFormatsChanged();
        }
    }
}
