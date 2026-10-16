using System.Numerics;
using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
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
/// a line over it. It holds <see cref="Capacity"/> samples (the span setting at the source's spacing)
/// and, when its width has fewer slots than that, folds neighbouring samples into one slot
/// (<see cref="MeterTiming.Fold"/>: the mean of a share, the peak of a rate), so the whole span is
/// always on screen, against a <see cref="ChartScale"/>: 100% for a share, the visible peak for a rate.
/// </para>
/// <para>
/// One element for every chart in the app (the meters and the GPU tile) so they read as one family
/// and there is one drawing path to get right.
/// </para>
/// <para>
/// [S-1826] It scrolls rather than steps: a new point is drawn one sample behind (just beyond the right
/// edge) and the traces are slid left by one step over the series' period by a composition animation
/// on their container's Translation, so between points nothing is redrawn on the UI thread. The next
/// point rebuilds the geometry and resets the translation in the same frame (<see cref="SparkScroll"/>).
/// The reading beside it is not delayed; only the line is a sample behind. The band clips to its own
/// bounds, so the slot sliding out at the left and the newest point beyond the right never show.
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
    // [S-1826] The traces, in the one container the slide translates; the baseline stays put. A
    // Canvas, not a Grid: a Grid arranges the shapes at the band's size and clips them there in their
    // own coordinates, so the point drawn beyond the right edge was cut off and slid in as a gap,
    // and the next point then appeared at the edge. The band's own clip is the one edge.
    private readonly Canvas _plot = new();
    private readonly RectangleGeometry _clip = new();
    private int _capacity;
    private long _added;
    private ChartScale _scale;

    private int _periodSeconds = SystemCadence.TickSeconds;
    private Visual? _visual;
    private ScalarKeyFrameAnimation? _slide;
    private double _slideDistance = double.NaN;
    private int _slidePeriod;

#if DEBUG
    /// <summary>
    /// Debug renders only: draw every band this far through its slide (0 = just after a point, the
    /// newest beyond the right edge; 1 = settled), with no animation, so a still image shows the motion.
    /// </summary>
    public static double? PreviewProgress { get; set; }
