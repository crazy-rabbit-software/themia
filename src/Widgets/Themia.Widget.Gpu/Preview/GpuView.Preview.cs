#if DEBUG
using Themia.App.Shell.Gallery;
using Themia.App.Services;
using Themia.Core.SystemData;
using Themia.Core.Widgets;
using Themia.Widget.Gpu;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only ([S-1917]): <c>history</c> fills the chart with a fixture series,
/// as the meters' does, so the GPU's band can be compared with theirs without waiting for samples;
/// <c>unavailable</c> a PC with no adapter to read ([S-1940]).
/// </summary>
internal sealed partial class GpuView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["history", "sample", "unavailable"];

    public Task ShowPreviewStateAsync(string state)
    {
        if (state == "unavailable")
        {
            _snapshots?.Dispose();
            _snapshots = null;
            _panel?.Unwatch();
            _surface?.SetHeaderTitle(null);
            _panel?.Absent(GpuStr.WidgetsB_Gpu_NoAdapter.Text());
            Arrange();
            return Task.CompletedTask;
        }

        if (state is "history" or "sample")
        {
            // [S-6300] sample: the snapshots stop too, so the fixed load and engines stay on screen.
            if (state == "sample")
            {
                _snapshots?.Dispose();
                _snapshots = null;
            }

            _panel?.ShowFixtureHistory();

            if (state == "sample")
            {
                _panel?.ShowSample();
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
            _spark.Load([.. PreviewSeries.Wave("gpu", _spark.Holds, 0.4, 0.3).Select(value => new SeriesPoint(value))]);
        }

        /// <summary>A fixed 62 % load and its engines: every style renders the same picture on every machine.</summary>
        public void ShowSample()
        {
            ShowLoad(new MeterReading(0.62, Formatting.Percent(0.62), true, Value: 62));
            ShowEngines(
            [
                new GpuEngineLoad(GpuEngineKind.ThreeD, 62),
                new GpuEngineLoad(GpuEngineKind.Copy, 8),
                new GpuEngineLoad(GpuEngineKind.VideoEncode, 0),
                new GpuEngineLoad(GpuEngineKind.VideoDecode, 23),
            ]);
        }
    }
}
#endif
