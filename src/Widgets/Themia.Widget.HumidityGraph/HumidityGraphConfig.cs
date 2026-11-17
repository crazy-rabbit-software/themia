using System.Text.Json.Serialization;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Widget.HumidityGraph;

/// <summary>[S-10601] The graph's looks: the same hours, drawn as the weather graphs draw ([S-12703]).</summary>
[JsonConverter(typeof(StyleJsonConverter<HumidityGraphStyle>))]
public enum HumidityGraphStyle
{
    Area = 0,
    Line = 1,
}

/// <summary>[S-10600] The graph's config: the place and window every weather graph holds ([S-12707]) and its look.</summary>
public sealed class HumidityGraphConfig : HourGraphConfig, IStyled
{
    private static readonly StyleOption[] Looks =
    [
        StyleOption.Of(HumidityGraphStyle.Area, Str.WeatherGraph_Style_Area.Local(), 0xE9D2),
        StyleOption.Of(HumidityGraphStyle.Line, Str.WeatherGraph_Style_Line.Local(), 0xE9D9),
    ];

    public HumidityGraphStyle Style { get; set; } = HumidityGraphStyle.Area;

    IReadOnlyList<StyleOption> IStyled.Styles => Looks;

    int IStyled.StyleValue
    {
        get => (int)Style;
        set => Style = (HumidityGraphStyle)value;
    }

    public override WidgetConfig Clone()
    {
        var copy = new HumidityGraphConfig { Style = Style };
        CopyTo(copy);
        return copy;
    }
}
