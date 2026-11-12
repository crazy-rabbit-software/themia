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
    public void S2056_a_price_series_is_a_seeded_walk_of_small_moves_ending_at_its_last_value()
    {
        var usd = SampleSeries.Walk("ecb-USD", 63, 1.1584, 0.0035);

        Assert.Equal(usd, SampleSeries.Walk("ecb-USD", 63, 1.1584, 0.0035));
        Assert.NotEqual(usd, SampleSeries.Walk("ecb-GBP", 63, 1.1584, 0.0035));
        Assert.Equal(63, usd.Count);
        Assert.Equal(1.1584, usd[^1], 10);
        var steps = usd.Zip(usd.Skip(1), (a, b) => Math.Abs(Math.Log(b / a))).ToList();
        Assert.All(steps, s => Assert.True(s <= (4 * 0.0035) + (0.03 * 0.2), $"step {s}"));
        Assert.True(steps.Count(s => s > 0.0005) > steps.Count / 2, "it moves most days");
        Assert.True(steps.Zip(steps.Skip(1), (a, b) => Math.Abs(a - b)).Distinct().Count() > 50, "not a regular curve");
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
        Assert.Equal(SamplePictures.Count, SamplePictures.Names.Count);
    }

    [Fact]
    public void S2210_every_sample_picture_has_one_unique_slug_of_its_names_words()
    {
        Assert.Equal(SamplePictures.Names.Count, SamplePictures.Slugs.Count);
        Assert.Equal(SamplePictures.Slugs.Count + 1, SamplePictures.WallpaperSlugs.Distinct(StringComparer.Ordinal).Count());
        Assert.Contains(SamplePictures.CoverSlug, SamplePictures.WallpaperSlugs);
        Assert.All(SamplePictures.Names.Zip(SamplePictures.Slugs), pair =>
        {
            var words = System.IO.Path.GetFileNameWithoutExtension(pair.First).ToLowerInvariant().Split(' ');
            Assert.Equal(string.Join('-', words), pair.Second);
        });
    }

    [Fact]
    public void S2210_a_sample_wallpaper_is_painted_by_its_slug_at_the_size_asked_and_an_unknown_slug_is_none()
    {
        for (var i = 0; i < SamplePictures.Count; i++)
        {
            Assert.Equal(SamplePictures.Pixels(i, 48, 27), SamplePictures.Wallpaper(SamplePictures.Slugs[i], 48, 27));
        }

        Assert.Equal(SamplePictures.AlbumCover(48, 27), SamplePictures.Wallpaper(SamplePictures.CoverSlug, 48, 27));
        Assert.Null(SamplePictures.Wallpaper("nowhere", 48, 27));
    }

    [Fact]
    public void S2056_a_sample_picture_is_the_same_at_every_size_and_tells_its_landscape_apart_even_as_a_thumbnail()
    {
        // Thumbnails and odd shapes repeat byte for byte too (the rows are painted in parallel).
        Assert.Equal(SamplePictures.Pixels(7, 64, 64), SamplePictures.Pixels(7, 64, 64));
        Assert.Equal(SamplePictures.Pixels(3, 33, 91), SamplePictures.Pixels(3, 33, 91));
        Assert.Equal(SamplePictures.Pixels(1, 16, 10), SamplePictures.Pixels(1 + SamplePictures.Count, 16, 10));

        // Each picture differs from every other by more than grain: compare their mean colours at 64 px.
        var means = Enumerable.Range(0, SamplePictures.Count).Select(i => Mean(SamplePictures.Pixels(i, 64, 64))).ToList();
        for (var a = 0; a < means.Count; a++)
        {
            for (var b = a + 1; b < means.Count; b++)
            {
                var d = Math.Abs(means[a].R - means[b].R) + Math.Abs(means[a].G - means[b].G) + Math.Abs(means[a].B - means[b].B);
                Assert.True(d > 6, $"{SamplePictures.Names[a]} and {SamplePictures.Names[b]} look alike ({d:F1})");
            }
        }

        var cover = SamplePictures.AlbumCover(48, 48);
        Assert.Equal(cover, SamplePictures.AlbumCover(48, 48));
        Assert.All(Enumerable.Range(0, 48 * 48), i => Assert.Equal(255, cover[(i * 4) + 3]));
        Assert.All(Enumerable.Range(0, SamplePictures.Count), i => Assert.NotEqual(cover, SamplePictures.Pixels(i, 48, 48)));

        static (double R, double G, double B) Mean(byte[] bgra)
        {
            double r = 0, g = 0, b = 0;
            for (var i = 0; i < bgra.Length; i += 4)
            {
                b += bgra[i];
                g += bgra[i + 1];
                r += bgra[i + 2];
            }

            var n = bgra.Length / 4.0;
            return (r / n, g / n, b / n);
        }
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
