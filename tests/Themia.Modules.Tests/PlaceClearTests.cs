using System.Text.Json;
using Themia.Core.Accounts;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Sync;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Widget.Header;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-2848] A widget's place can be cleared, and a cleared place is no place.</summary>
public class PlaceClearTests
{
    private static readonly WidgetId[] Placed =
    [
        new WidgetId("weatherNow"), new WidgetId("weatherForecast"), new WidgetId("airQuality"), new WidgetId("airFacts"), new WidgetId("rainNextHour"),
        new WidgetId("sun"), new WidgetId("sunTimes"), new WidgetId("moon"), new WidgetId("moonTimes"),
        new WidgetId("weatherHighLow"), new WidgetId("weatherFeelsLike"), new WidgetId("weatherHumidity"), new WidgetId("weatherWind"), new WidgetId("weatherPrecipitation"), new WidgetId("weatherPressure"), new WidgetId("weatherUv"), new WidgetId("weatherCloudCover"), new WidgetId("weatherGusts"), new WidgetId("weatherSunrise"), new WidgetId("weatherSunset"),
        new WidgetId("temperatureGraph"), new WidgetId("rainGraph"), new WidgetId("uvGraph"), new WidgetId("airQualityGraph"), new WidgetId("windGraph"), new WidgetId("humidityGraph"), new WidgetId("pressureGraph"),
    ];

    public static TheoryData<WidgetId> PlacedKinds()
    {
        var data = new TheoryData<WidgetId>();
        foreach (var kind in Placed)
        {
            data.Add(kind);
        }

        return data;
    }

    private static IPlaced WithPlace(WidgetId kind)
    {
        var config = WidgetConfig.CreateDefault(kind);

        switch (config)
        {
            case WeatherConfig weather:
                weather.Latitude = 51.5;
                weather.Longitude = -0.12;
                weather.LocationName = "London, England, United Kingdom";
                break;
            case SkyPlaceConfig sky:
                sky.Latitude = 51.5;
                sky.Longitude = -0.12;
                sky.Place = "London";
                break;
        }

        return (IPlaced)config;
    }

    private static WidgetConfig RoundTrip(WidgetConfig config)
    {
        var json = JsonSerializer.Serialize(config, StateJson.WidgetConfig);
        return JsonSerializer.Deserialize(json, StateJson.WidgetConfig)!;
    }

    [Fact]
    public void S2848_exactly_the_widgets_about_a_place_can_clear_it()
    {
        var placed = ModuleCatalog.Widgets.Select(w => w.Id).ToArray()
            .Where(kind => WidgetConfig.CreateDefault(kind) is IPlaced)
            .Order()
            .ToArray();

        // [S-9604] The Header widget holds the place of the widgets it heads, and never waits for one.
        Assert.Equal(Placed.Append(new WidgetId("header")).Order().ToArray(), placed);
    }

    [Fact]
    public void S9604_a_header_holds_a_place_and_shows_its_city_but_never_waits_for_one()
    {
        var header = Assert.IsType<HeaderConfig>(WidgetConfig.CreateDefault(new WidgetId("header")));
        Assert.False(WidgetSetup.Needed(header, FeatureSet.None));
        Assert.Null(header.PlaceLabel);

        // [S-9604] Set with the widgets it heads, it names the place at once: no switch of its own (owner, 2026-11-19).
        header.Latitude = 47.37;
        header.Longitude = 8.54;
        header.PlaceName = "Zürich, Switzerland";
        Assert.Equal("Zürich", header.PlaceLabel);

        var back = Assert.IsType<HeaderConfig>(RoundTrip(header));
        Assert.Equal("Zürich", back.PlaceLabel);

        header.ClearLocation();
        Assert.Null(header.PlaceLabel);
        Assert.False(WidgetSetup.Needed(header, FeatureSet.None));
    }

    [Theory]
    [MemberData(nameof(PlacedKinds))]
    public void S2848_a_cleared_place_is_no_place_and_the_widget_waits_for_setup(WidgetId kind)
    {
        var config = WithPlace(kind);
        Assert.True(config.HasLocation);
        Assert.False(WidgetSetup.Needed((WidgetConfig)config, FeatureSet.None));

        config.ClearLocation();

        Assert.False(config.HasLocation);
        Assert.True(WidgetSetup.Needed((WidgetConfig)config, FeatureSet.None));

        // [S-12835] Waiting for its place it still runs, its face under the card's compact Set up line (WR-10).
        Assert.True(WidgetLiveness.Runs((WidgetConfig)config, FeatureSet.None));
    }

    [Theory]
    [MemberData(nameof(PlacedKinds))]
    public void S2848_a_cleared_place_stays_cleared_through_save_and_load(WidgetId kind)
    {
        var config = WithPlace(kind);
        config.ClearLocation();

        var back = Assert.IsAssignableFrom<IPlaced>(RoundTrip((WidgetConfig)config));

        // The regression: a sky tile's null coordinates must not be left out of the file, which
        // would read them back as the Zurich a new tile starts at.
        Assert.False(back.HasLocation);
        Assert.Empty(((WidgetConfig)back).Repair());
    }

    [Theory]
    [MemberData(nameof(PlacedKinds))]
    public void S2848_a_cleared_place_survives_a_clone(WidgetId kind)
    {
        var config = WithPlace(kind);
        config.ClearLocation();

        Assert.False(Assert.IsAssignableFrom<IPlaced>(((WidgetConfig)config).Clone()).HasLocation);
    }

    [Fact]
    public void S2848_clearing_forgets_the_name()
    {
        var weather = (WeatherNowConfig)WithPlace(new WidgetId("weatherNow"));

        weather.ClearLocation();

        Assert.Equal((null, null, string.Empty), (weather.Latitude, weather.Longitude, weather.LocationName));
        Assert.Null(weather.PlaceLabel);
    }

    [Fact]
    public void S2848_a_cleared_sky_tile_has_no_name_either()
    {
        var sun = (SunConfig)WithPlace(new WidgetId("sun"));

        sun.ClearLocation();

        Assert.Equal((null, null, string.Empty), (sun.Latitude, sun.Longitude, sun.Place));
    }

    [Fact]
    public void S2848_a_cleared_place_asks_the_sync_for_nothing_so_the_old_place_is_never_fetched()
    {
        var now = (WeatherNowConfig)WithPlace(new WidgetId("weatherNow"));
        var forecast = (WeatherForecastConfig)WithPlace(new WidgetId("weatherForecast"));
        var air = (AirQualityConfig)WithPlace(new WidgetId("airQuality"));
        var rain = (RainNextHourConfig)WithPlace(new WidgetId("rainNextHour"));

        Assert.NotNull(WeatherInterests.For(now));
        Assert.NotNull(air.Interest());

        foreach (var config in new IPlaced[] { now, forecast, air, rain })
        {
            config.ClearLocation();
        }

        Assert.Null(WeatherInterests.For(now));
        Assert.Null(WeatherInterests.For(forecast));
        Assert.Null(air.Interest());
        Assert.Null(rain.Interest());
    }

}
