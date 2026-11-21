using System.Text.Json.Serialization;
using Themia.Core.Model;
using Themia.Core.SystemData;
using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-14710] Where a metric's graph takes its points from, and so what a point of its span is: the hub's charted series
/// (a point a period, the span in seconds), a place's hours (a point an hour, the span in hours), a price history (a
/// point a day, the span in days) or a nowcast's minutes (a point a quarter hour, the span in minutes: the rain's next
/// two hours, whose graph the generic one hosts once its widget is a metric).
/// </summary>
public enum GraphSeries
{
    Hub,
    Hours,
    Prices,
    Minutes,
}

/// <summary>
/// [S-14710] A single metric's graph as data, declared on its kind (owner, 2026-11-21: "i think it would be good if we merge
/// all the different graph types into the general metric widget and then go ahead and for every widget only make the graph
/// type/s that make sense available to display. the graphs should be implemented generically so that any widget can
/// configure its specific layout itself."): the one graph every metric draws on the one chart (<see cref="MetricRules.Graph"/>),
/// configured per kind by this record, never by code of its own: its series (<see cref="Series"/>), the chart types that make
/// sense for its data (<see cref="Types"/>, the first the page's), the one it draws until the user picks another
/// (<see cref="Default"/>), the spans offered (<see cref="Spans"/>, in its series' unit) and its default span
/// (<see cref="Span"/>), whether a span's points too many for the plot keep each fold's highest (<see cref="Peak"/>: a rate,
/// a latency) and whether Detailed (its grid and its values, written as the shown figure) is offered (<see cref="Detailed"/>).
/// </summary>
public sealed record MetricGraph
{
    /// <summary>[S-14710] Where its points come from.</summary>
    public required GraphSeries Series { get; init; }

    /// <summary>[S-14710] The chart types that make sense for its data, in the page's order.</summary>
    public required IReadOnlyList<ChartType> Types { get; init; }

    /// <summary>[S-14710] The type it draws by default: one of <see cref="Types"/>.</summary>
    public required ChartType Default { get; init; }

    /// <summary>[S-14711] The spans offered, in its series' unit (seconds, hours, days, minutes), shortest first.</summary>
    public required IReadOnlyList<int> Spans { get; init; }

    /// <summary>[S-14711] Its default span: one of <see cref="Spans"/>.</summary>
    public required int Span { get; init; }

    /// <summary>[S-14711] A fold of points keeps its highest (a rate, a latency: a burst is not averaged away); else its mean.</summary>
    public bool Peak { get; init; }

    /// <summary>[S-14703] Whether Detailed is offered (its grid and values, written as the shown figure).</summary>
    public bool Detailed { get; init; } = true;

    /// <summary>[S-14711] The weather graphs' windows, in hours.</summary>
    public static IReadOnlyList<int> HourSpans { get; } = [12, 24, 48];

    /// <summary>[S-14711] The air's windows, in hours: CAMS forecasts the air four days ahead.</summary>
    public static IReadOnlyList<int> AirSpans { get; } = [24, 48, 96];

    /// <summary>[S-14710] A hub series' graph: the histories' types (Area or Line), its spans the meters' ([S-1121]).</summary>
    public static MetricGraph Hub(int span = 60, bool peak = false) => new()
    {
        Series = GraphSeries.Hub,
        Types = ChartLooks.Lines,
        Default = ChartType.Area,
        Spans = MeterTiming.SpanChoices,
        Span = span,
        Peak = peak,
    };

    /// <summary>[S-14710] A place's hours: the weather graphs' types (Area, Line or Bars), a day by default.</summary>
    public static MetricGraph Hours(ChartType @default = ChartType.Area, int span = 24, IReadOnlyList<int>? spans = null) => new()
    {
        Series = GraphSeries.Hours,
        Types = ChartLooks.Hours,
        Default = @default,
        Spans = spans ?? HourSpans,
        Span = span,
    };

    /// <summary>[S-14710] A price history: the price's types (Area, Line or Candles), a span of days.</summary>
    public static MetricGraph Prices(int span = 30, IReadOnlyList<int>? spans = null) => new()
    {
        Series = GraphSeries.Prices,
        Types = ChartLooks.Prices,
        Default = ChartType.Area,
        Spans = spans ?? [7, 30, 90],
        Span = span,
    };

