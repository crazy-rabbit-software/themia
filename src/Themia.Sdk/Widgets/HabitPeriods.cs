using System.Globalization;
using System.Text.Json.Serialization;
using Themia.Core.Localization;

namespace Themia.Core.Widgets;

/// <summary>[S-1422] How often a habit's box clears.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<HabitPeriod>))]
public enum HabitPeriod
{
    Hourly,
    Daily,
    Weekly,
    Monthly,

    /// <summary>Every <see cref="HabitCadence.CustomMinutes"/> minutes, in fixed epoch-anchored windows.</summary>
    Custom,
}

/// <summary>
/// [S-1422] A reset period and, for <see cref="HabitPeriod.Custom"/>, its length. The minutes are
/// clamped on every read, because a stored number is the step of a loop and a hand-edited file can
/// hold anything.
/// </summary>
public readonly record struct HabitCadence(HabitPeriod Period, int CustomMinutes = HabitCadence.DefaultCustomMinutes)
{
    public const int DefaultCustomMinutes = 30;

    public const int MinCustomMinutes = 1;

    /// <summary>One day: past that a custom window is a coarser daily one with a moving edge.</summary>
    public const int MaxCustomMinutes = 1440;

    public static HabitCadence Daily => new(HabitPeriod.Daily);

    /// <summary>The window length actually used: the stored minutes, clamped.</summary>
    public int Minutes => Math.Clamp(CustomMinutes, MinCustomMinutes, MaxCustomMinutes);

    /// <summary>
    /// [S-1422] Read a typed custom interval. Whole minutes in range, or a reason it was refused — never
    /// a nearby value: "0" is refused rather than read as one minute.
    /// </summary>
    public static bool TryParseMinutes(string? text, out int minutes, out string? refusal)
        => TryParseMinutes(Strings.Current, text, out minutes, out refusal);

    /// <summary><see cref="TryParseMinutes(string?, out int, out string?)"/>, the refusal in <paramref name="locale"/>.</summary>
    public static bool TryParseMinutes(Locale locale, string? text, out int minutes, out string? refusal)
    {
        minutes = 0;
        var trimmed = (text ?? string.Empty).Trim();

        if (trimmed.Length == 0)
        {
            refusal = Strings.Get(locale, Str.WidgetsB_Habits_Minutes_Empty);
            return false;
        }

        if (!int.TryParse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture, out var value))
        {
            refusal = Strings.Format(locale, Str.WidgetsB_Habits_Minutes_NotWhole, ("example", HabitCadence.DefaultCustomMinutes));
            return false;
        }

        if (value is < MinCustomMinutes or > MaxCustomMinutes)
        {
            refusal = Strings.Format(locale, Str.WidgetsB_Habits_Minutes_Range, ("min", MinCustomMinutes), ("max", MaxCustomMinutes));
            return false;
        }

        minutes = value;
        refusal = null;
        return true;
    }
}

/// <summary>
/// One period of a cadence: the key a check-off in it is logged under, and a moment inside it.
/// </summary>
/// <param name="Key">What the log stores. Its shape names the cadence, so keys of two cadences never collide.</param>
/// <param name="Start">The period's first instant, in the zone's offset at that instant.</param>
public readonly record struct HabitBucket(string Key, DateTimeOffset Start);

/// <summary>
/// [S-1422] The periods a habit is ticked in: which one a moment falls in, the one before it, and
/// how each reads in words.
/// <para>
/// Every function takes the moment and the time zone as arguments; none reads the clock. Streaks
/// are the classic DST bug in this widget, and only a test that can put "now" on the last Sunday in
/// October catches it. The calendar periods (day, week, month) step on the calendar — a 23-hour day
/// is still one day — and an hour steps back from its own first instant, so the hour a spring
/// change skips is never visited and the hour an autumn change repeats is counted once.
/// </para>
/// <para>
/// Key shapes: daily <c>YYYY-MM-DD</c>, hourly <c>YYYY-MM-DD-Hhh</c>, weekly ISO <c>YYYY-Www</c>,
/// monthly <c>YYYY-MM</c>, custom <c>C{minutes}m-{window}</c> where the window counts whole
/// intervals since the Unix epoch.
/// </para>
/// </summary>
public static class HabitPeriods
{
    /// <summary>The period <paramref name="moment"/> falls in.</summary>
    public static HabitBucket At(DateTimeOffset moment, HabitCadence cadence, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);

