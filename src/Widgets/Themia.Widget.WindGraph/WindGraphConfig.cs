using System.Text.Json.Serialization;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Widget.WindGraph;

/// <summary>[S-10501] The graph's looks: the same hours, drawn as the weather graphs draw ([S-12703]).</summary>
[JsonConverter(typeof(StyleJsonConverter<WindGraphStyle>))]
public enum WindGraphStyle
{
    Line = 0,
    Bars = 1,
}

/// <summary>[S-10500] The graph's config: the place and window every weather graph holds ([S-12707]) and its look.</summary>
public sealed class WindGraphConfig : HourGraphConfig, IStyled
{
    private static readonly StyleOption[] Looks =
    [
        StyleOption.Of(WindGraphStyle.Line, Str.WeatherGraph_Style_Line.Local(), 0xE9D9),
        StyleOption.Of(WindGraphStyle.Bars, Str.WeatherGraph_Style_Bars.Local(), 0xE9D2),
    ];

    public WindGraphStyle Style { get; set; } = WindGraphStyle.Line;

    /// <summary>[S-10502] Whether the gusts as the graph's second, tertiary line is drawn.</summary>
    public bool ShowGusts { get; set; } = true;

    IReadOnlyList<StyleOption> IStyled.Styles => Looks;

    int IStyled.StyleValue
    {
        get => (int)Style;
        set => Style = (WindGraphStyle)value;
    }

    public override WidgetConfig Clone()
    {
        var copy = new WindGraphConfig { Style = Style, ShowGusts = ShowGusts };
        CopyTo(copy);
        return copy;
    }
}
