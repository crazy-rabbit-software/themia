using System.Text.Json.Serialization;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;

namespace Themia.Core.Model;

/// <summary>
/// [S-14603] [S-14604] What a single-metric kind without a place remembers besides its source (a drive, an adapter, a
/// host): its trend and [S-14703] [S-14710] its graph's chart look and [S-14711] span, as its kind's graph offers them
/// (<see cref="MetricKind.Graph"/>: the hub's series Area or Line, as the meters' histories, a span of the meters'). Its
/// kind's history says which trends it can draw; a stored one it cannot is repaired, loudly, and so is a chart look or a
/// span it does not offer ([S-6752]). A kind about a place (the weather's, the air's) holds the same through
/// <see cref="IMetricGraph"/> on its place's config.
/// </summary>
public abstract class MetricConfig : WidgetConfig, IMetricGraph
{
    /// <inheritdoc />
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public MetricTrend Trend { get; set; }

    /// <summary>[S-14703] How its graph is drawn: its kind's graph's default unless another is stored.</summary>
    [JsonIgnore]
    public ChartType Chart
    {
        get => ChartStored ?? DefaultChart;
        set => ChartStored = value == DefaultChart ? null : value;
    }

    /// <summary>[S-14710] The chart type stored: only one other than its kind's default (a new widget's file as before).</summary>
    [JsonPropertyName("chart")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ChartType? ChartStored { get; set; }

    /// <summary>[S-14703] Simple by default.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public ChartDetail Detail { get; set; }

    /// <inheritdoc />
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int Span { get; set; }

    /// <inheritdoc />
    [JsonIgnore]
    public MetricGraph? Graph => Metric.Graph;

    /// <summary>[S-14710] The types its kind's graph offers; none without a graph.</summary>
    [JsonIgnore]
    public IReadOnlyList<ChartType> ChartTypes => MetricGraphs.Types(Graph);

    [JsonIgnore]
    public ChartType DefaultChart => MetricGraphs.Default(Graph);

    [JsonIgnore]
    public bool OffersDetail => MetricGraphs.OffersDetail(Graph);

    /// <inheritdoc />
    [JsonIgnore]
    public MetricHistory History => Metric.History;

    /// <summary>Its kind: its history and its graph (a protected member: never a stored field).</summary>
    protected abstract MetricKind Metric { get; }

    /// <summary>[S-14603] A trend its metric cannot draw is the one it can, loudly; [S-6752] a chart look and [S-14711] a span it does not offer too.</summary>
    public override IReadOnlyList<string> Repair()
        => [.. MetricRules.Repair(this) is { } note ? [note] : Array.Empty<string>(), .. ChartLooks.Repair(this, GetType().Name),
            .. MetricGraphs.RepairSpan(this, GetType().Name) is { } span ? [span] : Array.Empty<string>()];

    /// <summary>A copy's trend, chart look and span.</summary>
    protected void CopyTo(MetricConfig other)
    {
        ArgumentNullException.ThrowIfNull(other);
        MetricGraphs.Copy(this, other);
    }
}
