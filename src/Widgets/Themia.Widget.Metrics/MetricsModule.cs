using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Themia.App.Shell;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Themia.Widget.Metrics;

[assembly: ThemiaWidget("metrics", typeof(MetricsModule))]

namespace Themia.Widget.Metrics;

/// <summary>
/// [S-16300] The Metric widget: any reading Themia has (the PC's, the weather's and the air's at a place, a price's, a rate's),
/// now and as a graph back or ahead, from one open catalog (<see cref="MetricCatalog"/>). Written fresh beside the single
/// metrics, which stay. Its behaviour is SPEC.md beside this file.
/// </summary>
public sealed class MetricsModule : IWidgetModule
{
    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("metrics"),
        ConfigType = typeof(MetricsConfig),
        CreateDefault = () => new MetricsConfig(),
        Json = MetricsJsonContext.Default,
        Label = MetricsStr.Metrics_Label.Local(),
        Description = MetricsStr.Metrics_Desc.Local(),
        Keywords = ["metric", "graph", "chart", "history", "forecast", "cpu", "gpu", "memory", "drive", "network", "ping", "battery", "temperature",
            "rain", "snow", "wind", "uv", "pollen", "air", "aqi", "stock", "exchange"],
        Glyph = Glyphs.ChartType,
        Sizing = c => MetricFaceRules.Sizing(c is MetricsConfig m ? m.Style : MetricsStyle.Graph),
        Traits = WidgetTraits.None,
        Add = new(AddFolderId.System, 99, 0),
    };

    public FrameworkElement CreateView(IWidgetHost host) => new MetricsView(host);

    /// <summary>[S-16308] Every option is on the selection's page ([S-3719]).</summary>
    public void MenuLines(WidgetMenuLines menu, WidgetConfig config)
        => MetricsMenu.Lines(menu, (MetricsConfig)config, MetricsMenuSources.Host());
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(MetricsConfig))]
internal sealed partial class MetricsJsonContext : JsonSerializerContext;
