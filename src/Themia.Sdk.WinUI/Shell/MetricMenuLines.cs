using Themia.App.Shell;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.Sdk.WinUI;

/// <summary>
/// [S-14603] The trend line every single-metric widget's page has, the same on every kind (a shared setting,
/// [S-11750]: several metrics selected set it once): None, Arrow and, where the metric has a series, Line. A metric
/// without a history (a text, a constant, a time) has no trend line at all.
/// </summary>
public static class MetricMenuLines
{
    /// <summary>The trend's line for <paramref name="config"/>, or null where its metric has no history.</summary>
    public static ItemMenuEntry? Trend(WidgetMenuLines menu, IMetricTrend config)
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

    /// <summary>Add the trend's line to <paramref name="menu"/>'s lines where the metric has one.</summary>
    public static void Add(WidgetMenuLines menu, IMetricTrend config)
    {
        if (Trend(menu, config) is { } line)
        {
            menu.Lines.Add(line);
        }
    }

    /// <summary>A trend's name on the page.</summary>
    public static string Label(MetricTrend trend) => trend switch
    {
        MetricTrend.Arrow => Str.Metric_Trend_Arrow.Text(),
        MetricTrend.Line => Str.Metric_Trend_Line.Text(),
        _ => Str.Metric_Trend_None.Text(),
    };
}
