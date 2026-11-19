using Microsoft.UI.Xaml.Controls;
using Themia.Core.Links;
using Themia.Core.Localization;
using Themia.Core.SystemData;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI.Kit;

namespace Themia.App.Widgets;

/// <summary>
/// The one history every meter draws, on the one <see cref="Chart"/> ([S-6717], [S-6750]): the reading's line at
/// 2 in its role over a 16 % wash (or alone, or Detailed: its chart look, [S-6752]), a second series in the tertiary role, the
/// newest sample at the right edge, on a fixed-slot x-axis so the trace grows in from the right
/// instead of rescaling while the buffer fills (Task Manager's convention, [S-1825]).
/// <para>
/// [S-1126] Up to two series (download and upload, reads and writes). It holds <see cref="Capacity"/>
/// samples (the span setting at the source's spacing) and, when its width has fewer slots than that,
/// folds neighbouring samples into one slot (<see cref="MeterTiming.Fold"/>: the mean of a share, the
/// peak of a rate), so the whole span is always on screen, against a <see cref="ChartScale"/>: 100 %
/// for a share, the visible peak for a rate.
/// </para>
/// <para>
/// [S-6330] [S-6331] It steps, it does not move: a new point draws the chart once, settled, the
/// newest finished slot on the right edge (<see cref="SparkSlots"/>), and nothing runs between two
/// points. A point that finishes no slot (a folded chart's slot still filling) draws nothing, and a
/// band not shown draws nothing until it is shown again.
/// </para>
/// </summary>
public sealed partial class SparkBand : Grid
{
    private readonly Chart _series = new();
    private ChartType _type = ChartType.Area;
    private ChartDetail _detail = ChartDetail.Simple;
    private TimeSpan _period = TimeSpan.FromSeconds(1);
    private Func<double, string>? _format;
    private bool _bytes;

    // [S-11911] The hairline at a moment pointed at on a history chart of the view.
    private readonly Microsoft.UI.Xaml.Shapes.Rectangle _marker = new()
    {
        Width = FamilyShape.Hairline,
        HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Left,
        Visibility = Microsoft.UI.Xaml.Visibility.Collapsed,
        IsHitTestVisible = false,
    };

    private DateTimeOffset? _markedAt;
    private TimeSpan _markedPeriod;
    private readonly Queue<double> _first;
    private readonly Queue<double> _second;
    private int _capacity;
    private long _added;
    private ChartScale _scale;

    // [S-6331] Points arrived while the band was not shown: it draws them when it is shown again.
    private bool _stale;

    public SparkBand(int capacity, ChartScale? scale = null)
    {
        _capacity = Math.Clamp(capacity, 2, MeterTiming.MaxSpanSeconds);
        _first = new Queue<double>(_capacity);
        _second = new Queue<double>(_capacity);
        _scale = scale ?? ChartScale.Share;

        // [S-1113] Time runs left to right in every language: newest at the right edge.
        FlowDirection = Microsoft.UI.Xaml.FlowDirection.LeftToRight;
        IsHitTestVisible = false;
        Children.Add(_series);
        _marker.Fill = Family.Brush(ColourRole.TextSecondary);
        Children.Add(_marker);
        SizeChanged += (_, _) =>
        {
            Redraw();
            PlaceMarker();
        };
        RegisterPropertyChangedCallback(VisibilityProperty, (_, _) =>
        {
            if (_stale && Shown)
            {
                Redraw();
            }
        });
    }

    /// <summary>
    /// [S-6331] How many of a history's newest points <see cref="Load"/> keeps: the span and the lead
    /// (the slot still filling, the oldest slot's members the span cuts off).
    /// </summary>
    public int Holds => _capacity + SparkSlots.Lead(MeterTiming.BucketSize(_capacity, ActualWidth));

    private bool Shown => Visibility == Microsoft.UI.Xaml.Visibility.Visible;

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

    /// <summary>[S-6753] Whether a band with nothing yet writes its caption with its baseline (a core's small cell does not: no room).</summary>
    public bool Captions { get; init; } = true;

    /// <summary>
    /// [S-6752] The chart look: its type and detail, the series' spacing (a Detailed chart's times) and how a value
    /// is written (a Detailed chart's grid and marks; in binary units for <paramref name="bytes"/>); drawn again only when one of them changes.
    /// </summary>
    public void Looks(ChartType type, ChartDetail detail, TimeSpan period, Func<double, string>? format, bool bytes = false)
    {
        _bytes = bytes;
        var changed = type != _type || detail != _detail || period != _period;
        (_type, _detail, _period, _format) = (type, detail, period, format);
        if (changed)
        {
            Redraw();
        }
    }

    /// <summary>[S-6717] The first series' role: the accent, or Caution/Critical over its threshold, disabled for a thing not on this PC.</summary>
    public void Role(ColourRole role) => _series.Role(role);

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

        // [S-6331] Only a point that finishes a slot changes what is drawn; a band not shown draws
        // when it is shown again.
        if (!Shown)
        {
            _stale = true;
        }
        else if (SparkSlots.Moves(_added, MeterTiming.BucketSize(_capacity, ActualWidth)))
        {
            Redraw();
        }
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

