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
/// [S-12700]..[S-12702] What the metrics over a place's hours share ([S-14710]): the hourly replies' parsers on synthetic
/// replies (never the network), a slot's place on a plot of cells and the offline store.
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

        [Fact]
    public void S14220_slots_sit_on_their_cells_middles_and_a_line_skips_gaps()
    {
        Assert.Equal(5, HourGraphRules.X(0, 10, 100));
        Assert.Equal(95, HourGraphRules.X(9, 10, 100));
        Assert.Equal([(0, 1), (3, 3), (5, 6)], ChartPlot.Runs([.. new double?[] { 1, 2, null, 4, null, 6, 7 }.Select((v, i) => new ChartSample(i, v ?? double.NaN))]));
    }

    [Fact]
    public void S14220_the_worse_role_of_two_hours_draws_their_piece()
    {
        Assert.Equal(ColourRole.Caution, ChartPlot.Worse(ColourRole.Accent, ColourRole.Caution));
        Assert.Equal(ColourRole.Critical, ChartPlot.Worse(ColourRole.Critical, ColourRole.Caution));
        Assert.Equal(ColourRole.Accent, ChartPlot.Worse(ColourRole.Accent, ColourRole.Accent));
    }

    [Fact]
    public void S14220_a_column_keeps_between_the_stroke_and_half_a_large_icon()
    {
        Assert.Equal(FamilyShape.Stroke, ChartPlot.BarWidth(1));
        Assert.Equal(6, ChartPlot.BarWidth(10));
        Assert.Equal(FamilyShape.IconLarge / 2, ChartPlot.BarWidth(100));
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
