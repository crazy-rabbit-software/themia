using Themia.Core.Widgets;

namespace Themia.Core.Model;

/// <summary>
/// Settings every meter shares: whether it draws its history and how much history the chart spans.
/// How often it samples is not a setting: every system figure moves on the hub's one cadence
/// ([S-2503]). Each kind's own options live in its own file beside this one.
/// </summary>
public abstract class GaugeConfig : WidgetConfig
{
    /// <param name="graphSpanSeconds">This kind's default span, one of <see cref="MeterTiming.SpanChoices"/>.</param>
    protected GaugeConfig(int graphSpanSeconds) => GraphSpanSeconds = graphSpanSeconds;

    /// <summary>Show the recent history behind the readout.</summary>
    public bool ShowGraph { get; set; } = true;

    /// <summary>[S-1121] How much history the chart spans, in seconds: one of <see cref="MeterTiming.SpanChoices"/>.</summary>
    public int GraphSpanSeconds { get; set; }

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