        Redraw();
    }

    /// <summary>[S-11911] How long the band spans at a series' spacing: its slots, oldest to newest.</summary>
    public TimeSpan Span(TimeSpan period) => period * Math.Max(1, _capacity - 1);

    /// <summary>[S-11911] The moment the band's <paramref name="x"/> stands for, the newest slot at <paramref name="now"/>.</summary>
    public DateTimeOffset InstantAt(double x, TimeSpan period, DateTimeOffset now)
        => LinkInstants.At(x, FamilyShape.Stroke, ActualWidth - FamilyShape.Stroke, Span(period), now);

    /// <summary>
    /// [S-11911] Draw the hairline at <paramref name="at"/> (null: none) where the band's span holds it,
    /// the newest slot at now: one crosshair down every history chart of the view.
    /// </summary>
    public void Mark(DateTimeOffset? at, TimeSpan period)
    {
        _markedAt = at;
        _markedPeriod = period;
        PlaceMarker();
    }

    private void PlaceMarker()
    {
        var x = _markedAt is { } at
            ? LinkInstants.X(at, FamilyShape.Stroke, ActualWidth - FamilyShape.Stroke, Span(_markedPeriod), DateTimeOffset.Now)
            : null;
        _marker.Visibility = x is null ? Microsoft.UI.Xaml.Visibility.Collapsed : Microsoft.UI.Xaml.Visibility.Visible;
        _marker.Margin = new Microsoft.UI.Xaml.Thickness(x ?? 0, 0, 0, 0);
    }

    /// <summary>Forget the history: the next reading is of something else.</summary>
    public void Clear()
    {
        _first.Clear();
        _second.Clear();
        _added = 0;
        Redraw();
    }

    private void Trim(Queue<double> series)
    {
        while (series.Count > Holds)
        {
            series.Dequeue();
        }
    }

    /// <summary>[S-6331] Draw the chart settled: the newest finished slot on the right edge; [S-6753] with nothing yet, its baseline.</summary>
    private void Redraw()
    {
        _stale = false;

        var width = ActualWidth;
        var height = ActualHeight;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var model = new ChartModel
        {
            Type = _type,
            Detail = _detail,
            High = _scale.Floor,
            Format = _format,
            Times = _detail == ChartDetail.Detailed ? Times(width) : [],
        };

        if (_first.Count < 2)
        {
            _series.Show(model with { Caption = Captions ? Str.Chart_NoData.Text() : string.Empty });
            return;
        }

        // [S-1121] The whole span, folded into the slots this width has: a slot is a fixed run of
        // points (by their absolute position), its peak on a rate chart and its mean on a share.
        var size = MeterTiming.BucketSize(_capacity, width);
        var window = MeterTiming.Slots(_capacity, size);
        var first = MeterTiming.Fold(_first.ToArray(), _added, size, _scale.FollowsWindow).AsSpan();
        var second = MeterTiming.Fold(_second.ToArray(), _added, size, _scale.FollowsWindow).AsSpan();

        // [S-6331] Each finished slot's x, the newest on the right edge; older than the window is left out.
        // [S-1824] Inset by the stroke at the sides, so the newest sample is never cut in half at the edge.
        var (skip, xs) = SparkSlots.Layout(first.Length, _added, size, window, width, FamilyShape.Stroke);

        // [S-1829] A slot still filling is not among them.
        if (skip + xs.Length > first.Length || skip + xs.Length > second.Length || xs.Length < 2)
        {
            _series.Show(model with { Caption = Captions ? Str.Chart_NoData.Text() : string.Empty });
            return;
        }

        first = first.Slice(skip, xs.Length);
        second = second.Slice(skip, xs.Length);

        // [S-1124] The scale is the drawn slots'.
        var ceiling = _scale.Ceiling(first, second);
        _series.Show(model with { High = ceiling, TickUnit = _bytes ? ChartPlot.BinaryUnit(ceiling) : 1, Series = Trace(first, xs), Second = Trace(second, xs) });
    }

    /// <summary>[S-6751] A Detailed history's times: whole steps of the clock across the span, the newest slot at now.</summary>
    private IReadOnlyList<ChartTick> Times(double width)
    {
        var now = DateTimeOffset.Now;
        var span = Span(_period);
        var (at, step) = ChartPlot.Times(now - span, now, ChartPlot.TimeRoom(width), TimeZoneInfo.Local);
        var ticks = new List<ChartTick>(at.Count);
        foreach (var moment in at)
        {
            if (LinkInstants.X(moment, FamilyShape.Stroke, width - FamilyShape.Stroke, span, now) is { } x)
            {
                var local = TimeZoneInfo.ConvertTime(moment, TimeZoneInfo.Local);
                ticks.Add(new ChartTick(x, LocaleFormat.Time(Strings.Current, TimeOnly.FromDateTime(local.DateTime), seconds: step < TimeSpan.FromMinutes(1))));
            }
        }

        return ticks;
    }

    /// <summary>A series as the chart's samples, at the slots' x; gaps are skipped.</summary>
    private static ChartSample[] Trace(ReadOnlySpan<double> samples, double[] xs)
    {
        var points = new List<ChartSample>(samples.Length);

        for (var i = 0; i < samples.Length && i < xs.Length; i++)
        {
            if (double.IsFinite(samples[i]))
            {
                points.Add(new ChartSample(xs[i], samples[i]));
            }
        }

        return [.. points];
    }
}
