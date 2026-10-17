using Themia.Core.Astronomy;
using Themia.Core.Office;
using Themia.Sdk;

namespace Themia.Core.Widgets;

/// <summary>[S-6051] One day of the Moon's phase month: the date, its phase (0 new, 0.5 full) and its name, and whether it is today.</summary>
public readonly record struct MoonDay(DateOnly Date, double Phase, MoonPhaseName Name, bool Today);

/// <summary>
/// [S-6051] The Moon's Phase month style: every day of the month holding now, each with the moon's
/// phase at that day's local noon in the machine's zone (the middle of the day, so a phase turning
/// at 01:00 or 23:00 belongs to the day most of it falls in), today marked.
/// </summary>
public static class MoonMonth
{
    public static IReadOnlyList<MoonDay> Days(DateTimeOffset now, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);

        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
        var first = new DateOnly(today.Year, today.Month, 1);
        var count = DateTime.DaysInMonth(today.Year, today.Month);
        var days = new MoonDay[count];

        for (var i = 0; i < count; i++)
        {
            var day = first.AddDays(i);
            var start = CalendarDays.LocalMidnight(day, zone);
            var end = CalendarDays.LocalMidnight(day.AddDays(1), zone);
            var noon = start + ((end - start) / 2);
            var moon = MoonPhase.At(noon);
            days[i] = new MoonDay(day, moon.Phase, moon.Name, day == today);
        }

        return days;
    }
}
