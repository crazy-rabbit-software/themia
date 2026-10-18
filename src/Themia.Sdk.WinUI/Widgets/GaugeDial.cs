using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Typography = Microsoft.UI.Xaml.Documents.Typography;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Themia.App.Shell;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Windows.Foundation;

namespace Themia.App.Widgets;

/// <summary>
/// [S-6302] A half-circle gauge: a track over the top from the left end to the right, and the
/// value's arc over it in the pressure colour, round-capped as Windows' progress ring is. Its
/// geometry is <see cref="GaugeLayout.Fit"/>'s; the reading inside its bowl is the caller's.
/// </summary>
public sealed partial class GaugeDial : Grid
{
    private readonly Microsoft.UI.Xaml.Shapes.Path _track = new() { StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
    private readonly Microsoft.UI.Xaml.Shapes.Path _value = new() { StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
    private readonly NumberGlide _sweep;
    private readonly ColorGlide _colour = new();
    private GaugeGeometry _geometry;
    private double? _fraction;

    /// <summary>[S-6328] A five-hundredth of the sweep: finer is not a pixel on any gauge.</summary>
    private const double SweepResolution = 0.002;

    public GaugeDial()
    {
        FlowDirection = FlowDirection.LeftToRight;
        IsHitTestVisible = false;
        ItemFill.PaintStroke(_track, ItemRole.Track);
        _colour.Set(MeterBrushes.Pressure(MeterPressure.Normal), TimeSpan.Zero, settle: true);
        _value.Stroke = _colour.Brush;
        _sweep = new NumberGlide(
            shown =>
            {
                _fraction = shown;
                Redraw();
            },
            SweepResolution);
        Children.Add(_track);
        Children.Add(_value);
    }

    /// <summary>[S-6328] How long the arc and its colour take to reach a new reading: the series' period.</summary>
    public TimeSpan GlidePeriod { get; set; } = GlideMotion.Tick;

    /// <summary>The geometry to draw at; the dial takes its box's size.</summary>
    public void Lay(GaugeGeometry geometry)
    {
        if (geometry == _geometry)
        {
            return;
        }

        _geometry = geometry;
        Width = geometry.Width;
        Height = geometry.Height;
        _track.StrokeThickness = geometry.Thickness;
        _value.StrokeThickness = geometry.Thickness;
        _track.Data = Arc(geometry, 1);
        Redraw();
    }

    /// <summary>The share shown, 0..1, glided to; null (no reading) draws the track alone at once.</summary>
    public void SetFraction(double? fraction, bool settle = false)
    {
        var value = fraction is { } f && double.IsFinite(f) ? Math.Clamp(f, 0, 1) : (double?)null;
        _sweep.Set(value, GlidePeriod, settle);

        if (value is null && _fraction is not null)
        {
            _fraction = null;
            Redraw();
        }
    }

    /// <summary>The arc's colour (a theme brush, read now), glided to through OKLab.</summary>
    public void SetBrush(Brush brush, bool settle = false) => _colour.Set(brush, GlidePeriod, settle);

    private void Redraw()
    {
        // Under a hundredth there is no arc to draw, only a round cap: nothing reads better than a dot.
        _value.Visibility = _fraction is >= 0.005 && _geometry.Radius > 0 ? Visibility.Visible : Visibility.Collapsed;
        _value.Data = _fraction is { } f && _geometry.Radius > 0 ? Arc(_geometry, f) : null;
    }

    private static PathGeometry Arc(GaugeGeometry g, double fraction)
    {
        var (sx, sy) = g.PointAt(0);
        var (ex, ey) = g.PointAt(fraction);
        var figure = new PathFigure { StartPoint = new Point(sx, sy), IsClosed = false, IsFilled = false };
        figure.Segments.Add(new ArcSegment
        {
            Point = new Point(ex, ey),
            Size = new Size(g.Radius, g.Radius),
            SweepDirection = SweepDirection.Clockwise,
            IsLargeArc = false,
        });

        var path = new PathGeometry();
        path.Figures.Add(figure);
        return path;
    }
}
