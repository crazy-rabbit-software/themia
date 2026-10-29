using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Themia.Sdk.Kit;
using Windows.Foundation;
using Path = Microsoft.UI.Xaml.Shapes.Path;

namespace Themia.Sdk.WinUI.Kit;

/// <summary>
/// [S-6717] The Chart block's band: one series in its role (the accent, or Caution/Critical while the
/// reading is over its threshold, <see cref="ChartRules.Role"/>) at 2 with a 16 % wash under it, a
/// second series in the tertiary role. It fills the box it is given (a history band runs edge to
/// edge at the card's bottom) and redraws when that box changes.
/// </summary>
public sealed partial class ChartSeries : Grid
{
    private readonly Path _wash = new() { Opacity = FamilyShape.Wash };
    private readonly Path _line = new() { StrokeThickness = FamilyShape.Stroke, StrokeLineJoin = PenLineJoin.Round };
    private readonly Path _second = new() { StrokeThickness = FamilyShape.Stroke, StrokeLineJoin = PenLineJoin.Round };
    private IReadOnlyList<double> _values = [];
    private IReadOnlyList<double> _secondValues = [];

    // Points laid out by the owner (a history on fixed slots): drawn as given, not spread evenly.
    private IReadOnlyList<ChartPoint>? _points;
    private IReadOnlyList<ChartPoint> _secondPoints = [];
    private ColourRole _role = ColourRole.Accent;

    public ChartSeries()
    {
        IsHitTestVisible = false;
        Children.Add(_wash);
        Children.Add(_second);
        Children.Add(_line);
        _second.Stroke = Family.Brush(ColourRole.TextTertiary);
        _line.Stroke = Family.Brush(_role);
        _wash.Fill = _line.Stroke;
        SizeChanged += (_, _) => Draw();
    }

    /// <summary>Show <paramref name="values"/> (0..1) and an optional second series, evenly spaced across the band.</summary>
    public void Show(IReadOnlyList<double> values, IReadOnlyList<double>? second = null)
    {
        _values = values ?? [];
        _secondValues = second ?? [];
        _points = null;
        Draw();
    }

    /// <summary>
    /// Show a line laid out by its owner in this band's own DIPs (a history on fixed slots, its newest
    /// point on the right edge, its heights from <see cref="ChartRules.Y"/>) and an optional second one.
    /// The owner lays them out again when the band's size changes.
    /// </summary>
    public void Show(IReadOnlyList<ChartPoint> points, IReadOnlyList<ChartPoint>? second)
    {
        _points = points ?? [];
        _secondPoints = second ?? [];
        Draw();
    }

    /// <summary>The series' role: the accent, or Caution/Critical over a threshold; set again only when it changes.</summary>
    public void Role(ColourRole role)
    {
        if (role == _role)
        {
            return;
        }

        _role = role;
        var brush = Family.Brush(role);
        _line.Stroke = brush;
        _wash.Fill = brush;
    }

    private void Draw()
    {
        var (w, h) = (ActualWidth, ActualHeight);
        var points = _points ?? ChartRules.Line(_values, w, h);
        _line.Data = Polyline(points, closed: false, h);
        _wash.Data = Polyline(points, closed: true, h);
        _second.Data = Polyline(_points is null ? ChartRules.Line(_secondValues, w, h) : _secondPoints, closed: false, h);
    }

    private static PathGeometry? Polyline(IReadOnlyList<ChartPoint> points, bool closed, double height)
    {
        if (points.Count < 2)
        {
            return null;
        }

        var figure = new PathFigure { StartPoint = new Point(points[0].X, points[0].Y), IsClosed = closed, IsFilled = closed };
        var segment = new PolyLineSegment();
        for (var i = 1; i < points.Count; i++)
        {
            segment.Points.Add(new Point(points[i].X, points[i].Y));
        }

        if (closed)
        {
            segment.Points.Add(new Point(points[^1].X, height));
            segment.Points.Add(new Point(points[0].X, height));
        }

        figure.Segments.Add(segment);
        return new PathGeometry { Figures = { figure } };
    }
}

/// <summary>
/// [S-6717] A meter's bar (and a range bar, and an inline meter in a row): a 4 track in the Track role,
/// round, with its fill in the accent (or Caution/Critical) from one fraction to another; or a column
/// (a core's load, a day's range standing up): the same track standing up, filled upwards from one
/// fraction of its height to another (from its foot when the first is 0). The fill is moved along the
/// track, never set off by a margin, so a range starting past the track's start stays on the family's
/// space scale ([S-6731]).
/// </summary>
public sealed partial class ChartBar : Grid
{
    private readonly Border _fill;
    private readonly TranslateTransform _offset = new();
    private readonly bool _vertical;
    private double _from;
    private double _to;
    private ColourRole? _role;

    /// <summary>A bar along its length, or a <paramref name="vertical"/> column filling from its foot.</summary>
    public ChartBar(bool vertical = false)
    {
        _vertical = vertical;
        if (vertical)
        {
            Width = FamilyShape.Track;
            HorizontalAlignment = HorizontalAlignment.Center;
        }
        else
        {
            Height = FamilyShape.Track;
            VerticalAlignment = VerticalAlignment.Center;
        }

        var round = Family.Round(FamilyShape.Track, FamilyShape.Track);
        Children.Add(new Border { Background = Family.Brush(ColourRole.Track), CornerRadius = round });
        _fill = new Border
        {
            CornerRadius = round,
            HorizontalAlignment = vertical ? HorizontalAlignment.Stretch : HorizontalAlignment.Left,
            VerticalAlignment = vertical ? VerticalAlignment.Bottom : VerticalAlignment.Stretch,
            RenderTransform = _offset,
        };
        Children.Add(_fill);
        Role(ColourRole.Accent);
        SizeChanged += (_, _) => Draw();
    }

