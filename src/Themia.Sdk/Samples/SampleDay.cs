#if DEBUG
namespace Themia.Sdk.Samples;

/// <summary>
/// Debug renders only ([S-2050]): the one moment every widget's sample is drawn at, the day the render
/// runs on at 10:42 local time. A preview that shows a time relative to now, "today", a countdown or
/// an age computes it from here, never from the real clock, so a render at 9 am and one at 3 pm draw
/// the same picture (apart from the calendar date).
/// </summary>
public static class SampleDay
{
    /// <summary>The time of day of the sample moment: the clock's sample time.</summary>
    public static TimeOnly Time { get; } = new(10, 42);

    /// <summary>The day the render runs on, local.</summary>
    public static DateOnly Today => DateOnly.FromDateTime(DateTime.Now);

    /// <summary>The sample moment: today at <see cref="Time"/>, local.</summary>
    public static DateTimeOffset Now => On(Today, TimeZoneInfo.Local);

    /// <summary>The sample moment on <paramref name="day"/> in <paramref name="zone"/>.</summary>
    public static DateTimeOffset On(DateOnly day, TimeZoneInfo zone) => At(day, Time, zone);

    /// <summary><paramref name="time"/> on <paramref name="day"/> in <paramref name="zone"/>, with that zone's offset on that day.</summary>
    public static DateTimeOffset At(DateOnly day, TimeOnly time, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);
        var local = day.ToDateTime(time, DateTimeKind.Unspecified);
        return new DateTimeOffset(local, zone.GetUtcOffset(local));
    }

    /// <summary><paramref name="time"/> today, local: a time on the sample day's schedule.</summary>
    public static DateTimeOffset TodayAt(TimeOnly time) => At(Today, time, TimeZoneInfo.Local);

    /// <summary>The sample moment less <paramref name="minutes"/>: when something happened, as an age.</summary>
    public static DateTimeOffset MinutesAgo(double minutes) => Now.AddMinutes(-minutes);

    /// <summary>The sample moment less <paramref name="hours"/>.</summary>
    public static DateTimeOffset HoursAgo(double hours) => Now.AddHours(-hours);
}
#endif
