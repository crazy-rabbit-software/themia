using Themia.App.Shell;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Settings;
using Themia.Core.Widgets;

namespace Themia.Sdk.WinUI;

/// <summary>
/// [S-14603] The trend line every single-metric widget's page has, the same on every kind (a shared setting,
/// [S-11750]: several metrics selected set it once): None, Arrow and, where the metric has a series, Line. A metric
/// without a history (a text, a constant, a time) has no trend line at all. [S-14703] Under it, while the trend is the
/// graph, the graph's chart look: the graph widgets' own two lines (<see cref="ChartMenuLines"/>: Chart type, Detail).
/// </summary>
public static class MetricMenuLines
{
    /// <summary>The trend's line for <paramref name="config"/>, or null where its metric has no history.</summary>
    public static SettingsLine? Trend(WidgetMenuLines menu, IMetricTrend config)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);
        var offered = MetricRules.Offered(config.History);
        if (offered.Count == 0)
        {
            return null;
        }

        return WidgetMenuLines.Shared(SharedSettings.MetricTrend, menu.Choice(Glyphs.ChartType, Str.Metric_Trend.Text(),
            [.. offered.Select(trend => (trend, Label(trend)))],
            MetricRules.Effective(config),
            trend => config.Trend = trend));
    }

    /// <summary>
    /// Add the trend's line to <paramref name="menu"/>'s lines where the metric has one and, [S-14703] while it draws its
    /// graph, the chart look's lines under it (owner, 2026-11-20: "for the trendline you should also be able to set its style").
    /// </summary>
    public static void Add(WidgetMenuLines menu, IMetricTrend config)
    {
        if (Trend(menu, config) is not { } line)
        {
            return;
        }

        menu.Lines.Add(line);
        if (MetricRules.Effective(config) == MetricTrend.Line && config is IChartLooks looks)
        {
            menu.Lines.AddRange(ChartMenuLines.Lines(menu, looks));
            if (config is IMetricGraph graph && Span(menu, graph) is { } span)
            {
                menu.Lines.Add(span);
            }
        }
    }

    /// <summary>
    /// [S-14711] The graph's span line, the spans its kind's graph offers (owner, 2026-11-20: "What the items are (which facts,
    /// which span of time a chart covers) stays a setting"): a hub series' as the meters' Chart span (a minute to an hour),
    /// a place's hours as the weather graphs' Time span (hours, or days past two); none where its graph offers one span.
    /// </summary>
    public static SettingsLine? Span(WidgetMenuLines menu, IMetricGraph config)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);
        if (config.Graph is not { Spans.Count: > 1 } graph)
        {
            return null;
        }

        var hub = graph.Series == GraphSeries.Hub;
        return WidgetMenuLines.Shared(SharedSettings.MetricSpan, menu.Choice(hub ? Glyphs.Hours : Glyphs.Calendar,
            hub ? Str.WidgetsB_Settings_Meters_ChartSpan_Label.Text() : Str.WeatherGraph_Window.Text(),
            [.. graph.Spans.Select(span => (span, SpanLabel(graph.Series, span)))],
            MetricGraphs.EffectiveSpan(config),
            span => config.Span = span == graph.Span ? 0 : span));
    }

    /// <summary>[S-14711] A span's name in its series' unit: "1 minute", "24 hours", "4 days".</summary>
    public static string SpanLabel(GraphSeries series, int span) => series switch
    {
        GraphSeries.Hub => MeterTiming.SpanLabel(span),
        GraphSeries.Prices => Str.WeatherGraph_WindowDays.Format(("count", LocaleFormat.Integer(Strings.Current, span))),
        GraphSeries.Minutes => MeterTiming.SpanLabel(span * 60),
        _ => span > 48 && span % 24 == 0
            ? Str.WeatherGraph_WindowDays.Format(("count", LocaleFormat.Integer(Strings.Current, span / 24)))
            : Str.WeatherGraph_WindowHours.Format(("count", LocaleFormat.Integer(Strings.Current, span))),
    };

    /// <summary>A trend's name on the page.</summary>
    public static string Label(MetricTrend trend) => trend switch
    {
        MetricTrend.Arrow => Str.Metric_Trend_Arrow.Text(),
        MetricTrend.Line => Str.Metric_Trend_Line.Text(),
        _ => Str.Metric_Trend_None.Text(),
    };
}
