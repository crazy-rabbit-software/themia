using Xunit;
using Themia.Core.Astronomy;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.Core.Tests;

/// <summary>Package PJ: Clock, Sun &amp; moon, Quote and Scratchpad parity (S-1480..S-1499).</summary>
public sealed class DeskParityTests
{
    // ---------------------------------------------------------------------------------------------
    // Clock: zones
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void S1480_every_windows_zone_in_the_table_resolves_under_invariant_globalization()
    {
        var missing = ZoneCatalog.Choices
            .Select(entry => entry.WindowsId)
            .Distinct(StringComparer.Ordinal)
            .Where(id => !TimeZoneInfo.TryFindSystemTimeZoneById(id, out _))
            .ToList();

        Assert.Empty(missing);
    }

    [Fact]
    public void S1480_an_iana_id_does_not_resolve_on_its_own_which_is_why_the_table_exists()
        => Assert.False(TimeZoneInfo.TryFindSystemTimeZoneById("Asia/Tokyo", out _));

    [Theory]
    [InlineData("Asia/Tokyo", "Tokyo Standard Time")]
    [InlineData("Europe/Zurich", "W. Europe Standard Time")]
    [InlineData("America/New_York", "Eastern Standard Time")]
    [InlineData("Asia/Kolkata", "India Standard Time")]
    [InlineData("Asia/Calcutta", "India Standard Time")]
    [InlineData("Etc/UTC", "UTC")]
    [InlineData("asia/tokyo", "Tokyo Standard Time")]
    public void S1480_iana_ids_map_to_their_windows_zone(string iana, string windows)
        => Assert.Equal(windows, ZoneCatalog.Find(iana)?.WindowsId);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Mars/Olympus_Mons")]
    [InlineData("W. Europe Standard Time")]
    public void S1480_an_unknown_id_resolves_to_nothing_and_the_clock_falls_back_to_local(string? id)
    {
        Assert.Null(ZoneCatalog.Resolve(id));
        Assert.Equal(TimeZoneInfo.Local.Id, new ClockConfig { TimeZoneId = id }.ResolveTimeZone().Id);
    }

    [Fact]
    public void S1480_a_chosen_zone_drives_the_reading()
    {
        var config = new ClockConfig { TimeZoneId = "Asia/Tokyo" };
        var reading = ClockFormatter.Read(new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero), config, Locale.En, use24Hour: true);

