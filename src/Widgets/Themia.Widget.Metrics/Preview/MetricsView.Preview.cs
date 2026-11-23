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
/// reads and writes). [S-16901] <c>history</c> a system metric's made-up series and <c>later</c> the same series 37 points
/// on: drawn side by side, every run both hold stands at the same height, the graph stepped left by whole runs.
/// </summary>
internal sealed partial class MetricsView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample", "stale", "empty", "line", "bars", "step", "range", "candles", "pair", "history", "later"];

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
            case "history" or "later":
                ShowSample(stale: false, empty: false, Made(_config, state == "later" ? Later : 0));
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

    /// <summary>[S-16901] How many points the <c>later</c> state's series is on from <c>history</c>'s: no whole number of runs.</summary>
    private const int Later = 37;

    /// <summary>
    /// A system metric's made-up series over its span: a seeded walk at the metric's scale (a pair's second a third of it), a
    /// span's worth of points before it (the oldest run's members), <paramref name="on"/> points further along the same walk.
    /// </summary>
    private static MetricSeries? Made(MetricsConfig config, int on = 0)
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
        var length = (2 * count) + Later;
        var first = SampleSeries.Walk("metrics-" + def.Id, length, last, move);
        var second = SampleSeries.Walk("metrics-" + def.Id + "-second", length, last / 3, move);
        var held = (2 * count) + on;
        var points = first.Take(held).Select((v, i) => new SeriesPoint(Math.Clamp(v, 0, top), feed.Shape == MetricShape.Pair ? Math.Clamp(second[i], 0, top) : double.NaN)).ToList();
        var series = MetricAdapters.Hub(def, points, null, span, period, SampleDay.Now.AddSeconds(on * period), held);
        return series with { Now = series.Slots[^1] };
    }
}
