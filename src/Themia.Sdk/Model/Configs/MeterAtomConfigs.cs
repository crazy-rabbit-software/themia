using Themia.Core.Widgets;
using Themia.Sdk.Kit;

namespace Themia.Core.Model;

/// <summary>[S-1121] A config whose face draws a history and says how much of it the chart spans.</summary>
public interface ISpansHistory
{
    /// <summary>How much history the chart spans, in seconds: one of <see cref="MeterTiming.SpanChoices"/>.</summary>
    int GraphSpanSeconds { get; set; }
}

/// <summary>
/// [S-11001] What every reading atom of a system subject shares (CPU, memory, GPU, disk, network, ping,
/// battery): the reading alone in one of its drawings. It draws no history (that is the history atom's,
/// [S-11002]), so it has no span. It reads on the hub's one cadence ([S-2503]).
/// </summary>
public abstract class ReadingConfig : WidgetConfig, IDrawsItems
{
    /// <summary>[S-6573] Whether the current drawing paints items (a gauge's or a ring's track, a bar row): every style but the first, the number, by default.</summary>
    bool IDrawsItems.DrawsItems => StyleDrawsItems;

    /// <summary>[S-6574] Every item a reading paints is a track: no item text reaches one.</summary>
    bool IDrawsItems.DrawsItemText => false;

    [System.Text.Json.Serialization.JsonIgnore]
    protected virtual bool StyleDrawsItems => WidgetStyles.Current(this) is not (null or 0);

    public override IReadOnlyList<string> Repair() => [];
}

/// <summary>
/// [S-11002] What every history atom (and the cores' graphs) shares: how much history its chart spans.
/// The chart is the card's own drawing, edge to edge; a wider card is a longer chart of the same span.
/// [S-6752] And its chart look: the line with its wash (the histories' look until now) or alone, Simple or Detailed.
/// </summary>
public abstract class HistoryConfig : WidgetConfig, ISpansHistory, IDrawsItems, IChartLooks
{
    /// <summary>[S-6573] The history band is the card's own drawing: no item. A kind whose cells are items (the cores) says so.</summary>
    bool IDrawsItems.DrawsItems => PaintsItems;

    /// <summary>[S-6574] Every item such a kind paints is a track: no item text reaches one.</summary>
    bool IDrawsItems.DrawsItemText => false;

    [System.Text.Json.Serialization.JsonIgnore]
    protected virtual bool PaintsItems => false;

    /// <param name="graphSpanSeconds">This kind's default span, one of <see cref="MeterTiming.SpanChoices"/>.</param>
    protected HistoryConfig(int graphSpanSeconds) => GraphSpanSeconds = graphSpanSeconds;

    /// <summary>[S-1121] How much history the chart spans, in seconds.</summary>
    public int GraphSpanSeconds { get; set; }

    /// <summary>[S-6752] How the history is drawn: the line with its wash by default.</summary>
    public ChartType Chart { get; set; } = ChartType.Area;

    /// <summary>[S-6752] Simple by default.</summary>
    public ChartDetail Detail { get; set; }

    /// <summary>[S-6752] The line with its wash or alone; a kind whose look draws no history (the cores' bars) offers no choice.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public virtual IReadOnlyList<ChartType> ChartTypes => ChartLooks.Lines;

    [System.Text.Json.Serialization.JsonIgnore]
    public ChartType DefaultChart => ChartType.Area;

    /// <summary>[S-6752] A kind whose chart is many small ones (the cores, whose cells are items) writes nothing on them.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool OffersDetail => !PaintsItems;

    protected void CopyTo(HistoryConfig other)
    {
        ArgumentNullException.ThrowIfNull(other);
        other.GraphSpanSeconds = GraphSpanSeconds;
        ChartLooks.Copy(this, other);
    }

    /// <summary>Snap the span to an offered choice and the chart look to an offered one; a kind with repairs of its own appends to these.</summary>
    public override IReadOnlyList<string> Repair()
    {
        var notes = new List<string>(ChartLooks.Repair(this, Kind.ToString()));
        var span = MeterTiming.NearestSpan(GraphSpanSeconds);
        if (span != GraphSpanSeconds)
        {
            notes.Add($"{Kind}: graph span {GraphSpanSeconds} s is not an offered span; using {span}");
            GraphSpanSeconds = span;
        }

        return notes;
    }
}
