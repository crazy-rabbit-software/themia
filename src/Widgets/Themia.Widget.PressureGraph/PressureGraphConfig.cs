using System.Text.Json.Serialization;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Widget.PressureGraph;

/// <summary>[S-10701] The graph's looks: the same hours, drawn as the weather graphs draw ([S-12703]).</summary>
[JsonConverter(typeof(StyleJsonConverter<PressureGraphStyle>))]
public enum PressureGraphStyle
{
    Line = 0,
    Area = 1,
}

/// <summary>[S-10700] The graph's config: the place and window every weather graph holds ([S-12707]) and its look.</summary>
public sealed class PressureGraphConfig : HourGraphConfig, IStyled
{
    private static readonly StyleOption[] Looks =
    [
        StyleOption.Of(PressureGraphStyle.Line, Str.WeatherGraph_Style_Line.Local(), 0xE9D9),
        StyleOption.Of(PressureGraphStyle.Area, Str.WeatherGraph_Style_Area.Local(), 0xE9D2),
    ];

    public PressureGraphStyle Style { get; set; } = PressureGraphStyle.Line;

    IReadOnlyList<StyleOption> IStyled.Styles => Looks;

    int IStyled.StyleValue
    {
        get => (int)Style;
        set => Style = (PressureGraphStyle)value;
    }

    public override WidgetConfig Clone()
    {
        var copy = new PressureGraphConfig { Style = Style };
        CopyTo(copy);
        return copy;
    }
}
