using System.Text.Json.Serialization;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.SystemData;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Widget.Memory;

namespace Themia.Core.Model;

/// <summary>[S-6315] The memory meter's looks: the same share in use, three ways ([S-6000]).</summary>
[JsonConverter(typeof(StyleJsonConverter<MemoryStyle>))]
public enum MemoryStyle
{
    /// <summary>[S-1762] The reading over its history.</summary>
    Graph = 0,

    /// <summary>[S-6316] The reading over one bar, used against free.</summary>
    Bar = 1,

    /// <summary>[S-6314] The reading inside a half-circle gauge.</summary>
    Gauge = 2,
}

/// <summary>[S-1130] Physical memory in use: the reading and its history, nothing else ([S-1762]).</summary>
public sealed class MemoryConfig : GaugeConfig, IStyled, IReadsSystem
{
    public MemoryConfig()
        : base(graphSpanSeconds: 60)
    {
    }

    /// <summary>[S-6315] The meter's look.</summary>
    public MemoryStyle Style { get; set; } = MemoryStyle.Graph;

    private static readonly StyleOption[] MemoryStyles =
    [
        StyleOption.Of(MemoryStyle.Graph, MemoryStr.Styles_Memory_Graph.Local(), 0xE9D2),
        StyleOption.Of(MemoryStyle.Bar, MemoryStr.Styles_Memory_Bar.Local(), 0xE7A5),
        StyleOption.Of(MemoryStyle.Gauge, MemoryStr.Styles_Memory_Gauge.Local(), 0xEC4A),
    ];

    IReadOnlyList<StyleOption> IStyled.Styles => MemoryStyles;

    int IStyled.StyleValue
    {
        get => (int)Style;
        set => Style = (MemoryStyle)value;
    }

    /// <summary>[S-2513] Memory in use and its history.</summary>
    public SystemInterests SystemWants(string systemRoot) => SystemInterests.Chart(SystemSource.Memory);

    public override WidgetConfig Clone()
    {
        var copy = new MemoryConfig { Style = Style };
        CopyTo(copy);
        return copy;
    }
}
