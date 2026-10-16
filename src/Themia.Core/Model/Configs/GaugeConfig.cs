using Themia.Core.Widgets;

namespace Themia.Core.Model;

/// <summary>[S-6074] The history rows every meter's page may offer.</summary>
public enum MeterSetting
{
    /// <summary>The history switch, on a meter without styles.</summary>
    History,

    /// <summary>How much history the chart spans.</summary>
    Span,
}

/// <summary>
/// Settings every meter shares: whether it draws its history and how much history the chart spans.
/// How often it samples is not a setting: every system figure moves on the hub's one cadence
/// ([S-2503]). Each kind's own options live in its own file beside this one.
/// </summary>
public abstract class GaugeConfig : WidgetConfig, IShownSettings<MeterSetting>
{
    /// <param name="graphSpanSeconds">This kind's default span, one of <see cref="MeterTiming.SpanChoices"/>.</param>
    protected GaugeConfig(int graphSpanSeconds) => GraphSpanSeconds = graphSpanSeconds;

    /// <summary>Show the recent history behind the readout.</summary>
    public bool ShowGraph { get; set; } = true;

    /// <summary>
    /// [S-6267] Whether the meter draws its history: the switch, or for a styled kind its style
    /// (only a graph style has one; the switch is then not offered, [S-6000]).
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public virtual bool DrawsHistory => ShowGraph;

    /// <summary>
    /// [S-6074] The history rows the page offers. A styled meter's history is its graph style, its
    /// first ([S-6300]), so the span is offered in that style only: every other look draws no
    /// history. A meter without styles offers the switch, and the span while the switch is on.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyList<MeterSetting> ShownSettings => WidgetStyles.Offers(Kind)
        ? StyleChartsHistory ? [MeterSetting.Span] : []
        : ShowGraph ? [MeterSetting.History, MeterSetting.Span] : [MeterSetting.History];

    /// <summary>[S-6074] Whether the current style draws a history (and so offers its span): the graph style, the first.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    protected virtual bool StyleChartsHistory => WidgetStyles.Current(this) is null or 0;

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
