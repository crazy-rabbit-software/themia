using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Widget.Countdown;

namespace Themia.Core.Widgets;

/// <summary>
/// What one countdown row shows at one instant.
/// </summary>
/// <param name="Text">The duration, already worded for its format and direction.</param>
/// <param name="Elapsed">
/// True once the target is at or behind <c>now</c>. The view uses it to dim the label and switch the
/// duration to the critical colour. [C-40]
/// </param>
public readonly record struct CountdownReading(string Text, bool Elapsed);

/// <summary>
/// Turns a target instant plus a <see cref="CountdownFormat"/> into the strings a countdown row
/// shows.
/// <para>
/// Every entry point takes <c>now</c> and a <see cref="TimeZoneInfo"/> explicitly. Reading
/// <c>DateTimeOffset.Now</c> or <c>TimeZoneInfo.Local</c> in here would make the thresholds in
/// [C-30] only reachable by waiting for them, and the boundaries are exactly what is worth pinning.
/// </para>
/// <para>
/// Worded through the string tables and <see cref="LocaleFormat"/> rather than culture patterns, for
/// the same reason as <see cref="ClockFormatter"/>: the app builds with <c>InvariantGlobalization</c>.
/// The language is passed in, so a test pins it ([S-1106]).
/// </para>
/// </summary>
public static class CountdownFormatter
{
    private static readonly TimeSpan Minute = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan Hour = TimeSpan.FromHours(1);
    private static readonly TimeSpan Day = TimeSpan.FromDays(1);
    private static readonly TimeSpan Week = TimeSpan.FromDays(7);

    /// <summary>The name shown for a timer with no label of its own. [C-03]</summary>
    public static string Untitled(Locale locale) => CountdownStr.WidgetsA_Countdown_Untitled.Text(locale);

    /// <summary>
    /// The duration between <paramref name="now"/> and <paramref name="target"/>, worded for
    /// <paramref name="format"/> in <paramref name="locale"/>. [C-11]
    /// <para>
    /// A target exactly on <c>now</c> counts as elapsed: the alternative is a row that reads "in a
    /// moment" for one whole tick after the moment has passed.
    /// </para>
    /// </summary>
    public static CountdownReading Read(DateTimeOffset target, DateTimeOffset now, CountdownFormat format, Locale locale)
    {
        var difference = target - now;
        var elapsed = difference <= TimeSpan.Zero;
        var span = difference.Duration();
        var wording = Word(span, format, locale);

        var text = wording.Text;
        if (elapsed)
        {
            // [C-41] "in a moment ago" is not English. Under a minute in the past is "just now".
            // A bare day count has its own "ago" form: German says "vor 3 Tagen", not "vor 3 Tage".
            text = format == CountdownFormat.Auto && span < Minute ? CountdownStr.WidgetsA_Countdown_JustNow.Text(locale)
                : wording.Days is { } days ? CountdownStr.WidgetsA_Countdown_DaysAgo.Plural(locale, days)
                : CountdownStr.WidgetsA_Countdown_Ago.Format(locale, ("duration", wording.Text));
        }

        return new CountdownReading(text, elapsed);
    }

    /// <summary>
    /// [S-1446] "in 12 days" / "in 3 hrs" / "in a moment": how far ahead a timer is, as the "Next"
    /// line says it. <paramref name="target"/> is expected ahead of <paramref name="now"/>.
    /// </summary>
    public static string Ahead(DateTimeOffset target, DateTimeOffset now, Locale locale)
    {
        var span = (target - now).Duration();
        if (span < Minute)
        {
            return CountdownStr.WidgetsA_Countdown_InAMoment.Text(locale);
        }

        var wording = Word(span, CountdownFormat.Auto, locale);
        return wording.Days is { } days
            ? CountdownStr.WidgetsA_Countdown_DaysIn.Plural(locale, days)
            : CountdownStr.WidgetsA_Countdown_In.Format(locale, ("duration", wording.Text));
    }

    /// <summary>A worded duration; <see cref="Days"/> is set when it is the bare "N days" form, which some languages inflect.</summary>
    private readonly record struct Wording(string Text, long? Days);

    private static Wording Word(TimeSpan span, CountdownFormat format, Locale locale) => format switch
    {
        CountdownFormat.Days => DaysOnly((long)span.TotalDays, locale),
        CountdownFormat.DaysHoursMinutes => new(CountdownStr.WidgetsA_Countdown_Compact_Dhm.Format(locale,
            ("days", (long)span.TotalDays), ("hours", span.Hours), ("minutes", span.Minutes)), null),
        CountdownFormat.DaysHoursMinutesSeconds => new(CountdownStr.WidgetsA_Countdown_Compact_Dhms.Format(locale,
            ("days", (long)span.TotalDays), ("hours", span.Hours), ("minutes", span.Minutes), ("seconds", span.Seconds)), null),
        _ => Auto(span, locale),
    };

    private static Wording DaysOnly(long days, Locale locale) => new(CountdownStr.WidgetsA_Countdown_Days.Plural(locale, days), days);

