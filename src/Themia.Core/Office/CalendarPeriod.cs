using Themia.Core.Model;

namespace Themia.Core.Office;

/// <summary>
/// [S-568] What, beside its kind and the day it is anchored on, decides the days a calendar layout
/// covers: the week start ([S-564]), the week calendar's own length and first column ([S-2719]) and
/// the agenda's date range ([S-3636]). A week start alone is a whole week and the agenda's default.
/// </summary>
/// <param name="WeekStart">The shared regional week start ([S-3770]).</param>
/// <param name="WeekDays">The week calendar's day columns (1 to 14).</param>
/// <param name="WeekFrom">Whether the week calendar's first column is the week's first day or today.</param>
/// <param name="AgendaDays">The days the agenda covers, today first (1 to 365).</param>
public readonly record struct CalendarPeriod(
    WeekStartDay WeekStart,
    int WeekDays = WeekCalendarConfig.DefaultDays,
    WeekFirstDay WeekFrom = WeekFirstDay.Week,
    int AgendaDays = AgendaConfig.DefaultDays)
{
    /// <summary>The week calendar's columns, inside their range whatever was passed.</summary>
    public int Days => Math.Clamp(WeekDays, WeekCalendarConfig.MinDays, WeekCalendarConfig.MaxDays);

    /// <summary>The agenda's days, inside their range whatever was passed.</summary>
    public int AgendaLength => Math.Clamp(AgendaDays, AgendaConfig.MinDays, AgendaConfig.MaxDays);

    /// <summary>A week start alone: a whole week from it, and the agenda's default range.</summary>
    public static implicit operator CalendarPeriod(WeekStartDay start) => FromWeekStartDay(start);

    /// <summary>The named form of the conversion from a week start.</summary>
    public static CalendarPeriod FromWeekStartDay(WeekStartDay start) => new(start);
}
