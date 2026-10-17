using System.Text.Json.Serialization;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Core.Model;

/// <summary>[S-6272] The battery meter's looks: the charge over its history, a battery, or a ring.</summary>
[JsonConverter(typeof(StyleJsonConverter<BatteryStyle>))]
public enum BatteryStyle
{
    /// <summary>[S-1187] The charge led by the battery glyph, over its history.</summary>
    Graph = 0,

    /// <summary>[S-6272] A large battery filled to the charge, the bolt while charging.</summary>
    Battery = 1,

    /// <summary>[S-6272] A ring filled to the charge, the figure inside.</summary>
    Ring = 2,
}

/// <summary>[S-1180] Charge level: the reading with the battery icon, and its history ([S-1762]).</summary>
public sealed class BatteryConfig : GaugeConfig, IStyled
{
    private static readonly StyleOption[] BatteryStyles =
    [
        StyleOption.Of(BatteryStyle.Graph, Str.Styles_Io_Graph, 0xE9D2),
        StyleOption.Of(BatteryStyle.Battery, Str.Styles_Io_Battery, 0xE83F),
        StyleOption.Of(BatteryStyle.Ring, Str.Styles_Io_Ring, 0xEA3A),
    ];

    /// <summary>Charge moves slowly: a quarter of an hour of history.</summary>
    public BatteryConfig()
        : base(graphSpanSeconds: 900)
    {
    }

    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.Battery;

    /// <summary>[S-6902] This widget's descriptor: what the host knows of it without its UI.</summary>
    public static WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("battery"),
        Kind = WidgetKind.Battery,
    };

    /// <summary>[S-6272] The meter's look; what was its history switch ([S-6000]).</summary>
    public BatteryStyle Style { get; set; } = BatteryStyle.Graph;

    IReadOnlyList<StyleOption> IStyled.Styles => BatteryStyles;

    int IStyled.StyleValue
    {
        get => (int)Style;
        set => Style = (BatteryStyle)value;
    }

    /// <summary>[S-6272] Only the graph style draws the history.</summary>
    [JsonIgnore]
    public override bool DrawsHistory => Style == BatteryStyle.Graph;

    public override WidgetConfig Clone()
    {
        var copy = new BatteryConfig { Style = Style };
        CopyTo(copy);
        return copy;
    }
}
