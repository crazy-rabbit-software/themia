namespace Themia.Core.Model;

/// <summary>[S-2851] Where Windows' location stands for the widgets that use it.</summary>
public enum DeviceLocationState
{
    /// <summary>No widget uses the device's location: nothing is asked of Windows.</summary>
    Off,

    /// <summary>Asked, no position yet (the first fix, or after Windows turned location back on).</summary>
    Locating,

    /// <summary>A position is held.</summary>
    Located,

    /// <summary>Windows says no: location is turned off, for the device or for Themia.</summary>
    Denied,

    /// <summary>Allowed, but Windows has no position to give (no Wi-Fi to place it, a failure).</summary>
    Unavailable,
}

/// <summary>What Windows answered when asked for access to the location ([S-2851]).</summary>
public enum DeviceAccess
{
    Allowed,
    Denied,
}

/// <summary>What Windows' locator says of itself as it changes ([S-2851]).</summary>
public enum DeviceStatus
{
    /// <summary>Ready or starting: positions will come.</summary>
    Working,

    /// <summary>Location is turned off for the device or for Themia.</summary>
    Disabled,

    /// <summary>No position can be had on this device now.</summary>
    NotAvailable,
}

/// <summary>
/// [S-2851] [S-2852] The device's location as the widgets may see it: a state and, while
/// <see cref="DeviceLocationState.Located"/>, a position already rounded to about a kilometre
/// (<see cref="DeviceLocationRules.Grain"/>), so nothing finer is ever held, drawn, asked for or
/// cached. Never stored in a widget's config: the config holds the switch only.
/// </summary>
public readonly record struct DeviceLocation(DeviceLocationState State, double Latitude = 0, double Longitude = 0)
{
    public static DeviceLocation Off => new(DeviceLocationState.Off);

    public static DeviceLocation Locating => new(DeviceLocationState.Locating);

    public static DeviceLocation Denied => new(DeviceLocationState.Denied);

    public static DeviceLocation Unavailable => new(DeviceLocationState.Unavailable);

    /// <summary>A position, rounded to the grain; a reading that is not a place on Earth is no position.</summary>
    public static DeviceLocation At(double latitude, double longitude)
        => DeviceLocationRules.IsPlace(latitude, longitude)
            ? new(DeviceLocationState.Located, DeviceLocationRules.Round(latitude), DeviceLocationRules.Round(longitude))
            : Unavailable;

    public bool IsLocated => State == DeviceLocationState.Located;
}

/// <summary>
/// [S-2851] [S-2852] The device location's rules, pure: the privacy grain, when a move is worth a
/// new position, and how the state follows what Windows says. The host's service feeds Windows'
/// answers in and publishes what comes out; nothing here waits or polls.
/// </summary>
public static class DeviceLocationRules
{
    /// <summary>[S-2852] Degrees a position is rounded to: 0.01° is about 1.1 km north to south (less east to west).</summary>
    public const double Grain = 0.01;

    /// <summary>
    /// [S-2852] How far, in kilometres, the device must move before the widgets get a new position:
    /// twice the grain, so a reading that wobbles across a rounding edge does not move the weather.
    /// </summary>
    public const double MoveKm = 2;

    /// <summary>The metres Windows is asked to move before it reports again (its own filter, under <see cref="MoveKm"/>).</summary>
    public const double MovementThresholdMeters = 1000;

    /// <summary>[S-2852] A coordinate on the privacy grain.</summary>
    public static double Round(double degrees) => Math.Round(Math.Round(degrees / Grain) * Grain, 2);

    /// <summary>Finite and on Earth.</summary>
    public static bool IsPlace(double latitude, double longitude)
        => double.IsFinite(latitude) && double.IsFinite(longitude) && latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180;

