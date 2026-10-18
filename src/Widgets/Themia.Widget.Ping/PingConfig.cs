using System.Text.Json.Serialization;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.SystemData;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Widget.Ping;

namespace Themia.Core.Model;

/// <summary>[S-6273] The ping meter's looks: the latency over its history, or signal bars.</summary>
[JsonConverter(typeof(StyleJsonConverter<PingStyle>))]
public enum PingStyle
{
    /// <summary>[S-1172] The latency over its history.</summary>
    Graph = 0,

    /// <summary>[S-6273] Signal bars by the latency (<see cref="PingSignal"/>), the latency beside them.</summary>
    Signal = 1,
}

/// <summary>Round-trip time to a host, and the statistics behind it.</summary>
public sealed class PingConfig : GaugeConfig, IStyled, IReadsSystem
{
    /// <summary>A public resolver: reachable without configuration, and not a service this app talks to.</summary>
    public const string DefaultHost = HostAddress.DefaultPingHost;

    private static readonly StyleOption[] PingStyles =
    [
        StyleOption.Of(PingStyle.Graph, Str.Styles_Io_Graph.Local(), 0xE9D2),
        StyleOption.Of(PingStyle.Signal, PingStr.Styles_Io_Signal.Local(), 0xE870),
    ];

    /// <summary>A quarter of an hour of probes, one every five seconds ([S-2512]).</summary>
    public PingConfig()
        : base(graphSpanSeconds: 900)
    {
    }

    /// <summary>[S-6273] The meter's look; what was its history switch ([S-6000]).</summary>
    public PingStyle Style { get; set; } = PingStyle.Graph;

    IReadOnlyList<StyleOption> IStyled.Styles => PingStyles;

    int IStyled.StyleValue
    {
        get => (int)Style;
        set => Style = (PingStyle)value;
    }

    /// <summary>[S-6273] Only the graph style draws the history.</summary>
    [JsonIgnore]
    public override bool DrawsHistory => Style == PingStyle.Graph;

    /// <summary>Host or address to probe. Empty falls back to <see cref="DefaultHost"/>.</summary>
    public string Host { get; set; } = DefaultHost;

    /// <summary>The host actually probed. Never empty, so the widget always has something to show.</summary>
    [JsonIgnore]
    public string EffectiveHost => string.IsNullOrWhiteSpace(Host) ? DefaultHost : Host.Trim();

    /// <summary>[S-2513] Probes of its host and their history.</summary>
    public SystemInterests SystemWants(string systemRoot) => SystemInterests.Chart(FeedKey.Create(SystemSource.Ping, EffectiveHost));

    public override WidgetConfig Clone()
    {
        var copy = new PingConfig
        {
            Style = Style,
            Host = Host,
        };
        CopyTo(copy);
        return copy;
    }

    /// <summary>A hand-edited null host is the default one. [S-1173] The header names the host; a name for it is the card's own title.</summary>
    public override IReadOnlyList<string> Repair()
    {
        var notes = new List<string>(base.Repair());
        Host ??= DefaultHost;
        return notes;
    }
}
