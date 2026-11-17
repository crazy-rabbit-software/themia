using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Sync;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-12700]..[S-12709] What the weather graphs share: the hourly replies' parsers on synthetic replies (never the
/// network), the frame's geometry and rules, the shared config, the sync's wants and the offline store.
/// </summary>
public sealed class WeatherGraphsTests
{
    /// <summary>A whole hour, 2026-11-16 09:00 UTC.</summary>
    private const long Hour = 1_763_283_600;

    private static string HoursJson(string fields, int count = 3, string daily = "")
        => "{\"hourly\":{\"time\":[" + string.Join(',', Enumerable.Range(0, count).Select(i => Hour + (i * 3600))) + "]," + fields + "}" + daily + "}";

    // ---- wire ----------------------------------------------------------------------------------

    [Fact]
    public void S12700_a_good_reply_parses_every_figure_and_the_daylight()
    {
        var json = HoursJson(
            "\"temperature_2m\":[9.4,10.1,11],\"apparent_temperature\":[7.6,8,9],\"precipitation\":[0,0.4,1.2],"
            + "\"precipitation_probability\":[10,60,80],\"uv_index\":[0.5,1.2,2],\"wind_speed_10m\":[14,16,18],"
            + "\"wind_gusts_10m\":[29,31,35],\"wind_direction_10m\":[240,250,260],\"relative_humidity_2m\":[84,80,76],\"pressure_msl\":[1012,1011.5,1011]",
            daily: ",\"daily\":{\"sunrise\":[" + (Hour - 7200) + "],\"sunset\":[" + (Hour + 28800) + "]}");

        var result = WeatherHoursWire.Parse(json);

        Assert.Equal(FetchStatus.Ok, result.Status);
        var hours = result.Value!;
        Assert.Equal(3, hours.Hours.Count);
        var h = hours.Hours[1];
        Assert.Equal((10.1, 8.0, 0.4, 60.0, 1.2), (h.Temperature!.Value, h.FeelsLike!.Value, h.Precipitation!.Value, h.PrecipitationChance!.Value, h.Uv!.Value));
        Assert.Equal((16.0, 31.0, 250.0, 80.0, 1011.5), (h.Wind!.Value, h.Gusts!.Value, h.WindDirection!.Value, h.Humidity!.Value, h.Pressure!.Value));
        Assert.Single(hours.Daylight);
        Assert.Equal(json, hours.Source);
    }

    [Fact]
    public void S12700_a_missing_or_impossible_figure_is_a_gap_not_a_zero()
    {
        var json = HoursJson("\"temperature_2m\":[9.4,null,\"warm\"],\"precipitation\":[-1,0.2],\"pressure_msl\":[400,1012,1013]");

        var hours = WeatherHoursWire.Parse(json).Value!.Hours;

        Assert.Equal([9.4, null, null], hours.Select(h => h.Temperature));
        Assert.Equal([null, 0.2, null], hours.Select(h => h.Precipitation));
        Assert.Equal([null, 1012.0, 1013.0], hours.Select(h => h.Pressure));
        Assert.All(hours, h => Assert.Null(h.Uv));
    }

    [Fact]
    public void S12700_hours_come_in_time_order_once_and_bad_stamps_are_dropped()
    {
        var json = "{\"hourly\":{\"time\":[" + (Hour + 3600) + "," + Hour + "," + Hour + ",\"x\",-5],\"temperature_2m\":[2,1,9,3,4]}}";

        var hours = WeatherHoursWire.Parse(json).Value!.Hours;

        Assert.Equal([1.0, 2.0], hours.Select(h => h.Temperature!.Value));
        Assert.True(hours[0].At < hours[1].At);
    }

    [Theory]
    [InlineData(null, FetchStatus.BadResponse)]
    [InlineData("not json", FetchStatus.BadResponse)]
    [InlineData("{\"error\":true,\"reason\":\"bad\"}", FetchStatus.BadResponse)]
    [InlineData("{\"hourly\":{}}", FetchStatus.BadResponse)]
    [InlineData("{\"hourly\":{\"time\":[]}}", FetchStatus.Empty)]
    public void S12700_a_reply_that_is_not_the_schema_is_refused(string? json, FetchStatus status)
        => Assert.Equal(status, WeatherHoursWire.Parse(json).Status);

    [Fact]
    public void S12701_air_hours_keep_hours_with_any_figure_and_drop_empty_ones()
    {
        var json = HoursJson("\"european_aqi\":[25,null,40],\"pm2_5\":[7.7,null,12],\"nitrogen_dioxide\":[11,null,null]");

        var air = WeatherHoursWire.ParseAir(json).Value!;

        Assert.Equal(2, air.Hours.Count);
        Assert.Equal([25.0, 40.0], air.Hours.Select(h => h.EuropeanAqi!.Value));
        Assert.Equal(11, air.Hours[0].NitrogenDioxide);
        Assert.Equal(FetchStatus.Empty, WeatherHoursWire.ParseAir(HoursJson("\"pm2_5\":[null,null,null]")).Status);
    }

