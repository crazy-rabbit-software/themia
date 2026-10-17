using System.Text.Json;
using Themia.Core.Accounts;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Sync;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-2848] A widget's place can be cleared, and a cleared place is no place.</summary>
public class PlaceClearTests
{
    private static readonly WidgetId[] Placed =
    [
        WeatherNowConfig.Descriptor.Id, WeatherForecastConfig.Descriptor.Id, AirQualityConfig.Descriptor.Id, RainNextHourConfig.Descriptor.Id,
        SunConfig.Descriptor.Id, MoonConfig.Descriptor.Id,
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

        Assert.Equal(Placed.Order().ToArray(), placed);
    }

    [Theory]
    [MemberData(nameof(PlacedKinds))]
    public void S2848_a_cleared_place_is_no_place_and_the_widget_waits_for_setup(WidgetId kind)
    {
        var config = WithPlace(kind);
        Assert.True(config.HasLocation);
        Assert.False(WidgetSetup.Needed((WidgetConfig)config, AccountFeatures.None));

        config.ClearLocation();

        Assert.False(config.HasLocation);
        Assert.True(WidgetSetup.Needed((WidgetConfig)config, AccountFeatures.None));
        Assert.False(WidgetLiveness.Runs((WidgetConfig)config, AccountFeatures.None));
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
    public void S2848_clearing_forgets_the_name_and_keeps_the_header_preference()
    {
        var weather = (WeatherNowConfig)WithPlace(WeatherNowConfig.Descriptor.Id);
        weather.HideLocation = true;

        weather.ClearLocation();

        Assert.Equal((null, null, string.Empty, true), (weather.Latitude, weather.Longitude, weather.LocationName, weather.HideLocation));

        weather.HideLocation = false;
        Assert.Null(weather.Title);
    }

    [Fact]
    public void S2848_a_cleared_sky_tile_has_no_name_either()
    {
        var sun = (SunConfig)WithPlace(SunConfig.Descriptor.Id);

        sun.ClearLocation();

        Assert.Equal((null, null, string.Empty), (sun.Latitude, sun.Longitude, sun.Place));
    }

    [Fact]
    public void S2848_a_cleared_place_asks_the_sync_for_nothing_so_the_old_place_is_never_fetched()
    {
        var now = (WeatherNowConfig)WithPlace(WeatherNowConfig.Descriptor.Id);
        var forecast = (WeatherForecastConfig)WithPlace(WeatherForecastConfig.Descriptor.Id);
        var air = (AirQualityConfig)WithPlace(AirQualityConfig.Descriptor.Id);
        var rain = (RainNextHourConfig)WithPlace(RainNextHourConfig.Descriptor.Id);

        Assert.NotNull(SyncInterests.For(now));
        Assert.NotNull(OnlineCadence.For(air));

        foreach (var config in new IPlaced[] { now, forecast, air, rain })
        {
            config.ClearLocation();
        }

        Assert.Null(SyncInterests.For(now));
        Assert.Null(SyncInterests.For(forecast));
        Assert.Null(OnlineCadence.For(air));
        Assert.Null(OnlineCadence.For(rain));
    }

    [Theory]
    [InlineData("sun", "Sun")]
    [InlineData("moon", "Moon")]
    public void S2848_null_sky_coordinates_in_a_file_are_no_place_not_a_damaged_one(string discriminator, string kind)
    {
        var config = (SkyPlaceConfig)ConfigJson.Read(kind, $$"""{ "kind": "{{discriminator}}", "latitude": null, "longitude": null, "place": "" }""");

        Assert.False(config.HasLocation);
        Assert.True(WidgetSetup.Needed(config, AccountFeatures.None));
    }

    [Fact]
    public void S2848_a_new_sky_tile_still_starts_at_zurich()
    {
        var moon = new MoonConfig();

        Assert.True(moon.HasLocation);
        Assert.Equal((SkyPlaceConfig.DefaultLatitude, SkyPlaceConfig.DefaultLongitude), (moon.Latitude!.Value, moon.Longitude!.Value));
    }
}