        if (cadence.Period == HabitPeriod.Custom)
        {
            var length = cadence.Minutes * TimeSpan.TicksPerMinute;
            var window = Math.DivRem(moment.UtcTicks - DateTimeOffset.UnixEpoch.UtcTicks, length, out var rest);

            if (rest < 0)
            {
                window--;
            }

            var start = TimeZoneInfo.ConvertTime(
                new DateTimeOffset(DateTimeOffset.UnixEpoch.UtcTicks + (window * length), TimeSpan.Zero),
                zone);

            return new HabitBucket(CustomKey(cadence.Minutes, window), start);
        }

        var local = TimeZoneInfo.ConvertTime(moment, zone);
        var day = LocalDay.From(local);

        switch (cadence.Period)
        {
            case HabitPeriod.Hourly:
                // The hour's first instant, reached by subtracting the minutes already gone: within
                // one hour the offset does not change, so this is exact even on a DST day.
                var into = new TimeSpan(0, local.Minute, local.Second) + TimeSpan.FromTicks(local.Ticks % TimeSpan.TicksPerSecond);
                var hourStart = local - into;
                return new HabitBucket(HourKey(day, local.Hour), hourStart);

            case HabitPeriod.Weekly:
                var monday = day.AddDays(-(((int)day.DayOfWeek + 6) % 7));
                return new HabitBucket(WeekKey(monday), StartOfDay(monday, zone));

            case HabitPeriod.Monthly:
                var first = new LocalDay(new DateOnly(day.Year, day.Month, 1));
                return new HabitBucket(MonthKey(first), StartOfDay(first, zone));

            default:
                return new HabitBucket(day.Key, StartOfDay(day, zone));
        }
    }

    /// <summary>
    /// The period before <paramref name="bucket"/>. One step of the ladder the streak, the history
    /// dots and the best run all walk, so the three can never disagree about what "one back" means.
    /// </summary>
    public static HabitBucket Previous(HabitBucket bucket, HabitCadence cadence, TimeZoneInfo zone)
    {
        var previous = At(bucket.Start.AddTicks(-1), cadence, zone);

        // An autumn change repeats a local hour under the same key; the repeat is one period, not two.
        while (previous.Key == bucket.Key)
        {
            previous = At(previous.Start.AddTicks(-1), cadence, zone);
        }

        return previous;
    }

    /// <summary>
    /// Read a logged key back into its period, or refuse it. Strict: a key of another cadence, a
    /// malformed one, or a custom key for another interval length is not this cadence's, and is
    /// ignored by it — kept in the log, so switching the cadence back finds it again.
    /// </summary>
    public static bool TryParse(string? key, HabitCadence cadence, TimeZoneInfo zone, out HabitBucket bucket)
    {
        ArgumentNullException.ThrowIfNull(zone);
        bucket = default;

        if (string.IsNullOrEmpty(key))
        {
            return false;
        }

        switch (cadence.Period)
        {
            case HabitPeriod.Daily:
                if (!LocalDay.TryParse(key, out var day))
                {
                    return false;
                }

                bucket = new HabitBucket(key, StartOfDay(day, zone));
                return true;

            case HabitPeriod.Hourly:
                if (key.Length != 14 || key[10] != '-' || key[11] != 'H'
                    || !LocalDay.TryParse(key[..10], out var hourDay)
                    || !TryNumber(key.AsSpan(12, 2), out var hour) || hour > 23)
                {
                    return false;
                }

                var wall = hourDay.Date.ToDateTime(new TimeOnly(hour, 0));
                if (zone.IsInvalidTime(wall))
                {
                    return false;
                }

                bucket = new HabitBucket(key, new DateTimeOffset(wall, zone.GetUtcOffset(wall)));
                return true;

            case HabitPeriod.Weekly:
                if (key.Length != 8 || key[4] != '-' || key[5] != 'W'
                    || !TryNumber(key.AsSpan(0, 4), out var weekYear) || weekYear < 1
                    || !TryNumber(key.AsSpan(6, 2), out var week)
                    || week < 1 || week > ISOWeek.GetWeeksInYear(weekYear))
                {
                    return false;
                }

                var weekStart = new LocalDay(DateOnly.FromDateTime(ISOWeek.ToDateTime(weekYear, week, DayOfWeek.Monday)));
                bucket = new HabitBucket(key, StartOfDay(weekStart, zone));
                return true;

            case HabitPeriod.Monthly:
                if (key.Length != 7 || key[4] != '-'
                    || !TryNumber(key.AsSpan(0, 4), out var year) || year < 1
                    || !TryNumber(key.AsSpan(5, 2), out var month) || month is < 1 or > 12)
                {
                    return false;
                }

                bucket = new HabitBucket(key, StartOfDay(new LocalDay(new DateOnly(year, month, 1)), zone));
                return true;

            default:
                var prefix = $"C{cadence.Minutes.ToString(CultureInfo.InvariantCulture)}m-";
                if (!key.StartsWith(prefix, StringComparison.Ordinal)
                    || !long.TryParse(key.AsSpan(prefix.Length), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var window))
                {
                    return false;
                }

                var length = cadence.Minutes * TimeSpan.TicksPerMinute;
                var ticks = DateTimeOffset.UnixEpoch.UtcTicks + (window * length);
                if (window < 0 || ticks > DateTimeOffset.MaxValue.UtcTicks - length || key != CustomKey(cadence.Minutes, window))
                {
                    return false;
                }

                bucket = new HabitBucket(key, TimeZoneInfo.ConvertTime(new DateTimeOffset(ticks, TimeSpan.Zero), zone));
                return true;
        }
    }

    /// <summary>
    /// [S-1426] A period in words, for a history dot's tooltip: in the cadence's own units, so a
    /// weekly dot reads as a week and an hourly one names its hour.
    /// </summary>
    public static string Describe(HabitBucket bucket, HabitCadence cadence) => Describe(Strings.Current, bucket, cadence);

    /// <summary><see cref="Describe(HabitBucket, HabitCadence)"/> in <paramref name="locale"/>.</summary>
    public static string Describe(Locale locale, HabitBucket bucket, HabitCadence cadence)
    {
        var start = bucket.Start;
        var day = LocalDay.From(start);
        var date = LocaleFormat.Date(locale, day.Date, DateStyle.Full);

        return cadence.Period switch
        {
            HabitPeriod.Hourly => Strings.Format(locale, Str.WidgetsB_Habits_Describe_Hourly, ("time", Clock(locale, start)), ("date", date)),
            HabitPeriod.Weekly => Strings.Format(locale, Str.WidgetsB_Habits_Describe_Weekly, ("date", date)),
            HabitPeriod.Monthly => LocaleFormat.Date(locale, day.Date, DateStyle.MonthYear),
            HabitPeriod.Custom => Strings.Format(locale, Str.WidgetsB_Habits_Describe_Custom,
                ("start", Clock(locale, start)), ("end", Clock(locale, start.AddMinutes(cadence.Minutes))), ("date", date)),
            _ => date,
        };
    }

    /// <summary>[S-1422] "today", "this week" — the period a tick lands in, for the box's spoken name.</summary>
    public static string CurrentNoun(HabitPeriod period) => CurrentNoun(Strings.Current, period);

    /// <summary><see cref="CurrentNoun(HabitPeriod)"/> in <paramref name="locale"/>, in the form that follows "done" there.</summary>
    public static string CurrentNoun(Locale locale, HabitPeriod period) => Strings.Get(locale, period switch
    {
        HabitPeriod.Hourly => Str.WidgetsB_Habits_Now_Hour,
        HabitPeriod.Weekly => Str.WidgetsB_Habits_Now_Week,
        HabitPeriod.Monthly => Str.WidgetsB_Habits_Now_Month,
        HabitPeriod.Custom => Str.WidgetsB_Habits_Now_Interval,
        _ => Str.WidgetsB_Habits_Now_Day,
    });

    /// <summary>The unit a run is counted in, singular and plural: "1 day", "3 weeks".</summary>
    public static string Units(HabitPeriod period, int count) => Units(Strings.Current, period, count);

    /// <summary><see cref="Units(HabitPeriod, int)"/> in <paramref name="locale"/>, with its plural rules.</summary>
    public static string Units(Locale locale, HabitPeriod period, int count) => Strings.Plural(locale, period switch
    {
        HabitPeriod.Hourly => Str.WidgetsB_Habits_Units_Hours,
        HabitPeriod.Weekly => Str.WidgetsB_Habits_Units_Weeks,
        HabitPeriod.Monthly => Str.WidgetsB_Habits_Units_Months,
        HabitPeriod.Custom => Str.WidgetsB_Habits_Units_Intervals,
        _ => Str.WidgetsB_Habits_Units_Days,
    }, count);

    /// <summary>
    /// How often the widget re-reads the clock so a box ticked in a past period clears without being
    /// touched: 15 s for the short cadences, a minute otherwise.
    /// </summary>
    public static TimeSpan TickInterval(HabitPeriod period)
        => period is HabitPeriod.Hourly or HabitPeriod.Custom ? TimeSpan.FromSeconds(15) : TimeSpan.FromMinutes(1);

    private static string HourKey(LocalDay day, int hour)
        => $"{day.Key}-H{hour.ToString("D2", CultureInfo.InvariantCulture)}";

    private static string WeekKey(LocalDay monday)
    {
        var date = monday.Date.ToDateTime(TimeOnly.MinValue);
        return $"{ISOWeek.GetYear(date).ToString("D4", CultureInfo.InvariantCulture)}-W{ISOWeek.GetWeekOfYear(date).ToString("D2", CultureInfo.InvariantCulture)}";
    }

    private static string MonthKey(LocalDay first)
        => $"{first.Year.ToString("D4", CultureInfo.InvariantCulture)}-{first.Month.ToString("D2", CultureInfo.InvariantCulture)}";

    private static string CustomKey(int minutes, long window)
        => $"C{minutes.ToString(CultureInfo.InvariantCulture)}m-{window.ToString(CultureInfo.InvariantCulture)}";

    /// <summary>
    /// A day's first instant. Midnight itself can be skipped by a DST change in a few zones; the
    /// first valid minute after it stands in, so a day always has a start.
    /// </summary>
    private static DateTimeOffset StartOfDay(LocalDay day, TimeZoneInfo zone)
    {
        var wall = day.Date.ToDateTime(TimeOnly.MinValue);

        while (zone.IsInvalidTime(wall))
        {
            wall = wall.AddMinutes(1);
        }

        // An ambiguous midnight takes the earlier (daylight) offset: the day starts at the first one.
        var offset = zone.IsAmbiguousTime(wall) ? zone.GetAmbiguousTimeOffsets(wall).Max() : zone.GetUtcOffset(wall);
        return new DateTimeOffset(wall, offset);
    }

    private static string Clock(Locale locale, DateTimeOffset moment)
        => LocaleFormat.Time(locale, new TimeOnly(moment.Hour, moment.Minute));

    private static bool TryNumber(ReadOnlySpan<char> digits, out int value)
    {
        value = 0;

        foreach (var c in digits)
        {
            if (c is < '0' or > '9')
            {
                return false;
            }

            value = (value * 10) + (c - '0');
        }

        return digits.Length > 0;
    }
}