#endif

    public SparkBand(int capacity, ChartScale? scale = null)
    {
        _capacity = Math.Clamp(capacity, 2, MeterTiming.MaxSpanSeconds);
        _first = new Queue<double>(_capacity);
        _second = new Queue<double>(_capacity);
        _scale = scale ?? ChartScale.Share;

        // [S-1113] Time runs left to right in every language: newest at the right edge.
        FlowDirection = Microsoft.UI.Xaml.FlowDirection.LeftToRight;

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
        Clip = _clip;
        _plot.Children.Add(_fill);
        _plot.Children.Add(_line);
        _plot.Children.Add(_secondLine);
        Children.Add(_baseline);
        Children.Add(_plot);
        SizeChanged += (_, e) =>
        {
            _clip.Rect = new Rect(0, 0, e.NewSize.Width, e.NewSize.Height);
            Redraw(SparkChange.Settle);
        };
    }

    /// <summary>[S-1826] Seconds between the series' points ([S-2512]: five for a ping): how long one slide takes.</summary>
    public int PeriodSeconds
    {
        get => _periodSeconds;
        set => _periodSeconds = Math.Max(1, value);
    }

    /// <summary>
    /// [S-1826] How many of a history's newest points <see cref="Load"/> keeps: the span and the lead
    /// (the slot sliding out at the left, the newest beyond the right edge).
    /// </summary>
    public int Holds => _capacity + SparkScroll.Lead(MeterTiming.BucketSize(_capacity, ActualWidth));

    /// <summary>How many samples the band holds. Shrinking it drops the oldest; growing keeps them all.</summary>
    public int Capacity
    {
        get => _capacity;
        set
        {
            var capacity = Math.Clamp(value, 2, MeterTiming.MaxSpanSeconds);

            if (capacity == _capacity)
            {
                return;
            }

            _capacity = capacity;
            Trim(_first);
            Trim(_second);
            Redraw(SparkChange.Settle);
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
            Redraw(SparkChange.Settle);
        }
    }

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

        _first.Enqueue(first);
        _second.Enqueue(second is { } s && double.IsFinite(s) ? s : double.NaN);
        _added++;
        Trim(_first);
        Trim(_second);
        Redraw(SparkChange.Point);
    }

    /// <summary>
    /// [S-2504] Draw a series' history as the hub holds it, oldest first, replacing what the band held:
    /// a band built again (a view switch, edit mode) starts from the history, not from empty.
    /// <paramref name="added"/> is how many points the series ever had, so folded slots keep their
    /// members ([S-1121]); null counts these.
    /// </summary>
    public void Load(IReadOnlyList<SeriesPoint> points, long? added = null)
    {
        _first.Clear();
        _second.Clear();
        _added = added ?? points.Count;

        for (var i = Math.Max(0, points.Count - Holds); i < points.Count; i++)
        {
            var point = points[i];

            if (!double.IsFinite(point.First))
            {
                continue;
            }

            _first.Enqueue(point.First);
            _second.Enqueue(double.IsFinite(point.Second) ? point.Second : double.NaN);
        }

        Redraw(SparkChange.Settle);
    }

    /// <summary>Forget the history: the next reading is of something else.</summary>
    public void Clear()
    {
        _first.Clear();
        _second.Clear();
        _added = 0;
        Redraw(SparkChange.Settle);
    }

    private void Trim(Queue<double> series)
    {
        while (series.Count > Holds)
        {
            series.Dequeue();
        }
    }

    private void Redraw(SparkChange change)
    {
        // [S-1827] [S-1828] [S-6329] Only a new point on a shown band with smooth motion on (Windows'
        // animation effects and the user's Smooth value changes) slides; every other drawing is
        // settled, the newest point at the right edge.
        var animates = change == SparkChange.Point
            && Visibility == Microsoft.UI.Xaml.Visibility.Visible
            && GlideMotion.Smooth;
        var frame = SparkScroll.Plan(change, animates);
#if DEBUG
        // A still of the live slide: the geometry just after a point, translated part of the way.
        var preview = PreviewProgress is { } progress ? Math.Clamp(progress, 0, 1) : (double?)null;
        if (preview is not null)
        {
            frame = new SparkFrame(1, false);
        }
#endif

        // The geometry and the translation change in the same frame: a slide still running stops here.
        StopSlide();

        var width = ActualWidth;
        var height = ActualHeight;

        if (width <= 0 || height <= 0 || _first.Count < 2)
        {
            ClearTraces();
            return;
        }

        // [S-1121] The whole span, folded into the slots this width has: a slot is a fixed run of
        // points (by their absolute position), its peak on a rate chart and its mean on a share.
        var size = MeterTiming.BucketSize(_capacity, width);
        var window = MeterTiming.Slots(_capacity, size);
        var first = MeterTiming.Fold(_first.ToArray(), _added, size, _scale.FollowsWindow).AsSpan();
        var second = MeterTiming.Fold(_second.ToArray(), _added, size, _scale.FollowsWindow).AsSpan();

        // [S-1824] Inset by the stroke on every side, so 0%, 100% and the newest sample are never
        // clipped in half at the chart's edge.
        var inset = _line.StrokeThickness;
        var usable = Math.Max(1, height - inset);

        // [S-1826] Each slot's x at this lag; the slots wholly past the left edge are left out.
        var (skip, xs) = SparkScroll.Layout(first.Length, _added, size, window, width, inset, frame.Lag);
        // [S-1829] A slot still filling is not among them.
        if (skip + xs.Length > first.Length || skip + xs.Length > second.Length)
        {
            ClearTraces();
            return;
        }

        first = first.Slice(skip, xs.Length);
        second = second.Slice(skip, xs.Length);

        if (first.Length < 2)
        {
            ClearTraces();
            return;
        }

        // [S-1829] The scale is the drawn slots', the newest beyond the edge too, so it is in place
        // before that point slides into view.
        var ceiling = _scale.Ceiling(first, second);

        var line = Trace(first, xs, ceiling, inset, usable);
        if (line.Count == 0)
        {
            ClearTraces();
            return;
        }

        var fill = new PointCollection { new(line[0].X, height) };
        foreach (var point in line)
        {
            fill.Add(point);
        }

        fill.Add(new Point(line[^1].X, height));

        _line.Points = line;
        _fill.Points = fill;
        _secondLine.Points = Trace(second, xs, ceiling, inset, usable);

        if (frame.Slides)
        {
            StartSlide(SparkScroll.Step(size, window, width, inset) * frame.Lag);
        }
#if DEBUG
        else if (preview is { } through)
        {
            HoldSlide(SparkScroll.Step(size, window, width, inset) * through);
        }
#endif
    }

    private void ClearTraces()
    {
        _line.Points.Clear();
        _fill.Points.Clear();
        _secondLine.Points.Clear();
    }

    /// <summary>[S-1826] Stop a slide where it is and put the traces back at rest; the caller redraws them in the same frame.</summary>
    private void StopSlide()
    {
        if (_visual is null)
        {
            return;
        }

        _visual.StopAnimation("Translation.X");
        _visual.Properties.InsertVector3("Translation", Vector3.Zero);
    }

