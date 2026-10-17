using System.Text.Json.Serialization;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Widget.Moon;

namespace Themia.Core.Model;

/// <summary>[S-6074] The moon's rows that only some styles read.</summary>
public enum MoonSetting
{
    /// <summary>The longitude: the disc's rise and set.</summary>
    Longitude,
}

/// <summary>[S-6050] How the Moon draws itself: its disc, or this month's phases as small discs.</summary>
[JsonConverter(typeof(StyleJsonConverter<MoonStyle>))]
public enum MoonStyle
{
    Disc = 0,
    PhaseMonth = 1,
}

/// <summary>[S-3100] The moon where the observer is: its phase, rise and set.</summary>
public sealed class MoonConfig : SkyPlaceConfig, IStyled, IShownSettings<MoonSetting>, IContentAligned
{
    private static readonly StyleOption[] MoonStyles =
    [
        StyleOption.Of(MoonStyle.Disc, MoonStr.Styles_Moon_Disc.Local(), 0xE708),
        StyleOption.Of(MoonStyle.PhaseMonth, MoonStr.Styles_Moon_PhaseMonth.Local(), 0xE787),
    ];

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