    // ---- frame ---------------------------------------------------------------------------------

    private static readonly DateTimeOffset T0 = DateTimeOffset.FromUnixTimeSeconds(Hour);

    private static List<DateTimeOffset> Stamps(int count) => [.. Enumerable.Range(-1, count).Select(i => T0.AddHours(i))];

    [Fact]
    public void S12705_the_window_starts_at_the_hour_under_way_and_holds_its_hours_and_one()
    {
        var window = HourGraphRules.Window(Stamps(50), t => t, T0.AddMinutes(42), 24);

        Assert.Equal(25, window.Count);
        Assert.Equal(T0, window[0]);
        Assert.Equal(T0.AddHours(24), window[^1]);
    }

    [Fact]
    public void S12705_an_answer_too_old_for_now_gives_no_window_and_a_short_one_what_it_has()
    {
        Assert.Empty(HourGraphRules.Window(Stamps(5), t => t, T0.AddHours(10), 24));
        Assert.Equal(4, HourGraphRules.Window(Stamps(5), t => t, T0, 24).Count);
    }

    [Fact]
    public void S12705_the_scale_widens_a_flat_series_and_amounts_start_at_zero()
    {
        Assert.Equal((7.5, 12.5), HourGraphRules.Scale([10, 10.0, null], 5));
        Assert.Equal((2.0, 20.0), HourGraphRules.Scale([2, 20.0], 5));
        Assert.Equal((0.0, 2.0), HourGraphRules.Scale([0, 0.4], 0, fromZero: true, floorTop: 2));
        Assert.Equal((0.0, 9.0), HourGraphRules.Scale([0, 9.0], 0, fromZero: true, floorTop: 2));
        Assert.Null(HourGraphRules.Scale([null, null], 5));
    }

    [Fact]
    public void S12705_shares_extremes_and_runs_skip_gaps()
    {
        Assert.Equal(0.5, HourGraphRules.Share(5, (0, 10)));
        Assert.Equal(1.0, HourGraphRules.Share(15, (0, 10)));
        Assert.Null(HourGraphRules.Share(null, (0, 10)));
        Assert.Equal((1, 3), HourGraphRules.Extremes([null, 1, 4, 9, 9, null]));
        Assert.Null(HourGraphRules.Extremes([null, null]));
        Assert.Equal([(0, 1), (3, 3), (5, 6)], HourGraphRules.Runs([1, 2, null, 4, null, 6, 7]));
    }

    [Fact]
    public void S12705_a_temperature_is_drawn_in_the_shared_unit()
    {
        Assert.Equal(10, HourGraphRules.Temperature(10, TemperatureUnit.Celsius));
        Assert.Equal(50, HourGraphRules.Temperature(10, TemperatureUnit.Fahrenheit));
        Assert.Equal(-40, HourGraphRules.Temperature(-40, TemperatureUnit.Fahrenheit));
        Assert.Null(HourGraphRules.Temperature(null, TemperatureUnit.Fahrenheit));
        Assert.Null(HourGraphRules.Temperature(double.NaN, TemperatureUnit.Celsius));
    }

    [Fact]
    public void S12703_slots_sit_on_their_cells_middles_and_moments_between_them()
    {
        Assert.Equal(5, HourGraphRules.X(0, 10, 100));
        Assert.Equal(95, HourGraphRules.X(9, 10, 100));
        Assert.Equal(10, HourGraphRules.X(T0.AddMinutes(30), T0, TimeSpan.FromHours(1), 10, 100));
        Assert.Equal(15, HourGraphRules.X(T0.AddMinutes(15), T0, TimeSpan.FromMinutes(15), 10, 100));
    }

    [Fact]
    public void S12703_the_worse_role_of_two_hours_draws_their_piece()
    {
        Assert.Equal(ColourRole.Caution, HourGraphRules.Worse(ColourRole.Accent, ColourRole.Caution));
        Assert.Equal(ColourRole.Critical, HourGraphRules.Worse(ColourRole.Critical, ColourRole.Caution));
        Assert.Equal(ColourRole.Accent, HourGraphRules.Worse(ColourRole.Accent, ColourRole.Accent));
    }

    [Fact]
    public void S12703_a_column_keeps_between_the_stroke_and_half_a_large_icon()
    {
        Assert.Equal(FamilyShape.Stroke, HourGraphRules.BarWidth(1));
        Assert.Equal(6, HourGraphRules.BarWidth(10));
        Assert.Equal(FamilyShape.IconLarge / 2, HourGraphRules.BarWidth(100));
    }

