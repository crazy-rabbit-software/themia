using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Themia.App.Shell;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Windows.Foundation;

namespace Themia.App.Widgets;

/// <summary>
/// [S-6060] One progress ring for every widget that draws one (the Pomodoro, the Countdown, Year
/// progress): a track in the progress ring's rest fill and the filled arc over it, clockwise from
/// twelve, its geometry <see cref="RingArc"/>. Content (the digits, a percentage) sits in
/// <see cref="Root"/> over the ring, centred. Not the meters' half-circle gauge: that reads a level,
/// this one a share of a whole.
/// </summary>
internal sealed class RingPart
{
    private readonly Ellipse _track = new();
    private readonly Microsoft.UI.Xaml.Shapes.Path _arc = new() { StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
    private double _diameter;
    private double _stroke;
    private double? _fraction;

    public RingPart()
    {
        // [S-837] A progress ring's track is a control's strong rest fill; the disabled fill reads
        // as a ring that is switched off.
        ItemFill.PaintStroke(_track, ItemRole.Track);
        _arc.Stroke = ThemeBrush.Of("AccentFillColorDefaultBrush", Colors.DodgerBlue);

        Root = new Grid
        {
            FlowDirection = FlowDirection.LeftToRight,
            Children = { _track, _arc },
        };
    }

    /// <summary>The ring and whatever is laid over it.</summary>
    public Grid Root { get; }

    /// <summary>Size the ring; its stroke follows the diameter (<see cref="RingArc.Stroke"/>) unless one is given.</summary>
    public void Resize(double diameter, double? stroke = null)
    {
        _diameter = Math.Max(0, Math.Floor(diameter));
        _stroke = stroke ?? RingArc.Stroke(_diameter);
        Root.Width = _diameter;
        Root.Height = _diameter;
        _track.StrokeThickness = _stroke;
        _arc.StrokeThickness = _stroke;
        Redraw();
    }

    /// <summary>The share filled, or null for the track alone (a share that cannot be said).</summary>
    public void SetFraction(double? fraction)
    {
        if (_fraction == fraction)
        {
            return;
        }

        _fraction = fraction;
        Redraw();
    }

    public void SetBrush(Brush brush) => _arc.Stroke = brush;

    /// <summary>Put content (a readout) over the ring's middle.</summary>
    public void Add(FrameworkElement content)
    {
        content.HorizontalAlignment = HorizontalAlignment.Center;
        content.VerticalAlignment = VerticalAlignment.Center;
        Root.Children.Add(content);
    }

    private void Redraw()
    {
        _arc.Data = _fraction is { } fraction && _diameter > 0 ? Geometry(RingArc.Of(_diameter, _stroke, fraction)) : null;
    }

    /// <summary>The arc as a path: two halves when whole, because an arc from a point back to itself draws nothing.</summary>
    private static PathGeometry? Geometry(RingArc arc)
    {
        if (arc.Empty)
        {
            return null;
        }

        var size = new Size(arc.Radius, arc.Radius);
        var figure = new PathFigure { StartPoint = new Point(arc.StartX, arc.StartY), IsClosed = false };

        if (arc.Full)
        {
            figure.Segments.Add(new ArcSegment { Point = new Point(arc.Centre, arc.Centre + arc.Radius), Size = size, SweepDirection = SweepDirection.Clockwise });
            figure.Segments.Add(new ArcSegment { Point = new Point(arc.StartX, arc.StartY), Size = size, SweepDirection = SweepDirection.Clockwise });
        }
        else
        {
            figure.Segments.Add(new ArcSegment
            {
                Point = new Point(arc.EndX, arc.EndY),
                Size = size,
                SweepDirection = SweepDirection.Clockwise,
                IsLargeArc = arc.IsLarge,
            });
        }

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        return geometry;
    }
}
