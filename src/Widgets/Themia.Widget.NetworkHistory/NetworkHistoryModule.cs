using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Themia.App.Services;
using Themia.App.Services.SystemData;
using Themia.App.Shell;
using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.App.Widgets.Meters;
using Themia.Core.Links;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.SystemData;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Themia.Widget.NetworkHistory;

[assembly: ThemiaWidget("networkHistory", typeof(NetworkHistoryModule))]

namespace Themia.Widget.NetworkHistory;

/// <summary>[S-11019] Network throughput over time: its history alone, edge to edge. SPEC.md beside this file.</summary>
public sealed class NetworkHistoryModule : IWidgetModule
{
    /// <summary>[S-11019] A click on the card opens what the reading's opens.</summary>
    internal static CardOpen? Opens => CardOpen.Settings("network-status");

    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("networkHistory"),
        ConfigType = typeof(NetworkHistoryConfig),
        CreateDefault = () => new NetworkHistoryConfig(),
        Json = NetworkHistoryJsonContext.Default,
        Keywords = ["internet", "bandwidth", "download", "upload", "graph", "chart", "history"],
        Label = NetworkHistoryStr.NetworkHistory_Label.Local(),
        Description = NetworkHistoryStr.NetworkHistory_Desc.Local(),
        Glyph = Glyphs.Network,
        Sizing = _ => MeterSizing.History,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader | WidgetTraits.StartsHeaderless,
        Links = LinkUse.Of([LinkTopicId.HoveredInstant], [LinkTopicId.HoveredInstant]),
        Add = new(AddFolderId.NetworkReading, 0, 1),
    };

    /// <summary>[S-11019] Its one setting: how much history the chart spans ([S-1121]).</summary>
    public void MenuLines(WidgetMenuLines menu, WidgetConfig config)
        => menu.Lines.Add(AtomMenuLines.Span(menu, (NetworkHistoryConfig)config));

    public FrameworkElement CreateView(IWidgetHost host) => new MeterView(host, new NetworkHistoryMeter());
}

/// <summary>[S-11019] What one networkHistory remembers: its span.</summary>
public sealed class NetworkHistoryConfig : HistoryConfig, IReadsSystem
{
    public NetworkHistoryConfig()
        : base(graphSpanSeconds: 60)
    {
    }

    /// <summary>[S-2513] The reading's own series, so the two agree to the point.</summary>
    public SystemInterests SystemWants(string systemRoot) => SystemInterests.Chart(SystemSource.Network);

    public override WidgetConfig Clone()
    {
        var copy = new NetworkHistoryConfig();
        CopyTo(copy);
        return copy;
    }
}

/// <summary>[S-11019] The history in its pressure's role ([S-1123]).</summary>
internal sealed class NetworkHistoryMeter : MeterPart
{
    /// <summary>[S-11910] The moment under the pointer on its history goes to the view; a moment pointed at elsewhere is marked on it.</summary>
    public override LinkTopic<InstantLink>? Instant => LinkTopics.HoveredInstant;

    /// <summary>[S-11912] A point is the download per second, as the reading writes it.</summary>
    public override string? ValueAt(SeriesPoint point) => double.IsFinite(point.First) ? Formatting.Rate(point.First) : null;

    public override WidgetId Kind => new("networkHistory");

    public override MeterAtom Atom => MeterAtom.History;

    public override MeterLook Look(WidgetConfig config) => MeterLook.History;

    public override CardOpen? Opens(WidgetConfig config) => NetworkHistoryModule.Opens;

    public override ChartScale Scale => ChartScale.Peak(Sources.NetworkChartFloor);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(NetworkHistoryConfig))]
internal sealed partial class NetworkHistoryJsonContext : JsonSerializerContext;
