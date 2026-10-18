using System.Text.Json.Serialization;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.SystemData;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Core.Model;

/// <summary>[S-2426] Latency statistics over a window of probes: average, range, jitter and loss.</summary>
public sealed class PingDetailsConfig : DetailConfig, IReadsSystem
{
    public const int MinWindow = 5;

    public const int MaxWindow = SeriesHistory.MaxPingWindow;

    /// <summary>Host or address to probe. Empty falls back to <see cref="HostAddress.DefaultPingHost"/>.</summary>
    public string Host { get; set; } = HostAddress.DefaultPingHost;

    /// <summary>[S-2427] How many of the latest probes the statistics cover.</summary>
    public int Window { get; set; } = 60;

    [JsonIgnore]
    public string EffectiveHost => string.IsNullOrWhiteSpace(Host) ? HostAddress.DefaultPingHost : Host.Trim();

    [JsonIgnore]
    public int EffectiveWindow => Math.Clamp(Window, MinWindow, MaxWindow);

    /// <summary>[S-2513] The statistics' window is the prober's history: a series like the meter's.</summary>
    public SystemInterests SystemWants(string systemRoot) => SystemInterests.Chart(FeedKey.Create(SystemSource.Ping, EffectiveHost));

    public override WidgetConfig Clone()
    {
        var copy = new PingDetailsConfig
        {
            Host = Host,
            Window = Window,
        };

        return copy;
    }

    public override IReadOnlyList<string> Repair()
    {
        var notes = new List<string>(base.Repair());

        Host ??= HostAddress.DefaultPingHost;

        if (EffectiveWindow != Window)
        {
            notes.Add($"{Kind}: window {Window} is outside {MinWindow}..{MaxWindow}; using {EffectiveWindow}");
            Window = EffectiveWindow;
        }

        return notes;
    }
}
