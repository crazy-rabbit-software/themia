using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Themia.Core.Diagnostics;

namespace Themia.App.Widgets;

/// <summary>
/// [S-1913] The ring an armed power widget drains: an arc from twelve o'clock, clockwise, as long as
/// the share of the arm window that is left, shrinking back to twelve as the window runs out. It
/// drains on the compositor (one linear animation of the arc's end over the time that is left), so
/// nothing on the UI thread runs while it does; the window itself is the press time's
/// (<see cref="Themia.Core.Widgets.PowerButton"/>), never this drawing's. Drawn as Windows' own progress ring drew it
/// here before: no track, round caps, a stroke a tenth of the ring's size.
/// </summary>
public sealed class ArmRing
{
    private const string Scope = "power";
    private const string TrimEnd = nameof(CompositionGeometry.TrimEnd);

    /// <summary>The stroke's share of the ring's size: the WinUI progress ring's, which this replaced.</summary>
    private const double StrokeShare = 0.1;

    private readonly Grid _root = new() { IsHitTestVisible = false, Visibility = Visibility.Collapsed };
    private Compositor? _compositor;
    private ShapeVisual? _visual;
    private CompositionEllipseGeometry? _geometry;
    private CompositionSpriteShape? _shape;
    private CompositionColorBrush? _brush;
    private bool _failed;

    public ArmRing()
    {
        _root.SizeChanged += (_, e) => Size(e.NewSize.Width, e.NewSize.Height);
    }

    /// <summary>The element the ring is drawn in, placed and sized by its widget.</summary>
    public FrameworkElement Root => _root;

    /// <summary>Whether the ring is shown.</summary>
    public bool Shown => _root.Visibility == Visibility.Visible;

    /// <summary>
    /// Show the ring at <paramref name="remaining"/> (1 just armed, 0 run out) in
    /// <paramref name="brush"/>'s colour and drain it to nothing over <paramref name="left"/>.
    /// </summary>
    public void Drain(double remaining, TimeSpan left, Brush brush)
    {
        _root.Visibility = Visibility.Visible;

        if (!Ensure() || _compositor is not { } compositor || _geometry is not { } geometry || _brush is not { } fill)
        {
            return;
        }

        // The theme's brush is recoloured in place on a theme change; the drain takes its colour now.
        if (brush is SolidColorBrush solid)
        {
            fill.Color = solid.Color;
        }

        var from = (float)Math.Clamp(remaining, 0, 1);
        geometry.StopAnimation(TrimEnd);
        geometry.TrimEnd = from;

        // Under a millisecond there is nothing to drain; the widget's expiry hides the ring.
        if (from <= 0 || left < TimeSpan.FromMilliseconds(1))
        {
            geometry.TrimEnd = 0;
            return;
        }

        var drain = compositor.CreateScalarKeyFrameAnimation();
        drain.InsertKeyFrame(0f, from);
        drain.InsertKeyFrame(1f, 0f, compositor.CreateLinearEasingFunction());
        drain.Duration = left;
        geometry.StartAnimation(TrimEnd, drain);
    }

    /// <summary>Hide the ring and stop its drain.</summary>
    public void Stop()
    {
        _root.Visibility = Visibility.Collapsed;
        _geometry?.StopAnimation(TrimEnd);
    }

    /// <summary>The visual tree under the element, made on the first drain. False (logged once) where the compositor refuses it.</summary>
    private bool Ensure()
    {
        if (_visual is not null)
        {
            return true;
        }

        if (_failed)
        {
            return false;
        }

        try
        {
            var compositor = ElementCompositionPreview.GetElementVisual(_root).Compositor;
            var geometry = compositor.CreateEllipseGeometry();
            var brush = compositor.CreateColorBrush();
            var shape = compositor.CreateSpriteShape(geometry);
            shape.StrokeBrush = brush;
            shape.StrokeStartCap = CompositionStrokeCap.Round;
            shape.StrokeEndCap = CompositionStrokeCap.Round;

            // An ellipse geometry's outline starts at twelve o'clock and runs clockwise, which is
            // the ring's own start and direction: no turn (checked on the --state armed render).

            var visual = compositor.CreateShapeVisual();
            visual.Shapes.Add(shape);
            ElementCompositionPreview.SetElementChildVisual(_root, visual);

            (_compositor, _geometry, _brush, _shape, _visual) = (compositor, geometry, brush, shape, visual);
            Size(_root.ActualWidth, _root.ActualHeight);
            return true;
        }
        catch (Exception ex)
        {
            // The widget still arms, says so and expires on its own clock; only the ring is missing.
            _failed = true;
            Log.Warn(Scope, "could not draw the arm ring on the compositor", ex);
            return false;
        }
    }

    private void Size(double width, double height)
    {
        if (_visual is null || _geometry is null || _shape is null)
        {
            return;
        }

        var diameter = Math.Max(0, Math.Min(width, height));
        var stroke = Math.Max(1, diameter * StrokeShare);
        var centre = new Vector2((float)(width / 2), (float)(height / 2));

        _visual.Size = new Vector2((float)Math.Max(0, width), (float)Math.Max(0, height));
        _geometry.Center = centre;
        _geometry.Radius = new Vector2((float)Math.Max(0, (diameter - stroke) / 2));
        _shape.StrokeThickness = (float)stroke;
    }
}
