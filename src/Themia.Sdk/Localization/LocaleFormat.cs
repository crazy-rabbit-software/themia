using System.Globalization;
using System.Text;

namespace Themia.Core.Localization;

/// <summary>How much of a date to show. Examples are en / de.</summary>
public enum DateStyle
{
    /// <summary>10/4/2026 · 04.10.2026</summary>
    Short = 0,

    /// <summary>Oct 4, 2026 · 04.10.2026</summary>
    Medium = 1,

    /// <summary>October 4, 2026 · 4. Oktober 2026</summary>
    Long = 2,

    /// <summary>Sunday, October 4, 2026 · Sonntag, 4. Oktober 2026</summary>
    Full = 3,

    /// <summary>October 4 · 4. Oktober</summary>
    MonthDay = 4,

    /// <summary>Oct 4 · 4. Okt.</summary>
    MonthDayShort = 5,

    /// <summary>October 2026 · Oktober 2026</summary>
    MonthYear = 6,

    /// <summary>Sun, Oct 4 · So., 4. Okt.</summary>
    WeekdayMonthDayShort = 7,
}

/// <summary>Whether a time is shown on the 12- or 24-hour clock.</summary>
public enum HourCycle
{
    /// <summary>
    /// [S-3773] The user's clock setting (⚙ › Language &amp; region, else Windows'); before
    /// either is known, what the language normally uses (12 h for English, 24 h for the others).
    /// </summary>
    Locale = 0,
    H12 = 1,
    H24 = 2,
}

public enum NameWidth
{
    /// <summary>Monday · January</summary>
    Wide = 0,

    /// <summary>Mon · Jan</summary>
    Short = 1,

    /// <summary>M (weekdays only; months fall back to short)</summary>
    Narrow = 2,
}

public enum RelativeStyle
{
    /// <summary>3 minutes ago · vor 3 Minuten</summary>
    Long = 0,

    /// <summary>3 min. ago · vor 3 Min.</summary>
    Short = 1,
}

/// <summary>The unit a relative time is counted in, in the order of <c>LocaleData.Relative</c>.</summary>
public enum RelativeUnit
{
    Minute = 0,
    Hour = 1,
    Day = 2,
    Week = 3,
    Month = 4,
    Year = 5,
}

/// <summary>
/// [S-1106] Dates, times, numbers and relative times per language, without <c>CultureInfo</c>: the
/// app runs with <c>InvariantGlobalization</c>, so every name, pattern and separator comes from
/// <see cref="LocaleData"/>. Pure functions of their arguments; the overloads without a locale use
/// <see cref="Strings.Current"/>. [S-1115] A language with its own digits (Arabic, Persian) gets them in
/// every date, time and number written here.
/// </summary>
public static class LocaleFormat
{
    /// <summary>[S-1483] The language's morning marker of a 12-hour time ("AM" in English), from its locale data.</summary>
    public static string Am(Locale locale) => LocaleData.For(locale).Am;

    /// <summary>[S-1483] The language's afternoon marker of a 12-hour time ("PM" in English), from its locale data.</summary>
    public static string Pm(Locale locale) => LocaleData.For(locale).Pm;

    // -----------------------------------------------------------------------------------------
    // Names
    // -----------------------------------------------------------------------------------------

    /// <summary>A month's name on its own (a heading, a picker), 1 = January.</summary>
    public static string MonthName(Locale locale, int month, NameWidth width = NameWidth.Wide)
    {
        var data = LocaleData.For(locale);
        var index = Math.Clamp(month, 1, 12) - 1;
        return width == NameWidth.Wide ? data.Months[index] : data.MonthsShort[index];
    }

    public static string WeekdayName(Locale locale, DayOfWeek day, NameWidth width = NameWidth.Wide)
    {
        var data = LocaleData.For(locale);
        var index = Math.Clamp((int)day, 0, 6);
        return width switch
        {
            NameWidth.Short => data.WeekdaysShort[index],
            NameWidth.Narrow => data.WeekdaysNarrow[index],
            _ => data.Weekdays[index],
        };
    }

    /// <summary>The day a week starts on (a calendar's first column).</summary>
    public static DayOfWeek FirstDayOfWeek(Locale locale) => LocaleData.For(locale).FirstDayOfWeek;

    /// <summary>Whether the language normally shows times on the 24-hour clock.</summary>
    public static bool Uses24Hour(Locale locale) => LocaleData.For(locale).Uses24Hour;

