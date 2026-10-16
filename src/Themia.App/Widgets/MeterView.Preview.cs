#if DEBUG
using Themia.App.Services;
using Themia.App.Shell.Gallery;
using Themia.Core.SystemData;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only ([S-1917]): <c>history</c> fills the chart with a fixture series, so the
/// history's edges ([S-1824]) can be looked at without waiting a minute for real samples;
/// <c>sample</c> ([S-6300]) shows a fixed reading and the kind's fixed items, so every style
/// renders the same picture on every machine.
/// </summary>
internal sealed partial class MeterView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["history", "sample"];

    public Task ShowPreviewStateAsync(string state)
    {
        if (state == "sample")
        {
            ShowSample();
            return Task.CompletedTask;
        }

        if (state != "history")
        {
            return Task.CompletedTask;
        }

        // Stop the live series, which would push its own points over the fixture.
        _watch?.Dispose();
        _watch = null;

        // A share chart reads 0..1; a peak-scaled one only the shape, so any magnitude will do.
        var magnitude = _band.ValueScale == ChartScale.Share ? 1.0 : 1_000_000;
        var points = new List<SeriesPoint>(_band.Capacity);

        for (var i = 0; i < _band.Capacity; i++)
        {
            var first = 0.45 + (Math.Sin(i / 5.0) * 0.25) + (Math.Sin(i / 1.7) * 0.08);
            var second = 0.2 + (Math.Cos(i / 4.0) * 0.1);
            points.Add(new SeriesPoint(first * magnitude, second * magnitude));
        }

        _band.Load(points);
        return Task.CompletedTask;
    }

    /// <summary>Stop the live reads, then a fixed 62 % reading, the kind's sample items and the history fixture.</summary>
    private void ShowSample()
    {
        _items?.Dispose();
        _items = null;
        _ = ShowPreviewStateAsync("history");

        Present(_part.SampleReading);
        ShowItems(_part.SampleItems);
        Lay();
    }
}
#endif
