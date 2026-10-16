using System.Text.Json.Serialization;
using Themia.Core.Localization;
using Themia.Core.Widgets;

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
public sealed class MemoryConfig : GaugeConfig, IStyled
{
    public MemoryConfig()
        : base(graphSpanSeconds: 60)
    {
    }

    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.Memory;

    /// <summary>[S-6315] The meter's look.</summary>
    public MemoryStyle Style { get; set; } = MemoryStyle.Graph;

    private static readonly StyleOption[] MemoryStyles =
    [
        StyleOption.Of(MemoryStyle.Graph, Str.Styles_Memory_Graph, 0xE9D2),
        StyleOption.Of(MemoryStyle.Bar, Str.Styles_Memory_Bar, 0xE7A5),
        StyleOption.Of(MemoryStyle.Gauge, Str.Styles_Memory_Gauge, 0xEC4A),
    ];

    IReadOnlyList<StyleOption> IStyled.Styles => MemoryStyles;

    int IStyled.StyleValue
    {
        get => (int)Style;
        set => Style = (MemoryStyle)value;
    }

    public override WidgetConfig Clone()
    {
        var copy = new MemoryConfig { Style = Style };
        CopyTo(copy);
        return copy;
    }
}
