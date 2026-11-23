using Themia.Core.Model;
using Themia.Core.Sync;
using Themia.Core.SystemData;
using Themia.Core.Widgets;
using Themia.Sdk.Samples;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-16330]..[S-16332] [S-16340] What the Metric widget added to the host's data: the hourly replies' snow, cloud and
/// sunshine, the air's hourly pollen, a day back in both hourly answers, and a shared setting a config holds only while it
/// offers it. Synthetic replies only, never the network.
/// </summary>
public sealed class MetricDataTests
{
    /// <summary>A whole hour, 2026-11-16 09:00 UTC.</summary>
    private const long Hour = 1_763_283_600;

    private static string HoursJson(string fields, int count = 3)
        => "{\"hourly\":{\"time\":[" + string.Join(',', Enumerable.Range(0, count).Select(i => Hour + (i * 3600))) + "]," + fields + "}}";

    [Fact]
    public void S16330_an_hour_carries_its_snow_cloud_and_sunshine_in_their_ranges()
    {
        var json = HoursJson("\"temperature_2m\":[-1,-2,-3],\"snowfall\":[0.4,null,900],\"cloud_cover\":[100,40,120],\"sunshine_duration\":[0,1800,3600]");

        var hours = WeatherHoursWire.Parse(json).Value!.Hours;

        Assert.Equal(0.4, hours[0].Snowfall);
        Assert.Null(hours[1].Snowfall);
        Assert.Null(hours[2].Snowfall); // 900 cm in an hour is no reading
        Assert.Equal([100.0, 40.0], hours.Take(2).Select(h => h.CloudCover!.Value));
        Assert.Null(hours[2].CloudCover);
        Assert.Equal([0.0, 30.0, 60.0], hours.Select(h => h.Sunshine!.Value));
        Assert.Contains("snowfall", WeatherHoursWire.HourlyFields, StringComparison.Ordinal);
        Assert.Contains("cloud_cover", WeatherHoursWire.HourlyFields, StringComparison.Ordinal);
    }

    [Fact]
    public void S16331_an_air_hour_carries_the_pollen_the_reply_has_and_none_it_lacks()
    {
        var json = HoursJson("\"european_aqi\":[25,30,null],\"grass_pollen\":[3.5,null,12],\"birch_pollen\":[0,0,0]");

        var air = WeatherHoursWire.ParseAir(json).Value!;

        Assert.Equal(3, air.Hours.Count); // an hour with pollen alone is an hour
        Assert.Equal(3.5, air.Hours[0].Pollen[PollenType.Grass]);
        Assert.False(air.Hours[1].Pollen.ContainsKey(PollenType.Grass)); // no number is no reading, never 0
        Assert.Equal(0, air.Hours[1].Pollen[PollenType.Birch]);
        Assert.False(air.Hours[0].Pollen.ContainsKey(PollenType.Olive));
        Assert.Contains("ragweed_pollen", WeatherHoursWire.AirFields, StringComparison.Ordinal);
    }

    [Fact]
    public void S16332_the_sample_hours_reach_a_day_back_as_the_service_sends_them()
    {
        var now = SampleDay.Now;
        var hours = SampleHours.Weather().Hours;
        var air = SampleHours.Air().Hours;

        Assert.True(hours[0].At <= now.AddHours(-WeatherHoursWire.PastHours));
        Assert.True(hours[^1].At >= now.AddHours(WeatherHoursWire.Hours - 2));
        Assert.True(air[0].At <= now.AddHours(-WeatherHoursWire.PastHours));
        Assert.True(air[^1].At >= now.AddHours(WeatherHoursWire.AirHourCount - 2));

        // The hour under way reads as it did before the day back: 14 km/h of wind from the south-west.
        var under = hours.Last(h => h.At <= now);
        Assert.InRange(under.Wind!.Value, 13, 15);
        Assert.InRange(under.WindDirection!.Value, 240, 246);
    }

    [Fact]
    public void S16333_a_disk_series_with_its_reads_and_writes_apart_is_its_own_instance_of_the_same_drive()
    {
        Assert.Equal("rw|C:\\", DiskScopes.Apart("C:\\"));
        Assert.Equal((true, "C:\\"), DiskScopes.Split(DiskScopes.Apart("C:\\")));
        Assert.Equal((true, SystemInterests.AllDrives), DiskScopes.Split(DiskScopes.Apart(SystemInterests.AllDrives)));
        Assert.Equal((false, "D:\\"), DiskScopes.Split("D:\\"));
        Assert.NotEqual("C:\\", DiskScopes.Apart("C:\\")); // a ring of its own: the plain total series is untouched
    }

    private sealed class Offering : WidgetConfig, IPingHost, IOffersShared
    {
        public string Host { get; set; } = string.Empty;

        public bool Pings { get; set; }

        public bool Offers(string key) => Pings && key == SharedSettings.PingHost.Key;

        public override WidgetConfig Clone() => new Offering { Host = Host, Pings = Pings };
    }

    [Fact]
    public void S16340_a_config_holds_a_shared_setting_only_while_it_offers_it()
    {
        var (a, b) = (new Offering { Host = "a.example" }, new Offering { Host = "b.example" });

        Assert.False(SharedSettings.PingHost.Holds(a));
        Assert.False(SharedSettings.PingHost.Copy(a, b));
        Assert.Equal(0, SharedSettings.Read(SharedSettings.PingHost, [a, b]).Holders);

        (a.Pings, b.Pings) = (true, true);
        Assert.True(SharedSettings.PingHost.Holds(a));
        Assert.True(SharedSettings.PingHost.Copy(a, b));
        Assert.Equal("a.example", b.Host);
    }
}
