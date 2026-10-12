#if DEBUG
using Themia.App.Shell.Gallery;
using Themia.Core.SystemData;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only ([S-1917]): <c>history</c> fills every panel's chart with a fixture series,
/// as the meters' does, so the GPU's band can be compared with theirs without waiting for samples.
/// </summary>
internal sealed partial class GpuView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["history"];

    public Task ShowPreviewStateAsync(string state)
    {
        if (state == "history")
        {
            foreach (var panel in _shown)
            {
                panel.ShowFixtureHistory();
            }
        }

        return Task.CompletedTask;
    }

    private sealed partial class Panel
    {
        /// <summary>Stop the live series (it would push over the fixture) and draw a fixed load curve.</summary>
        public void ShowFixtureHistory()
        {
            Unwatch();
            var points = new List<SeriesPoint>(_spark.Capacity);

            for (var i = 0; i < _spark.Capacity; i++)
            {
                points.Add(new SeriesPoint(0.4 + (Math.Sin(i / 5.0) * 0.25) + (Math.Sin(i / 1.7) * 0.08)));
            }

            _spark.Load(points);
        }
    }
}
#endif
