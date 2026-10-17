using System.Text.Json.Serialization;
using Themia.Core.Widgets;
using Themia.Widget.YearProgress;

namespace Themia.Core.Model;

/// <summary>[S-6040] How Year progress draws its spans: bars, rings, or the year as a dot per day.</summary>
[JsonConverter(typeof(StyleJsonConverter<YearProgressStyle>))]
public enum YearProgressStyle
{
    Bars = 0,
    Rings = 1,
    Dots = 2,
}

/// <summary>
/// [S-4800] Year progress. Its one setting is its style ([S-6040]): the week start and the clock
/// are every widget's regional settings ([S-3770]).
/// </summary>
public sealed class YearProgressConfig : WidgetConfig, IStyled
{
    private static readonly StyleOption[] YearStyles =
    [
        StyleOption.Of(YearProgressStyle.Bars, YearProgressStr.Styles_YearProgress_Bars.Local(), 0xE7A5),
        StyleOption.Of(YearProgressStyle.Rings, YearProgressStr.Styles_YearProgress_Rings.Local(), 0xEA3A),
        StyleOption.Of(YearProgressStyle.Dots, YearProgressStr.Styles_YearProgress_Dots.Local(), 0xE75F),
    ];

    /// <summary>[S-6040] Bars, rings or dots.</summary>
    public YearProgressStyle Style { get; set; } = YearProgressStyle.Bars;

    IReadOnlyList<StyleOption> IStyled.Styles => YearStyles;

    int IStyled.StyleValue
    {
        get => (int)Style;
        set => Style = (YearProgressStyle)value;
    }

    /// <summary>[S-4804] A minute: the day bar moves by a fraction of a percent per minute, nothing faster.</summary>
    [JsonIgnore]
    public TimeSpan TickInterval => TimeSpan.FromMinutes(1);

    public override WidgetConfig Clone() => new YearProgressConfig { Style = Style };
}
