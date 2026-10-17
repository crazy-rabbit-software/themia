using System.Text.Json;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Tests;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>
/// [S-3100] [S-3101] [S-2848] The Sun and Moon widgets as a pair: two kinds, two configs, one shared
/// place repaired loudly, and a cleared place in a file. Here, not in Core's SkyWidgetsTests or
/// PlaceClearTests, because they ask both widgets' configs, which only this host has once the
/// widgets are module projects.
/// </summary>
public sealed class SkyWidgetsHostTests
{
    [Fact]
    public void S3100_sun_and_moon_are_two_kinds_each_with_its_own_config()
    {
        var sun = Assert.IsType<SunConfig>(WidgetConfig.CreateDefault(SunConfig.Descriptor.Id));
        var moon = Assert.IsType<MoonConfig>(WidgetConfig.CreateDefault(new WidgetId("moon")));

        Assert.Equal(SunConfig.Descriptor.Id, sun.Kind);
        Assert.Equal(new WidgetId("moon"), moon.Kind);
        Assert.DoesNotContain(ModuleCatalog.Widgets, w => w.Id.Value is "sunMoon" or "sunmoon");
    }

    [Fact]
    public void S3100_neither_needs_setting_up_they_start_at_a_place()
    {
        Assert.False(WidgetSetup.Needed(new SunConfig(), Themia.Sdk.FeatureSet.None));
        Assert.False(WidgetSetup.Needed(new MoonConfig(), Themia.Sdk.FeatureSet.None));
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

    [Theory]
    [InlineData("sun", "Sun")]
    [InlineData("moon", "Moon")]
    public void S2848_null_sky_coordinates_in_a_file_are_no_place_not_a_damaged_one(string discriminator, string kind)
    {
        var config = (SkyPlaceConfig)ConfigJson.Read(kind, $$"""{ "kind": "{{discriminator}}", "latitude": null, "longitude": null, "place": "" }""");

        Assert.False(config.HasLocation);
        Assert.True(WidgetSetup.Needed(config, FeatureSet.None));
    }

    [Fact]
    public void S2848_a_new_sky_tile_still_starts_at_zurich()
    {
        var moon = new MoonConfig();

        Assert.True(moon.HasLocation);
        Assert.Equal((SkyPlaceConfig.DefaultLatitude, SkyPlaceConfig.DefaultLongitude), (moon.Latitude!.Value, moon.Longitude!.Value));
    }

    private static WidgetConfig RoundTrip(WidgetConfig config)
    {
        var json = JsonSerializer.Serialize(config, StateJson.WidgetConfig);
        return JsonSerializer.Deserialize(json, StateJson.WidgetConfig)!;
    }
}
