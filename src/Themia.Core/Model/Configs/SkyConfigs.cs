using System.Text.Json.Serialization;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Core.Model;

/// <summary>[S-6074] The sun's rows that only some styles draw.</summary>
public enum SunSetting
{
    /// <summary>The sunrise and sunset rows under the graphic: not the times style, which is them.</summary>
    Events,
}

/// <summary>[S-6074] The moon's rows that only some styles read.</summary>
public enum MoonSetting
{
    /// <summary>The longitude: the disc's rise and set.</summary>
    Longitude,
}

/// <summary>
/// [S-6045] How the Sun draws its day: the path across the sky, the day as a bar with sunrise and
/// sunset on it, or the times alone (what was the hidden sun path).
/// </summary>
[JsonConverter(typeof(StyleJsonConverter<SunStyle>))]
public enum SunStyle
{
    Arc = 0,
    DayBar = 1,
    Times = 2,
}

/// <summary>[S-6050] How the Moon draws itself: its disc, or this month's phases as small discs.</summary>
[JsonConverter(typeof(StyleJsonConverter<MoonStyle>))]
public enum MoonStyle
{
    Disc = 0,
    PhaseMonth = 1,
}

/// <summary>
/// [S-3101] What the Sun and Moon widgets share: where the observer is. The astronomy runs offline
/// from these coordinates; the place name is only the header's title.
/// <para>
/// [S-2848] A new tile starts at Zurich; a cleared place is null coordinates, written as null (not
/// left out, which would read back as Zurich), and the tile waits for setup like a weather widget.
/// </para>
/// </summary>
public abstract class SkyPlaceConfig : WidgetConfig, IPlaced
{
    /// <summary>Zurich, the original's default.</summary>
    public const double DefaultLatitude = 47.37;

    public const double DefaultLongitude = 8.54;

    public const string DefaultPlace = "Zurich";

    /// <summary>A place name is a header title, not a document.</summary>
    public const int MaxPlaceLength = 80;

    /// <summary>Degrees north, -90 to 90, or null once the place is cleared ([S-2848]).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public double? Latitude { get; set; } = DefaultLatitude;

    /// <summary>Degrees east, -180 to 180, or null once the place is cleared ([S-2848]).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public double? Longitude { get; set; } = DefaultLongitude;

    /// <summary>The place name shown in the header.</summary>
    public string Place { get; set; } = DefaultPlace;

    /// <summary>[S-2848] Whether a place is chosen. Zero is a real coordinate, so a null test.</summary>
    [JsonIgnore]
    public bool HasLocation => Latitude is { } lat
        && Longitude is { } lon
        && double.IsFinite(lat)
        && double.IsFinite(lon);

    /// <summary>[S-2848] No place: the tile waits for setup until one is picked or typed.</summary>
    public void ClearLocation()
    {
        Latitude = null;
        Longitude = null;
        Place = string.Empty;
    }

    /// <summary>
    /// A position in the sky is only interesting at minute granularity; the rest of the readout
    /// changes a few times a day.
    /// </summary>
    [JsonIgnore]
    public TimeSpan TickInterval => TimeSpan.FromMinutes(1);

    /// <summary>
    /// The coordinates actually used. Clamped rather than rejected as a second line behind
    /// <see cref="Repair"/>: the widget draws every tick and must never throw on a value in memory.
    /// </summary>
    [JsonIgnore]
    public double EffectiveLatitude => Latitude is { } lat && double.IsFinite(lat) ? Math.Clamp(lat, -90, 90) : DefaultLatitude;

    [JsonIgnore]
    public double EffectiveLongitude => Longitude is { } lon && double.IsFinite(lon) ? Math.Clamp(lon, -180, 180) : DefaultLongitude;

    /// <summary>
    /// [S-3101] A coordinate that is not a number in range would put the observer somewhere they
    /// never chose, so the pair goes back to the default place together, and says so. A null
    /// coordinate is no place ([S-2848]), not a bad one: it is left for setup.
    /// </summary>
    public override IReadOnlyList<string> Repair()
    {
        var repairs = new List<string>();

        if ((Latitude is { } lat && !InRange(lat, 90)) || (Longitude is { } lon && !InRange(lon, 180)))
        {
            repairs.Add($"{Kind} coordinates out of range; {DefaultPlace}");
            Latitude = DefaultLatitude;
            Longitude = DefaultLongitude;
            Place = DefaultPlace;
        }

        if (Place is null)
        {
            repairs.Add($"{Kind} place missing; {DefaultPlace}");
            Place = DefaultPlace;
        }
        else if (Place.Length > MaxPlaceLength)
        {
            repairs.Add($"{Kind} place longer than {MaxPlaceLength}; cut");
            Place = Place[..MaxPlaceLength];
        }

        RepairOwn(repairs);
        return repairs;
    }

    /// <summary>The derived widget's own repairs, appended to the shared ones.</summary>
    protected virtual void RepairOwn(List<string> repairs)
    {
    }

