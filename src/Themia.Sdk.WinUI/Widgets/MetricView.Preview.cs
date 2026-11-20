#if DEBUG
using Themia.App.Shell.Gallery;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only ([S-1917], [S-14605]): a family's own states (its sample, stale, failed, ...) and, for a metric with
/// a history, <c>arrow</c> and <c>line</c>: the family's sample (or what its data holds) with that trend drawn, over a
/// reading before and a history made up from the value shown, so a render shows each trend option without waiting for
/// readings to come in.
/// </summary>
public abstract partial class MetricView : IWidgetPreviewState
{
    private MetricTrend? _previewTrend;

    /// <summary>The family's own render states, in its order.</summary>
    protected virtual IReadOnlyList<string> OwnPreviewStates => [];

    /// <summary>Show one of <see cref="OwnPreviewStates"/>.</summary>
    protected virtual Task ShowOwnPreviewStateAsync(string state) => Task.CompletedTask;

    /// <summary>[S-11603] A trend state draws the card as a config of that trend would: its room beside the value ([S-14600]).</summary>
    public Themia.Core.Model.WidgetConfig? DrawnConfig
    {
        get
        {
            if (_previewTrend is not { } trend || _config?.Clone() is not IMetricTrend copy)
            {
                return null;
            }

            copy.Trend = trend;
            return (Themia.Core.Model.WidgetConfig)copy;
        }
    }

    public IReadOnlyList<string> PreviewStates => History == MetricHistory.None
        ? OwnPreviewStates
        : [.. OwnPreviewStates, "arrow", .. History == MetricHistory.Series ? ["line"] : Array.Empty<string>()];

    public async Task ShowPreviewStateAsync(string state)
    {
        if (state is "arrow" or "line")
        {
            if (OwnPreviewStates.Contains("sample"))
            {
                await ShowOwnPreviewStateAsync("sample");
            }

            _previewTrend = state == "line" ? MetricTrend.Line : MetricTrend.Arrow;
            Draw();
            return;
        }

        await ShowOwnPreviewStateAsync(state);
    }

    partial void PreviewTrend(ref MetricShown shown, ref MetricTrend trend)
    {
        if (_previewTrend is not { } forced)
        {
            return;
        }

        trend = forced;
        var number = shown.Value.Number ?? 1;
        var step = Math.Abs(number) > 1 ? Math.Abs(number) * 0.08 : 0.08;
        shown = shown with
        {
            Direction = shown.Direction ?? TrendDirection.Up,
            Series = shown.Series is { Count: > 1 } series ? series : [.. Enumerable.Range(0, MetricRules.LinePoints).Select(i => (double?)(number - (step * 3) + (step * 3 * i / (MetricRules.LinePoints - 1)) + (step * Math.Sin(i * 0.9))))],
        };
    }
}
#endif
