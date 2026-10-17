using System.Text.Json.Serialization;
using Themia.Core.Sync;
using Themia.Sdk;

namespace Themia.Core.Model;

/// <summary>
/// Settings shared by the two weather widgets: where. The unit and the cadence are every widget's
/// ([S-3770], [S-3780]), not one widget's.
/// <para>
/// The place is stored as <b>coordinates plus the name they resolved to</b>, not as the text the
/// user typed. Re-geocoding a free-text place on every refresh would mean two requests instead of
/// one, and a widget whose location silently moves when the geocoder changes its mind about
/// "Springfield". The typed query is not kept at all ([S-2846]): half a word is not a place.
/// </para>
/// </summary>
public abstract class WeatherConfig : WidgetConfig, IPlaced
{
    /// <summary>Degrees north, or null when no location has been chosen. Zero is a real latitude.</summary>
    public double? Latitude { get; set; }

    /// <summary>Degrees east, or null when no location has been chosen. Zero is a real longitude.</summary>
    public double? Longitude { get; set; }

    /// <summary>The chosen place's label ([S-2842]), e.g. "Zürich, Zurich, Switzerland".</summary>
    public string LocationName { get; set; } = string.Empty;

    /// <summary>
    /// [S-728] Keep the place's name out of the card header, which then reads as the widget's own
    /// label ("Weather"). A title the user typed still wins either way.
    /// </summary>
    public bool HideLocation { get; set; }

    /// <summary>
    /// [S-728] The header title for the chosen place: the city, the first part of the name the
    /// geocoder gave ("Zürich" of "Zürich, Zurich, Switzerland"), not the text that was typed to
    /// find it, which is as often "zurich" or "zuerich". Null until a place has been picked.
    /// </summary>
    [JsonIgnore]
    public string? Title
    {
        get
        {
            if (HideLocation)
            {
                return null;
            }

            var name = LocationName.Split(',', 2)[0].Trim();

            return name.Length > 0 ? name : null;
        }
    }

    /// <summary>
    /// Whether a location has actually been chosen.
    /// <para>
    /// A null test, not a truthiness test: latitude 0 is the equator and longitude 0 is the prime
    /// meridian, and a widget pointed at Accra must not read as unconfigured.
    /// </para>
    /// </summary>
    [JsonIgnore]
    public bool HasLocation => Latitude is { } lat
        && Longitude is { } lon
        && double.IsFinite(lat)
        && double.IsFinite(lon);

    /// <summary>[S-2848] No place: the widget waits for setup again, and asks for nothing.</summary>
    public void ClearLocation()
    {
        Latitude = null;
        Longitude = null;
        LocationName = string.Empty;
    }

    /// <summary>
    /// [S-5101] [S-5121] Whether it reads the weather feed itself (the two weather widgets); its
    /// siblings on the same place (rain, air quality) ask their own feeds.
    /// </summary>
    [JsonIgnore]
    public virtual bool ReadsWeather => false;

    /// <summary>The forecast days it asks the weather feed for; null for the conditions now only.</summary>
    [JsonIgnore]
    public virtual int? ForecastDays => null;

    /// <summary>[S-3780] The shared weather cadence (Settings › Widgets), never faster than open-meteo's own cycle.</summary>
    [JsonIgnore]
    public TimeSpan TickInterval => Sync.UpdateCadence.Interval(Sync.HostCadences.Weather);

    protected void CopyTo(WeatherConfig other)
    {
        other.Latitude = Latitude;
        other.Longitude = Longitude;
        other.LocationName = LocationName;
        other.HideLocation = HideLocation;
    }
}
