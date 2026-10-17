using System.Text.Json.Serialization;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Storage;
using Themia.Core.Sync;
using Themia.Core.Widgets;
using Themia.Sdk;

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

/// <summary>Conditions right now, from open-meteo's <c>current</c> block.</summary>
public sealed class WeatherNowConfig : WeatherConfig, IStyled, IShownSettings<WeatherNowSetting>, IContentAligned, IInterests
{
    [JsonIgnore]
    public override bool ReadsWeather => true;

    /// <summary>[S-2608] What the background sync keeps fresh for it.</summary>
    public void AddInterests(InterestsBuilder interests)
    {
        if (SyncInterests.For(this, interests.Unit, UpdateCadence.Interval(interests.Refresh, HostCadences.Weather)) is { } wants)
        {
            interests.Weather.Add(wants);
        }
    }

    private static readonly StyleOption[] NowStyles =
    [
        StyleOption.Of(WeatherNowStyle.Standard, Str.Styles_WeatherNow_Standard, 0xE753),
        StyleOption.Of(WeatherNowStyle.LargeIcon, Str.Styles_WeatherNow_LargeIcon, 0xE706),
        StyleOption.Of(WeatherNowStyle.Minimal, Str.Styles_WeatherNow_Minimal, 0xE738),
    ];


    /// <summary>[S-6902] This widget's descriptor: what the host knows of it without its UI.</summary>
    public static WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("weatherNow"),
        ConfigType = typeof(WeatherNowConfig),
        CreateDefault = () => new WeatherNowConfig(),
        Json = WidgetConfigJsonContext.Default,
        Keywords = ["temperature", "forecast", "rain", "wind", "humidity", "climate", "degrees"],
        Label = Str.Widget_WeatherCurrent_Label.Local(),
        Description = Str.WidgetsA_Catalog_Weather_Description.Local(),
        Glyph = Glyphs.Weather,
        DefaultWidth = 260,
        DefaultHeight = 200,
        Traits = WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.Weather, 0, 0, Popular: 1),
    };

    /// <summary>[S-6581] Its one block aligns; where it sits when nothing was chosen.</summary>
    ContentAlign IContentAligned.DefaultAlign => Style == WeatherNowStyle.Standard ? ContentAlign.Left : ContentAlign.Center;

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
