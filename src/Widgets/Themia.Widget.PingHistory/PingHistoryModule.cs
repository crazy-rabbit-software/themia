using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Themia.App.Shell;
using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.App.Widgets.Meters;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.SystemData;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Themia.Widget.PingHistory;

[assembly: ThemiaWidget("pingHistory", typeof(PingHistoryModule))]

namespace Themia.Widget.PingHistory;

/// <summary>[S-11022] A host's latency over time: its history alone, edge to edge. SPEC.md beside this file.</summary>
public sealed class PingHistoryModule : IWidgetModule
{
    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("pingHistory"),
        ConfigType = typeof(PingHistoryConfig),
        CreateDefault = () => new PingHistoryConfig(),
        Json = PingHistoryJsonContext.Default,
        Keywords = ["ping", "latency", "internet", "graph", "chart", "history"],
        Label = PingHistoryStr.PingHistory_Label.Local(),
        Description = PingHistoryStr.PingHistory_Desc.Local(),
        Glyph = Glyphs.Network,
        Sizing = _ => MeterSizing.History,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader | WidgetTraits.StartsHeaderless,
        Add = new(AddFolderId.PingReading, 0, 1),
    };

    /// <summary>[S-11022] The host ([S-109]) and the chart's span ([S-1121]).</summary>
    public void MenuLines(WidgetMenuLines menu, WidgetConfig config)
    {
        ArgumentNullException.ThrowIfNull(menu);
        var ping = (PingHistoryConfig)config;
        menu.Lines.Add(PingHostLine.Line(menu, ping.Host, value => ping.Host = value));
        menu.Lines.Add(AtomMenuLines.Span(menu, ping));
    }

    public FrameworkElement CreateView(IWidgetHost host) => new MeterView(host, new PingHistoryMeter());
}

/// <summary>[S-11022] What one ping history remembers: its host and its span (a quarter of an hour of probes).</summary>
public sealed class PingHistoryConfig : HistoryConfig, IReadsSystem, IPingHost
{
    public PingHistoryConfig()
        : base(graphSpanSeconds: 900)
    {
    }

    /// <summary>Host or address to probe. Empty falls back to the default host.</summary>
    public string Host { get; set; } = HostAddress.DefaultPingHost;

    /// <summary>[S-2513] The host's prober and its history, the reading's own ([S-2508]).</summary>
    public SystemInterests SystemWants(string systemRoot) => PingHosts.Wants(this);

    public override WidgetConfig Clone()
    {
        var copy = new PingHistoryConfig { Host = Host };
        CopyTo(copy);
        return copy;
    }

    public override IReadOnlyList<string> Repair()
    {
        Host ??= HostAddress.DefaultPingHost;
        return base.Repair();
    }
}

/// <summary>[S-11022] The latency as a history against the window's slowest answer ([S-1172]).</summary>
internal sealed class PingHistoryMeter : PingPart
{
    public override WidgetId Kind => new("pingHistory");

    public override MeterAtom Atom => MeterAtom.History;

    public override MeterLook Look(WidgetConfig config) => MeterLook.History;
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(PingHistoryConfig))]
internal sealed partial class PingHistoryJsonContext : JsonSerializerContext;