    [Fact]
    public void S12703_the_days_borders_are_the_local_midnights_inside_the_window()
    {
        var zone = TimeZoneInfo.Utc;
        var midnights = HourGraphRules.Midnights(T0, T0.AddHours(48), zone);

        Assert.Equal(2, midnights.Count);
        Assert.All(midnights, m => Assert.Equal(TimeSpan.Zero, m.TimeOfDay));
        Assert.Empty(HourGraphRules.Midnights(T0, T0.AddHours(3), zone));
    }

    [Fact]
    public void S12708_S12801_the_frame_leads_with_the_glyph_says_a_chosen_measure_and_the_window_ends()
    {
        var slots = Enumerable.Range(0, 25).Select(i => new HourSlot(T0.AddHours(i), 0.5)).ToList();
        var model = HourGraphRules.Framed(new HourGraphModel(slots, HourGraphLook.Line), "", string.Empty, T0.AddMinutes(10), Locale.En, TimeZoneInfo.Utc);

        // [S-12801] No name and no place: a group's Header widget says them once; the glyph says what the reading is.
        Assert.Equal("", model.Glyph);
        Assert.Equal(string.Empty, model.Label);
        Assert.Equal("Now", model.StartLabel);
        Assert.StartsWith(LocaleFormat.WeekdayName(Locale.En, T0.AddHours(24).DayOfWeek, NameWidth.Short), model.EndLabel, StringComparison.Ordinal);
        Assert.Equal("PM2.5 µg/m³", HourGraphRules.Framed(new HourGraphModel(slots, HourGraphLook.Line), "", "PM2.5 µg/m³", T0, Locale.En, TimeZoneInfo.Utc).Label);
    }

    [Fact]
    public void S12704_every_graph_states_one_sizing_relaxed_as_a_chart()
    {
        var plain = HourGraphRules.Sizing(arrows: false);
        var wind = HourGraphRules.Sizing(arrows: true);

        Assert.Empty(plain.Problems());
        Assert.Equal(StretchRoom.Picture, plain.Room);
        Assert.Equal((220.0, 320.0), (plain.Min.Width, plain.Natural.Width));
        Assert.Equal(12 + 20 + 8 + 96 + 4 + 16 + 12, plain.Natural.Height);
        Assert.Equal(plain.Natural.Height + FamilySpace.S1 + FamilyShape.Icon, wind.Natural.Height);
    }

    // ---- config and sync -----------------------------------------------------------------------

    private sealed class Graph : HourGraphConfig
    {
        public override WidgetConfig Clone()
        {
            var copy = new Graph();
            CopyTo(copy);
            return copy;
        }
    }

    [Fact]
    public void S12707_a_window_that_is_no_choice_is_repaired_to_the_nearest()
    {
        var config = new Graph { Hours = 30 };

        Assert.Equal(24, config.EffectiveHours);
        Assert.Single(config.Repair());
        Assert.Equal(24, config.Hours);
        Assert.Empty(new Graph { Hours = 48 }.Repair());
    }

    [Fact]
    public void S12702_graphs_at_one_place_share_one_hourly_want_and_none_without_a_place()
    {
        var builder = new InterestsBuilder(new RefreshPreferences(), TemperatureUnit.Celsius);
        new Graph { Latitude = 47.37, Longitude = 8.54, Hours = 12 }.AddInterests(builder);
        new Graph { Latitude = 47.37, Longitude = 8.54, Hours = 48 }.AddInterests(builder);
        new Graph().AddInterests(builder);

        var wants = builder.Of<HoursInterest>();

        Assert.Equal(2, wants.Count);
        Assert.Single(wants.Distinct());
        Assert.Equal(12, ((Graph)new Graph { Latitude = 1, Longitude = 2, Hours = 12 }.Clone()).Hours);
    }

    // ---- offline store -------------------------------------------------------------------------

    [Fact]
    public void S12702_stored_hourly_replies_come_back_through_their_parsers()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(Hour + 1800);
        var json = OnlineCacheStore.Serialize(
        [
            new CachedReply(OnlineSource.Hours, "hours|47.37,8.54", now.AddMinutes(-20), HoursJson("\"temperature_2m\":[1,2,3]")),
            new CachedReply(OnlineSource.AirHours, "airhours|47.37,8.54", now.AddMinutes(-20), HoursJson("\"pm2_5\":[1,2,3]")),
            new CachedReply(OnlineSource.Hours, "hours|1,2", now.AddDays(-2), HoursJson("\"temperature_2m\":[1,2,3]")),
        ]);

        var back = OnlineCacheStore.Parse(json, now)!;

        Assert.Single(back.Hours);
        Assert.Equal(3, back.Hours[0].Value.Hours.Count);
        Assert.Single(back.AirHours);
    }
}
