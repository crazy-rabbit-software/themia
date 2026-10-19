using Themia.Core.Model;
using Themia.Sdk;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-2850]..[S-2854] A place widget's Use my location: the privacy grain, the move rule, the state
/// Windows' answers lead to, the copy the widget reads and when it waits.
/// </summary>
public sealed class DeviceLocationTests
{
    /// <summary>A weather-family config of no kind in particular (Core's tests have no widget).</summary>
    private sealed class Weather : WeatherConfig
    {
        public override WidgetConfig Clone()
        {
            var copy = new Weather();
            CopyTo(copy);
            return copy;
        }
    }

    private sealed class Sky : SkyPlaceConfig
    {
        public override WidgetConfig Clone() => CopyPlaceTo(new Sky());
    }

    private static readonly DeviceLocation Zurich = DeviceLocation.At(47.3769, 8.5417);

    [Theory]
    [InlineData(47.3769, 47.38)]
    [InlineData(8.5417, 8.54)]
    [InlineData(-33.86882, -33.87)]
    [InlineData(0.004, 0)]
    public void S2852_a_position_is_rounded_to_about_a_kilometre(double degrees, double rounded)
        => Assert.Equal(rounded, DeviceLocationRules.Round(degrees), 10);

    [Fact]
    public void S2852_a_position_is_held_only_rounded()
        => Assert.Equal(new DeviceLocation(DeviceLocationState.Located, 47.38, 8.54), Zurich);

    [Theory]
    [InlineData(double.NaN, 8.5)]
    [InlineData(91, 8.5)]
    [InlineData(47, 181)]
    public void S2852_a_reading_that_is_not_on_earth_is_no_position(double latitude, double longitude)
    {
        Assert.Equal(DeviceLocation.Unavailable, DeviceLocation.At(latitude, longitude));
        Assert.Equal(DeviceLocation.Locating, DeviceLocationRules.Position(DeviceLocation.Locating, latitude, longitude));
    }

    [Fact]
    public void S2852_a_move_under_two_kilometres_keeps_the_position_held()
    {
        // About 1.1 km north: across a rounding edge, but no new weather for it.
        Assert.Equal(Zurich, DeviceLocationRules.Position(Zurich, 47.3869, 8.5417));
    }

    [Fact]
    public void S2852_a_move_of_two_kilometres_or_more_is_a_new_position()
    {
        var moved = DeviceLocationRules.Position(Zurich, 47.40, 8.5417);
        Assert.Equal(DeviceLocation.At(47.40, 8.5417), moved);
        Assert.True(DeviceLocationRules.DistanceKm(Zurich.Latitude, Zurich.Longitude, 47.40, 8.5417) >= DeviceLocationRules.MoveKm);
    }

    [Fact]
    public void S2852_the_distance_is_great_circle_kilometres()
        => Assert.InRange(DeviceLocationRules.DistanceKm(47.3769, 8.5417, 46.9480, 7.4474), 94, 97); // Zurich to Bern

    [Fact]
    public void S2851_wanted_starts_locating_and_unwanted_forgets_the_position()
    {
        Assert.Equal(DeviceLocation.Locating, DeviceLocationRules.Wanted(DeviceLocation.Off, wanted: true));
        Assert.Equal(Zurich, DeviceLocationRules.Wanted(Zurich, wanted: true));
        Assert.Equal(DeviceLocation.Off, DeviceLocationRules.Wanted(Zurich, wanted: false));
    }

    [Fact]
    public void S2851_access_denied_is_denied_and_allowed_again_looks_again()
    {
        Assert.Equal(DeviceLocation.Denied, DeviceLocationRules.Access(DeviceLocation.Locating, DeviceAccess.Denied));
        Assert.Equal(DeviceLocation.Locating, DeviceLocationRules.Access(DeviceLocation.Locating, DeviceAccess.Allowed));
        Assert.Equal(DeviceLocation.Locating, DeviceLocationRules.Access(DeviceLocation.Denied, DeviceAccess.Allowed));
        Assert.Equal(Zurich, DeviceLocationRules.Access(Zurich, DeviceAccess.Allowed));
    }

    [Fact]
    public void S2851_location_turned_off_in_settings_is_denied_and_turned_on_looks_again()
    {
        var off = DeviceLocationRules.Status(Zurich, DeviceStatus.Disabled);
        Assert.Equal(DeviceLocation.Denied, off);
        Assert.Equal(DeviceLocation.Locating, DeviceLocationRules.Status(off, DeviceStatus.Working));
    }

