using Themia.Core.Storage;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-729] The weather widgets' saved answers: round trip, validation, and what is not restored.</summary>
public sealed class WeatherCacheStoreTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 4, 15, 0, 0, TimeSpan.FromHours(2));

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "themia-weather-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private static CurrentWeather Reading() => new(18.4, 17.2, 63, 11, 2)
    {
        Pressure = 1016,
        WindDirection = 315,
        UvIndex = 3,
        Days = [new ForecastDay(new DateOnly(2026, 10, 4), 2, 11, 19, 0) { Sunrise = new TimeOnly(7, 31), PrecipitationChance = 10 }],
        Hours = [new HourlyPoint(Now, 18, 0), new HourlyPoint(Now.AddHours(1), 17.5, 20)],
    };

    private static WeatherCacheSnapshot Snapshot(DateTimeOffset at) => new(
        [new CachedWeather<CurrentWeather>("now|47.37,8.54|Celsius", at, Reading())],
        [new CachedWeather<Forecast>("days|47.37,8.54|Celsius|7", at, new Forecast([new ForecastDay(new DateOnly(2026, 10, 5), 61, 10, 15, 3.4)]))]);

    [Fact]
    public void S729_a_snapshot_survives_the_round_trip()
    {
        var at = Now.AddHours(-3);
        var back = WeatherCacheStore.Parse(WeatherCacheStore.Serialize(Snapshot(at)), Now);

        Assert.NotNull(back);
        var current = Assert.Single(back.Current);
        Assert.Equal("now|47.37,8.54|Celsius", current.Key);
        Assert.Equal(at, current.At);
        Assert.Equal(18.4, current.Value.Temperature);
        Assert.Equal(315, current.Value.WindDirection);
        Assert.Equal(new TimeOnly(7, 31), current.Value.Today?.Sunrise);
        Assert.Equal(2, current.Value.Hours.Count);
        Assert.Equal(Now.AddHours(1), current.Value.Hours[1].At);

        var forecast = Assert.Single(back.Forecasts);
        Assert.Equal(3.4, forecast.Value.Days[0].Precipitation);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{\"schemaVersion\":2,\"current\":[],\"forecasts\":[]}")]
    [InlineData("{\"schemaVersion\":1,\"forecasts\":[]}")]
    public void S729_a_file_that_is_not_this_schema_is_corrupt(string json)
        => Assert.Null(WeatherCacheStore.Parse(json, Now));

    [Fact]
    public void S729_an_answer_too_old_or_from_the_future_is_not_restored()
    {
        Assert.Empty(WeatherCacheStore.Parse(WeatherCacheStore.Serialize(Snapshot(Now - WeatherCacheStore.MaxAge - TimeSpan.FromMinutes(1))), Now)!.Current);
        Assert.Empty(WeatherCacheStore.Parse(WeatherCacheStore.Serialize(Snapshot(Now.AddHours(1))), Now)!.Current);
    }

    [Fact]
    public void S729_a_reading_without_a_temperature_is_dropped_and_out_of_range_values_are_cleared()
    {
        const string json = """
            {"schemaVersion":1,
             "current":[{"key":"a","at":"2026-10-04T14:00:00+02:00","reading":{"humidity":50}},
                        {"key":"b","at":"2026-10-04T14:00:00+02:00","reading":{"temperature":12,"humidity":250,"code":400}}],
             "forecasts":[{"key":"c","at":"2026-10-04T14:00:00+02:00","days":[{"day":"nope"}]}]}
            """;

        var back = WeatherCacheStore.Parse(json, Now);

        Assert.NotNull(back);
        var kept = Assert.Single(back.Current);
        Assert.Equal("b", kept.Key);
        Assert.Null(kept.Value.Humidity);
        Assert.Null(kept.Value.Code);
        Assert.Empty(back.Forecasts);
    }
}
