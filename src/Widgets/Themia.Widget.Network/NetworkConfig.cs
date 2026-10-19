using System.Text.Json.Serialization;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.SystemData;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Widget.Network;

namespace Themia.Core.Model;

/// <summary>[S-6270] The network meter's looks: the reading over its history, or the two rates alone.</summary>
[JsonConverter(typeof(StyleJsonConverter<NetworkStyle>))]
public enum NetworkStyle
{
    /// <summary>[S-1191] Download as the reading, download and upload as the history.</summary>
    Graph = 0,

    /// <summary>[S-6270] Download and upload, both large, and no history.</summary>
    Numbers = 1,
}

/// <summary>
/// [S-1190] Throughput across the active adapters: download as the reading, download and upload as
/// the history, nothing else ([S-1762]).
/// </summary>
public sealed class NetworkConfig : GaugeConfig, IStyled, IReadsSystem
{
    private static readonly StyleOption[] NetworkStyles =
    [
        StyleOption.Of(NetworkStyle.Graph, Str.Styles_Io_Graph.Local(), 0xE9D2),
        StyleOption.Of(NetworkStyle.Numbers, NetworkStr.Styles_Io_Numbers.Local(), 0xE896),
    ];

    public NetworkConfig()
        : base(graphSpanSeconds: 60)
    {
    }

    /// <summary>[S-6270] The meter's look; what was its history switch ([S-6000]).</summary>
    public NetworkStyle Style { get; set; } = NetworkStyle.Graph;

    IReadOnlyList<StyleOption> IStyled.Styles => NetworkStyles;

    int IStyled.StyleValue
    {
        get => (int)Style;
        set => Style = (NetworkStyle)value;
    }

    /// <summary>[S-6270] Only the graph style draws the history.</summary>
    [JsonIgnore]
    public override bool DrawsHistory => Style == NetworkStyle.Graph;

    /// <summary>[S-2513] The throughput and its history.</summary>
    public SystemInterests SystemWants(string systemRoot) => SystemInterests.Chart(SystemSource.Network);

    /// <summary>[S-6573] Both looks are the card's own drawing: the history graph and the two figures paint no item.</summary>
    protected override bool StyleDrawsItems => false;

    public override WidgetConfig Clone()
    {
        var copy = new NetworkConfig { Style = Style };
        CopyTo(copy);
        return copy;
    }
}
