using System.Text.Json.Serialization;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;

namespace Themia.Core.Model;

/// <summary>
/// [S-14603] [S-14604] What a single-metric kind without a place remembers besides its source (a drive, an adapter, a
/// host): its trend and [S-14703] its graph's chart look (the hub's series: Area or Line, as the meters' histories offer;
/// Simple or Detailed). Its kind's history says which trends it can draw; a stored one it cannot is repaired, loudly, and
/// so is a chart look it does not offer ([S-6752]). A kind about a place (the weather's, the air's) holds the same through
/// <see cref="IMetricTrend"/> and <see cref="IChartLooks"/> on its place's config.
/// </summary>
public abstract class MetricConfig : WidgetConfig, IMetricTrend, IChartLooks
{
    /// <inheritdoc />
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public MetricTrend Trend { get; set; }

    /// <summary>[S-14703] How its graph is drawn; Area (the look until now) by default.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public ChartType Chart { get; set; }

    /// <summary>[S-14703] Simple by default.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public ChartDetail Detail { get; set; }

    /// <summary>[S-14703] The histories' types where its metric has a series (a graph to draw); none without.</summary>
    [JsonIgnore]
    public IReadOnlyList<ChartType> ChartTypes => History == MetricHistory.Series ? ChartLooks.Lines : [];

    [JsonIgnore]
    public ChartType DefaultChart => ChartType.Area;

    [JsonIgnore]
    public bool OffersDetail => History == MetricHistory.Series;

    /// <inheritdoc />
    [JsonIgnore]
    public MetricHistory History => KindHistory;

    /// <summary>What history its kind's metric has (a protected member: never a stored field).</summary>
    protected abstract MetricHistory KindHistory { get; }

    /// <summary>[S-14603] A trend its metric cannot draw is the one it can, loudly; [S-6752] a chart look it does not offer too.</summary>
    public override IReadOnlyList<string> Repair()
        => [.. MetricRules.Repair(this) is { } note ? [note] : Array.Empty<string>(), .. ChartLooks.Repair(this, GetType().Name)];

    /// <summary>A copy's trend and chart look.</summary>
    protected void CopyTo(MetricConfig other)
    {
        ArgumentNullException.ThrowIfNull(other);
        other.Trend = Trend;
        ChartLooks.Copy(this, other);
    }
}
