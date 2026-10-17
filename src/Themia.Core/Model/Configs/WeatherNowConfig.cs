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
        StyleOption.Of(WeatherNowStyle.Standard, Str.Styles_WeatherNow_Standard.Local(), 0xE753),
        StyleOption.Of(WeatherNowStyle.LargeIcon, Str.Styles_WeatherNow_LargeIcon.Local(), 0xE706),
        StyleOption.Of(WeatherNowStyle.Minimal, Str.Styles_WeatherNow_Minimal.Local(), 0xE738),
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