        Assert.Equal("21:00", reading.Time);
    }

    [Fact]
    public void S1481_the_picker_lists_each_place_once_without_old_names_or_bare_offsets()
    {
        var ids = ZoneCatalog.Choices.Select(entry => entry.IanaId).ToList();

        Assert.Contains("Asia/Kolkata", ids);
        Assert.Contains("Europe/Kyiv", ids);
        Assert.Contains("Etc/UTC", ids);
        Assert.DoesNotContain("Asia/Calcutta", ids);
        Assert.DoesNotContain("Europe/Kiev", ids);
        Assert.DoesNotContain("Etc/GMT+12", ids);
        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
    }

    [Theory]
    [InlineData("America/Argentina/Buenos_Aires", "Buenos Aires", "America")]
    [InlineData("Europe/Zurich", "Zurich", "Europe")]
    [InlineData("Etc/UTC", "UTC", "")]
    public void S1481_a_zone_reads_as_its_city_and_region(string iana, string city, string region)
    {
        Assert.Equal(city, ZoneCatalog.City(iana));
        Assert.Equal(region, ZoneCatalog.Region(iana));
    }

    [Fact]
    public void S1481_search_matches_city_words_and_puts_prefix_matches_first()
    {
        var hits = ZoneCatalog.Search("york");
        Assert.Contains(hits, hit => hit.IanaId == "America/New_York");

        var buenos = ZoneCatalog.Search("buenos aires");
        Assert.Equal("America/Argentina/Buenos_Aires", buenos[0].IanaId);

        var z = ZoneCatalog.Search("zur");
        Assert.Equal("Europe/Zurich", z[0].IanaId);

        Assert.Equal(5, ZoneCatalog.Search(null, 5).Count);
        Assert.Empty(ZoneCatalog.Search("qqqq"));
    }

    // ---------------------------------------------------------------------------------------------
    // Clock: label, offset, repair
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void S1482_the_label_is_the_custom_one_else_the_city_of_a_chosen_zone_else_none()
    {
        Assert.Null(ClockFormatter.Label(new ClockConfig()));
        Assert.Equal("Tokyo", ClockFormatter.Label(new ClockConfig { TimeZoneId = "Asia/Tokyo" }));
        Assert.Equal("Office", ClockFormatter.Label(new ClockConfig { TimeZoneId = "Asia/Tokyo", CustomLabel = "  Office " }));
        Assert.Equal("Home", ClockFormatter.Label(new ClockConfig { CustomLabel = "Home" }));
        Assert.Null(ClockFormatter.Label(new ClockConfig { CustomLabel = "   " }));
    }

    [Theory]
    [InlineData(0, 0, ClockOffsetMode.Utc, "UTC")]
    [InlineData(120, 0, ClockOffsetMode.Utc, "UTC+2")]
    [InlineData(-180, 0, ClockOffsetMode.Utc, "UTC−3")]
    [InlineData(330, 0, ClockOffsetMode.Utc, "UTC+5:30")]
    [InlineData(-570, 0, ClockOffsetMode.Utc, "UTC−9:30")]
    [InlineData(540, 60, ClockOffsetMode.Local, "+8h local")]
    [InlineData(-300, 0, ClockOffsetMode.Local, "−5h local")]
    [InlineData(330, 120, ClockOffsetMode.Local, "+3:30h local")]
    [InlineData(120, 120, ClockOffsetMode.Local, "same as local")]
    public void S1485_the_offset_line_is_written_as_the_original_wrote_it(int zoneMinutes, int localMinutes, ClockOffsetMode mode, string expected)
        => Assert.Equal(expected, ClockFormatter.OffsetText(TimeSpan.FromMinutes(zoneMinutes), TimeSpan.FromMinutes(localMinutes), mode, Locale.En));

    [Fact]
    public void S1485_no_offset_line_when_the_mode_is_none()
    {
        Assert.Null(ClockFormatter.OffsetText(TimeSpan.FromHours(2), TimeSpan.Zero, ClockOffsetMode.None, Locale.En));
        Assert.Null(ClockFormatter.OffsetLine(DateTimeOffset.UtcNow, new ClockConfig(), Locale.En));
    }

    [Fact]
    public void S1486_repair_brings_a_hand_edited_clock_back_to_its_invariants()
    {
        var config = new ClockConfig
        {
            OffsetMode = (ClockOffsetMode)9,
            TimeZoneId = "Atlantis/Capital",
            CustomLabel = new string('x', 80),
        };

        var repairs = config.Repair();

        Assert.Equal(3, repairs.Count);
        Assert.Equal(ClockOffsetMode.None, config.OffsetMode);
        Assert.Null(config.TimeZoneId);
        Assert.Equal(ClockConfig.MaxLabelLength, config.CustomLabel!.Length);
    }

    [Fact]
    public void S1486_a_sound_clock_needs_no_repair()
        => Assert.Empty(new ClockConfig { TimeZoneId = "Asia/Tokyo", Style = ClockStyle.Stacked, CustomLabel = "Tokyo" }.Repair());

    [Fact]
    public void S1483_the_new_options_survive_a_clone()
    {
        var clone = (ClockConfig)new ClockConfig
        {
            Style = ClockStyle.Stacked,
            OffsetMode = ClockOffsetMode.Local,
            CustomLabel = "Home",
            TimeZoneId = "Asia/Tokyo",
        }.Clone();

        Assert.Equal(ClockStyle.Stacked, clone.Style);
        Assert.Equal(ClockOffsetMode.Local, clone.OffsetMode);
        Assert.Equal("Home", clone.CustomLabel);
        Assert.Equal("Asia/Tokyo", clone.TimeZoneId);
    }

    // ---------------------------------------------------------------------------------------------
    // Sun & moon
    // ---------------------------------------------------------------------------------------------

    private static readonly DateTimeOffset ZurichNoonOct = new(2026, 10, 4, 11, 0, 0, TimeSpan.Zero);

    [Fact]
    public void S1490_civil_twilight_brackets_sunrise_and_sunset()
    {
        var sun = SolarPosition.TimesFor(ZurichNoonOct, 47.37, 8.54);
        var twilight = SolarPosition.CivilTwilightFor(ZurichNoonOct, 47.37, 8.54);

        Assert.Equal(PolarState.None, twilight.Polar);
        Assert.True(twilight.Sunrise < sun.Sunrise);
        Assert.True(twilight.Sunset > sun.Sunset);

        // About half an hour at this latitude in October.
        var dawnGap = (sun.Sunrise!.Value - twilight.Sunrise!.Value).TotalMinutes;
        Assert.InRange(dawnGap, 25, 40);
    }

    [Fact]
    public void S1490_white_nights_have_no_civil_dusk()
    {
        // Saint Petersburg-ish, but further north: 65°N at midsummer never goes 6° under.
        var twilight = SolarPosition.CivilTwilightFor(new DateTimeOffset(2026, 6, 21, 12, 0, 0, TimeSpan.Zero), 65, 25);

        Assert.Equal(PolarState.Day, twilight.Polar);
        Assert.Null(twilight.Sunrise);
    }

    [Fact]
    public void S1490_solar_noon_sits_between_sunrise_and_sunset_at_the_noon_altitude()
    {
        var sun = SolarPosition.TimesFor(ZurichNoonOct, 47.37, 8.54);
        var noon = SolarPosition.NoonFor(ZurichNoonOct, 47.37, 8.54);

        Assert.True(noon.At > sun.Sunrise && noon.At < sun.Sunset);
        Assert.InRange((noon.At - sun.Sunrise!.Value) - (sun.Sunset!.Value - noon.At), TimeSpan.FromMinutes(-2), TimeSpan.FromMinutes(2));

        // Declination about -4.5° in early October: 90 - |47.37 + 4.5| ≈ 38°.
        Assert.InRange(noon.AltitudeDegrees, 36, 40);
    }

    [Fact]
    public void S1490_days_shorten_in_autumn_and_lengthen_in_spring_in_the_north()
    {
        var autumn = SolarPosition.DaylightChange(ZurichNoonOct, 47.37, 8.54);
        var spring = SolarPosition.DaylightChange(new DateTimeOffset(2026, 3, 20, 11, 0, 0, TimeSpan.Zero), 47.37, 8.54);

        Assert.NotNull(autumn);
        Assert.InRange(autumn!.Value.TotalMinutes, -4, -2);
        Assert.InRange(spring!.Value.TotalMinutes, 2, 4);
    }

    [Fact]
    public void S1490_no_daylight_change_inside_the_polar_circle()
        => Assert.Null(SolarPosition.DaylightChange(new DateTimeOffset(2026, 6, 21, 12, 0, 0, TimeSpan.Zero), 80, 15));

    [Theory]
    [InlineData(134, "+2m 14s")]
    [InlineData(-58, "−58s")]
    [InlineData(-61, "−1m 1s")]
    [InlineData(0.4, "same as yesterday")]
    public void S1490_the_daylight_change_reads_as_minutes_and_seconds(double seconds, string expected)
        => Assert.Equal(expected, SkyText.Change(Locale.En, TimeSpan.FromSeconds(seconds)));

    [Fact]
    public void S1490_the_sky_text_is_in_the_language_asked_for()
    {
        Assert.Equal("+2 min 14 s", SkyText.Change(Locale.De, TimeSpan.FromSeconds(134)));
        Assert.Equal("wie gestern", SkyText.Change(Locale.De, TimeSpan.Zero));
        Assert.Equal("in 6 h 45 min", SkyText.Countdown(Locale.De, TimeSpan.FromMinutes(405)));
        Assert.Equal("6時間45分後", SkyText.Countdown(Locale.Ja, TimeSpan.FromMinutes(405)));
        Assert.Equal("Mi., 7. Okt.", SkyText.ShortDate(Locale.De, new DateTimeOffset(2026, 10, 7, 9, 0, 0, TimeSpan.Zero)));
        Assert.Equal("Vollmond", MoonPhase.Label(Locale.De, MoonPhaseName.FullMoon));
    }

    [Theory]
    [InlineData(38.4, "38°")]
    [InlineData(-0.4, "0°")]
    [InlineData(-12.6, "-13°")]
    public void S1490_degrees_are_whole_and_never_minus_zero(double degrees, string expected)
        => Assert.Equal(expected, SkyText.Degrees(degrees));

    [Fact]
    public void S1490_daylight_and_countdown_text()
    {
        Assert.Equal("24h", SkyText.Daylight(Locale.En, new SunTimes(null, null, PolarState.Day)));
        Assert.Equal("0h", SkyText.Daylight(Locale.En, new SunTimes(null, null, PolarState.Night)));

        var rise = new DateTimeOffset(2026, 10, 4, 5, 28, 0, TimeSpan.Zero);
        Assert.Equal("11h 33m", SkyText.Daylight(Locale.En, new SunTimes(rise, rise.AddMinutes(693), PolarState.None)));

        Assert.Equal("in 6h 45m", SkyText.Countdown(Locale.En, TimeSpan.FromMinutes(405)));
        Assert.Equal("in 0m", SkyText.Countdown(Locale.En, TimeSpan.FromMinutes(-3)));
        Assert.Equal("Wed, Oct 7", SkyText.ShortDate(Locale.En, new DateTimeOffset(2026, 10, 7, 9, 0, 0, TimeSpan.Zero)));
    }

    [Fact]
    public void S1491_the_next_major_phase_is_ahead_and_lands_on_full_or_new()
    {
        var now = ZurichNoonOct;
        var next = MoonPhase.NextMajor(now);

        Assert.True(next.At > now);
        Assert.True(next.At - now <= TimeSpan.FromDays(15));

        var phase = MoonPhase.At(next.At).Phase;
        var target = next.Name == MoonPhaseName.FullMoon ? 0.5 : 0.0;
        var distance = Math.Min(Math.Abs(phase - target), 1 - Math.Abs(phase - target));
        Assert.True(distance < 0.001, $"phase {phase} at {next.At}");
    }

    [Fact]
    public void S1491_at_the_full_moon_itself_the_next_major_phase_is_the_new_moon()
    {
        var full = MoonPhase.NextMajor(ZurichNoonOct);
        var after = full.Name == MoonPhaseName.FullMoon ? MoonPhase.NextMajor(full.At) : MoonPhase.NextMajor(MoonPhase.NextMajor(full.At).At);

        Assert.True(after.At > full.At);
    }

    // ---------------------------------------------------------------------------------------------
    // Scratchpad
    // ---------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(1, 2)]
    [InlineData(3, 2)]
    [InlineData(3.1, 4)]
    [InlineData(6, 4)]
    [InlineData(7, 8)]
    [InlineData(24, 8)]
    public void S1497_a_width_marks_the_nearest_nib(double width, double nib)
        => Assert.Equal(nib, InkPalette.NearestWidth(width));

    [Fact]
    public void S1497_an_ink_matches_its_hex_however_it_was_written()
    {
        var blue = InkPalette.Inks[0];

        Assert.True(InkPalette.Matches(blue, "#0078d4"));
        Assert.True(InkPalette.Matches(blue, "0078D4"));
        Assert.False(InkPalette.Matches(blue, "#F43F5E"));
        Assert.False(InkPalette.Matches(blue, null));
        Assert.Equal(InkPalette.Inks.Count, InkPalette.Inks.Select(ink => ink.Hex).Distinct().Count());
    }
}
