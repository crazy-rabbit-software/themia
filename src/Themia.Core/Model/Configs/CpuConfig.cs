using System.Text.Json.Serialization;
using Themia.Core.Localization;
using Themia.Core.Widgets;

namespace Themia.Core.Model;

/// <summary>[S-6311] The processor meter's looks: the same load, four ways ([S-6000]).</summary>
[JsonConverter(typeof(StyleJsonConverter<CpuStyle>))]
public enum CpuStyle
{
    /// <summary>[S-1762] The reading over its history.</summary>
    Graph = 0,

    /// <summary>[S-6312] The reading over one bar per logical processor.</summary>
    Cores = 1,

    /// <summary>[S-6313] The reading over a tile per logical processor, shaded by its load.</summary>
    Grid = 2,

    /// <summary>[S-6314] The reading inside a half-circle gauge.</summary>
    Gauge = 3,
}

/// <summary>[S-1140] Processor load: the reading and its history, nothing else ([S-1762]).</summary>
public sealed class CpuConfig : GaugeConfig, IStyled
{
    /// <summary>A minute of history, as Task Manager shows.</summary>
    public CpuConfig()
        : base(graphSpanSeconds: 60)
    {
    }

    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.Cpu;

    /// <summary>[S-6311] The meter's look.</summary>
    public CpuStyle Style { get; set; } = CpuStyle.Graph;

    private static readonly StyleOption[] CpuStyles =
    [
        StyleOption.Of(CpuStyle.Graph, Str.Styles_Cpu_Graph, 0xE9D2),
        StyleOption.Of(CpuStyle.Cores, Str.Styles_Cpu_Cores, 0xE9E9),
        StyleOption.Of(CpuStyle.Grid, Str.Styles_Cpu_Grid, 0xF0E2),
        StyleOption.Of(CpuStyle.Gauge, Str.Styles_Cpu_Gauge, 0xEC4A),
    ];

    IReadOnlyList<StyleOption> IStyled.Styles => CpuStyles;

    int IStyled.StyleValue
    {
        get => (int)Style;
        set => Style = (CpuStyle)value;
    }

    /// <summary>[S-6310] Whether this look draws each logical processor, so the hub reads them.</summary>
    [JsonIgnore]
    public bool ShowsCores => Style is CpuStyle.Cores or CpuStyle.Grid;

    public override WidgetConfig Clone()
    {
        var copy = new CpuConfig { Style = Style };
        CopyTo(copy);
        return copy;
    }
}
