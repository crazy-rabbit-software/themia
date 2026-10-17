using Themia.Core.Localization;

namespace Themia.Core.Widgets;

/// <summary>
/// Number and size formatting for the readout widgets.
/// <para>
/// Explicit rather than culture-driven, for the same reason as the clock: the app builds with
/// <c>InvariantGlobalization</c>, so culture format strings do not behave the way a display string
/// needs to.
/// </para>
/// </summary>
public static class Formatting
{
    private static readonly Str[] ByteUnits =
        [Str.WidgetsB_Units_Bytes, Str.WidgetsB_Units_Kilobytes, Str.WidgetsB_Units_Megabytes, Str.WidgetsB_Units_Gigabytes, Str.WidgetsB_Units_Terabytes, Str.WidgetsB_Units_Petabytes];

    /// <summary>
    /// A byte count as a short human string: <c>931 GB</c>, <c>1.4 GB</c>, <c>512 MB</c>.
    /// <para>
    /// Binary units (1024), because that is what Windows reports and what every other disk readout
    /// on the machine shows — a widget disagreeing with File Explorer about the size of the same
    /// drive reads as a bug.
    /// </para>
    /// </summary>
    public static string Bytes(double bytes) => Bytes(Strings.Current, bytes);

    /// <summary>
    /// <see cref="Bytes(double)"/> in <paramref name="locale"/>: its decimal separator, and its unit
    /// symbols where Windows uses its own (French says <c>Go</c>, not <c>GB</c>).
    /// </summary>
    public static string Bytes(Locale locale, double bytes)
    {
        if (!double.IsFinite(bytes) || bytes <= 0)
        {
            return Unit(locale, 0, "0");
        }

        var unit = 0;
        while (bytes >= 1024 && unit < ByteUnits.Length - 1)
        {
            bytes /= 1024;
            unit++;
        }

        // One decimal only while it still carries information: "1.4 GB" is useful, "931.0 GB" is noise.
        var digits = unit == 0 || bytes >= 100 ? 0 : bytes >= 10 ? 0 : 1;
        return Unit(locale, unit, LocaleFormat.Number(locale, bytes, digits));
    }

    /// <summary>A per-second rate, e.g. <c>1.2 MB/s</c>.</summary>
    public static string Rate(double bytesPerSecond) => Rate(Strings.Current, bytesPerSecond);

    /// <summary><see cref="Rate(double)"/> in <paramref name="locale"/>.</summary>
    public static string Rate(Locale locale, double bytesPerSecond)
        => Strings.Format(locale, Str.WidgetsB_Units_PerSecond, ("value", Bytes(locale, bytesPerSecond)));

    private static string Unit(Locale locale, int unit, string value)
        => Strings.Format(locale, ByteUnits[unit], ("value", value));

    /// <summary>
    /// A 0..1 fraction as a whole percentage, clamped. Never rounds a non-zero load to "0%" or an
    /// incomplete one to "100%", because both read as "nothing is happening" when something is.
    /// </summary>
    public static string Percent(double fraction) => Percent(Strings.Current, fraction);

    /// <summary><see cref="Percent(double)"/> in <paramref name="locale"/> (<c>42%</c>, <c>42 %</c>, <c>%42</c>).</summary>
    public static string Percent(Locale locale, double fraction)
    {
        if (!double.IsFinite(fraction))
        {
            return "--";
        }

        var clamped = Math.Clamp(fraction, 0, 1) * 100;
        var rounded = Math.Round(clamped);

        if (rounded <= 0 && clamped > 0)
        {
            rounded = 1;
        }

        if (rounded >= 100 && clamped < 100)
        {
            rounded = 99;
        }

        return LocaleFormat.Percent(locale, rounded);
    }

    /// <summary>
    /// A round-trip time: <c>18 ms</c> below a second, <c>1.4 s</c> at or above one.
    /// <para>
    /// Milliseconds stop carrying information once a link is that slow — nobody reads "1438 ms" as
    /// faster than "1502 ms", they read both as "broken" — so past a second this switches unit
    /// rather than printing four digits.
    /// </para>
    /// </summary>
    public static string Latency(double milliseconds) => Latency(Strings.Current, milliseconds);

    /// <summary><see cref="Latency(double)"/> in <paramref name="locale"/>.</summary>
    public static string Latency(Locale locale, double milliseconds)
    {
        if (!double.IsFinite(milliseconds) || milliseconds < 0)
        {
            return "--";
        }

        if (milliseconds < 1000)
        {
            return Strings.Format(locale, Str.WidgetsB_Units_Milliseconds, ("value", LocaleFormat.Number(locale, Math.Round(milliseconds), 0)));
        }

        return Strings.Format(locale, Str.WidgetsB_Units_Seconds, ("value", LocaleFormat.Number(locale, milliseconds / 1000, 1)));
    }

    /// <summary>A duration as <c>4h 20m</c> / <c>35m</c>, for battery time remaining.</summary>
    public static string Duration(TimeSpan span) => Duration(Strings.Current, span);

