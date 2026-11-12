using System.Text.Json.Serialization;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.SystemData;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Widget.Ping;

namespace Themia.Core.Model;

/// <summary>[S-6273] The ping reading's drawings: the latency alone, or with signal bars.</summary>
[JsonConverter(typeof(StyleJsonConverter<PingStyle>))]
public enum PingStyle
{
    /// <summary>[S-1172] The latency alone, at 28.</summary>
    Number = 0,

    /// <summary>[S-6273] The latency over signal bars by it (<see cref="PingSignal"/>).</summary>
    Signal = 1,
}

/// <summary>Round-trip time to a host: the reading alone. Its history is the Ping history atom's, its statistics the Ping facts'.</summary>
public sealed class PingConfig : ReadingConfig, IStyled, IReadsSystem, IPingHost
{
    /// <summary>A public resolver: reachable without configuration, and not a service this app talks to.</summary>
    public const string DefaultHost = HostAddress.DefaultPingHost;

    private static readonly StyleOption[] PingStyles =
    [
        StyleOption.Of(PingStyle.Number, PingStr.Styles_Ping_Number.Local(), 0xE8EF),
        StyleOption.Of(PingStyle.Signal, PingStr.Styles_Io_Signal.Local(), 0xE870),
    ];

    /// <summary>[S-6273] The reading's drawing.</summary>
    public PingStyle Style { get; set; } = PingStyle.Number;

    IReadOnlyList<StyleOption> IStyled.Styles => PingStyles;

    int IStyled.StyleValue
    {
        get => (int)Style;
        set => Style = (PingStyle)value;
    }

    /// <summary>Host or address to probe. Empty falls back to <see cref="DefaultHost"/>.</summary>
    public string Host { get; set; } = DefaultHost;

    /// <summary>The host actually probed. Never empty, so the widget always has something to show.</summary>
    [JsonIgnore]
    public string EffectiveHost => PingHosts.Effective(this);

    /// <summary>[S-2513] Probes of its host and their history (the history atom's and the facts' too).</summary>
    public SystemInterests SystemWants(string systemRoot) => PingHosts.Wants(this);

    public override WidgetConfig Clone() => new PingConfig { Style = Style, Host = Host };

    /// <summary>A hand-edited null host is the default one. [S-1173] The header names the host; a name for it is the card's own title.</summary>
    public override IReadOnlyList<string> Repair()
    {
        Host ??= DefaultHost;
        return [];
    }
}
