using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Widget.Clock.Tests;

/// <summary>
/// Package PJ: the clock's parity with the original ([S-1480]..[S-1486]): zones, the picker, the
/// label, the offset line and repair. Moved with the widget from Core's DeskParityTests, unchanged in
/// what they assert.
/// </summary>
public sealed class ClockParityTests
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
    public void S1482_the_label_is_the_city_of_a_chosen_zone_else_none()
    {
        Assert.Null(ClockFormatter.Label(new ClockConfig()));
        Assert.Equal("Tokyo", ClockFormatter.Label(new ClockConfig { TimeZoneId = "Asia/Tokyo" }));
        Assert.Null(ClockFormatter.Label(new ClockConfig { TimeZoneId = "Atlantis/Capital" }));
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
        };

        var repairs = config.Repair();

        Assert.Equal(2, repairs.Count);
        Assert.Equal(ClockOffsetMode.None, config.OffsetMode);
        Assert.Null(config.TimeZoneId);
    }

    [Fact]
    public void S1486_a_sound_clock_needs_no_repair()
        => Assert.Empty(new ClockConfig { TimeZoneId = "Asia/Tokyo", Style = ClockStyle.Stacked }.Repair());

    [Fact]
    public void S1483_the_new_options_survive_a_clone()
    {
        var clone = (ClockConfig)new ClockConfig
        {
            Style = ClockStyle.Stacked,
            OffsetMode = ClockOffsetMode.Local,
            TimeZoneId = "Asia/Tokyo",
        }.Clone();

        Assert.Equal(ClockStyle.Stacked, clone.Style);
        Assert.Equal(ClockOffsetMode.Local, clone.OffsetMode);
        Assert.Equal("Asia/Tokyo", clone.TimeZoneId);
    }
}