    /// <summary>[S-4603] The language's decimal sign, for a figure written by hand (the stopwatch's tenth).</summary>
    public static char DecimalSeparator(Locale locale) => LocaleData.For(locale).Decimal;

    /// <summary>[S-2776] The language's digit grouping, for an amount typed by hand (a stock holding).</summary>
    public static string GroupSeparator(Locale locale) => LocaleData.For(locale).Group;

    // -----------------------------------------------------------------------------------------
    // Dates and times
    // -----------------------------------------------------------------------------------------

    public static string Date(DateOnly date, DateStyle style) => Date(Strings.Current, date, style);

    public static string Date(Locale locale, DateOnly date, DateStyle style)
    {
        var data = LocaleData.For(locale);
        var index = (int)style;
        var pattern = (uint)index < (uint)data.DatePatterns.Length ? data.DatePatterns[index] : data.DatePatterns[0];
        return Render(data, pattern, date, default);
    }

    /// <summary>The date part of a <see cref="DateTime"/>, as given (the caller decides local or UTC).</summary>
    public static string Date(Locale locale, DateTime value, DateStyle style) => Date(locale, DateOnly.FromDateTime(value), style);

    public static string Time(TimeOnly time, HourCycle cycle = HourCycle.Locale, bool seconds = false)
        => Time(Strings.Current, time, cycle, seconds);

    public static string Time(Locale locale, TimeOnly time, HourCycle cycle = HourCycle.Locale, bool seconds = false)
    {
        var data = LocaleData.For(locale);
        var use24 = cycle switch
        {
            HourCycle.H12 => false,
            HourCycle.H24 => true,
            _ => RegionFormat.Uses24Hour(locale),
        };

        var pattern = use24 ? data.Time24 : data.Time12;
        if (seconds)
        {
            // Seconds take the separator the minutes have: 15:05:09, Indonesian 15.05.09.
            var at = pattern.IndexOf("mm", StringComparison.Ordinal);
            var separator = at > 0 ? pattern[at - 1] : ':';
            pattern = pattern.Replace("mm", "mm" + separator + "ss", StringComparison.Ordinal);
        }

        return Render(data, pattern, default, time);
    }

    public static string Time(Locale locale, DateTime value, HourCycle cycle = HourCycle.Locale, bool seconds = false)
        => Time(locale, TimeOnly.FromDateTime(value), cycle, seconds);

    /// <summary>
    /// [S-1107] "3 minutes ago" / "in 2 days" for <paramref name="then"/> seen from <paramref name="now"/>.
    /// Counts in whole units, truncated (1 h 59 min is "1 hour ago"): under a minute is "now", then
    /// minutes, hours, days, weeks (from 7 days), months of 30 days (from 30), years of 365 (from 365).
    /// </summary>
    public static string Relative(Locale locale, DateTimeOffset then, DateTimeOffset now, RelativeStyle style = RelativeStyle.Long)
        => Relative(locale, then - now, style);

    public static string Relative(DateTimeOffset then, DateTimeOffset now, RelativeStyle style = RelativeStyle.Long)
        => Relative(Strings.Current, then - now, style);

    /// <summary>Relative time for an offset from now: negative is the past, positive the future.</summary>
    public static string Relative(Locale locale, TimeSpan offset, RelativeStyle style = RelativeStyle.Long)
    {
        var future = offset > TimeSpan.Zero;
        var span = offset == TimeSpan.MinValue ? TimeSpan.MaxValue : offset.Duration();

        if (span < TimeSpan.FromMinutes(1))
        {
            return LocaleData.For(locale).Now;
        }

        var (unit, count) = span switch
        {
            _ when span < TimeSpan.FromHours(1) => (RelativeUnit.Minute, (long)span.TotalMinutes),
            _ when span < TimeSpan.FromDays(1) => (RelativeUnit.Hour, (long)span.TotalHours),
            _ when span < TimeSpan.FromDays(7) => (RelativeUnit.Day, (long)span.TotalDays),
            _ when span < TimeSpan.FromDays(30) => (RelativeUnit.Week, (long)span.TotalDays / 7),
            _ when span < TimeSpan.FromDays(365) => (RelativeUnit.Month, (long)span.TotalDays / 30),
            _ => (RelativeUnit.Year, (long)span.TotalDays / 365),
        };

        return Relative(locale, unit, future ? count : -count, style);
    }