    /// <summary>[S-6330] Fill from <paramref name="from"/> to <paramref name="to"/> (fractions of the track), at once.</summary>
    public void Show(double to, double from = 0)
    {
        if (to.Equals(_to) && from.Equals(_from))
        {
            return;
        }

        _to = to;
        _from = from;
        Draw();
    }

    /// <summary>The fill's role, set again only when it changes.</summary>
    public void Role(ColourRole role)
    {
        if (role != _role)
        {
            _role = role;
            _fill.Background = Family.Brush(role);
        }
    }

    private void Draw()
    {
        if (_vertical)
        {
            // A column fills upwards: its share of the height, raised by what lies under it; round at any length.
            var (foot, height) = ChartRules.Bar(ActualHeight, _to, _from);
            _fill.Height = height;
            _offset.Y = -foot;
            _fill.CornerRadius = Family.Round(FamilyShape.Track, height);
            return;
        }

        var (left, width) = ChartRules.Bar(ActualWidth, _to, _from);
        _offset.X = left;
        _fill.Width = width;
        _fill.CornerRadius = Family.Round(width, FamilyShape.Track);
    }
}

/// <summary>
/// [S-6717] A meter's ring: a track ring in the Track role at the Chart's stroke, and the reading's
/// arc over it in the accent (or Caution/Critical), from twelve, clockwise, round caps. Square, the
/// size of its box's short side. A gauge is its upper half, the arc from nine o'clock.
/// </summary>
public sealed partial class ChartRing : Grid
{
    private readonly Ellipse _track = new() { StrokeThickness = FamilyShape.Stroke };
    private readonly Path _halfTrack = new() { StrokeThickness = FamilyShape.Stroke, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
    private readonly Path _arc = new() { StrokeThickness = FamilyShape.Stroke, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
    private readonly bool _half;
    private double _fraction = double.NaN;
    private ColourRole? _role;

    /// <summary>
    /// A ring, or a <paramref name="half"/> one (a gauge): the upper half from nine o'clock over the
    /// top to three, as wide as its box and <see cref="ChartRules.GaugeHeight"/> of that tall.
    /// </summary>
    public ChartRing(bool half = false)
    {
        _half = half;
        IsHitTestVisible = false;
        var track = Family.Brush(ColourRole.Track);
        _track.Stroke = track;
        _halfTrack.Stroke = track;
        _track.Visibility = half ? Visibility.Collapsed : Visibility.Visible;
        _halfTrack.Visibility = half ? Visibility.Visible : Visibility.Collapsed;
        Children.Add(_track);
        Children.Add(_halfTrack);
        Children.Add(_arc);
        Role(ColourRole.Accent);
        SizeChanged += (_, _) => Draw(resized: true);
    }

    /// <summary>[S-6330] The reading's share, drawn at once; the same share again draws nothing.</summary>
    public void Show(double fraction)
    {
        if (fraction.Equals(_fraction))
        {
            return;
        }

        _fraction = fraction;
        Draw(resized: false);
    }

    /// <summary>The arc's role, set again only when it changes.</summary>
    public void Role(ColourRole role)
    {
        if (role != _role)
        {
            _role = role;
            _arc.Stroke = Family.Brush(role);
        }
    }

    private void Draw(bool resized)
    {
        var (w, h) = (ActualWidth, ActualHeight);
        if (_half)
        {
            DrawHalf(w, h, resized);
            return;
        }

        var side = Math.Min(w, h);
        _track.Width = side;
        _track.Height = side;
        var sweep = ChartRules.Sweep(_fraction);
        if (side <= FamilyShape.Stroke || sweep <= 0)
        {
            _arc.Data = null;
            return;
        }

        var r = (side - FamilyShape.Stroke) / 2;
        var (cx, cy) = (w / 2, h / 2);
        var end = Math.Min(sweep, 359.99) * Math.PI / 180;
        _arc.Data = Arc(new Point(cx, cy - r), new Point(cx + (r * Math.Sin(end)), cy - (r * Math.Cos(end))), r, sweep > 180);
    }

    /// <summary>The gauge: its track drawn again only for a new size, its arc from nine o'clock by the share of a half turn.</summary>
    private void DrawHalf(double w, double h, bool resized)
    {
        var diameter = Math.Min(w, 2 * (h - (FamilyShape.Stroke / 2)));
        var r = (diameter - FamilyShape.Stroke) / 2;
        var (cx, cy) = (w / 2, (FamilyShape.Stroke / 2) + r);
        if (r <= 0)
        {
            _halfTrack.Data = null;
            _arc.Data = null;
            return;
        }

        if (resized || _halfTrack.Data is null)
        {
            _halfTrack.Data = Arc(new Point(cx - r, cy), new Point(cx + r, cy), r, large: false);
        }

        var sweep = ChartRules.GaugeSweep(_fraction);
        if (sweep <= 0)
        {
            _arc.Data = null;
            return;
        }

        var angle = Math.Min(sweep, 179.99) * Math.PI / 180;
        _arc.Data = Arc(new Point(cx - r, cy), new Point(cx - (r * Math.Cos(angle)), cy - (r * Math.Sin(angle))), r, large: false);
    }

    private static PathGeometry Arc(Point from, Point to, double r, bool large)
    {
        var figure = new PathFigure { StartPoint = from, IsClosed = false, IsFilled = false };
        figure.Segments.Add(new ArcSegment { Point = to, Size = new Size(r, r), SweepDirection = SweepDirection.Clockwise, IsLargeArc = large });
        return new PathGeometry { Figures = { figure } };
    }
}
