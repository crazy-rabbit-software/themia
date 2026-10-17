using Themia.Core.Astronomy;
using Themia.Core.Office;
using Themia.Sdk;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-6046] The Sun's day bar at one instant: the local day from midnight to midnight as 0..1
/// across, the daylight between the sunrise and sunset ticks, and where now is.
/// </summary>
/// <param name="Rise">Sunrise across the bar, or null (polar day or night, or a sunrise that is not on this day).</param>
/// <param name="Set">Sunset across the bar, or null.</param>
/// <param name="Now">Now across the bar.</param>
/// <param name="Polar">Polar day lights the whole bar, polar night none of it.</param>
public readonly record struct SunDayBarPlan(double? Rise, double? Set, double Now, PolarState Polar)
{
    /// <summary>The lit stretch, as (from, to) across the bar; empty for polar night.</summary>
    public (double From, double To) Daylight => Polar switch
    {
        PolarState.Day => (0, 1),
        PolarState.Night => (0, 0),
        _ => (Rise ?? 0, Set ?? 1),
    };

    /// <summary>Whether now is in daylight: the marker is the accent then, the disabled accent at night.</summary>
    public bool NowLit => Polar == PolarState.Day
        || (Polar == PolarState.None && Now >= Daylight.From && Now <= Daylight.To);
}

/// <summary>
/// [S-6046] Where the day bar's marks fall. The day is the local calendar day holding now, read in
/// the machine's zone (a 23- or 25-hour day is that long across, never 24 hours scaled); an instant
/// is placed by its share of that day in real time, so the ticks line up with the clock on the wall.
/// </summary>
public static class SunDayBar
{
    public static SunDayBarPlan Of(DateTimeOffset now, SunTimes times, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);

        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
        var start = CalendarDays.LocalMidnight(today, zone);
        var end = CalendarDays.LocalMidnight(today.AddDays(1), zone);

        return new SunDayBarPlan(
            Share(times.Sunrise, start, end),
            Share(times.Sunset, start, end),
            Share(now, start, end) ?? 0,
            times.Polar);
    }

    /// <summary>An instant's place across the day; null when it is absent or not on this day.</summary>
    public static double? Share(DateTimeOffset? at, DateTimeOffset start, DateTimeOffset end)
    {
        if (at is not { } instant || end <= start || instant < start || instant > end)
        {
            return null;
        }

        return (instant - start).Ticks / (double)(end - start).Ticks;
    }
}
