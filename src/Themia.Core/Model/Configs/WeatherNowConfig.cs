using System.Text.Json.Serialization;
using Themia.Core.Localization;
using Themia.Core.Widgets;

namespace Themia.Core.Model;

/// <summary>[S-6074] The current conditions' rows that only some styles draw.</summary>
public enum WeatherNowSetting
{
    /// <summary>The feels-like, humidity and wind line (Standard).</summary>
    Details,
}

/// <summary>[S-6160] The current conditions' styles: the same reading, three looks.</summary>
[JsonConverter(typeof(StyleJsonConverter<WeatherNowStyle>))]
public enum WeatherNowStyle
{
    /// <summary>[S-723] The reading and its readings, on the ladder.</summary>
    Standard = 0,

    /// <summary>[S-6161] The sky's glyph large, the temperature beside it.</summary>
    LargeIcon = 1,

    /// <summary>[S-6162] The temperature alone, in a light weight.</summary>
    Minimal = 2,
}

/// <summary>Which scale temperatures are asked for and shown in.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<TemperatureUnit>))]
public enum TemperatureUnit
{
    Celsius = 0,
    Fahrenheit = 1,
}

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
public abstract class WeatherConfig : WidgetConfig
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

    /// <summary>[S-3780] The shared weather cadence (Settings › Widgets), never faster than open-meteo's own cycle.</summary>
    [JsonIgnore]
    public TimeSpan TickInterval => Sync.UpdateCadence.Interval(Sync.UpdateSource.Weather);

    protected void CopyTo(WeatherConfig other)
    {
        other.Latitude = Latitude;
        other.Longitude = Longitude;
        other.LocationName = LocationName;
        other.HideLocation = HideLocation;
    }
}

/// <summary>Conditions right now, from open-meteo's <c>current</c> block.</summary>
public sealed class WeatherNowConfig : WeatherConfig, IStyled, IShownSettings<WeatherNowSetting>
{
    private static readonly StyleOption[] NowStyles =
    [
        StyleOption.Of(WeatherNowStyle.Standard, Str.Styles_WeatherNow_Standard, 0xE753),
        StyleOption.Of(WeatherNowStyle.LargeIcon, Str.Styles_WeatherNow_LargeIcon, 0xE706),
        StyleOption.Of(WeatherNowStyle.Minimal, Str.Styles_WeatherNow_Minimal, 0xE738),
    ];

    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.WeatherNow;

    /// <summary>[S-6160] The widget's look.</summary>
    public WeatherNowStyle Style { get; set; } = WeatherNowStyle.Standard;

    IReadOnlyList<StyleOption> IStyled.Styles => NowStyles;

    int IStyled.StyleValue
    {
        get => (int)Style;
        set => Style = (WeatherNowStyle)value;
    }

    /// <summary>Show the feels-like, humidity and wind line under the temperature.</summary>
    public bool ShowDetails { get; set; } = true;

    /// <summary>[S-6074] The readings line is the standard style's own: the large icon and the minimal style never draw it.</summary>
    [JsonIgnore]
    public IReadOnlyList<WeatherNowSetting> ShownSettings => Style == WeatherNowStyle.Standard ? [WeatherNowSetting.Details] : [];

    public override WidgetConfig Clone()
    {
        var copy = new WeatherNowConfig { Style = Style, ShowDetails = ShowDetails };
        CopyTo(copy);
        return copy;
    }
}