    /// <summary>An exact count of one unit: negative is "ago", positive "in", e.g. (-3, Day) → "3 days ago".</summary>
    public static string Relative(Locale locale, RelativeUnit unit, long count, RelativeStyle style = RelativeStyle.Long)
    {
        var data = LocaleData.For(locale);
        var table = style == RelativeStyle.Short ? data.RelativeShort : data.Relative;
        var forms = table[Math.Clamp((int)unit, 0, table.Length - 1)];
        var magnitude = count == long.MinValue ? long.MaxValue : Math.Abs(count);
        var text = (count > 0 ? forms.Future : forms.Past).For(PluralRules.For(locale, magnitude));
        return text.Replace("{0}", Integer(locale, magnitude), StringComparison.Ordinal);
    }

    // -----------------------------------------------------------------------------------------
    // Numbers
    // -----------------------------------------------------------------------------------------

    /// <summary>
    /// [S-1115] Whether <paramref name="locale"/> writes its own digits rather than 0–9: Arabic
    /// (Arabic-Indic ٠–٩) and Persian (Extended Arabic-Indic ۰–۹, Windows' fa-IR). Urdu and Bengali keep Latin digits, as Windows does for ur-PK and the
    /// clock's type ladder needs for Bengali ([S-1114]).
    /// </summary>
    public static bool HasNativeDigits(Locale locale) => LocaleData.For(locale).DigitZero is not null;

    /// <summary>
    /// [S-1115] <paramref name="text"/> with its ASCII digits written as <paramref name="locale"/>'s
    /// own (Arabic "09:41" → "٠٩:٤١"); unchanged for a Latin-digit language. For numbers the app
    /// composes itself; never for text that has to stay as typed (URLs, addresses, symbols, what
    /// the user wrote). Every other character is left as it is, so the length never changes.
    /// </summary>
    public static string Digits(Locale locale, string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var data = LocaleData.For(locale);
        return data.DigitZero is null || !text.AsSpan().ContainsAnyInRange('0', '9') ? text : Native(data, new StringBuilder(text));
    }

    public static string Integer(long value) => Integer(Strings.Current, value);

    /// <summary>A whole number with the language's grouping: 12,345 · 12.345 · 12 345 · 1,23,456.</summary>
    public static string Integer(Locale locale, long value)
        => Compose(LocaleData.For(locale), value < 0, value == long.MinValue ? "9223372036854775808" : Math.Abs(value).ToString(CultureInfo.InvariantCulture), string.Empty);

    public static string Number(double value, int decimals) => Number(Strings.Current, value, decimals);

