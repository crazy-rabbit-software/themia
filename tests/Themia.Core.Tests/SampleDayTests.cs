using Themia.Core.Astronomy;
using Themia.Sdk.Samples;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-2050]..[S-2055] The Debug renders' one sample day: one moment, one place, one schedule, one cast.</summary>
public class SampleDayTests
{
    private static readonly DateOnly Day = new(2026, 11, 12);

    [Fact]
    public void S2050_the_moment_is_10_42_on_the_day_in_the_zone_with_that_days_offset()
    {
        var zone = TimeZoneInfo.CreateCustomTimeZone("plus-two", TimeSpan.FromHours(2), "plus-two", "plus-two");

        var moment = SampleDay.On(Day, zone);

        Assert.Equal(new DateTime(2026, 11, 12, 10, 42, 0), moment.DateTime);
        Assert.Equal(TimeSpan.FromHours(2), moment.Offset);
        Assert.Equal(SampleDay.On(SampleDay.Today, TimeZoneInfo.Local), SampleDay.Now);
        Assert.Equal(SampleDay.Now.AddMinutes(-4), SampleDay.MinutesAgo(4));
    }

    [Fact]
    public void S2051_the_places_sunrise_and_sunset_are_the_astronomys_and_every_forecast_day_carries_its_own()
    {
        var (sunrise, sunset) = SamplePlace.Sun(SampleDay.Today);
        var times = SolarPosition.TimesFor(SampleDay.TodayAt(new TimeOnly(12, 0)), SamplePlace.Latitude, SamplePlace.Longitude);

        Assert.Equal(TimeOnly.FromDateTime(times.Sunrise!.Value.ToLocalTime().DateTime), sunrise);
        Assert.Equal(TimeOnly.FromDateTime(times.Sunset!.Value.ToLocalTime().DateTime), sunset);
        Assert.All(SampleWeather.Days(7), d => Assert.Equal(SamplePlace.Sun(d.Day), (d.Sunrise, d.Sunset)));
    }

    [Fact]
    public void S2052_one_reading_now_inside_todays_low_and_high_with_the_rain_today_names_on_its_way()
    {
        var reading = SampleWeather.Reading();
        var today = Assert.IsType<Themia.Core.Widgets.ForecastDay>(reading.Today);

        Assert.Equal(SampleDay.Today, today.Day);
        Assert.InRange(SampleWeather.Temperature, today.Low!.Value, today.High!.Value);
        Assert.Equal(SampleWeather.UvIndex, reading.UvIndex);
        Assert.InRange(SampleWeather.UvIndex, 0, today.UvMax!.Value);
        Assert.True(today.Precipitation > 0 && today.PrecipitationChance >= 50, "today's forecast names the rain");
        Assert.Equal(0, SampleWeather.NextQuarters[0]);
        Assert.Equal(today.Precipitation!.Value, SampleWeather.NextQuarters.Sum(), 6);
        Assert.Equal(reading.Days, SampleWeather.Days(7));
    }

    [Fact]
    public void S2053_the_cast_is_invented_on_example_domains()
    {
        Assert.All([SampleCast.Owner, .. SampleCast.Contacts], p => Assert.EndsWith(".example", p.Email, StringComparison.Ordinal));
        Assert.EndsWith(".example", SampleCast.OwnerHomeEmail, StringComparison.Ordinal);
        Assert.InRange(SampleCast.Contacts.Count, 8, 12);
        Assert.Equal(SampleCast.Contacts.Count, SampleCast.Contacts.Select(p => p.Name).Distinct().Count());
    }

    [Theory]
    [InlineData(2026, 11, 9)]
    [InlineData(2026, 11, 12)]
    [InlineData(2026, 11, 14)]
    [InlineData(2026, 11, 15)]
    public void S2054_the_next_meeting_after_the_moment_is_the_design_review_eighteen_minutes_on(int year, int month, int day)
    {
        var today = new DateOnly(year, month, day);
        var zone = TimeZoneInfo.Local;
        var now = SampleDay.On(today, zone);

        var next = SampleSchedule.Events(today, zone)
            .Where(e => !e.AllDay && e.Start > now)
            .OrderBy(e => e.Start)
            .First();

        Assert.Equal(SampleSchedule.NextMeetingId, next.Id);
        Assert.Equal(SampleSchedule.NextMeetingSubject, next.Subject);
        Assert.Equal(TimeSpan.FromMinutes(18), next.Start - now);
        Assert.Equal(SampleCast.Ana.Name, next.Organizer);
        Assert.NotNull(next.JoinUrl);
    }

    [Fact]
    public void S2054_every_day_near_today_has_one_to_five_events_and_a_workday_two_to_five()
    {
        var events = SampleSchedule.Events(Day, TimeZoneInfo.Local);
        var trips = events.Where(e => e.AllDay).ToList();

        for (var offset = -14; offset <= 14; offset++)
        {
            var day = Day.AddDays(offset);
            if (trips.Any(t => day >= t.FirstDay && day < t.EndDay))
            {
                continue;
            }

            var timed = events.Count(e => !e.AllDay && DateOnly.FromDateTime(e.Start.LocalDateTime) == day);
            var workday = day.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);
            Assert.InRange(timed, workday ? 2 : 1, 5);
        }

