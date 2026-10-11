using System.Text.Json.Serialization;
using Themia.Core.Widgets;

namespace Themia.Core.Model;

/// <summary>
/// Settings every meter shares: whether it draws its history and how much history the chart spans.
/// How often it samples is the kind's own fixed cadence ([S-3793]), not a setting. Each kind's own
/// options live in its own file beside this one.
/// </summary>
public abstract class GaugeConfig : WidgetConfig
{
    /// <param name="intervalSeconds">This kind's refresh: how fast the number actually moves.</param>
    /// <param name="graphSpanSeconds">This kind's default span, one of <see cref="MeterTiming.SpanChoices"/>.</param>
    protected GaugeConfig(int intervalSeconds, int graphSpanSeconds)
    {
        IntervalSeconds = intervalSeconds;
        GraphSpanSeconds = graphSpanSeconds;
    }

    /// <summary>Show the recent history behind the readout.</summary>
    public bool ShowGraph { get; set; } = true;

    /// <summary>[S-1120] [S-3793] Seconds between samples: the kind's own, fixed.</summary>
    [JsonIgnore]
    public int IntervalSeconds { get; }

    /// <summary>[S-1121] How much history the chart spans, in seconds: one of <see cref="MeterTiming.SpanChoices"/>.</summary>
    public int GraphSpanSeconds { get; set; }

    [JsonIgnore]
    public TimeSpan TickInterval => TimeSpan.FromSeconds(IntervalSeconds);

    /// <summary>[S-1121] How many samples the chart holds: the span over the refresh, 2..600.</summary>
    [JsonIgnore]
    public int GraphCapacity => MeterTiming.Capacity(GraphSpanSeconds, IntervalSeconds);

    protected void CopyTo(GaugeConfig other)
    {
        other.ShowGraph = ShowGraph;
        other.GraphSpanSeconds = GraphSpanSeconds;
    }

    /// <summary>
    /// Snap the span to an offered choice. A kind that adds its own repairs overrides this and
    /// appends to what it returns.
    /// </summary>
    public override IReadOnlyList<string> Repair()
    {
        var notes = new List<string>();

        var span = MeterTiming.NearestSpan(GraphSpanSeconds);
        if (span != GraphSpanSeconds)
        {
            notes.Add($"{Kind}: graph span {GraphSpanSeconds} s is not an offered span; using {span}");
            GraphSpanSeconds = span;
        }

        return notes;
    }
}