    /// <summary><see cref="Duration(TimeSpan)"/> in <paramref name="locale"/>.</summary>
    public static string Duration(Locale locale, TimeSpan span)
    {
        if (span <= TimeSpan.Zero)
        {
            return "--";
        }

        var hours = (int)span.TotalHours;
        return hours > 0
            ? Strings.Format(locale, Str.WidgetsB_Units_HoursMinutes, ("hours", hours), ("minutes", span.Minutes))
            : Strings.Format(locale, Str.WidgetsB_Units_Minutes, ("minutes", span.Minutes));
    }

    /// <summary>
    /// How long ago an instant was, in the terms a mail list uses: <c>now</c>, <c>4m</c>,
    /// <c>3h</c>, <c>Tue</c>, then a date.
    /// </summary>
    public static string Ago(DateTimeOffset when, DateTimeOffset now) => Ago(Strings.Current, when, now);

    /// <summary><see cref="Ago(DateTimeOffset, DateTimeOffset)"/> in <paramref name="locale"/>.</summary>
    public static string Ago(Locale locale, DateTimeOffset when, DateTimeOffset now)
    {
        var delta = now - when;

        // A negative age is clock skew between the provider and this machine: "now", not "in 5m".
        if (delta < TimeSpan.FromMinutes(1))
        {
            return LocaleFormat.Relative(locale, TimeSpan.Zero);
        }

        if (delta < TimeSpan.FromHours(1))
        {
            return Strings.Format(locale, Str.WidgetsB_Time_MinutesCompact, ("count", (long)delta.TotalMinutes));
        }

        if (delta < TimeSpan.FromDays(1))
        {
            return Strings.Format(locale, Str.WidgetsB_Time_HoursCompact, ("count", (long)delta.TotalHours));
        }

        if (delta < TimeSpan.FromDays(7))
        {
            return LocaleFormat.WeekdayName(locale, when.DayOfWeek, NameWidth.Short);
        }

        return ShortDate(locale, DateOnly.FromDateTime(when.DateTime));
    }

    /// <summary>A day and month, as a list column shows it: <c>Sep 22</c> · <c>22. Sept.</c></summary>
    public static string ShortDate(DateOnly day) => ShortDate(Strings.Current, day);

    /// <summary><see cref="ShortDate(DateOnly)"/> in <paramref name="locale"/>.</summary>
    public static string ShortDate(Locale locale, DateOnly day) => LocaleFormat.Date(locale, day, DateStyle.MonthDayShort);

    /// <summary>
    /// A heading over a day's rows: <c>Today</c>, <c>Tomorrow</c>, or the weekday and date.
    /// <para>
    /// Named days rather than dates for the two the reader is actually planning around; past that,
    /// a date is more use than counting forward from "in 3 days".
    /// </para>
    /// </summary>
    public static string DayHeading(DateOnly day, DateOnly today) => DayHeading(Strings.Current, day, today);

    /// <summary><see cref="DayHeading(DateOnly, DateOnly)"/> in <paramref name="locale"/>.</summary>
    public static string DayHeading(Locale locale, DateOnly day, DateOnly today)
    {
        if (day == today)
        {
            return Strings.Get(locale, Str.Common_Today);
        }

        if (day == today.AddDays(1))
        {
            return Strings.Get(locale, Str.Common_Tomorrow);
        }

        return LocaleFormat.Date(locale, day, DateStyle.WeekdayMonthDayShort);
    }

    /// <summary>
    /// A due date in the terms a task list reads in.
    /// <para>
    /// Overdue is said in <b>words</b> as well as carried by colour, because a state a reader can
    /// only get from a colour is a state some readers do not get at all.
    /// </para>
    /// </summary>
    public static string DueDate(DateOnly due, DateOnly today, bool overdue) => DueDate(Strings.Current, due, today, overdue);

    /// <summary><see cref="DueDate(DateOnly, DateOnly, bool)"/> in <paramref name="locale"/>.</summary>
    public static string DueDate(Locale locale, DateOnly due, DateOnly today, bool overdue)
        => overdue
            ? Strings.Format(locale, Str.WidgetsB_Time_OverdueOn, ("date", ShortDate(locale, due)))
            : DayHeading(locale, due, today);

    /// <summary>
    /// The time of day on a 24-hour clock, with the language's separator.
    /// <para>
    /// Always 24-hour: callers that want the language's own clock use
    /// <see cref="LocaleFormat.Time(Locale, TimeOnly, HourCycle, bool)"/> with <see cref="HourCycle.Locale"/>.
    /// </para>
    /// </summary>
    public static string Clock24(DateTimeOffset moment) => Clock24(Strings.Current, moment);

    /// <summary><see cref="Clock24(DateTimeOffset)"/> in <paramref name="locale"/>.</summary>
    public static string Clock24(Locale locale, DateTimeOffset moment)
        => LocaleFormat.Time(locale, moment.ToLocalTime().DateTime, HourCycle.H24);

    /// <summary>
    /// One or two initials for an avatar: the first and last word of the name, which is what every
    /// address book on Windows does. A name with nothing to take an initial from gets a placeholder
    /// rather than an empty circle.
    /// </summary>
    public static string Monogram(string? who)
    {
        var words = (who ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (words.Length == 0)
        {
            return "?";
        }

        var first = char.ToUpperInvariant(words[0][0]);

        return words.Length == 1
            ? first.ToString()
            : string.Concat(first.ToString(), char.ToUpperInvariant(words[^1][0]).ToString());
    }
}
