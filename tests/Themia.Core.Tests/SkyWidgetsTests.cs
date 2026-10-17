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

    // -----------------------------------------------------------------------------------------
    // [S-3100] Two kinds, two configs
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void S3100_sun_and_moon_are_two_kinds_each_with_its_own_config()
    {
        var sun = Assert.IsType<SunConfig>(WidgetConfig.CreateDefault(SunConfig.Descriptor.Id));
        var moon = Assert.IsType<MoonConfig>(WidgetConfig.CreateDefault(MoonConfig.Descriptor.Id));

        Assert.Equal(SunConfig.Descriptor.Id, sun.Kind);
        Assert.Equal(MoonConfig.Descriptor.Id, moon.Kind);
        Assert.DoesNotContain(ModuleCatalog.Widgets, w => w.Id.Value is "sunMoon" or "sunmoon");
    }

    [Fact]
    public void S3100_neither_needs_setting_up_they_start_at_a_place()
    {
        Assert.False(WidgetSetup.Needed(new SunConfig(), Themia.Core.Accounts.AccountFeatures.None));
        Assert.False(WidgetSetup.Needed(new MoonConfig(), Themia.Core.Accounts.AccountFeatures.None));
    }

    [Fact]
    public void S3100_the_sun_config_round_trips_with_its_place_and_display_settings()
    {
        var config = new SunConfig { Latitude = -33.87, Longitude = 151.21, Place = "Sydney", Style = SunStyle.DayBar, ShowEvents = false };

        var back = Assert.IsType<SunConfig>(RoundTrip(config));

        Assert.Equal(-33.87, back.Latitude);
        Assert.Equal(151.21, back.Longitude);
        Assert.Equal("Sydney", back.Place);
        Assert.Equal(SunStyle.DayBar, back.Style);
        Assert.False(back.ShowEvents);
    }

    [Fact]
    public void S3100_the_moon_config_round_trips_with_its_place()
    {
        var config = new MoonConfig { Latitude = 64.15, Longitude = -21.94, Place = "Reykjavík" };

        var back = Assert.IsType<MoonConfig>(RoundTrip(config));

        Assert.Equal(64.15, back.Latitude);
        Assert.Equal(-21.94, back.Longitude);
        Assert.Equal("Reykjavík", back.Place);
    }

    [Fact]
    public void S3100_clone_copies_every_field()
    {
        var sun = (SunConfig)new SunConfig { Latitude = 1, Longitude = 2, Place = "P", Style = SunStyle.Times, ShowEvents = false }.Clone();
        var moon = (MoonConfig)new MoonConfig { Latitude = 3, Longitude = 4, Place = "Q", Style = MoonStyle.PhaseMonth }.Clone();

        Assert.Equal((1d, 2d, "P", SunStyle.Times, false), (sun.Latitude, sun.Longitude, sun.Place, sun.Style, sun.ShowEvents));
        Assert.Equal((3d, 4d, "Q", MoonStyle.PhaseMonth), (moon.Latitude, moon.Longitude, moon.Place, moon.Style));
    }

    // -----------------------------------------------------------------------------------------
    // [S-3101] Shared place, repaired loudly
    // -----------------------------------------------------------------------------------------

    [Theory]
    [InlineData("sun", "Sun")]
    [InlineData("moon", "Moon")]
    public void S3101_coordinates_out_of_range_go_back_to_the_default_place_together(string discriminator, string kind)
    {
        var config = (SkyPlaceConfig)ConfigJson.Read(kind, $$"""{ "kind": "{{discriminator}}", "latitude": 123, "longitude": 8, "place": "Nowhere" }""");

        Assert.Equal(SkyPlaceConfig.DefaultLatitude, config.Latitude);
        Assert.Equal(SkyPlaceConfig.DefaultLongitude, config.Longitude);
        Assert.Equal(SkyPlaceConfig.DefaultPlace, config.Place);
    }

    [Fact]
    public void S3101_a_good_place_is_left_alone_and_reports_no_repair()
        => Assert.Empty(new MoonConfig { Latitude = -90, Longitude = 180, Place = "South Pole" }.Repair());

    [Fact]
    public void S3101_an_over_long_place_name_is_cut_and_reported()
    {
        var config = new SunConfig { Place = new string('x', 300) };

        var repairs = config.Repair();

        Assert.Single(repairs);
        Assert.Equal(SkyPlaceConfig.MaxPlaceLength, config.Place.Length);
    }

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
    // [S-3105] The Moon widget's ladder
    // -----------------------------------------------------------------------------------------

    private static MoonPlan Plan(double width, double height)
    {
        var area = new WidgetArea(width, height);
        var compact = area.WidthClass == SizeClass.Compact || area.HeightClass <= SizeClass.Regular;
        return MoonLayout.Plan(area, compact ? 32 : 40, 15);
    }

    [Fact]
    public void S3105_the_disc_is_never_dropped_and_stays_inside_its_floor_and_ceiling()
    {
        foreach (var tier in WidgetTiers.All)
        {
            var plan = Plan(tier.Width, tier.Height - 40);
            Assert.InRange(plan.Disc, MoonLayout.DiscFloor, MoonLayout.DiscCeiling);
            Assert.Equal(0, plan.Disc % 2);
        }

        Assert.Equal(MoonLayout.DiscFloor, Plan(10, 10).Disc);
    }

    [Fact]
    public void S3105_a_tiny_tile_is_the_disc_and_a_tall_one_has_every_row()
    {
        var tiny = Plan(120, 60);
        Assert.Equal(0, tiny.Rows);
        Assert.False(tiny.Side);

        var tall = Plan(200, 360);
        Assert.True(tall.Name && tall.Percent);
        Assert.Equal(MoonLayout.Ladder.Count, tall.Rows);
    }

    [Fact]
    public void S3105_the_name_comes_before_the_percentage_and_both_before_the_rows()
    {
        // Just room for the disc and one line: the name, not the percentage (a Compact column, so
        // the short tile is not read as a strip).
        var plan = Plan(170, MoonLayout.DiscFloor + MoonLayout.Gap * 2 + MoonLayout.NameStep.LineHeight);

        Assert.True(plan.Name);
        Assert.False(plan.Percent);
        Assert.Equal(0, plan.Rows);
    }

    [Fact]
    public void S3105_growing_the_tile_only_ever_adds_parts()
    {
        // Within one arrangement: a column from the height a 320 px tile stops being a strip.
        var before = Plan(320, 161);

        for (var height = 162.0; height <= 600; height += 1)
        {
            var plan = Plan(320, height);
            Assert.False(plan.Side);
            Assert.True(plan.Rows >= before.Rows || plan.Rows == MoonLayout.Ladder.Count, $"rows at {height}");
            Assert.True(!before.Name || plan.Name, $"name at {height}");
            Assert.True(!before.Percent || plan.Percent, $"percent at {height}");
            Assert.True(plan.Disc >= MoonLayout.DiscFloor);
            before = plan;
        }
    }

    [Fact]
    public void S3105_a_wide_strip_puts_the_text_beside_the_disc()
    {
        var plan = Plan(480, 120);

        Assert.True(plan.Side);
        Assert.True(plan.Name);
        Assert.True(plan.Disc <= 120);
    }

    [Fact]
    public void S3105_the_rows_run_rise_set_then_the_next_full_or_new_moon()
        => Assert.Equal([MoonRow.Rise, MoonRow.Set, MoonRow.NextMajor], MoonLayout.Ladder);

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