    /// <summary>A number with exactly <paramref name="decimals"/> fraction digits (rounded half away from zero).</summary>
    public static string Number(Locale locale, double value, int decimals)
    {
        if (double.IsNaN(value))
        {
            return "NaN";
        }

        if (double.IsInfinity(value))
        {
            return value > 0 ? "∞" : "-∞";
        }

        decimals = Math.Clamp(decimals, 0, 15);
        var rounded = Math.Round(value, decimals, MidpointRounding.AwayFromZero);
        var invariant = Math.Abs(rounded).ToString("F" + decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        var dot = invariant.IndexOf('.');
        var whole = dot < 0 ? invariant : invariant[..dot];
        var fraction = dot < 0 ? string.Empty : invariant[(dot + 1)..];

        // "-0.00" is not a number anyone wants to read.
        var negative = rounded < 0 && invariant.AsSpan().ContainsAnyExcept('0', '.');
        return Compose(LocaleData.For(locale), negative, whole, fraction);
    }

    /// <summary>A percentage of a value already in percent (42 → "42%" / "42 %").</summary>
    public static string Percent(Locale locale, double percent, int decimals = 0)
        => LocaleData.For(locale).Percent.Replace("{0}", Number(locale, percent, decimals), StringComparison.Ordinal);

    // -----------------------------------------------------------------------------------------

    private static string Compose(LocaleData data, bool negative, string whole, string fraction)
    {
        var sb = new StringBuilder(whole.Length + fraction.Length + 8);
        if (negative)
        {
            sb.Append('-');
        }

        if (whole.Length >= 3 + data.MinimumGrouping)
        {
            // The last three digits are one group; the rest are grouped by the secondary size
            // (3, or 2 in the Indian system: 12,34,567).
            var rest = whole.Length - 3;
            var size = data.SecondaryGrouping;
            var head = rest % size;
            if (head > 0)
            {
                sb.Append(whole, 0, head).Append(data.Group);
            }

            for (var i = head; i < rest; i += size)
            {
                sb.Append(whole, i, size).Append(data.Group);
            }

            sb.Append(whole, rest, 3);
        }
        else
        {
            sb.Append(whole);
        }

        if (fraction.Length > 0)
        {
            sb.Append(data.Decimal).Append(fraction);
        }

        return Native(data, sb);
    }

    /// <summary>Render a CLDR-style pattern (see <see cref="LocaleData"/>) for a date and/or a time.</summary>
    private static string Render(LocaleData data, string pattern, DateOnly date, TimeOnly time)
    {
        var sb = new StringBuilder(pattern.Length + 16);
        var i = 0;
        while (i < pattern.Length)
        {
            var c = pattern[i];

            if (c == '\'')
            {
                // Quoted literal; '' is a quote.
                var end = pattern.IndexOf('\'', i + 1);
                if (end == i + 1)
                {
                    sb.Append('\'');
                    i += 2;
                    continue;
                }

                if (end < 0)
                {
                    end = pattern.Length;
                }

                sb.Append(pattern, i + 1, end - i - 1);
                i = end + 1;
                continue;
            }

            if (!char.IsAsciiLetter(c))
            {
                sb.Append(c);
                i++;
                continue;
            }

            var run = 1;
            while (i + run < pattern.Length && pattern[i + run] == c)
            {
                run++;
            }

            i += run;
            switch (c)
            {
                case 'd':
                    Pad(sb, date.Day, run);
                    break;
                case 'M':
                    if (run >= 4)
                    {
                        sb.Append((data.MonthsInDate ?? data.Months)[date.Month - 1]);
                    }
                    else if (run == 3)
                    {
                        sb.Append((data.MonthsShortInDate ?? data.MonthsShort)[date.Month - 1]);
                    }
                    else
                    {
                        Pad(sb, date.Month, run);
                    }

                    break;
                case 'L':
                    if (run >= 4)
                    {
                        sb.Append(data.Months[date.Month - 1]);
                    }
                    else if (run == 3)
                    {
                        sb.Append(data.MonthsShort[date.Month - 1]);
                    }
                    else
                    {
                        Pad(sb, date.Month, run);
                    }

                    break;
                case 'y':
                    if (run == 2)
                    {
                        Pad(sb, date.Year % 100, 2);
                    }
                    else
                    {
                        sb.Append(date.Year.ToString(CultureInfo.InvariantCulture));
                    }

                    break;
                case 'E':
                    var day = (int)date.DayOfWeek;
                    sb.Append(run switch
                    {
                        4 => data.Weekdays[day],
                        >= 5 => data.WeekdaysNarrow[day],
                        _ => data.WeekdaysShort[day],
                    });
                    break;
                case 'H':
                    Pad(sb, time.Hour, run);
                    break;
                case 'h':
                    Pad(sb, time.Hour % 12 == 0 ? 12 : time.Hour % 12, run);
                    break;
                case 'K':
                    Pad(sb, time.Hour % 12, run);
                    break;
                case 'm':
                    Pad(sb, time.Minute, run);
                    break;
                case 's':
                    Pad(sb, time.Second, run);
                    break;
                case 'a':
                    sb.Append(time.Hour < 12 ? data.Am : data.Pm);
                    break;
                default:
                    sb.Append(c, run);
                    break;
            }
        }

        // The patterns and names carry no digits of their own in a language that swaps them, so
        // every digit here is a field's.
        return Native(data, sb);
    }

    /// <summary>[S-1115] The built text with the language's own digits, when it has some.</summary>
    private static string Native(LocaleData data, StringBuilder sb)
    {
        if (data.DigitZero is { } zero)
        {
            for (var i = 0; i < sb.Length; i++)
            {
                if (char.IsAsciiDigit(sb[i]))
                {
                    sb[i] = (char)(zero + (sb[i] - '0'));
                }
            }
        }

        return sb.ToString();
    }

    private static void Pad(StringBuilder sb, int value, int width)
    {
        if (width >= 2 && value < 10)
        {
            sb.Append('0');
        }

        sb.Append(value.ToString(CultureInfo.InvariantCulture));
    }
}