#if DEBUG
    /// <summary>Debug renders: the traces translated <paramref name="distance"/> DIPs left, still, as a slide stands part-way.</summary>
    private void HoldSlide(double distance)
    {
        EnsureVisual().Properties.InsertVector3("Translation", new Vector3((float)-distance, 0, 0));
    }
#endif

    private Visual EnsureVisual()
    {
        if (_visual is null)
        {
            ElementCompositionPreview.SetIsTranslationEnabled(_plot, true);
            _visual = ElementCompositionPreview.GetElementVisual(_plot);
            _visual.Properties.InsertVector3("Translation", Vector3.Zero);
        }

        return _visual;
    }

    /// <summary>
    /// [S-1826] Slide the traces <paramref name="distance"/> DIPs left over one period, linearly, on
    /// the compositor: no per-frame work on the UI thread. The animation is made once and reused.
    /// </summary>
    private void StartSlide(double distance)
    {
        if (!(distance > 0))
        {
            return;
        }

        var visual = EnsureVisual();

        if (_slide is null || distance != _slideDistance || _periodSeconds != _slidePeriod)
        {
            var compositor = visual.Compositor;
            var linear = compositor.CreateLinearEasingFunction();
            _slide ??= compositor.CreateScalarKeyFrameAnimation();
            _slide.InsertKeyFrame(0f, 0f, linear);
            _slide.InsertKeyFrame(1f, (float)-distance, linear);
            _slide.Duration = TimeSpan.FromSeconds(_periodSeconds);
            _slideDistance = distance;
            _slidePeriod = _periodSeconds;
        }

        visual.StartAnimation("Translation.X", _slide);
    }

    /// <summary>A series as points, at the slots' x from <see cref="SparkScroll.Layout"/>; gaps are skipped.</summary>
    private static PointCollection Trace(ReadOnlySpan<double> samples, double[] xs, double ceiling, double inset, double usable)
    {
        var points = new PointCollection();

        for (var i = 0; i < samples.Length && i < xs.Length; i++)
        {
            var fraction = ChartScale.Fraction(samples[i], ceiling);
            if (double.IsNaN(fraction))
            {
                continue;
            }

            var y = (inset / 2) + (Sparkline.SampleY(fraction) * usable);
            points.Add(new Point(xs[i], y));
        }

        return points;
    }
}
