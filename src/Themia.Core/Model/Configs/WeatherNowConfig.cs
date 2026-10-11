using System.Text.Json.Serialization;

namespace Themia.Core.Model;

/// <summary>Which scale temperatures are asked for and shown in.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<TemperatureUnit>))]
public enum TemperatureUnit
{
    Celsius = 0,
    Fahrenheit = 1,
}

/// <summary>
/// Settings shared by the two weather widgets: where, in what unit, and how often.
/// <para>
/// The place is stored as <b>coordinates plus the name they resolved to</b>, not as the text the
/// user typed. Re-geocoding a free-text place on every refresh would mean two requests instead of
/// one, and a widget whose location silently moves when the geocoder changes its mind about
/// "Springfield". The typed query is not kept at all ([S-2846]): half a word is not a place.
/// </para>
/// </summary>
public abstract class WeatherConfig : WidgetConfig
{
    /// <summary>Degrees north, or null when no location has been chosen. Zero is a real latitude.</summary>
    public double? Latitude { get; set; }

    /// <summary>Degrees east, or null when no location has been chosen. Zero is a real longitude.</summary>
    public double? Longitude { get; set; }

    /// <summary>The chosen place's label ([S-2842]), e.g. "Zürich, Zurich, Switzerland".</summary>
    public string LocationName { get; set; } = string.Empty;

    public TemperatureUnit Unit { get; set; } = TemperatureUnit.Celsius;

    /// <summary>How often to ask open-meteo, in minutes.</summary>
    public int RefreshMinutes { get; set; } = 15;

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

    /// <summary>
    /// The cadence, clamped. Five minutes is the floor: open-meteo publishes on a 15-minute cycle
    /// and is free and unauthenticated, so asking faster gets the same answer at somebody else's
    /// expense.
    /// </summary>
    [JsonIgnore]
    public TimeSpan TickInterval => TimeSpan.FromMinutes(Math.Clamp(RefreshMinutes, 5, 240));

    protected void CopyTo(WeatherConfig other)
    {
        other.Latitude = Latitude;
        other.Longitude = Longitude;
        other.LocationName = LocationName;
        other.Unit = Unit;
        other.RefreshMinutes = RefreshMinutes;
        other.HideLocation = HideLocation;
    }
}

/// <summary>Conditions right now, from open-meteo's <c>current</c> block.</summary>
public sealed class WeatherNowConfig : WeatherConfig
{
    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.WeatherNow;

    /// <summary>Show the feels-like, humidity and wind line under the temperature.</summary>
    public bool ShowDetails { get; set; } = true;

    public override WidgetConfig Clone()
    {
        var copy = new WeatherNowConfig { ShowDetails = ShowDetails };
        CopyTo(copy);
        return copy;
    }
}