    /// <summary>
    /// [C-30] The coarsest wording that still says something useful, picked from how far away the
    /// target is.
    /// </summary>
    private static Wording Auto(TimeSpan span, Locale locale)
    {
        if (span < Minute)
        {
            return new(CountdownStr.WidgetsA_Countdown_InAMoment.Text(locale), null);
        }

        if (span < Hour)
        {
            return new(CountdownStr.WidgetsA_Countdown_Minutes.Plural(locale, (long)span.TotalMinutes), null);
        }

        if (span < Day)
        {
            var hours = (int)span.TotalHours;

            // A round number reads better spelled out: "3 hrs", not "3h 0m".
            return new(span.Minutes == 0
                ? CountdownStr.WidgetsA_Countdown_Hours.Plural(locale, hours)
                : CountdownStr.WidgetsA_Countdown_Compact_Hm.Format(locale, ("hours", hours), ("minutes", span.Minutes)), null);
        }

        var days = (long)span.TotalDays;

        if (span < Week && span.Hours != 0)
        {
            return new(CountdownStr.WidgetsA_Countdown_Compact_Dh.Format(locale, ("days", days), ("hours", span.Hours)), null);
        }

        // Past a week, hours are noise.
        return DaysOnly(days, locale);
    }

    /// <summary>
    /// The target as an absolute local date and time, for the row subtitle. [C-03]
    /// <para>
    /// The year appears only when it is not the current one: on a countdown to next Tuesday it is
    /// four characters of nothing, and on a countdown to 2031 its absence would be misleading.
    /// </para>
    /// </summary>
    public static string TargetLabel(DateTimeOffset target, DateTimeOffset now, TimeZoneInfo zone, Locale locale)
    {
        ArgumentNullException.ThrowIfNull(zone);

        var local = TimeZoneInfo.ConvertTime(target, zone);
        var here = TimeZoneInfo.ConvertTime(now, zone);
        var day = DateOnly.FromDateTime(local.DateTime);
        var date = LocaleFormat.Date(locale, day, local.Year == here.Year ? DateStyle.MonthDayShort : DateStyle.Medium);

        return CountdownStr.WidgetsA_Countdown_DateTime.Format(locale,
            ("date", date), ("time", LocaleFormat.Time(locale, TimeOnly.FromDateTime(local.DateTime))));
    }

    /// <summary>The full local date and time, for the row's tooltip. [C-05]</summary>
    public static string FullTargetLabel(DateTimeOffset target, TimeZoneInfo zone, Locale locale)
    {
        ArgumentNullException.ThrowIfNull(zone);

        var local = TimeZoneInfo.ConvertTime(target, zone);
        return CountdownStr.WidgetsA_Countdown_FullDateTime.Format(locale,
            ("date", LocaleFormat.Date(locale, DateOnly.FromDateTime(local.DateTime), DateStyle.Full)),
            ("time", LocaleFormat.Time(locale, TimeOnly.FromDateTime(local.DateTime))));
    }

    /// <summary>The local calendar day of a target, for seeding the settings date picker. [C-53]</summary>
    public static DateTimeOffset LocalDate(DateTimeOffset target, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);

        var local = TimeZoneInfo.ConvertTime(target, zone);
        return new DateTimeOffset(local.Date, local.Offset);
    }

    /// <summary>The local time of day of a target, for seeding the settings time picker. [C-53]</summary>
    public static TimeSpan LocalTimeOfDay(DateTimeOffset target, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);
        return TimeZoneInfo.ConvertTime(target, zone).TimeOfDay;
    }

    /// <summary>
    /// [C-55] Build a UTC target from a picked local day and time of day, or <c>null</c> when there
    /// is nothing usable to build from.
    /// <para>
    /// Null, never "now". The original had a bug here: clearing the date field wrote
    /// <c>new Date()</c> back to the timer, silently retargeting it to the current instant and
    /// refilling the field the user had just cleared. A caller that gets <c>null</c> keeps the
    /// target it already had, which is the only safe reading of "the user cleared the field".
    /// </para>
    /// <para>
    /// A local time that does not exist — the hour a DST jump skips — is also <c>null</c> rather than
    /// a guess, for the same reason.
    /// </para>
    /// </summary>
    public static DateTimeOffset? CombineLocal(DateTimeOffset? date, TimeSpan? timeOfDay, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);

        if (date is not { } day || timeOfDay is not { } time)
        {
            return null;
        }

        if (time < TimeSpan.Zero || time >= Day)
        {
            return null;
        }

        var midnight = day.DateTime.Date;

        // A picker cannot reach this, but a persisted target can, and DateTime.Add throws on overflow.
        if (midnight > DateTime.MaxValue - time)
        {
            return null;
        }

        var local = DateTime.SpecifyKind(midnight + time, DateTimeKind.Unspecified);

        if (zone.IsInvalidTime(local))
        {
            return null;
        }

        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, zone), TimeSpan.Zero);
    }

    /// <summary>The name for a format in the settings picker. [C-11]</summary>
    public static string FormatLabel(CountdownFormat format, Locale locale) => (format switch
    {
        CountdownFormat.Days => CountdownStr.WidgetsA_Countdown_Format_Days,
        CountdownFormat.DaysHoursMinutes => CountdownStr.WidgetsA_Countdown_Format_Dhm,
        CountdownFormat.DaysHoursMinutesSeconds => CountdownStr.WidgetsA_Countdown_Format_Dhms,
        _ => CountdownStr.WidgetsA_Countdown_Format_Auto,
    }).Text(locale);
}
