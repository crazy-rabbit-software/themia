using System.Text.Json.Serialization;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Widget.Sun;

namespace Themia.Core.Model;

/// <summary>[S-6045] How the Sun draws its day: the path across the sky, or the day as a bar.</summary>
[JsonConverter(typeof(StyleJsonConverter<SunStyle>))]
public enum SunStyle
{
    Arc = 0,
    DayBar = 1,
}

/// <summary>[S-11110] The sun's day where the observer is, as a graphic; its times are the <c>sunTimes</c> atom's.</summary>
public sealed class SunConfig : SkyPlaceConfig, IStyled
{
    private static readonly StyleOption[] SunStyles =
    [
        StyleOption.Of(SunStyle.Arc, SunStr.Styles_Sun_Arc.Local(), 0xE706),
        StyleOption.Of(SunStyle.DayBar, SunStr.Styles_Sun_DayBar.Local(), 0xE7A5),
    ];

    /// <summary>[S-6045] The sun's day as an arc or a bar.</summary>
    public SunStyle Style { get; set; } = SunStyle.Arc;

    IReadOnlyList<StyleOption> IStyled.Styles => SunStyles;

    int IStyled.StyleValue
    {
        get => (int)Style;
        set => Style = (SunStyle)value;
    }

    public override WidgetConfig Clone() => CopyPlaceTo(new SunConfig { Style = Style });
}
