using Themia.App.Shell.Gallery;
using Themia.Core.SystemData;
using Themia.Sdk.Samples;

namespace Themia.Widget.Metrics;

/// <summary>
/// Debug renders only ([S-1917]), [S-16309]: <c>sample</c> is the add page's picture (<see cref="ShowSampleAsync"/>); <c>stale</c>
/// the metric kept through a failed refresh (its last graph and reading, the status's problem glyph); <c>empty</c> no answer:
/// the graph's default across its foot, "--" and why. A system metric draws a made-up series of its own in these (the live hub
/// has neither face). And one state a graph type and shape, so the checks draw each: <c>line</c> (the temperature hour by hour),
/// <c>bars</c> (UV), <c>step</c> (pressure), <c>range</c> (the days' low and high), <c>candles</c> (MSFT), <c>pair</c> (a drive's
/// reads and writes).
/// </summary>
internal sealed partial class MetricsView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample", "stale", "empty", "line", "bars", "step", "range", "candles", "pair"];

    public Task ShowPreviewStateAsync(string state)
    {
        switch (state)
        {
            case "sample":
                ShowSample(stale: false, empty: false);
                break;
            case "stale" or "empty":
                ShowSample(stale: state == "stale", empty: state == "empty", Made(_config));
                break;
            case "line" or "bars" or "step" or "range" or "candles" or "pair":
                var config = Variant(state);
                _config = config;
                ShowSample(stale: false, empty: false, Made(config));
                break;
        }

        return Task.CompletedTask;
    }

    /// <summary>The metric, step and graph each graph-type state shows, in the card's own style.</summary>
    private MetricsConfig Variant(string state)
    {
        var config = (MetricsConfig)_config.Clone();
        (string Metric, MetricStep Step, MetricGraphType Graph) pick = state switch
        {
            "line" => ("temperature", MetricStep.Hour, MetricGraphType.Line),
            "bars" => ("uv", MetricStep.Hour, MetricGraphType.Bars),
            "step" => ("pressure", MetricStep.Hour, MetricGraphType.Step),
            "range" => ("temperature", MetricStep.Day, MetricGraphType.Range),
            "candles" => ("stock", MetricStep.Day, MetricGraphType.Candles),
            _ => ("drive", MetricStep.Live, MetricGraphType.Line),
        };
        config.Choose(pick.Metric);
        config.ChooseStep(pick.Step);
        config.Graph = pick.Graph;
        return config;
    }

    /// <summary>A system metric's made-up series over its span: a seeded walk at the metric's scale (a pair's second a third of it).</summary>
    private static MetricSeries? Made(MetricsConfig config)
    {
        var def = config.Def;
        var feed = config.Feed;
        if (feed.Source != MetricSource.Hub)
        {
            return null;
        }

        var span = config.SpanInForce;
        var period = SystemCadence.PeriodSeconds(feed.Hub ?? SystemSource.Cpu);
        var count = MetricAdapters.HubSlots(span, period);
        var (last, move, top) = def.Unit switch
        {
            MetricUnit.Rate => (2.5e6, 0.35, double.PositiveInfinity),
            MetricUnit.Milliseconds => (18.0, 0.15, double.PositiveInfinity),
            _ => (0.31, 0.12, 1.0),
        };
        var first = SampleSeries.Walk("metrics-" + def.Id, count, last, move);
        var second = SampleSeries.Walk("metrics-" + def.Id + "-second", count, last / 3, move);
        var points = first.Select((v, i) => new SeriesPoint(Math.Clamp(v, 0, top), feed.Shape == MetricShape.Pair ? Math.Clamp(second[i], 0, top) : double.NaN)).ToList();
        var series = MetricAdapters.Hub(def, points, null, span, period, SampleDay.Now);
        return series with { Now = series.Slots[^1] };
    }
}
