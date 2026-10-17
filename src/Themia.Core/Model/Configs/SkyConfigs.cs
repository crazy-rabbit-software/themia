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

/// <summary>[S-3100] The sun's day where the observer is: its path, rise, set and daylight.</summary>
public sealed class SunConfig : SkyPlaceConfig, IStyled, IShownSettings<SunSetting>
{
    private static readonly StyleOption[] SunStyles =
    [
        StyleOption.Of(SunStyle.Arc, Str.Styles_Sun_Arc.Local(), 0xE706),
        StyleOption.Of(SunStyle.DayBar, Str.Styles_Sun_DayBar.Local(), 0xE7A5),
        StyleOption.Of(SunStyle.Times, Str.Styles_Sun_Times.Local(), 0xEA37),
    ];


    /// <summary>[S-6902] This widget's descriptor: what the host knows of it without its UI.</summary>
    public static WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("sun"),
        ConfigType = typeof(SunConfig),
        CreateDefault = () => new SunConfig(),
        Json = WidgetConfigJsonContext.Default,
        Keywords = ["sunrise", "sunset", "daylight", "golden hour", "dawn", "dusk", "solar noon", "astronomy"],
        Label = Str.WidgetsC_Sun_Label.Local(),
        Description = Str.WidgetsC_Sun_Desc.Local(),
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
