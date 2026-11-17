using System.Text.Json.Serialization;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Widget.TemperatureGraph;

/// <summary>[S-10101] The temperature graph's looks: the same hours, the curve with its wash or the line alone.</summary>
[JsonConverter(typeof(StyleJsonConverter<TemperatureGraphStyle>))]
public enum TemperatureGraphStyle
{
    Area = 0,
    Line = 1,
}

/// <summary>[S-10100] A temperature graph: a place, its window, its look and whether the feels-like line is drawn.</summary>
public sealed class TemperatureGraphConfig : HourGraphConfig, IStyled
{
    private static readonly StyleOption[] Looks =
    [
        StyleOption.Of(TemperatureGraphStyle.Area, Str.WeatherGraph_Style_Area.Local(), 0xE9D2),
        StyleOption.Of(TemperatureGraphStyle.Line, Str.WeatherGraph_Style_Line.Local(), 0xE9D9),
    ];

    public TemperatureGraphStyle Style { get; set; } = TemperatureGraphStyle.Area;

    /// <summary>[S-10102] The apparent temperature as the graph's second, tertiary line.</summary>
    public bool ShowFeelsLike { get; set; } = true;

    IReadOnlyList<StyleOption> IStyled.Styles => Looks;

    int IStyled.StyleValue
    {
        get => (int)Style;
        set => Style = (TemperatureGraphStyle)value;
    }

    public override WidgetConfig Clone()
    {
        var copy = new TemperatureGraphConfig { Style = Style, ShowFeelsLike = ShowFeelsLike };
        CopyTo(copy);
        return copy;
    }
}
