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
    int WeekDays = CalendarPeriod.WeekDefaultDays,
    WeekFirstDay WeekFrom = WeekFirstDay.Week,
    int AgendaDays = CalendarPeriod.AgendaDefaultDays)
{
    /// <summary>[S-3637] A whole week of day columns, the week's default.</summary>
    public const int WeekDefaultDays = 7;

    public const int WeekMinDays = 1;

    public const int WeekMaxDays = 14;

    /// <summary>[S-3637] A working week, which settings name beside the whole one.</summary>
    public const int WorkWeekDays = 5;

    public const int AgendaDefaultCount = 20;

    /// <summary>[S-3636] The agenda looks this many days ahead, today included, unless set otherwise.</summary>
    public const int AgendaDefaultDays = 30;

    public const int AgendaMinDays = 1;

    /// <summary>[S-3636] A year: as far as the shared source keeps events ([S-588]).</summary>
    public const int AgendaMaxDays = 365;

    /// <summary>[S-3636] The ranges settings offer by name: today, today and tomorrow, 3, 7 and 30 days; any other is custom.</summary>
    public static IReadOnlyList<int> AgendaDayPresets { get; } = [1, 2, 3, 7, 30];

    /// <summary>The week calendar's columns, inside their range whatever was passed.</summary>
    public int Days => Math.Clamp(WeekDays, WeekMinDays, WeekMaxDays);

    /// <summary>The agenda's days, inside their range whatever was passed.</summary>
    public int AgendaLength => Math.Clamp(AgendaDays, AgendaMinDays, AgendaMaxDays);

    /// <summary>A week start alone: a whole week from it, and the agenda's default range.</summary>
    public static implicit operator CalendarPeriod(WeekStartDay start) => FromWeekStartDay(start);

    /// <summary>The named form of the conversion from a week start.</summary>
    public static CalendarPeriod FromWeekStartDay(WeekStartDay start) => new(start);
}