        Assert.Equal(events.Count, events.Select(e => e.Id).Distinct().Count());
        Assert.True(trips.Count(t => t.EndDay.DayNumber - t.FirstDay.DayNumber > 1 && Math.Abs(t.FirstDay.DayNumber - Day.DayNumber) < 31) >= 3, "the month keeps a few trips of several days");
    }

    [Fact]
    public void S2054_a_free_today_has_nothing_today_and_the_rest_of_the_week_unchanged()
    {
        var zone = TimeZoneInfo.Local;
        var busy = SampleSchedule.Events(Day, zone);
        var free = SampleSchedule.Events(Day, zone, freeToday: true);

        bool Today(Themia.Core.Office.CalendarEvent e) => !e.AllDay && DateOnly.FromDateTime(e.Start.LocalDateTime) == Day;
        Assert.DoesNotContain(free, Today);
        Assert.Equal(5, busy.Count(Today));
        Assert.Equal(busy.Where(e => !Today(e)).Select(e => e.Id), free.Select(e => e.Id));
    }

    [Fact]
    public void S2054_the_calendar_seam_filters_to_the_range_and_holds_the_problem_from_twenty_minutes_ago()
    {
        var data = new SampleCalendarData(stale: true);
        var today = SampleDay.Today;

        var slice = data.Slice(today, today, TimeZoneInfo.Local);

        Assert.Contains(slice.Events, e => e.Id == SampleSchedule.NextMeetingId);
        Assert.All(slice.Events, e => Assert.True(e.AllDay ? e.FirstDay <= today && e.EndDay > today : e.Start.LocalDateTime.Date <= today.ToDateTime(TimeOnly.MinValue) && e.End.LocalDateTime.Date >= today.ToDateTime(TimeOnly.MinValue)));
        var problem = Assert.Single(slice.Problems);
        Assert.Equal(SampleSchedule.WorkAccount, problem.Account);
        Assert.Equal(SampleDay.MinutesAgo(20), problem.At);
        Assert.Empty(new SampleCalendarData(empty: true).Slice(today, today.AddDays(30), TimeZoneInfo.Local).Events);
    }

    [Fact]
    public void S2056_a_sample_picture_is_the_same_every_run_opaque_and_unlike_the_others()
    {
        var first = SamplePictures.Pixels(0, 32, 20);

        Assert.Equal(first, SamplePictures.Pixels(0, 32, 20));
        Assert.Equal(32 * 20 * 4, first.Length);
        Assert.All(Enumerable.Range(0, 32 * 20), i => Assert.Equal(255, first[(i * 4) + 3]));
        Assert.Equal(SamplePictures.Count, Enumerable.Range(0, SamplePictures.Count)
            .Select(i => Convert.ToHexString(SamplePictures.Pixels(i, 32, 20))).Distinct().Count());
        Assert.Equal(SamplePictures.Names.Count, SamplePictures.Names.Distinct().Count());
    }

    [Fact]
    public void S2056_the_sample_folder_is_written_fresh_with_each_files_size_and_age_and_nothing_else()
    {
        var name = "test-" + Guid.NewGuid().ToString("n");
        var path = SampleFiles.Write(name, [new("a.txt", 10, 1), new("Sub", 0, 2, IsFolder: true)]);
        Assert.NotNull(path);

        try
        {
            File.WriteAllText(Path.Combine(path, "stray.txt"), "x");
            path = SampleFiles.Write(name, SampleFiles.Documents);
            Assert.NotNull(path);

            Assert.False(File.Exists(Path.Combine(path, "stray.txt")));
            Assert.Equal(SampleFiles.Documents.Count, Directory.GetFileSystemEntries(path).Length);
            var pptx = SampleFiles.Documents.First(f => f.Name.EndsWith(".pptx", StringComparison.Ordinal));
            var info = new FileInfo(Path.Combine(path, pptx.Name));
            Assert.Equal(pptx.Size, info.Length);
            Assert.Equal(SampleDay.Now.AddHours(-pptx.HoursAgo).UtcDateTime, info.LastWriteTimeUtc, TimeSpan.FromSeconds(2));
        }
        finally
        {
            Directory.Delete(path!, recursive: true);
        }
    }

    [Fact]
    public void S2055_the_week_planners_errands_are_the_lists_due_by_today_overdue_first()
    {
        var due = SampleTasks.DueByToday;

        Assert.InRange(SampleTasks.Errands.Count, 8, 12);
        Assert.Equal(-2, due[0].DueInDays);
        Assert.All(due, t => Assert.True(t.DueInDays <= 0));
        Assert.Equal(SampleTasks.Errands.Count(t => t.DueInDays <= 0), due.Count);
        Assert.Contains(due, t => t.Title.Contains(SampleCast.Mia.First, StringComparison.Ordinal));
    }
}