    /// <summary>Great-circle distance in kilometres (haversine, mean Earth radius).</summary>
    public static double DistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double radius = 6371.0088;
        static double Rad(double degrees) => degrees * Math.PI / 180;
        var dLat = Rad(lat2 - lat1);
        var dLon = Rad(lon2 - lon1);
        var a = (Math.Sin(dLat / 2) * Math.Sin(dLat / 2)) + (Math.Cos(Rad(lat1)) * Math.Cos(Rad(lat2)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2));
        return 2 * radius * Math.Asin(Math.Min(1, Math.Sqrt(a)));
    }

    /// <summary>
    /// [S-2851] Whether any widget wants the location changed: wanted from <see cref="DeviceLocationState.Off"/>
    /// starts locating; not wanted is off, whatever was held (the position is forgotten).
    /// </summary>
    public static DeviceLocation Wanted(DeviceLocation now, bool wanted)
        => !wanted ? DeviceLocation.Off : now.State == DeviceLocationState.Off ? DeviceLocation.Locating : now;

    /// <summary>[S-2851] Windows answered the access request: denied is denied; allowed keeps what is held (positions come).</summary>
    public static DeviceLocation Access(DeviceLocation now, DeviceAccess access)
        => now.State == DeviceLocationState.Off ? now
            : access == DeviceAccess.Denied ? DeviceLocation.Denied
            : now.State is DeviceLocationState.Denied or DeviceLocationState.Unavailable ? DeviceLocation.Locating
            : now;

    /// <summary>[S-2851] The locator said how it stands: turned off is denied; back on looks again; nothing to give is unavailable unless a position is held.</summary>
    public static DeviceLocation Status(DeviceLocation now, DeviceStatus status) => now.State == DeviceLocationState.Off ? now : status switch
    {
        DeviceStatus.Disabled => DeviceLocation.Denied,
        DeviceStatus.NotAvailable => now.IsLocated ? now : DeviceLocation.Unavailable,
        _ => now.State is DeviceLocationState.Denied or DeviceLocationState.Unavailable ? DeviceLocation.Locating : now,
    };

    /// <summary>
    /// [S-2852] A position came: the first one, or one at least <see cref="MoveKm"/> from the one
    /// held, is the new position (rounded); a smaller move keeps the one held, so the widgets ask for
    /// nothing new.
    /// </summary>
    public static DeviceLocation Position(DeviceLocation now, double latitude, double longitude)
    {
        if (now.State == DeviceLocationState.Off || !IsPlace(latitude, longitude))
        {
            return now;
        }

        return now.IsLocated && DistanceKm(now.Latitude, now.Longitude, latitude, longitude) < MoveKm
            ? now
            : DeviceLocation.At(latitude, longitude);
    }

    /// <summary>[S-2851] Asking for a position failed (a timeout, no fix): a position held stays; otherwise unavailable.</summary>
    public static DeviceLocation Failed(DeviceLocation now)
        => now.State == DeviceLocationState.Off || now.IsLocated ? now : DeviceLocation.Unavailable;
}

/// <summary>
/// [S-2853] A widget about a place that uses the device's location, as the host hands it on: its
/// view, its sync wants and its title read a copy of its config with the device's position in it,
/// so no view, interest or title knows where the coordinates came from, and the stored config keeps
/// the switch and the place last chosen by hand, never the device's coordinates.
/// </summary>
public static class DevicePlaces
{
    /// <summary>
    /// The config as the widget reads it: <paramref name="config"/> itself unless it is a place that
    /// uses the device's location; then a copy at <paramref name="device"/>'s position named
    /// <paramref name="name"/> (the localized "My location"), or with no place while there is none.
    /// </summary>
    public static WidgetConfig Resolve(WidgetConfig config, DeviceLocation device, string name)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(name);

        return config is IPlaced { UseDeviceLocation: true } placed
            ? placed.AtDevice(device.IsLocated ? device.Latitude : null, device.IsLocated ? device.Longitude : null, name)
            : config;
    }

    /// <summary>
    /// [S-2854] What the widget waits for instead of running: the device location's state while it
    /// uses it and none is held (locating, denied, unavailable); null when it has its place.
    /// </summary>
    public static DeviceLocationState? Waits(WidgetConfig config, DeviceLocation device)
    {
        ArgumentNullException.ThrowIfNull(config);

        return config is IPlaced { UseDeviceLocation: true } && !device.IsLocated
            ? device.State == DeviceLocationState.Off ? DeviceLocationState.Locating : device.State
            : null;
    }
}