    /// <summary>Copy the shared fields onto <paramref name="target"/>.</summary>
    protected T CopyPlaceTo<T>(T target) where T : SkyPlaceConfig
    {
        target.Latitude = Latitude;
        target.Longitude = Longitude;
        target.Place = Place;
        return target;
    }

    private static bool InRange(double value, double limit) => double.IsFinite(value) && value >= -limit && value <= limit;
}

/// <summary>[S-3100] The sun's day where the observer is: its path, rise, set and daylight.</summary>
public sealed class SunConfig : SkyPlaceConfig, IStyled, IShownSettings<SunSetting>
{
    private static readonly StyleOption[] SunStyles =
    [
        StyleOption.Of(SunStyle.Arc, Str.Styles_Sun_Arc, 0xE706),
        StyleOption.Of(SunStyle.DayBar, Str.Styles_Sun_DayBar, 0xE7A5),
        StyleOption.Of(SunStyle.Times, Str.Styles_Sun_Times, 0xEA37),
    ];

    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.Sun;

    /// <summary>[S-6902] This widget's descriptor: what the host knows of it without its UI.</summary>
    public static WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("sun"),
        Kind = WidgetKind.Sun,
        ConfigType = typeof(SunConfig),
        CreateDefault = () => new SunConfig(),
        Json = WidgetConfigJsonContext.Default,
        Keywords = ["sunrise", "sunset", "daylight", "golden hour", "dawn", "dusk", "solar noon", "astronomy"],
        LabelId = Str.WidgetsC_Sun_Label,
        DescriptionId = Str.WidgetsC_Sun_Desc,
        Glyph = Glyphs.Sun,
        DefaultWidth = 320,
        DefaultHeight = 240,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader | WidgetTraits.StartsHeaderless,
        Add = new(AddFolderId.Weather, 1, 0),
    };

    /// <summary>[S-6045] The sun's day as an arc, a bar, or the times alone.</summary>
    public SunStyle Style { get; set; } = SunStyle.Arc;

    IReadOnlyList<StyleOption> IStyled.Styles => SunStyles;

    int IStyled.StyleValue
    {
        get => (int)Style;
        set => Style = (SunStyle)value;
    }

    /// <summary>[S-170] Show the sunrise/sunset/daylight rows.</summary>
    public bool ShowEvents { get; set; } = true;

    /// <summary>[S-6074] The times style is the events alone, always drawn: the switch is the graphic styles' own.</summary>
    [JsonIgnore]
    public IReadOnlyList<SunSetting> ShownSettings => Style == SunStyle.Times ? [] : [SunSetting.Events];

    public override WidgetConfig Clone() => CopyPlaceTo(new SunConfig
    {
        Style = Style,
        ShowEvents = ShowEvents,
    });
}

/// <summary>[S-3100] The moon where the observer is: its phase, rise and set.</summary>
public sealed class MoonConfig : SkyPlaceConfig, IStyled, IShownSettings<MoonSetting>, IContentAligned
{
    private static readonly StyleOption[] MoonStyles =
    [
        StyleOption.Of(MoonStyle.Disc, Str.Styles_Moon_Disc, 0xE708),
        StyleOption.Of(MoonStyle.PhaseMonth, Str.Styles_Moon_PhaseMonth, 0xE787),
    ];

    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.Moon;

    /// <summary>[S-6902] This widget's descriptor: what the host knows of it without its UI.</summary>
    public static WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("moon"),
        Kind = WidgetKind.Moon,
        ConfigType = typeof(MoonConfig),
        CreateDefault = () => new MoonConfig(),
        Json = WidgetConfigJsonContext.Default,
        Keywords = ["moon phase", "full moon", "new moon", "moonrise", "moonset", "lunar", "astronomy"],
        LabelId = Str.WidgetsC_Moon_Label,
        DescriptionId = Str.WidgetsC_Moon_Desc,
        Glyph = Glyphs.Moon,
        DefaultWidth = 240,
        DefaultHeight = 220,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader | WidgetTraits.StartsHeaderless,
        Add = new(AddFolderId.Weather, 1, 1),
    };

    /// <summary>[S-6581] Its one block aligns; where it sits when nothing was chosen.</summary>
    bool IContentAligned.AlignApplies => Style == MoonStyle.Disc;
    ContentAlign IContentAligned.DefaultAlign => ContentAlign.Center;

    /// <summary>[S-6050] Tonight's disc, or the month's phases.</summary>
    public MoonStyle Style { get; set; } = MoonStyle.Disc;

    IReadOnlyList<StyleOption> IStyled.Styles => MoonStyles;

    int IStyled.StyleValue
    {
        get => (int)Style;
        set => Style = (MoonStyle)value;
    }

    /// <summary>
    /// [S-6074] The month's phases read only the latitude's sign (which way round a southern
    /// observer sees the lit side); the longitude is the disc's, for the rise and set.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<MoonSetting> ShownSettings => Style == MoonStyle.Disc ? [MoonSetting.Longitude] : [];

    public override WidgetConfig Clone() => CopyPlaceTo(new MoonConfig { Style = Style });
}
