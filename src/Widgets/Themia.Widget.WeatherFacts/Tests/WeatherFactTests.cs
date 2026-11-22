using System.Text.Json;
using Themia.App.Shell.Config;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Sync;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.WeatherFacts.Tests;

/// <summary>[S-722] [S-13900]..[S-13903] [S-14608] The weather facts family: a kind a fact, its reading, its trend and its menu.</summary>
public sealed class WeatherFactTests
{
    private static readonly string[] Ids =
    [
        "weatherTemperature", "weatherHighLow", "weatherFeelsLike", "weatherHumidity", "weatherWind", "weatherPrecipitation", "weatherPressure",
        "weatherUv", "weatherCloudCover", "weatherGusts", "weatherSunrise", "weatherSunset",
    ];

    private static WidgetDescriptor Kind(string id) => ModuleCatalog.Of(new WidgetId(id));

    private static string Value(CurrentWeather reading, WeatherFactKind kind, Locale locale = Locale.En)
        => WeatherFactRules.Value(locale, kind, reading, TemperatureUnit.Celsius);

    // ---- [S-13900] the family -----------------------------------------------------------------------

    [Fact]
    public void S13900_every_fact_is_a_kind_of_its_own_in_the_facts_order()
    {
        Assert.Equal(WeatherFactRules.All.Count, Ids.Length);
        for (var i = 0; i < Ids.Length; i++)
        {
            var kind = Kind(Ids[i]);
            var config = Assert.IsAssignableFrom<WeatherFactConfig>(kind.CreateDefault());
            Assert.Equal(WeatherFactRules.All[i], config.Fact);
            Assert.Equal(kind.ConfigType, config.GetType());
            Assert.Equal(WeatherFactKinds.Glyph(config.Fact), kind.Glyph);
            Assert.Contains("weather", kind.Keywords);
            Assert.Equal(new AddPlacement(AddFolderId.WeatherFacts, 0, i), kind.Add);
        }

        Assert.Equal(Ids.Length, Ids.Select(id => Kind(id).Label.Text(Locale.En)).Distinct().Count());
    }

    [Fact]
    public void S13900_the_kinds_are_named_after_their_fact()
    {
        Assert.Equal(
            ["Temperature", "High and low", "Feels like", "Humidity", "Wind", "Precipitation", "Pressure", "UV index", "Cloud cover", "Gusts", "Sunrise", "Sunset"],
            Ids.Select(id => Kind(id).Label.Text(Locale.En)));
        Assert.Equal(["Luftfeuchte", "Sonnenaufgang"], new[] { "weatherHumidity", "weatherSunrise" }.Select(id => Kind(id).Label.Text(Locale.De)));
    }

