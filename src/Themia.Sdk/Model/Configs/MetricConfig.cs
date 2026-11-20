using System.Text.Json.Serialization;
using Themia.Core.Widgets;

namespace Themia.Core.Model;

/// <summary>
/// [S-14603] [S-14604] What a single-metric kind without a place remembers besides its source (a drive, an adapter, a
/// host): its trend. Its kind's history says which trends it can draw; a stored one it cannot is repaired, loudly. A
/// kind about a place (the weather's, the air's, the sky's) holds the same through <see cref="IMetricTrend"/> on its
/// place's config.
/// </summary>
public abstract class MetricConfig : WidgetConfig, IMetricTrend
{
    /// <inheritdoc />
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public MetricTrend Trend { get; set; }

    /// <inheritdoc />
    [JsonIgnore]
    public MetricHistory History => KindHistory;

    /// <summary>What history its kind's metric has (a protected member: never a stored field).</summary>
    protected abstract MetricHistory KindHistory { get; }

    /// <summary>[S-14603] A trend its metric cannot draw is the one it can, loudly.</summary>
    public override IReadOnlyList<string> Repair() => MetricRules.Repair(this) is { } note ? [note] : [];

    /// <summary>A copy's trend.</summary>
    protected void CopyTo(MetricConfig other)
    {
        ArgumentNullException.ThrowIfNull(other);
        other.Trend = Trend;
    }
}
