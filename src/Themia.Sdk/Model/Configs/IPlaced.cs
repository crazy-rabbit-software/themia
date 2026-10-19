using Themia.Core.Accounts;
using Themia.Sdk;

namespace Themia.Core.Model;

/// <summary>
/// [S-2848] A widget about one place on Earth: the weather family (current conditions, forecast,
/// air quality, rain) and the sky (Sun, Moon). One rule for all of them: the place can be chosen and
/// it can be cleared, and a cleared place is no place, so the widget waits for setup ([S-1860])
/// rather than falling back to somewhere the user never picked.
/// </summary>
public interface IPlaced : ISetupRule
{
    /// <summary>Whether a place is chosen: both coordinates set and finite.</summary>
    bool HasLocation { get; }

    /// <summary>
    /// [S-2850] The place is wherever Windows says the device is, not the one chosen by hand (which is
    /// kept, unused, for when the switch goes off). The config stores the switch only, never the
    /// device's coordinates: the host hands the widget a copy at them (<see cref="DevicePlaces.Resolve"/>).
    /// </summary>
    bool UseDeviceLocation { get; set; }

    /// <summary>
    /// Forget the place: the coordinates and the name it resolved to, and the device's location as
    /// its source. Display preferences (whether the header names the place) are the user's and stay.
    /// </summary>
    void ClearLocation();

    /// <summary>
    /// [S-2853] A copy of the config at the device's position (<paramref name="latitude"/> and
    /// <paramref name="longitude"/> already rounded; both null while there is none) under
    /// <paramref name="name"/>: what the widget reads while it uses the device's location.
    /// </summary>
    WidgetConfig AtDevice(double? latitude, double? longitude, string name);

    /// <summary>[S-2848] [S-2850] No place chosen, or cleared, is the Set up state; the device's location is a place.</summary>
    bool ISetupRule.NeedsSetup(FeatureSet signedIn) => !HasLocation && !UseDeviceLocation;
}
