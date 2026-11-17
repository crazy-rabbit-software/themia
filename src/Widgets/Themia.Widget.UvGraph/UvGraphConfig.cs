using System.Text.Json.Serialization;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Widget.UvGraph;

/// <summary>[S-10301] The graph's looks: the same hours, drawn as the weather graphs draw ([S-12703]).</summary>
[JsonConverter(typeof(StyleJsonConverter<UvGraphStyle>))]
public enum UvGraphStyle
{
    Bars = 0,
    Area = 1,
}

/// <summary>[S-10300] The graph's config: the place and window every weather graph holds ([S-12707]) and its look.</summary>
public sealed class UvGraphConfig : HourGraphConfig, IStyled
{
    private static readonly StyleOption[] Looks =
    [
        StyleOption.Of(UvGraphStyle.Bars, Str.WeatherGraph_Style_Bars.Local(), 0xE9D2),
        StyleOption.Of(UvGraphStyle.Area, Str.WeatherGraph_Style_Area.Local(), 0xE9D2),
    ];

    public UvGraphStyle Style { get; set; } = UvGraphStyle.Bars;

    IReadOnlyList<StyleOption> IStyled.Styles => Looks;

    int IStyled.StyleValue
    {
        get => (int)Style;
        set => Style = (UvGraphStyle)value;
    }

    public override WidgetConfig Clone()
    {
        var copy = new UvGraphConfig { Style = Style };
        CopyTo(copy);
        return copy;
    }
}