    /// <summary>
    /// [S-14710] A nowcast's minutes: a column a quarter hour over the next two hours (the rain's, [S-5124]), the bars by
    /// default; the generic graph hosts it as it hosts the hours, a point a <see cref="QuarterMinutes"/>.
    /// </summary>
    public static MetricGraph Minutes(int span = 120) => new()
    {
        Series = GraphSeries.Minutes,
        Types = ChartLooks.Hours,
        Default = ChartType.Bars,
        Spans = [span],
        Span = span,
    };

    /// <summary>[S-14710] A nowcast's step, in minutes.</summary>
    public const int QuarterMinutes = 15;

    /// <summary>[S-14711] The span offered nearest <paramref name="span"/> (the default for nothing offered).</summary>
    public int Nearest(int span) => Spans.Count == 0 ? Span : Spans.MinBy(s => Math.Abs(s - span));

    /// <summary>
    /// [S-14711] How many points <paramref name="span"/> holds on this graph: the hub's at one point every
    /// <paramref name="periodSeconds"/> (<see cref="MeterTiming.Points"/>), an hour a point, a day a point, a quarter hour a
    /// point; two at least.
    /// </summary>
    public int Points(int span, int periodSeconds = SystemCadence.TickSeconds) => Series switch
    {
        GraphSeries.Hub => MeterTiming.Points(span, periodSeconds),
        GraphSeries.Minutes => Math.Max(2, span / QuarterMinutes),
        _ => Math.Max(2, span),
    };
}

/// <summary>
/// [S-14710] [S-14711] A config whose metric draws the one graph: its trend and chart look, its span (0: its kind's
/// default; stored only when another) and its kind's graph spec (none for a metric without a series).
/// </summary>
public interface IMetricGraph : IMetricTrend, IChartLooks
{
    /// <summary>[S-14711] The span its graph covers, in its series' unit; 0 for its kind's default.</summary>
    int Span { get; set; }

    /// <summary>[S-14710] Its kind's graph; null for a metric with no series.</summary>
    [JsonIgnore]
    MetricGraph? Graph { get; }
}

/// <summary>[S-14710] [S-14711] The rules over <see cref="IMetricGraph"/>: the chart look its kind's graph offers, its span and their repair.</summary>
public static class MetricGraphs
{
    /// <summary>[S-14710] The chart types <paramref name="graph"/> offers: none without one.</summary>
    public static IReadOnlyList<ChartType> Types(MetricGraph? graph) => graph?.Types ?? [];

    /// <summary>[S-14710] The type <paramref name="graph"/> draws until another is picked: Area without one.</summary>
    public static ChartType Default(MetricGraph? graph) => graph?.Default ?? ChartType.Area;

    /// <summary>[S-14703] Whether <paramref name="graph"/> offers Detailed.</summary>
    public static bool OffersDetail(MetricGraph? graph) => graph?.Detailed ?? false;

    /// <summary>[S-14711] The span drawn: the stored one where its graph offers it, else the graph's default (0 without a graph).</summary>
    public static int EffectiveSpan(IMetricGraph config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return config.Graph is not { } graph ? 0 : graph.Spans.Contains(config.Span) ? config.Span : graph.Span;
    }

    /// <summary>[S-14711] The points the drawn span holds at one point every <paramref name="periodSeconds"/> (the hub's series).</summary>
    public static int Points(IMetricGraph config, int periodSeconds = SystemCadence.TickSeconds)
    {
        ArgumentNullException.ThrowIfNull(config);
        return config.Graph is { } graph ? graph.Points(EffectiveSpan(config), periodSeconds) : MetricRules.LinePoints;
    }

    /// <summary>
    /// [S-14711] A stored span its graph does not offer (a hand edit, a span of a removed graph kind) is the nearest one
    /// offered, its default stored as none; said, never silent. Null when it holds.
    /// </summary>
    public static string? RepairSpan(IMetricGraph config, string kind)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (config.Span == 0)
        {
            return null;
        }

        if (config.Graph is not { } graph)
        {
            var none = $"{kind}: span {config.Span} but no graph; none";
            config.Span = 0;
            return none;
        }

        var nearest = graph.Nearest(config.Span);
        var stored = nearest == graph.Span ? 0 : nearest;
        if (stored == config.Span)
        {
            return null;
        }

        var note = graph.Spans.Contains(config.Span) ? null : $"{kind}: span {config.Span} is not offered; {nearest}";
        config.Span = stored;
        return note;
    }

    /// <summary>[S-14711] Copy the span (a Clone; the chart look is <see cref="ChartLooks.Copy"/>'s).</summary>
    public static void Copy(IMetricGraph from, IMetricGraph to)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);
        to.Span = from.Span;
        ChartLooks.Copy(from, to);
        to.Trend = from.Trend;
    }
}
