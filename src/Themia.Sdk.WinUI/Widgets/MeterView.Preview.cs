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
/// <c>unavailable</c> ([S-1940]) is the kind's reading for a thing not on this machine (no battery),
/// for a kind that can be missing.
/// </summary>
public sealed partial class MeterView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates => _part.AbsentReading is null ? ["history", "sample"] : ["history", "sample", "unavailable"];

    public Task ShowPreviewStateAsync(string state)
    {
        if (state == "sample")
        {
            ShowSample();
            return Task.CompletedTask;
        }

        if (state == "unavailable" && _part.AbsentReading is { } absent)
        {
            // The live series would put this machine's reading back.
            _frozen = true;
            _watch?.Dispose();
            _watch = null;
            _standIn = false;
            Present(absent);
            Lay();
            return Task.CompletedTask;
        }

        if (state != "history")
        {
            return Task.CompletedTask;
        }

        // Stop the live series, which would push its own points over the fixture.
        _watch?.Dispose();
        _watch = null;

        // [S-6317] The core graphs draw the kind's sample cores, each a fixture history of its own.
        if (_graphs is not null)
        {
            _frozen = true;
            _items?.Dispose();
            _items = null;
            ShowItems(_part.SampleItems, point: false);

            for (var core = 0; core < _graphs.Count; core++)
            {
                var level = _part.SampleItems[core].Fraction ?? 0.3;
                var history = PreviewSeries.Wave($"{_part.Kind.Value}/{core}", _graphs.Holds, level, 0.18)
                    .Select(value => new SeriesPoint(value)).ToList();

                _graphs.Load(core, history, history.Count);
            }
        }

        // A share chart reads 0..1; a peak-scaled one only the shape, so any magnitude will do.
        var magnitude = _band.ValueScale == ChartScale.Share ? 1.0 : 1_000_000;
        var first = PreviewSeries.Wave(_part.Kind.Value, _band.Holds, _part.SampleReading.Fraction is { } f and > 0 ? f : 0.45, 0.3);
        var second = PreviewSeries.Wave(_part.Kind.Value + "/second", _band.Holds, 0.2, 0.12);
        _band.Load([.. first.Zip(second, (a, b) => new SeriesPoint(a * magnitude, b * magnitude))]);
        return Task.CompletedTask;
    }

    /// <summary>Stop the live reads, then a fixed 62 % reading, the kind's sample items and the history fixture.</summary>
    private void ShowSample()
    {
        _frozen = true;
        _items?.Dispose();
        _items = null;
        _ = ShowPreviewStateAsync("history");

        Present(_part.SampleReading);
        ShowItems(_part.SampleItems, point: false);
        Lay();
    }
}
#endif