    [Fact]
    public void S2851_nothing_to_give_is_unavailable_unless_a_position_is_held()
    {
        Assert.Equal(DeviceLocation.Unavailable, DeviceLocationRules.Status(DeviceLocation.Locating, DeviceStatus.NotAvailable));
        Assert.Equal(Zurich, DeviceLocationRules.Status(Zurich, DeviceStatus.NotAvailable));
        Assert.Equal(DeviceLocation.Unavailable, DeviceLocationRules.Failed(DeviceLocation.Locating));
        Assert.Equal(Zurich, DeviceLocationRules.Failed(Zurich));
    }

    [Fact]
    public void S2851_nothing_moves_the_state_while_no_widget_wants_it()
    {
        Assert.Equal(DeviceLocation.Off, DeviceLocationRules.Access(DeviceLocation.Off, DeviceAccess.Denied));
        Assert.Equal(DeviceLocation.Off, DeviceLocationRules.Status(DeviceLocation.Off, DeviceStatus.Disabled));
        Assert.Equal(DeviceLocation.Off, DeviceLocationRules.Position(DeviceLocation.Off, 47.37, 8.54));
        Assert.Equal(DeviceLocation.Off, DeviceLocationRules.Failed(DeviceLocation.Off));
    }

    [Fact]
    public void S2853_a_widget_on_its_own_place_reads_its_own_config()
    {
        var config = new Weather { Latitude = 1, Longitude = 2, LocationName = "Accra" };
        Assert.Same(config, DevicePlaces.Resolve(config, Zurich, "My location"));
    }

    [Fact]
    public void S2853_a_widget_on_the_device_reads_a_copy_there_and_the_stored_config_keeps_the_switch_only()
    {
        var config = new Weather { Latitude = 1, Longitude = 2, LocationName = "Accra, Ghana", UseDeviceLocation = true };

        var read = Assert.IsType<Weather>(DevicePlaces.Resolve(config, Zurich, "My location"));

        Assert.Equal((47.38, 8.54, "My location", "My location"), (read.Latitude, read.Longitude, read.LocationName, read.Title));
        Assert.Equal((1d, 2d, "Accra, Ghana"), (config.Latitude, config.Longitude, config.LocationName));
    }

    [Fact]
    public void S2853_without_a_device_position_the_copy_has_no_place()
    {
        var sky = new Sky { UseDeviceLocation = true };

        var read = Assert.IsType<Sky>(DevicePlaces.Resolve(sky, DeviceLocation.Denied, "My location"));

        Assert.False(read.HasLocation);
        Assert.Equal(string.Empty, read.Place);
        Assert.True(sky.HasLocation); // still the place chosen by hand (Zurich), kept for the switch going off
    }

    [Fact]
    public void S2854_a_widget_on_the_device_waits_while_windows_gives_no_position()
    {
        var config = new Weather { UseDeviceLocation = true };

        Assert.Equal(DeviceLocationState.Locating, DevicePlaces.Waits(config, DeviceLocation.Off));
        Assert.Equal(DeviceLocationState.Locating, DevicePlaces.Waits(config, DeviceLocation.Locating));
        Assert.Equal(DeviceLocationState.Denied, DevicePlaces.Waits(config, DeviceLocation.Denied));
        Assert.Equal(DeviceLocationState.Unavailable, DevicePlaces.Waits(config, DeviceLocation.Unavailable));
        Assert.Null(DevicePlaces.Waits(config, Zurich));
        Assert.Null(DevicePlaces.Waits(new Weather(), DeviceLocation.Denied));
    }

    [Fact]
    public void S2850_the_switch_on_is_set_up_and_clearing_the_place_turns_it_off()
    {
        var config = new Weather { UseDeviceLocation = true };
        ISetupRule rule = config;

        Assert.False(rule.NeedsSetup(FeatureSet.None));

        config.ClearLocation();
        Assert.False(config.UseDeviceLocation);
        Assert.True(rule.NeedsSetup(FeatureSet.None));
    }

    [Fact]
    public void S2850_the_switch_is_copied_with_the_config()
    {
        Assert.True(((Weather)new Weather { UseDeviceLocation = true }.Clone()).UseDeviceLocation);
        Assert.True(((Sky)new Sky { UseDeviceLocation = true }.Clone()).UseDeviceLocation);
    }
}