    [Fact]
    public void S13900_a_fact_card_keeps_its_place_and_trend_through_the_state_file_and_a_copy()
    {
        WidgetConfig config = new WeatherWindConfig { Latitude = 47.3769, Longitude = 8.5417, LocationName = "Zurich, Switzerland", Trend = MetricTrend.Line };
        var json = JsonSerializer.Serialize(config, StateJson.WidgetConfig);

        Assert.Contains("\"kind\": \"weatherWind\"", json, StringComparison.Ordinal);
        Assert.Contains("\"trend\": \"Line\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("trend", JsonSerializer.Serialize<WidgetConfig>(new WeatherWindConfig(), StateJson.WidgetConfig), StringComparison.Ordinal);
        var back = Assert.IsType<WeatherWindConfig>(JsonSerializer.Deserialize(json, StateJson.WidgetConfig));
        Assert.Equal(("Zurich, Switzerland", WeatherFactKind.Wind, MetricTrend.Line), (back.LocationName, back.Fact, back.Trend));
        var copy = Assert.IsType<WeatherWindConfig>(config.Clone());
        Assert.NotSame(config, copy);
        Assert.Equal((47.3769, MetricTrend.Line), (copy.Latitude, copy.Trend));
    }

    // ---- [S-14603] the trend -------------------------------------------------------------------------

    [Fact]
    public void S14608_the_hourly_readings_draw_a_line_cloud_cover_an_arrow_and_the_day_s_facts_none()
    {
        Assert.Equal(
            [MetricHistory.Series, MetricHistory.None, MetricHistory.Series, MetricHistory.Series, MetricHistory.Series, MetricHistory.Series, MetricHistory.Series,
             MetricHistory.Series, MetricHistory.Readings, MetricHistory.Series, MetricHistory.None, MetricHistory.None],
            WeatherFactRules.All.Select(WeatherFactRules.History));
        var humidity = new WeatherHour(DateTimeOffset.UnixEpoch) { Humidity = 63, Pressure = 1013 };
        Assert.Equal((63d, 1013d), (WeatherFactRules.Hourly(WeatherFactKind.Humidity, humidity), WeatherFactRules.Hourly(WeatherFactKind.Pressure, humidity)));
        Assert.Null(WeatherFactRules.Hourly(WeatherFactKind.CloudCover, humidity));

        // [S-14714] The temperature now and its hours: the temperature graph's metric.
        Assert.Equal(9.4, WeatherFactRules.Hourly(WeatherFactKind.Temperature, new WeatherHour(DateTimeOffset.UnixEpoch) { Temperature = 9.4 }));
    }

    [Fact]
    public void S14608_a_trend_over_the_hours_asks_the_place_s_hours_and_none_asks_nothing_more()
    {
        var place = new WeatherPressureConfig { Latitude = 47.37, Longitude = 8.54 };
        Assert.Null(place.HoursInterest(TimeSpan.FromMinutes(30)));

        place.Trend = MetricTrend.Arrow;
        Assert.Equal(new HoursInterest(47.37, 8.54, TimeSpan.FromMinutes(30)), place.HoursInterest(TimeSpan.FromMinutes(30)));

        // Cloud cover's arrow is the readings the card saw: the hours are never asked.
        Assert.Null(new WeatherCloudCoverConfig { Latitude = 47.37, Longitude = 8.54, Trend = MetricTrend.Arrow }.HoursInterest(TimeSpan.FromMinutes(30)));
    }

    [Fact]
    public void S14608_a_line_where_the_fact_has_no_hours_is_repaired_to_its_arrow_and_one_with_no_history_to_none()
    {
        var cloud = new WeatherCloudCoverConfig { Trend = MetricTrend.Line };
        Assert.NotEmpty(cloud.Repair());
        Assert.Equal(MetricTrend.Arrow, cloud.Trend);

        var sunrise = new WeatherSunriseConfig { Trend = MetricTrend.Arrow };
        Assert.NotEmpty(sunrise.Repair());
        Assert.Equal(MetricTrend.None, sunrise.Trend);
        Assert.Empty(new WeatherPressureConfig { Trend = MetricTrend.Line }.Repair());
    }

    // ---- [S-722] [S-13902] the reading ---------------------------------------------------------------

    [Fact]
    public void S722_a_fact_is_its_value_and_its_number_and_a_missing_reading_is_the_dash()
    {
        var reading = new CurrentWeather(18, 17, 63, 11, 2) { WindDirection = 315, Pressure = null, UvIndex = 3 };

        Assert.Equal(new MetricValue("11 km/h NW", 11), WeatherFactRules.Metric(Locale.En, WeatherFactKind.Wind, reading, TemperatureUnit.Celsius));
        Assert.Equal(new MetricValue("—", null), WeatherFactRules.Metric(Locale.En, WeatherFactKind.Pressure, reading, TemperatureUnit.Celsius));
        Assert.Equal("—", Value(reading, WeatherFactKind.Today));
    }

    [Fact]
    public void S722_today_s_range_is_written_high_then_low()
    {
        var today = new ForecastDay(new DateOnly(2026, 10, 4), 2, 8, 16, 0);
        var reading = new CurrentWeather(14, 13, 71, 13, 2) { Days = [today] };

        Assert.Equal(("Today", "H 16°  L 8°"), (WeatherFactRules.Name(WeatherFactKind.Today, Locale.En), Value(reading, WeatherFactKind.Today)));
    }

    [Fact]
    public void S722_precipitation_is_the_amount_while_it_rains_and_today_s_chance_otherwise()
    {
        var today = new ForecastDay(new DateOnly(2026, 10, 4), 61, 9, 15, 3) { PrecipitationChance = 80 };
        var dry = new CurrentWeather(15, 14, 80, 5, 61) { Days = [today] };
        var wet = dry with { Precipitation = 0.6 };

        Assert.Equal("80%", Value(dry, WeatherFactKind.Precipitation));
        Assert.Equal("0.6 mm", Value(wet, WeatherFactKind.Precipitation));
        Assert.Equal("0,6 mm", Value(wet, WeatherFactKind.Precipitation, Locale.De));
    }

    [Fact]
    public void S722_sunrise_and_sunset_come_from_today_on_the_language_s_clock()
    {
        var today = new ForecastDay(new DateOnly(2026, 10, 4), 0, 9, 15, 0) { Sunrise = new TimeOnly(7, 31), Sunset = new TimeOnly(18, 54) };
        var reading = new CurrentWeather(15, 14, 50, 5, 0) { Days = [today] };

        Assert.Equal("7:31 AM", Value(reading, WeatherFactKind.Sunrise));
        Assert.Equal("18:54", Value(reading, WeatherFactKind.Sunset, Locale.De));
    }

    [Fact]
    public void S722_the_names_are_in_the_language_asked_for()
    {
        Assert.Equal(["Gefühlt", "Luftfeuchte", "Wind", "Luftdruck"],
            new[] { WeatherFactKind.FeelsLike, WeatherFactKind.Humidity, WeatherFactKind.Wind, WeatherFactKind.Pressure }.Select(k => WeatherFactRules.Name(k, Locale.De)));
    }

    // ---- [S-13903] the menu and the click ------------------------------------------------------------

    [Fact]
    public void S13903_the_menu_is_the_place_then_the_trend_where_the_fact_has_one()
    {
        var menu = new WidgetMenuLines(() => { });
        ((IWidgetModule)new WeatherHumidityModule()).MenuLines(menu, new WeatherHumidityConfig());

        var place = WeatherPlaceRows.Lines(new WidgetMenuLines(() => { }), new WeatherHumidityConfig()).Select(l => l.Text).ToList();
        Assert.Equal([.. place, Str.Metric_Trend.Text()], menu.Lines.Select(l => l.Text));
        Assert.DoesNotContain(menu.Lines, l => l.LinkTo is not null);

        // [S-15560] A fact that means something for a day ahead has its Day after the place.
        var sunrise = new WidgetMenuLines(() => { });
        ((IWidgetModule)new WeatherSunriseModule()).MenuLines(sunrise, new WeatherSunriseConfig());
        Assert.Equal([.. place, "Day"], sunrise.Lines.Select(l => l.Text));
        Assert.Equal("day", sunrise.Lines[^1].Shares);
    }

    // ---- [S-15560] a day ahead --------------------------------------------------------------------

    [Fact]
    public void S15560_exactly_the_facts_that_mean_something_for_a_day_ahead_take_a_day()
    {
        Assert.Equal(
            [WeatherFactKind.Today, WeatherFactKind.Wind, WeatherFactKind.Precipitation, WeatherFactKind.Uv, WeatherFactKind.Sunrise, WeatherFactKind.Sunset],
            WeatherFactRules.All.Where(WeatherFactRules.TakesDay));
        Assert.True(new WeatherHighLowConfig().TakesDay);
        Assert.False(new WeatherHumidityConfig().TakesDay);
    }

    [Fact]
    public void S15560_a_day_ahead_reads_that_days_forecast_and_a_day_past_it_has_none()
    {
        var today = new DateOnly(2026, 11, 21);
        var reading = new CurrentWeather(18, 17, 63, 11, 2)
        {
            Days =
            [
                new ForecastDay(today, 61, 6, 13, 2.6) { PrecipitationChance = 75, WindMax = 21, UvMax = 2 },
                new ForecastDay(today.AddDays(1), 0, 3, 14, 0) { PrecipitationChance = 5, WindMax = 9, UvMax = 3 },
            ],
        };

        Assert.Equal("H 14°  L 3°", WeatherFactRules.Value(Locale.En, WeatherFactKind.Today, reading, TemperatureUnit.Celsius, 1));
        Assert.Equal("5%", WeatherFactRules.Value(Locale.En, WeatherFactKind.Precipitation, reading, TemperatureUnit.Celsius, 1));
        Assert.Equal("9 km/h", WeatherFactRules.Value(Locale.En, WeatherFactKind.Wind, reading, TemperatureUnit.Celsius, 1));
        Assert.Equal("Sun", WeatherFactRules.DayRowName(Locale.En, WeatherFactKind.Today, reading, 1));
        Assert.Null(WeatherFactRules.DayRowName(Locale.En, WeatherFactKind.Today, reading, 0));
        Assert.Equal(WeatherFactRules.Missing, WeatherFactRules.Value(Locale.En, WeatherFactKind.Today, reading, TemperatureUnit.Celsius, 5));
        Assert.Null(WeatherFactRules.Metric(Locale.En, WeatherFactKind.Uv, reading, TemperatureUnit.Celsius, 1).Number);
    }

    [Fact]
    public void S15560_a_day_survives_a_copy_and_a_day_a_fact_cannot_show_is_today_after_repair()
    {
        var config = new WeatherSunsetConfig { Day = 3 };
        Assert.Equal(3, ((WeatherSunsetConfig)config.Clone()).Day);

        var humidity = new WeatherHumidityConfig { Day = 2 };
        Assert.NotEmpty(humidity.Repair());
        Assert.Equal(0, humidity.Day);
    }

    [Fact]
    public void S13903_a_click_opens_the_forecast_for_its_place_and_nothing_without_one()
    {
        var config = new WeatherUvConfig { Latitude = 47.3769, Longitude = 8.5417, LocationName = "Zurich, Switzerland" };

        // [S-14706] Every fact's kind opens it, one target for the family.
        Assert.All(WeatherFactKinds.All, kind => Assert.Same(MetricOpens.Weather, kind.Opens));
        Assert.StartsWith("https://www.msn.com/", MetricOpens.Weather.Target(config)!.Value, StringComparison.Ordinal);
        Assert.Contains("/weather/forecast/in-Zurich?loc=", MetricOpens.Weather.Target(config)!.Value, StringComparison.Ordinal);
        Assert.Null(MetricOpens.Weather.Target(new WeatherUvConfig()));
    }
}
