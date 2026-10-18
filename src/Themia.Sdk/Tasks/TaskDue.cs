using System.Globalization;
using Themia.Core.Localization;

namespace Themia.Core.Tasks;

/// <summary>[S-594] Which label a due date earns.</summary>
public enum DueLabel
{
    Overdue,
    Today,
    Tomorrow,

    /// <summary>Two to six days out: the short weekday.</summary>
    Weekday,

    /// <summary>A week or more out: the date.</summary>
    Date,
}

/// <summary>A due date resolved against one "today".</summary>
/// <param name="Day">The calendar day the task is due.</param>
/// <param name="Days">Whole days from today; negative when overdue.</param>
public readonly record struct Due(DateOnly Day, DueLabel Label, int Days)
{
    public bool IsPast => Days < 0;
}

/// <summary>
/// [S-593] A To Do due date is a calendar <b>day</b>, but neither provider says so in its type.
/// <para>
/// What arrives is one of two encodings. <b>Midnight of the intended day</b> — Graph's zoneless
/// <c>2026-08-10T00:00:00.0000000</c>, Google's <c>2026-08-10T00:00:00.000Z</c> (its time part is
/// documented as meaningless), or a bare date — whose date part <i>is</i> the day: converting it
/// through UTC would push every viewer west of Greenwich back a day. Or <b>the creator's local
/// midnight already converted to UTC</b> — the Microsoft To Do mobile app writes
/// <c>2026-08-09T17:00:00.0000000</c> for a UTC+7 user's "due the 10th" — which is a real instant,
/// read as UTC when zoneless, whose day is the viewer's local day. Comparing strings, or reading
/// that second shape as local time, is how a task due today showed "overdue" (classic issue #101).
/// </para>
/// <para>
/// Pure, with the viewer's zone and "today" passed in, so the rule is tested in real zones against a
/// fixed clock rather than against whatever the build machine's day happens to be.
/// </para>
/// </summary>
public static class TaskDue
{
    /// <summary>The intended day of a due value, or null when it is absent or not a date.</summary>
    public static DateOnly? Day(string? raw, TimeZoneInfo viewer)
    {
        ArgumentNullException.ThrowIfNull(viewer);
        return TaskStamp.Parse(raw)?.DayIn(viewer);
    }

    /// <summary>[S-594] Resolve a due value against <paramref name="today"/>.</summary>
    public static Due? Resolve(string? raw, DateOnly today, TimeZoneInfo viewer)
    {
        if (Day(raw, viewer) is not { } day)
        {
            return null;
        }

        // Day numbers, not instants: a DST change between the two days cannot shift the count.
        var days = day.DayNumber - today.DayNumber;
        var label = days switch
        {
            < 0 => DueLabel.Overdue,
            0 => DueLabel.Today,
            1 => DueLabel.Tomorrow,
            < 7 => DueLabel.Weekday,
            _ => DueLabel.Date,
        };

        return new Due(day, label, days);
    }

    /// <summary>[S-594] True when the due day is before today. An undated task is never past.</summary>
    public static bool IsPast(string? raw, DateOnly today, TimeZoneInfo viewer)
        => Resolve(raw, today, viewer) is { IsPast: true };

    /// <summary>
    /// The label in words. Overdue is said as a word, not only coloured ([S-186]): a state a reader
    /// can only get from a colour is one some readers do not get at all.
    /// </summary>
    public static string Text(Due due) => Text(Strings.Current, due);

    /// <summary><see cref="Text(Due)"/> in an explicit language.</summary>
    public static string Text(Locale locale, Due due) => due.Label switch
    {
        DueLabel.Overdue => Strings.Get(locale, Str.WidgetsC_Tasks_Overdue),
        DueLabel.Today => Strings.Get(locale, Str.Common_Today),
        DueLabel.Tomorrow => Strings.Get(locale, Str.Common_Tomorrow),
        DueLabel.Weekday => LocaleFormat.WeekdayName(locale, due.Day.DayOfWeek, NameWidth.Short),
        _ => ShortDate(locale, due.Day),
    };

    /// <summary>
    /// [S-789] The label with its date, for a due column with room for it: <c>Overdue · 2 Oct</c>,
    /// <c>Today</c>, <c>Tomorrow</c>, <c>Thu 8 Oct</c>. Today and tomorrow need no date to be exact.
    /// </summary>
    public static string WideText(Due due) => WideText(Strings.Current, due);

    /// <summary><see cref="WideText(Due)"/> in an explicit language.</summary>
    public static string WideText(Locale locale, Due due) => due.Label switch
    {
        DueLabel.Overdue => Strings.Format(locale, Str.WidgetsC_Tasks_OverdueOn, ("date", ShortDate(locale, due.Day))),
        DueLabel.Today => Strings.Get(locale, Str.Common_Today),
        DueLabel.Tomorrow => Strings.Get(locale, Str.Common_Tomorrow),
        _ => LongText(locale, due),
    };

    /// <summary>[S-780] Whether a due date is worth a line on a tile with room for almost nothing.</summary>
    public static bool IsUrgent(Due due) => due.Label is DueLabel.Overdue or DueLabel.Today;

    /// <summary>The whole date, for a tooltip: <c>Wed, Oct 14</c> / <c>Mi., 14. Okt.</c>.</summary>
    public static string LongText(Due due) => LongText(Strings.Current, due);

    /// <summary><see cref="LongText(Due)"/> in an explicit language.</summary>
    public static string LongText(Locale locale, Due due) => LocaleFormat.Date(locale, due.Day, DateStyle.WeekdayMonthDayShort);

    /// <summary>A day as the list writes it, <c>Oct 14</c> / <c>14. Okt.</c>: the language's short day and month.</summary>
    public static string ShortDate(DateOnly day) => ShortDate(Strings.Current, day);

    /// <summary><see cref="ShortDate(DateOnly)"/> in an explicit language.</summary>
    public static string ShortDate(Locale locale, DateOnly day) => LocaleFormat.Date(locale, day, DateStyle.MonthDayShort);

    /// <summary>
    /// [S-597] A day as the one encoding both providers read back as the day meant: bare
    /// <c>yyyy-MM-dd</c>, wrapped in each provider's field shape by <see cref="TaskWire"/>.
    /// </summary>
    public static string Wire(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
