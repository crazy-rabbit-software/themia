using Microsoft.UI;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Themia.App.Shell;
using Themia.Core.SystemData;
using Themia.Core.Widgets;
using Windows.Foundation;

namespace Themia.App.Widgets;

/// <summary>
/// The one sparkline every meter draws: a translucent area under a 2px line, newest sample at the
/// right edge, on a fixed-slot x-axis so the trace grows in from the right instead of rescaling
/// while the buffer fills (Task Manager's convention, [S-1825]). A hairline baseline spans the whole
/// width from the first frame, so a window still filling reads as a chart with little history yet,
/// not as an empty hole under the reading.
/// <para>
/// [S-1126] Up to two series — download and upload, reads and writes — the first filled, the second
/// a line over it. It holds <see cref="Capacity"/> samples (the span setting over the refresh) and
/// draws as many of the newest as its width has slots for (<see cref="MeterTiming.Window"/>), against
/// a <see cref="ChartScale"/>: 100% for a share, the visible window's peak for a rate.
/// </para>
/// <para>
/// One element for every chart in the app (the meters and the GPU tile) so they read as one family
/// and there is one drawing path to get right.
/// </para>
/// </summary>
internal sealed partial class SparkBand : Grid
{
    private readonly Polygon _fill = new();
    private readonly Polyline _line = new();
    private readonly Polyline _secondLine = new();
    private readonly Rectangle _baseline = new() { Height = 1, VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Bottom };
    private readonly Queue<double> _first;
    private readonly Queue<double> _second;
    private int _capacity;
    private ChartScale _scale;

    public SparkBand(int capacity, ChartScale? scale = null)
    {
        _capacity = Math.Clamp(capacity, 2, MeterTiming.MaxCapacity);
        _first = new Queue<double>(_capacity);
        _second = new Queue<double>(_capacity);
        _scale = scale ?? ChartScale.Share;

        var accent = FluentLayout.Themed("AccentFillColorDefaultBrush", Colors.DodgerBlue);

        // The wash is the stroke's colour at low alpha, applied to the shape and never to the shared
        // brush, which every other accent surface in the app also paints with.
        _fill.Fill = accent;
        _fill.Opacity = Fluent.ChartWash;
        _fill.Stretch = Stretch.None;

        _line.Stroke = accent;
        _line.StrokeThickness = Fluent.ChartStroke;
        _line.StrokeLineJoin = PenLineJoin.Round;
        _line.Stretch = Stretch.None;

        // The second series is a line only: two washes over each other turn into one muddy area.
        _secondLine.Stroke = FluentLayout.Themed("AccentTextFillColorTertiaryBrush", Colors.SkyBlue);
        _secondLine.StrokeThickness = Fluent.ChartStroke / 2;
        _secondLine.StrokeLineJoin = PenLineJoin.Round;
        _secondLine.Stretch = Stretch.None;

        _baseline.Fill = FluentLayout.Themed("DividerStrokeColorDefaultBrush", Colors.Gray);

        IsHitTestVisible = false;
        Children.Add(_baseline);
        Children.Add(_fill);
        Children.Add(_line);
        Children.Add(_secondLine);
        SizeChanged += (_, _) => Redraw();
    }

    /// <summary>How many samples the band holds. Shrinking it drops the oldest; growing keeps them all.</summary>
    public int Capacity
    {
        get => _capacity;
        set
        {
            var capacity = Math.Clamp(value, 2, MeterTiming.MaxCapacity);

            if (capacity == _capacity)
            {
                return;
            }

            _capacity = capacity;
            Trim(_first);
            Trim(_second);
            Redraw();
        }
    }

    /// <summary>What the top of the chart stands for.</summary>
    public ChartScale ValueScale
    {
        get => _scale;
        set
        {
            if (value == _scale)
            {
                return;
            }

            _scale = value;
            Redraw();
        }
    }

    /// <summary>How many of the newest samples the band draws at its current width.</summary>
    public int Window => MeterTiming.Window(_capacity, ActualWidth);

