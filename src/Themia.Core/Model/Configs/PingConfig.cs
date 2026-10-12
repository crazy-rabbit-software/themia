using System.Text.Json.Serialization;

namespace Themia.Core.Model;

/// <summary>Round-trip time to a host, and the statistics behind it.</summary>
public sealed class PingConfig : GaugeConfig
{
    /// <summary>A public resolver: reachable without configuration, and not a service this app talks to.</summary>
    public const string DefaultHost = "1.1.1.1";

    /// <summary>A quarter of an hour of probes, one every five seconds ([S-2512]).</summary>
    public PingConfig()
        : base(graphSpanSeconds: 900)
    {
    }

    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.Ping;

    /// <summary>Host or address to probe. Empty falls back to <see cref="DefaultHost"/>.</summary>
    public string Host { get; set; } = DefaultHost;

    /// <summary>The host actually probed. Never empty, so the widget always has something to show.</summary>
    [JsonIgnore]
    public string EffectiveHost => string.IsNullOrWhiteSpace(Host) ? DefaultHost : Host.Trim();

    public override WidgetConfig Clone()
    {
        var copy = new PingConfig
        {
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
