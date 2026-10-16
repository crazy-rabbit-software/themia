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
/// renders the same picture on every machine. With <c>--tween p</c> the sample is drawn that far
/// from a lower reading and shifted items ([S-6328]): every glide between two values, still.
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

        // [S-6317] The core graphs draw the kind's sample cores, each a fixture history of its own.
        if (_graphs is not null)
        {
            _frozen = true;
            _items?.Dispose();
            _items = null;
            ShowItems(_part.SampleItems, point: false, settle: true);

            for (var core = 0; core < _graphs.Count; core++)
            {
                var history = new List<SeriesPoint>(_graphs.Holds);

                for (var i = 0; i < _graphs.Holds; i++)
                {
                    var level = _part.SampleItems[core].Fraction ?? 0.3;
                    var wave = (Math.Sin((i + (core * 7)) / 4.0) * 0.18) + (Math.Sin((i * (core + 3)) / 5.3) * 0.07);
                    history.Add(new SeriesPoint(Math.Clamp(level + wave, 0, 1)));
                }

                _graphs.Load(core, history, history.Count);
            }
        }

        // A share chart reads 0..1; a peak-scaled one only the shape, so any magnitude will do.
        var magnitude = _band.ValueScale == ChartScale.Share ? 1.0 : 1_000_000;
        var points = new List<SeriesPoint>(_band.Holds);

        for (var i = 0; i < _band.Holds; i++)
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
        _frozen = true;
        _items?.Dispose();
        _items = null;
        _ = ShowPreviewStateAsync("history");

        if (GlideMotion.PreviewProgress is not null)
        {
            // [S-6328] Where the glide starts: a lower reading and the items shifted by five.
            Present(Lower(_part.SampleReading), settle: true);
            var items = _part.SampleItems;
            ShowItems([.. items.Select((_, i) => items[(i + 5) % items.Count])], point: false, settle: true);
        }

        Present(_part.SampleReading);
        ShowItems(_part.SampleItems, point: false);
        Lay();
    }

    /// <summary>A reading at a third of <paramref name="reading"/>, written as the kind writes it.</summary>
    private MeterReading Lower(MeterReading reading)
    {
        var number = _part.Number(reading) is { } n ? n / 3 : 0;
        return reading with
        {
            Fraction = reading.Fraction / 3,
            Value = reading.Value / 3,
            Second = reading.Second / 3,
            Primary = _part.Format(number),
        };
    }
}
#endif
