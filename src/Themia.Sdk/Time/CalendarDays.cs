using Themia.Core.Model;

namespace Themia.Sdk;

/// <summary>
/// [S-564] Days and weeks as every widget counts them: which weekday a week starts on, the start of
/// a day's week, and a local midnight in a zone (a day is 23 or 25 hours across a clock change).
/// Pure; shared by the calendar's rules in the host and by any module.
/// </summary>
public static class CalendarDays
{
    /// <summary>[S-564] The weekday a week starting on <paramref name="start"/> begins with.</summary>
    public static DayOfWeek FirstDay(WeekStartDay start) => start switch
    {
        WeekStartDay.Sunday => DayOfWeek.Sunday,
        WeekStartDay.Saturday => DayOfWeek.Saturday,
        _ => DayOfWeek.Monday,
    };

    /// <summary>[S-564] Days to step back from <paramref name="day"/> to reach the first day of its week.</summary>
    public static int WeekStartOffset(DayOfWeek day, WeekStartDay start)
        => ((int)day - (int)FirstDay(start) + 7) % 7;

    public static DateOnly StartOfWeek(DateOnly day, WeekStartDay start) => day.AddDays(-WeekStartOffset(day.DayOfWeek, start));

    /// <summary>Midnight of <paramref name="day"/> in <paramref name="zone"/>, never throwing in a DST gap.</summary>
    public static DateTimeOffset LocalMidnight(DateOnly day, TimeZoneInfo zone)
    {
        var clock = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return new DateTimeOffset(clock, zone.GetUtcOffset(clock));
    }
}