    /// <summary>Paint the first series in another colour — a pressure colour, for instance.</summary>
    public void SetBrush(Brush brush)
    {
        if (ReferenceEquals(_line.Stroke, brush))
        {
            return;
        }

        _line.Stroke = brush;
        _fill.Fill = brush;
    }

    /// <summary>Paint the second series in another colour.</summary>
    public void SetSecondBrush(Brush brush) => _secondLine.Stroke = brush;

    /// <summary>
    /// Add a sample: the first series in the chart's terms (0..1 on a share chart, a value on a
    /// window-scaled one), and the second when there is one. A non-finite first sample is not a
    /// sample and is dropped; a missing second is a gap in that line only.
    /// </summary>
    public void Push(double first, double? second = null)
    {
        if (!double.IsFinite(first))
        {
            return;
        }

        if (_first.Count >= _capacity)
        {
            _first.Dequeue();
            _second.Dequeue();
        }

        _first.Enqueue(first);
        _second.Enqueue(second is { } s && double.IsFinite(s) ? s : double.NaN);
        Redraw();
    }

    /// <summary>
    /// [S-2504] Draw a series' history as the hub holds it, oldest first, replacing what the band held:
    /// a band built again (a view switch, edit mode) starts from the history, not from empty.
    /// </summary>
    public void Load(IReadOnlyList<SeriesPoint> points)
    {
        _first.Clear();
        _second.Clear();

        for (var i = Math.Max(0, points.Count - _capacity); i < points.Count; i++)
        {
            var point = points[i];

            if (!double.IsFinite(point.First))
            {
                continue;
            }

            _first.Enqueue(point.First);
            _second.Enqueue(double.IsFinite(point.Second) ? point.Second : double.NaN);
        }

        Redraw();
    }

    /// <summary>Forget the history: the next reading is of something else.</summary>
    public void Clear()
    {
        _first.Clear();
        _second.Clear();
        Redraw();
    }

    private void Trim(Queue<double> series)
    {
        while (series.Count > _capacity)
        {
            series.Dequeue();
        }
    }

    private void Redraw()
    {
        var width = ActualWidth;
        var height = ActualHeight;

        if (width <= 0 || height <= 0 || _first.Count < 2)
        {
            _line.Points.Clear();
            _fill.Points.Clear();
            _secondLine.Points.Clear();
            return;
        }

        // The newest samples, as many as this chart's width has slots for.
        var window = MeterTiming.Window(_capacity, width);
        var first = _first.ToArray().AsSpan();
        var second = _second.ToArray().AsSpan();
        first = first[Math.Max(0, first.Length - window)..];
        second = second[Math.Max(0, second.Length - window)..];

        var ceiling = _scale.Ceiling(first, second);

        // [S-1824] Inset by the stroke on every side, so 0%, 100% and the newest sample are never
        // clipped in half at the chart's edge.
        var inset = _line.StrokeThickness;
        var usable = Math.Max(1, height - inset);

        var line = Trace(first, window, ceiling, width, inset, usable);
        var fill = new PointCollection { new(line[0].X, height) };
        foreach (var point in line)
        {
            fill.Add(point);
        }

        fill.Add(new Point(line[^1].X, height));

        _line.Points = line;
        _fill.Points = fill;
        _secondLine.Points = Trace(second, window, ceiling, width, inset, usable);
    }

    /// <summary>A series as points. X from <see cref="Sparkline.PlotX"/>, which pins the newest to the right edge less half a stroke; gaps are skipped.</summary>
    private static PointCollection Trace(ReadOnlySpan<double> samples, int window, double ceiling, double width, double inset, double usable)
    {
        var points = new PointCollection();

        for (var i = 0; i < samples.Length; i++)
        {
            var fraction = ChartScale.Fraction(samples[i], ceiling);
            if (double.IsNaN(fraction))
            {
                continue;
            }

            var x = Sparkline.PlotX(i, samples.Length, window, width, inset);
            var y = (inset / 2) + (Sparkline.SampleY(fraction) * usable);
            points.Add(new Point(x, y));
        }

        return points;
    }
}
