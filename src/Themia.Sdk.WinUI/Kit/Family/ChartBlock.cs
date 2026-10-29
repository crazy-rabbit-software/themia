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

    public ChartSeries()
    {
        IsHitTestVisible = false;
        Children.Add(_wash);
        Children.Add(_second);
        Children.Add(_line);
        _second.Stroke = Family.Brush(ColourRole.TextTertiary);
        Role(ColourRole.Accent);
        SizeChanged += (_, _) => Draw();
    }

    /// <summary>Show <paramref name="values"/> (0..1) and an optional second series.</summary>
    public void Show(IReadOnlyList<double> values, IReadOnlyList<double>? second = null)
    {
        _values = values ?? [];
        _secondValues = second ?? [];
        Draw();
    }

    /// <summary>The series' role: the accent, or Caution/Critical over a threshold.</summary>
    public void Role(ColourRole role)
    {
        var brush = Family.Brush(role);
        _line.Stroke = brush;
        _wash.Fill = brush;
    }

    private void Draw()
    {
        var (w, h) = (ActualWidth, ActualHeight);
        var points = ChartRules.Line(_values, w, h);
        _line.Data = Polyline(points, closed: false, h);
        _wash.Data = Polyline(points, closed: true, h);
        _second.Data = Polyline(ChartRules.Line(_secondValues, w, h), closed: false, h);
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
/// round, with its fill in the accent (or Caution/Critical) from one fraction to another.
/// </summary>
public sealed partial class ChartBar : Grid
{
    private readonly Border _fill;
    private double _from;
    private double _to;

    public ChartBar()
    {
        Height = FamilyShape.Track;
        VerticalAlignment = VerticalAlignment.Center;
        var round = Family.Round(FamilyShape.Track, FamilyShape.Track);
        Children.Add(new Border { Background = Family.Brush(ColourRole.Track), CornerRadius = round });
        _fill = new Border { CornerRadius = round, HorizontalAlignment = HorizontalAlignment.Left };
        Children.Add(_fill);
        Role(ColourRole.Accent);
        SizeChanged += (_, _) => Draw();
    }

    /// <summary>Fill from <paramref name="from"/> to <paramref name="to"/> (fractions of the track).</summary>
    public void Show(double to, double from = 0)
    {
        _to = to;
        _from = from;
        Draw();
    }

    public void Role(ColourRole role) => _fill.Background = Family.Brush(role);

    private void Draw()
    {
        var (left, width) = ChartRules.Bar(ActualWidth, _to, _from);
        _fill.Margin = new Thickness(left, 0, 0, 0);
        _fill.Width = width;
    }
}

/// <summary>
/// [S-6717] A meter's ring: a track ring in the Track role at the Chart's stroke, and the reading's
/// arc over it in the accent (or Caution/Critical), from twelve, clockwise, round caps. Square, the
/// size of its box's short side.
/// </summary>
public sealed partial class ChartRing : Grid
{
    private readonly Ellipse _track = new() { StrokeThickness = FamilyShape.Stroke };
    private readonly Path _arc = new() { StrokeThickness = FamilyShape.Stroke, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
    private double _fraction;

    public ChartRing()
    {
        IsHitTestVisible = false;
        _track.Stroke = Family.Brush(ColourRole.Track);
        Children.Add(_track);
        Children.Add(_arc);
        Role(ColourRole.Accent);
        SizeChanged += (_, _) => Draw();
    }

    public void Show(double fraction)
    {
        _fraction = fraction;
        Draw();
    }

    public void Role(ColourRole role) => _arc.Stroke = Family.Brush(role);

    private void Draw()
    {
        var size = Math.Min(ActualWidth, ActualHeight);
        _track.Width = size;
        _track.Height = size;
        var sweep = ChartRules.Sweep(_fraction);
        if (size <= FamilyShape.Stroke || sweep <= 0)
        {
            _arc.Data = null;
            return;
        }

        var r = (size - FamilyShape.Stroke) / 2;
        var (cx, cy) = (ActualWidth / 2, ActualHeight / 2);
        var end = Math.Min(sweep, 359.99) * Math.PI / 180;
        var figure = new PathFigure { StartPoint = new Point(cx, cy - r), IsClosed = false, IsFilled = false };
        figure.Segments.Add(new ArcSegment
        {
            Point = new Point(cx + (r * Math.Sin(end)), cy - (r * Math.Cos(end))),
            Size = new Size(r, r),
            SweepDirection = SweepDirection.Clockwise,
            IsLargeArc = sweep > 180,
        });
        _arc.Data = new PathGeometry { Figures = { figure } };
    }
}
