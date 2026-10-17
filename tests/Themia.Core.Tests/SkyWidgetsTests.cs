using System.Text.Json;
using Themia.Core.Astronomy;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>The Sun and Moon widgets, split from Sun &amp; moon (S-3100..S-3106).</summary>
public class SkyWidgetsTests
{
    private const double Zurich = 47.37;
    private const double ZurichEast = 8.54;

    [Fact]
    public void S6045_an_unknown_sun_style_is_repaired_to_the_arc()
    {
        var config = (SunConfig)ConfigJson.Read("Sun", """{ "kind": "sun", "style": "numbers" }""");

        Assert.Equal(SunStyle.Arc, config.Style);
    }

    // -----------------------------------------------------------------------------------------
    // [S-3104] Moonrise and moonset
    // -----------------------------------------------------------------------------------------

    private static double Altitude(DateTimeOffset at, double latitude, double longitude)
        => SkyPath.PositionOf(MoonPhase.SublunarPoint(at), latitude, longitude).AltitudeDegrees;

    [Fact]
    public void S3104_rise_and_set_are_found_after_now_and_are_real_horizon_crossings()
    {
        var start = new DateTimeOffset(2026, 10, 11, 0, 0, 0, TimeSpan.Zero);

        for (var day = 0; day < 30; day++)
        {
            var now = start.AddHours(day * 23.3);
            var times = MoonPhase.NextRiseSet(now, Zurich, ZurichEast);

            var rise = Assert.NotNull(times.Rise);
            var set = Assert.NotNull(times.Set);
            Assert.True(rise > now && set > now);
            Assert.True(rise - now < TimeSpan.FromHours(26) && set - now < TimeSpan.FromHours(26), $"day {day}");

            // Below the rise altitude a minute before, above it a minute after; the other way at the set.
            Assert.True(Altitude(rise.AddMinutes(-1), Zurich, ZurichEast) < MoonPhase.RiseAltitudeDegrees);
            Assert.True(Altitude(rise.AddMinutes(1), Zurich, ZurichEast) > MoonPhase.RiseAltitudeDegrees);
            Assert.True(Altitude(set.AddMinutes(-1), Zurich, ZurichEast) > MoonPhase.RiseAltitudeDegrees);
            Assert.True(Altitude(set.AddMinutes(1), Zurich, ZurichEast) < MoonPhase.RiseAltitudeDegrees);
        }
    }

    [Fact]
    public void S3104_successive_moonrises_come_a_lunar_day_apart()
    {
        var first = MoonPhase.NextRiseSet(new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero), Zurich, ZurichEast).Rise!.Value;
        var second = MoonPhase.NextRiseSet(first.AddMinutes(1), Zurich, ZurichEast).Rise!.Value;

        // About 24 h 50 min on average; the moon's changing declination swings it by most of an hour.
        Assert.InRange((second - first).TotalHours, 24.2, 25.6);
    }

    [Fact]
    public void S3104_a_full_moon_rises_around_sunset()
    {
        // The full moon stands opposite the sun, so it comes up as the sun goes down.
        var full = MoonPhase.NextMajor(new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero));
        if (full.Name != MoonPhaseName.FullMoon)
        {
            full = MoonPhase.NextMajor(full.At.AddHours(1));
        }

        var sunset = SolarPosition.TimesFor(full.At, Zurich, ZurichEast).Sunset!.Value;
        var rise = MoonPhase.NextRiseSet(sunset.AddHours(-6), Zurich, ZurichEast).Rise!.Value;

        Assert.InRange((rise - sunset).TotalHours, -2, 2);
    }

    [Fact]
    public void S3104_near_the_pole_a_moon_that_stays_up_has_neither_rise_nor_set()
    {
        // Find an instant when the moon is well north, then look from close to the North Pole.
        var at = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        while (MoonPhase.SublunarPoint(at).Latitude < 15)
        {
            at = at.AddHours(6);
        }

        var times = MoonPhase.NextRiseSet(at, 89.5, 0);

        Assert.Null(times.Rise);
        Assert.Null(times.Set);
        Assert.Null(times.Next);
    }

    [Fact]
    public void S3104_next_is_the_earlier_of_the_two()
    {
        var rise = new DateTimeOffset(2026, 1, 1, 6, 0, 0, TimeSpan.Zero);
        var set = rise.AddHours(3);

        Assert.Equal((rise, true), new MoonTimes(rise, set).Next);
        Assert.Equal((set, false), new MoonTimes(set.AddHours(9), set).Next);
        Assert.Equal((set, false), new MoonTimes(null, set).Next);
    }

    // -----------------------------------------------------------------------------------------
    // [S-3106] When a rise or set falls
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void S3106_today_is_the_time_alone_and_another_day_carries_its_weekday()
    {
        var noon = new DateTimeOffset(DateTime.Today.AddHours(12));

        var today = SkyText.EventTime(Locale.En, noon.AddHours(1), noon);
        var tomorrow = SkyText.EventTime(Locale.En, noon.AddDays(1), noon);

        Assert.Equal(LocaleFormat.Time(Locale.En, TimeOnly.FromDateTime(noon.AddHours(1).DateTime)), today);
        Assert.StartsWith(LocaleFormat.WeekdayName(Locale.En, noon.AddDays(1).DayOfWeek, NameWidth.Short) + " ", tomorrow);
        Assert.EndsWith(LocaleFormat.Time(Locale.En, TimeOnly.FromDateTime(noon.DateTime)), tomorrow);
    }

    [Fact]
    public void S3106_no_event_is_an_em_dash()
        => Assert.Equal("—", SkyText.EventTime(Locale.En, null, DateTimeOffset.Now));

    private static WidgetConfig RoundTrip(WidgetConfig config)
    {
        var json = JsonSerializer.Serialize(config, StateJson.WidgetConfig);
        return JsonSerializer.Deserialize(json, StateJson.WidgetConfig)!;
    }
}
